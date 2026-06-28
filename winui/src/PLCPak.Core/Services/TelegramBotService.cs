using System.Text;
using System.Text.Json;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class TelegramBotService
{
    private const int TelegramMaxLength = 4096;
    private const int ChunkSize = 4000;

    private static readonly HttpClient SharedHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static TelegramBotService()
    {
        ResilientHttpHelper.EnsureUserAgent(SharedHttp);
    }

    public async Task<TelegramSendResult> SendMessageAsync(
        string botToken,
        string chatId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return new TelegramSendResult
            {
                Success = false,
                Error = "Telegram Bot Token 未配置",
                ChatId = chatId,
                OriginalLength = text.Length
            };
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            return new TelegramSendResult
            {
                Success = false,
                Error = "Telegram 频道/聊天 ID 未配置",
                ChatId = chatId,
                OriginalLength = text.Length
            };
        }

        var (chunks, wasTruncated) = SplitMessageChunks(text);
        var partsSent = 0;
        int? lastMessageId = null;

        foreach (var chunk in chunks)
        {
            var result = await SendSingleMessageAsync(botToken, chatId, chunk, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                return new TelegramSendResult
                {
                    Success = false,
                    Error = result.Error,
                    ChatId = chatId,
                    MessageId = lastMessageId,
                    WasTruncated = wasTruncated,
                    OriginalLength = text.Length,
                    PartsSent = partsSent
                };
            }

            partsSent++;
            if (result.MessageId.HasValue)
                lastMessageId = result.MessageId;
        }

        return new TelegramSendResult
        {
            Success = true,
            MessageId = lastMessageId,
            ChatId = chatId,
            WasTruncated = wasTruncated,
            OriginalLength = text.Length,
            PartsSent = partsSent
        };
    }

    public static (IReadOnlyList<string> Chunks, bool WasTruncated) SplitMessageChunks(
        string text,
        int maxChunkSize = ChunkSize,
        int telegramMaxLength = TelegramMaxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= telegramMaxLength)
            return ([text], false);

        var chunks = new List<string>();
        var remaining = text;
        var wasTruncated = false;

        while (remaining.Length > 0)
        {
            if (remaining.Length <= maxChunkSize)
            {
                chunks.Add(remaining);
                break;
            }

            var packed = TryPackLines(remaining, maxChunkSize);
            if (packed > 0)
            {
                chunks.Add(remaining[..packed]);
                remaining = remaining[packed..];
                continue;
            }

            var slice = remaining[..maxChunkSize];
            var lastNewline = slice.LastIndexOf('\n');
            if (lastNewline > 0)
            {
                chunks.Add(remaining[..lastNewline]);
                remaining = remaining[(lastNewline + 1)..];
            }
            else
            {
                chunks.Add(remaining[..maxChunkSize]);
                remaining = remaining[maxChunkSize..];
                wasTruncated = true;
            }
        }

        return (chunks, wasTruncated);
    }

    private static int TryPackLines(string text, int maxChunkSize)
    {
        var packed = 0;
        while (packed < text.Length)
        {
            var nextNewline = text.IndexOf('\n', packed);
            var lineEnd = nextNewline >= 0 ? nextNewline + 1 : text.Length;
            if (lineEnd - packed > maxChunkSize)
                break;

            if (lineEnd > maxChunkSize)
                break;

            packed = lineEnd;
            if (nextNewline < 0)
                break;
        }

        return packed;
    }

    private static async Task<TelegramSendResult> SendSingleMessageAsync(
        string botToken,
        string chatId,
        string text,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            chat_id = chatId,
            text
        };

        try
        {
            var requestUri = $"https://api.telegram.org/bot{botToken.Trim()}/sendMessage";
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await ResilientHttpHelper.PostAsync(SharedHttp, requestUri, content, cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();

            if (!ok)
            {
                var error = root.TryGetProperty("description", out var desc) ? desc.GetString() : body;
                return new TelegramSendResult
                {
                    Success = false,
                    Error = error ?? "Telegram API 返回失败",
                    ChatId = chatId
                };
            }

            int? messageId = null;
            if (root.TryGetProperty("result", out var result)
                && result.TryGetProperty("message_id", out var messageIdProp)
                && messageIdProp.TryGetInt32(out var id))
                messageId = id;

            return new TelegramSendResult
            {
                Success = true,
                MessageId = messageId,
                ChatId = chatId
            };
        }
        catch (Exception ex)
        {
            return new TelegramSendResult
            {
                Success = false,
                Error = ex.Message,
                ChatId = chatId
            };
        }
    }

    public static string ResolveChatId(string telegramChannelUrl)
    {
        if (string.IsNullOrWhiteSpace(telegramChannelUrl))
            return string.Empty;

        var value = telegramChannelUrl.Trim();
        if (value.StartsWith('@') || long.TryParse(value, out _))
            return value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return value;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return value;

        var channel = segments[^1];
        return channel.StartsWith('@') ? channel : $"@{channel}";
    }
}
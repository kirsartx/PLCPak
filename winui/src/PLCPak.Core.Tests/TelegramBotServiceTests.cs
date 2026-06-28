using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class TelegramBotServiceTests
{
    [Theory]
    [InlineData("https://t.me/my_channel", "@my_channel")]
    [InlineData("https://t.me/@already_prefixed", "@already_prefixed")]
    [InlineData("@direct_channel", "@direct_channel")]
    [InlineData("-1001234567890", "-1001234567890")]
    public void ResolveChatId_extracts_channel_from_url(string input, string expected)
    {
        Assert.Equal(expected, TelegramBotService.ResolveChatId(input));
    }

    [Fact]
    public void SplitMessageChunks_keeps_short_text_in_single_chunk()
    {
        const string text = "short message";

        var (chunks, wasTruncated) = TelegramBotService.SplitMessageChunks(text);

        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
        Assert.False(wasTruncated);
    }

    [Fact]
    public void SplitMessageChunks_splits_at_newlines_before_hard_cut()
    {
        var line = new string('a', 2500);
        var text = string.Join('\n', line, line);

        var (chunks, wasTruncated) = TelegramBotService.SplitMessageChunks(text);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Length <= 4000));
        Assert.False(wasTruncated);
    }

    [Fact]
    public void SplitMessageChunks_hard_cuts_when_no_newline_available()
    {
        var text = new string('x', 9000);

        var (chunks, wasTruncated) = TelegramBotService.SplitMessageChunks(text);

        Assert.Equal(3, chunks.Count);
        Assert.True(wasTruncated);
        Assert.Equal(4000, chunks[0].Length);
        Assert.Equal(4000, chunks[1].Length);
        Assert.Equal(1000, chunks[2].Length);
    }
}
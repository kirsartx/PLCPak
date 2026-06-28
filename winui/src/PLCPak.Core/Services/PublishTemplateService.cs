using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class PublishTemplateService
{
    private readonly AppPaths _paths;

    public PublishTemplateService(AppPaths paths) => _paths = paths;

    public string CatalogPath => Path.Combine(_paths.DataRoot, "publish-templates.json");

    public PublishTemplateCatalog Load()
    {
        var catalog = JsonHelper.ReadFile<PublishTemplateCatalog>(CatalogPath);
        if (catalog is null || catalog.Templates.Count == 0)
            return CreateDefault();

        catalog.Templates ??= [];
        if (string.IsNullOrWhiteSpace(catalog.DefaultTemplateId))
            catalog.DefaultTemplateId = catalog.Templates[0].Id;

        return catalog;
    }

    public void Save(PublishTemplateCatalog catalog)
    {
        Directory.CreateDirectory(_paths.DataRoot);
        JsonHelper.WriteFile(CatalogPath, catalog, utf8Bom: true);
    }

    public PublishTemplate? GetTemplate(string? templateId = null)
    {
        var catalog = Load();
        templateId ??= catalog.DefaultTemplateId;
        return catalog.Templates.FirstOrDefault(t => t.Id.Equals(templateId, StringComparison.OrdinalIgnoreCase))
            ?? catalog.Templates.FirstOrDefault();
    }

    public PublishCopyResult Render(PublishJob job, StudioConfig studio, string? templateId = null)
    {
        var catalog = Load();
        var template = GetTemplate(templateId) ?? CreateDefault().Templates[0];
        var vars = BuildVariables(job, studio);
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link))
            missing.Add("百度链接");
        if (string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
            missing.Add("夸克链接");

        var text = template.Body;
        foreach (var pair in vars)
            text = text.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.Ordinal);

        return new PublishCopyResult
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            Text = text.Trim(),
            MissingFields = missing
        };
    }

    public PublishCopyResult PreviewTemplate(string? templateId, PublishJob? job, StudioConfig studio)
        => Render(job ?? CreateSampleJob(), studio, templateId);

    private static PublishJob CreateSampleJob() => new()
    {
        Title = "示例游戏标题",
        Source = new JobSource
        {
            Site = "示例论坛",
            ThreadUrl = "https://example.com/thread/12345"
        },
        Publish = new JobPublishState
        {
            Baidu = new PublishChannelState
            {
                Link = "https://pan.baidu.com/s/example",
                Password = "abcd"
            },
            Quark = new PublishChannelState
            {
                Link = "https://pan.quark.cn/s/example",
                Password = "efgh"
            },
            Telegram = new PublishChannelState
            {
                Link = "https://t.me/example/1"
            }
        },
        Artifacts = new JobArtifacts
        {
            OutputArchives =
            [
                Path.Combine("output", "示例游戏", "示例游戏(PC).7z"),
                Path.Combine("output", "示例游戏", "示例游戏(AZ).tar.zst")
            ]
        }
    };

    public static Dictionary<string, string> BuildVariables(PublishJob job, StudioConfig studio)
    {
        var baiduLink = NullIfEmpty(job.Publish.Baidu.Link);
        var baiduPwd = NullIfEmpty(job.Publish.Baidu.Password);
        var quarkLink = NullIfEmpty(job.Publish.Quark.Link);
        var quarkPwd = NullIfEmpty(job.Publish.Quark.Password);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = job.Title,
            ["source_site"] = string.IsNullOrWhiteSpace(job.Source.Site) ? "(未知来源)" : job.Source.Site,
            ["thread_url"] = string.IsNullOrWhiteSpace(job.Source.ThreadUrl) ? "(无原帖链接)" : job.Source.ThreadUrl,
            ["baidu_link"] = baiduLink ?? "(待填)",
            ["baidu_pwd"] = baiduPwd ?? "(待填)",
            ["baidu_line"] = FormatChannelLine("百度网盘", baiduLink, baiduPwd),
            ["quark_link"] = quarkLink ?? "(待填)",
            ["quark_pwd"] = quarkPwd ?? "(待填)",
            ["quark_line"] = FormatChannelLine("夸克网盘", quarkLink, quarkPwd),
            ["output_summary"] = BuildOutputSummary(job),
            ["output_files"] = BuildOutputFiles(job),
            ["date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["telegram_channel"] = string.IsNullOrWhiteSpace(studio.TelegramChannelUrl)
                ? "(未配置频道)"
                : studio.TelegramChannelUrl,
            ["telegram_post"] = string.IsNullOrWhiteSpace(job.Publish.Telegram.Link)
                ? "(发布后回填 TG 帖子链接)"
                : job.Publish.Telegram.Link
        };
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatChannelLine(string label, string? link, string? password)
    {
        if (link is null)
            return $"{label}：(待填)";

        return password is null
            ? $"{label}：{link}"
            : $"{label}：{link}  提取码：{password}";
    }

    private static string BuildOutputSummary(PublishJob job)
    {
        if (job.Artifacts.OutputArchives.Count == 0)
            return "(暂无输出文件，请先完成压缩)";

        return string.Join(
            Environment.NewLine,
            job.Artifacts.OutputArchives.Select(path =>
            {
                var file = new FileInfo(path);
                return $"- {file.Name} ({FormatSize(file.Exists ? file.Length : 0)})";
            }));
    }

    private static string BuildOutputFiles(PublishJob job)
    {
        if (job.Artifacts.OutputArchives.Count == 0)
            return "(暂无)";

        return string.Join(Environment.NewLine, job.Artifacts.OutputArchives.Select(Path.GetFileName));
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    private static PublishTemplateCatalog CreateDefault() => new()
    {
        Version = 1,
        DefaultTemplateId = "telegram-default",
        Templates =
        [
            new PublishTemplate
            {
                Id = "telegram-default",
                Name = "TG 频道默认",
                Channel = "telegram",
                Body = """
                    🎮 {title}

                    📦 百度网盘
                    {baidu_line}

                    📦 夸克网盘
                    {quark_line}

                    📁 打包文件
                    {output_summary}

                    📢 频道：{telegram_channel}

                    🔗 原帖：{thread_url}
                    """
            },
            new PublishTemplate
            {
                Id = "forum-reply",
                Name = "论坛回帖简版",
                Channel = "forum",
                Body = """
                    【{title}】

                    百度：{baidu_link}  提取码：{baidu_pwd}
                    夸克：{quark_link}  提取码：{quark_pwd}

                    文件：
                    {output_summary}
                    """
            }
        ]
    };
}
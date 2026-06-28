using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchPanLinksCsvServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _appRoot;
    private readonly JobStore _store;
    private readonly JobRunner _runner;

    public BatchPanLinksCsvServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-pan-csv-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        _appRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(_appRoot);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $"{{\"workspaceRoot\":\"{ws}\"}}");

        var appContext = PlcPakAppContext.Create(_appRoot);
        _store = appContext.Jobs;
        _runner = appContext.JobRunner;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ParseLines_skips_optional_header_and_parses_fields()
    {
        var lines = new[]
        {
            "jobId,title,baiduLink,baiduPwd,quarkLink,quarkPwd,telegramLink",
            "abc123,测试任务,https://baidu.test,abcd,https://quark.test,efgh,https://t.me/test"
        };

        var rows = BatchPanLinksCsvService.ParseLines(lines);

        Assert.Single(rows);
        Assert.Equal("abc123", rows[0].JobId);
        Assert.Equal("测试任务", rows[0].Title);
        Assert.Equal("https://baidu.test", rows[0].BaiduLink);
        Assert.Equal("abcd", rows[0].BaiduPwd);
        Assert.Equal("https://quark.test", rows[0].QuarkLink);
        Assert.Equal("efgh", rows[0].QuarkPwd);
        Assert.Equal("https://t.me/test", rows[0].TelegramLink);
    }

    [Fact]
    public void ApplyToJobs_matches_by_jobId_and_title()
    {
        var byId = _store.Create("按ID匹配");
        var byTitle = _store.Create("按标题匹配");
        _store.Save(byId);
        _store.Save(byTitle);

        var rows = new[]
        {
            new BatchPanLinksCsvRow
            {
                JobId = byId.Id,
                BaiduLink = "https://baidu.byid",
                QuarkLink = "https://quark.byid"
            },
            new BatchPanLinksCsvRow
            {
                Title = byTitle.Title,
                BaiduLink = "https://baidu.bytitle",
                QuarkLink = "https://quark.bytitle",
                TelegramLink = "https://t.me/bytitle"
            }
        };

        var result = BatchPanLinksCsvService.ApplyToJobs(_store, _runner, rows);

        Assert.True(result.Success);
        Assert.Equal(2, result.Applied);
        Assert.Equal(0, result.Failed);

        var updatedById = _store.Get(byId.Id)!;
        Assert.Equal("https://baidu.byid", updatedById.Publish.Baidu.Link);
        Assert.Equal("https://quark.byid", updatedById.Publish.Quark.Link);

        var updatedByTitle = _store.Get(byTitle.Id)!;
        Assert.Equal("https://baidu.bytitle", updatedByTitle.Publish.Baidu.Link);
        Assert.Equal("https://t.me/bytitle", updatedByTitle.Publish.Telegram.Link);
    }

    [Fact]
    public void ApplyToJobs_reports_failure_for_unknown_job()
    {
        var rows = new[]
        {
            new BatchPanLinksCsvRow
            {
                JobId = "missing",
                BaiduLink = "https://baidu.test"
            }
        };

        var result = BatchPanLinksCsvService.ApplyToJobs(_store, _runner, rows);

        Assert.False(result.Success);
        Assert.Equal(0, result.Applied);
        Assert.Equal(1, result.Failed);
        Assert.Contains("未找到任务", result.Messages[0]);
    }
}
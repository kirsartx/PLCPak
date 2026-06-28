using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class DailyReportServiceTests : IDisposable
{
    private readonly string _root;

    public DailyReportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-daily-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void BuildReport_counts_status_buckets()
    {
        var jobs = new[]
        {
            new PublishJob { Id = "1", Title = "已发布", Status = JobStatus.Published, UpdatedAt = DateTime.Now },
            new PublishJob { Id = "2", Title = "待发布", Status = JobStatus.Processed, UpdatedAt = DateTime.Now.AddMinutes(-1) },
            new PublishJob { Id = "3", Title = "失败", Status = JobStatus.Failed, UpdatedAt = DateTime.Now.AddMinutes(-2) },
            new PublishJob { Id = "4", Title = "进行中", Status = JobStatus.Extracted, UpdatedAt = DateTime.Now.AddMinutes(-3) }
        };

        var snapshot = DailyReportService.BuildReport(jobs, new DateTime(2026, 6, 25));

        Assert.Equal(4, snapshot.TotalJobs);
        Assert.Equal(4, snapshot.Stats.Total);
        Assert.Equal(1, snapshot.PendingPublishCount);
        Assert.Equal(1, snapshot.FailedCount);
        Assert.Equal(1, snapshot.Stats.Published);
        Assert.Equal(1, snapshot.Stats.Active);
        Assert.Equal(new DateTime(2026, 6, 25), snapshot.Date);
        Assert.Equal(4, snapshot.Entries.Count);
        Assert.Equal("已发布", snapshot.Entries[0].Title);
        Assert.Equal("回填发布链接", snapshot.Entries[1].NextActionLabel);
    }

    [Fact]
    public void ToCsv_includes_header_and_escapes_commas()
    {
        var snapshot = new DailyReportSnapshot
        {
            Date = new DateTime(2026, 6, 25),
            Entries =
            [
                new DailyReportEntry
                {
                    JobId = "abc",
                    Title = "游戏,测试",
                    Status = JobStatus.Processed,
                    StatusLabel = "已压缩",
                    PublishSummary = "百度未填 | 夸克未填 | TG未填",
                    NextActionLabel = "回填发布链接",
                    UpdatedAt = new DateTime(2026, 6, 25, 10, 0, 0)
                }
            ]
        };

        var csv = DailyReportService.ToCsv(snapshot);

        Assert.StartsWith("jobId,title,status,statusLabel,publishSummary,nextActionLabel,updatedAt", csv);
        Assert.Contains("\"游戏,测试\"", csv);
        Assert.Contains("Processed", csv);
        Assert.Contains("2026-06-25 10:00:00", csv);
    }

    [Fact]
    public void Export_writes_csv_and_json_under_reports()
    {
        var snapshot = DailyReportService.BuildReport(
        [
            new PublishJob
            {
                Id = "export-1",
                Title = "导出任务",
                Status = JobStatus.Draft,
                UpdatedAt = DateTime.Now
            }
        ],
        new DateTime(2026, 6, 25));

        var csvPath = DailyReportService.Export(_root, snapshot);

        Assert.Equal(Path.Combine(_root, "reports", "daily-2026-06-25.csv"), csvPath);
        Assert.True(File.Exists(csvPath));
        Assert.True(File.Exists(Path.Combine(_root, "reports", "daily-2026-06-25.json")));
        Assert.Contains("jobId,title", File.ReadAllText(csvPath));
        Assert.Contains("export-1", File.ReadAllText(snapshot.JsonPath!));
    }
}
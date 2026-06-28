using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-activity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Append_writes_json_lines_to_workspace_logs_activity_log()
    {
        ActivityLogService.Append(_root, "import", "批量导入网盘链接");
        ActivityLogService.Append(_root, "telegram", "发送 TG 文案");

        var path = ActivityLogService.GetLogPath(_root);
        Assert.True(File.Exists(path));
        Assert.Equal(Path.Combine(_root, "logs", "activity.log"), path);

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("import", lines[0]);
        Assert.Contains("批量导入网盘链接", lines[0]);
    }

    [Fact]
    public void ReadRecent_returns_latest_entries_up_to_limit()
    {
        for (var i = 1; i <= 3; i++)
            ActivityLogService.Append(_root, "test", $"message-{i}");

        var recent = ActivityLogService.ReadRecent(_root, limit: 2);

        Assert.Equal(2, recent.Count);
        Assert.Equal("message-2", recent[0].Message);
        Assert.Equal("message-3", recent[1].Message);
        Assert.Equal("test", recent[1].Category);
    }

    [Fact]
    public void ReadRecent_filters_by_category()
    {
        ActivityLogService.Append(_root, "telegram", "send");
        ActivityLogService.Append(_root, "import", "csv");

        var filtered = ActivityLogService.ReadRecent(_root, limit: 10, category: "telegram");

        Assert.Single(filtered);
        Assert.Equal("send", filtered[0].Message);
    }

    [Fact]
    public void Export_writes_filtered_json_report()
    {
        ActivityLogService.Append(_root, "telegram", "send");
        ActivityLogService.Append(_root, "import", "csv");

        var export = ActivityLogService.Export(_root, category: "import");

        Assert.Equal(1, export.EntryCount);
        Assert.True(File.Exists(export.ExportPath));
    }

    [Fact]
    public void PreviewTrim_reports_counts_without_deleting_entries()
    {
        var oldEntry = new ActivityLogEntry
        {
            Timestamp = DateTime.Now.AddDays(-40),
            Category = "old",
            Message = "old message"
        };
        var path = ActivityLogService.GetLogPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            System.Text.Json.JsonSerializer.Serialize(oldEntry) + Environment.NewLine);
        ActivityLogService.Append(_root, "new", "fresh");

        var preview = ActivityLogService.PreviewTrim(_root, keepDays: 30);

        Assert.Equal(2, preview.TotalCount);
        Assert.Equal(1, preview.WouldRemoveCount);
        Assert.Equal(1, preview.WouldRemainCount);
        Assert.Equal(2, ActivityLogService.ReadAll(_root).Count);
    }

    [Fact]
    public void ArchiveOlderThan_moves_expired_entries_to_archive_and_removes_from_main_log()
    {
        var oldEntry = new ActivityLogEntry
        {
            Timestamp = DateTime.Now.AddDays(-40),
            Category = "old",
            Message = "old message"
        };
        var path = ActivityLogService.GetLogPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            System.Text.Json.JsonSerializer.Serialize(oldEntry) + Environment.NewLine);
        ActivityLogService.Append(_root, "new", "fresh");

        var result = ActivityLogService.ArchiveOlderThan(_root, keepDays: 30);

        Assert.Equal(1, result.ArchivedCount);
        Assert.Equal(1, result.RemainingCount);
        Assert.Equal(30, result.KeepDays);
        Assert.NotNull(result.ArchivePath);
        Assert.True(File.Exists(result.ArchivePath));
        Assert.Contains("activity-archive-", Path.GetFileName(result.ArchivePath));

        var remaining = ActivityLogService.ReadAll(_root);
        Assert.Single(remaining);
        Assert.Equal("fresh", remaining[0].Message);

        var archived = JsonHelper.ReadFile<List<ActivityLogEntry>>(result.ArchivePath!) ?? [];
        Assert.Single(archived);
        Assert.Equal("old message", archived[0].Message);
    }

    [Fact]
    public void ArchiveOlderThan_with_no_expired_entries_does_not_create_archive_file()
    {
        ActivityLogService.Append(_root, "new", "fresh");

        var result = ActivityLogService.ArchiveOlderThan(_root, keepDays: 30);

        Assert.Equal(0, result.ArchivedCount);
        Assert.Equal(1, result.RemainingCount);
        Assert.Null(result.ArchivePath);
        Assert.Single(ActivityLogService.ReadAll(_root));
    }

    [Fact]
    public void ArchiveOlderThan_with_category_only_archives_expired_entries_in_that_category()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "telegram", "old tg");
        WriteEntry(DateTime.Now.AddDays(-40), "import", "old import");
        ActivityLogService.Append(_root, "telegram", "fresh tg");

        var result = ActivityLogService.ArchiveOlderThan(_root, keepDays: 30, category: "telegram");

        Assert.Equal(1, result.ArchivedCount);
        Assert.Equal(2, result.RemainingCount);
        Assert.NotNull(result.ArchivePath);

        var remaining = ActivityLogService.ReadAll(_root);
        Assert.Contains(remaining, entry => entry.Category == "import" && entry.Message == "old import");
        Assert.Contains(remaining, entry => entry.Category == "telegram" && entry.Message == "fresh tg");
        Assert.DoesNotContain(remaining, entry => entry.Message == "old tg");
    }

    [Fact]
    public void Trim_removes_entries_older_than_keep_days()
    {
        var oldEntry = new ActivityLogEntry
        {
            Timestamp = DateTime.Now.AddDays(-40),
            Category = "old",
            Message = "old message"
        };
        var path = ActivityLogService.GetLogPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            System.Text.Json.JsonSerializer.Serialize(oldEntry) + Environment.NewLine);
        ActivityLogService.Append(_root, "new", "fresh");

        var result = ActivityLogService.Trim(_root, keepDays: 30);

        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(1, result.RemainingCount);
    }

    [Fact]
    public void PreviewTrim_with_category_only_removes_expired_entries_in_that_category()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "telegram", "old tg");
        WriteEntry(DateTime.Now.AddDays(-40), "import", "old import");
        ActivityLogService.Append(_root, "telegram", "fresh tg");

        var preview = ActivityLogService.PreviewTrim(_root, keepDays: 30, category: "telegram");

        Assert.Equal(3, preview.TotalCount);
        Assert.Equal(1, preview.WouldRemoveCount);
        Assert.Equal(2, preview.WouldRemainCount);
        Assert.Equal(3, ActivityLogService.ReadAll(_root).Count);
    }

    [Fact]
    public void Trim_with_category_keeps_other_categories_regardless_of_age()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "telegram", "old tg");
        WriteEntry(DateTime.Now.AddDays(-40), "import", "old import");
        ActivityLogService.Append(_root, "telegram", "fresh tg");

        var result = ActivityLogService.Trim(_root, keepDays: 30, category: "telegram");

        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(2, result.RemainingCount);

        var remaining = ActivityLogService.ReadAll(_root);
        Assert.Contains(remaining, entry => entry.Category == "import" && entry.Message == "old import");
        Assert.Contains(remaining, entry => entry.Category == "telegram" && entry.Message == "fresh tg");
        Assert.DoesNotContain(remaining, entry => entry.Message == "old tg");
    }

    [Fact]
    public void ReadAll_reads_all_entries_via_line_iteration()
    {
        for (var i = 1; i <= 5; i++)
            ActivityLogService.Append(_root, "test", $"message-{i}");

        var all = ActivityLogService.ReadAll(_root);

        Assert.Equal(5, all.Count);
        Assert.Equal("message-1", all[0].Message);
        Assert.Equal("message-5", all[4].Message);
    }

    [Fact]
    public void SearchPage_returns_filtered_slice_with_pagination_metadata()
    {
        for (var i = 1; i <= 5; i++)
            ActivityLogService.Append(_root, "test", $"message-{i}");

        var firstPage = ActivityLogService.SearchPage(_root, new ActivityLogQueryOptions
        {
            Limit = 2,
            Offset = 0
        });

        Assert.Equal(5, firstPage.TotalMatched);
        Assert.Equal(2, firstPage.Entries.Count);
        Assert.Equal(0, firstPage.Offset);
        Assert.Equal(2, firstPage.Limit);
        Assert.True(firstPage.HasMore);
        Assert.Equal("message-1", firstPage.Entries[0].Message);
        Assert.Equal("message-2", firstPage.Entries[1].Message);
        Assert.Contains("匹配 5 条", firstPage.SummaryText);

        var secondPage = ActivityLogService.SearchPage(_root, new ActivityLogQueryOptions
        {
            Limit = 2,
            Offset = 2
        });

        Assert.Equal(5, secondPage.TotalMatched);
        Assert.Equal(2, secondPage.Offset);
        Assert.Equal(2, secondPage.Entries.Count);
        Assert.Equal("message-3", secondPage.Entries[0].Message);
        Assert.Equal("message-4", secondPage.Entries[1].Message);
        Assert.True(secondPage.HasMore);

        var lastPage = ActivityLogService.SearchPage(_root, new ActivityLogQueryOptions
        {
            Limit = 2,
            Offset = 4
        });

        Assert.Single(lastPage.Entries);
        Assert.Equal("message-5", lastPage.Entries[0].Message);
        Assert.False(lastPage.HasMore);
    }

    [Fact]
    public void ActivityLogService_SearchPage_second_page_HasMore_and_TotalMatched()
    {
        for (var i = 1; i <= 6; i++)
            ActivityLogService.Append(_root, "page", $"entry-{i}");

        var secondPage = ActivityLogService.SearchPage(_root, new ActivityLogQueryOptions
        {
            Limit = 2,
            Offset = 2
        });

        Assert.Equal(6, secondPage.TotalMatched);
        Assert.Equal(2, secondPage.Offset);
        Assert.Equal(2, secondPage.Limit);
        Assert.Equal(2, secondPage.Entries.Count);
        Assert.True(secondPage.HasMore);
        Assert.Equal("entry-3", secondPage.Entries[0].Message);
        Assert.Equal("entry-4", secondPage.Entries[1].Message);
    }

    [Fact]
    public void SearchPage_applies_category_and_since_days_filters_before_paging()
    {
        WriteEntry(DateTime.Now.AddDays(-10), "old", "ancient");
        WriteEntry(DateTime.Now.AddDays(-2), "telegram", "fresh tg");
        WriteEntry(DateTime.Now.AddDays(-2), "import", "fresh import");

        var page = ActivityLogService.SearchPage(_root, new ActivityLogQueryOptions
        {
            Category = "telegram",
            SinceDays = 7,
            Limit = 10,
            Offset = 0
        });

        Assert.Equal(1, page.TotalMatched);
        Assert.Single(page.Entries);
        Assert.Equal("fresh tg", page.Entries[0].Message);
        Assert.False(page.HasMore);
    }

    [Fact]
    public void GetRecentCategories_returns_categories_ordered_by_latest_activity()
    {
        WriteEntry(DateTime.Now.AddHours(-3), "import", "older import");
        WriteEntry(DateTime.Now.AddHours(-2), "telegram", "older telegram");
        WriteEntry(DateTime.Now.AddHours(-1), "import", "newer import");
        ActivityLogService.Append(_root, "export", "latest export");

        var recent = ActivityLogService.GetRecentCategories(_root, limit: 3);

        Assert.Equal(["export", "import", "telegram"], recent);
    }

    [Fact]
    public void GetRecentCategories_respects_limit_and_skips_blank_categories()
    {
        ActivityLogService.Append(_root, "telegram", "send");
        ActivityLogService.Append(_root, "import", "csv");
        ActivityLogService.Append(_root, "export", "report");
        ActivityLogService.Append(_root, " ", "blank category");
        ActivityLogService.Append(_root, "merge", "dup");

        var recent = ActivityLogService.GetRecentCategories(_root, limit: 2);

        Assert.Equal(2, recent.Count);
        Assert.Equal("merge", recent[0]);
        Assert.Equal("export", recent[1]);
    }

    [Fact]
    public void GetCategoryCounts_counts_categories_in_single_pass_without_building_full_entry_list()
    {
        ActivityLogService.Append(_root, "telegram", "send 1");
        ActivityLogService.Append(_root, "telegram", "send 2");
        ActivityLogService.Append(_root, "import", "csv");

        var counts = ActivityLogService.GetCategoryCounts(_root);

        Assert.Equal(2, counts.Count);
        Assert.Equal(2, counts["telegram"]);
        Assert.Equal(1, counts["import"]);
        Assert.Equal(["import", "telegram"], ActivityLogService.GetCategories(_root));
    }

    [Fact]
    public void GetCategoryCounts_respects_since_days_and_category_filters()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "telegram", "stale");
        ActivityLogService.Append(_root, "telegram", "fresh");
        ActivityLogService.Append(_root, "import", "csv");

        var counts = ActivityLogService.GetCategoryCounts(_root, sinceDays: 7, category: "telegram");

        Assert.Single(counts);
        Assert.Equal(1, counts["telegram"]);
    }

    [Fact]
    public void Search_finds_entries_by_message_keyword()
    {
        ActivityLogService.Append(_root, "telegram", "批量发送 TG 待发布");
        ActivityLogService.Append(_root, "import", "csv 导入");

        var results = ActivityLogService.Search(_root, "TG", limit: 10);

        Assert.Single(results);
        Assert.Contains("TG", results[0].Message);
    }

    [Fact]
    public void Search_filters_by_since_days()
    {
        WriteEntry(DateTime.Now.AddDays(-10), "old", "ancient");
        WriteEntry(DateTime.Now.AddDays(-2), "recent", "fresh");

        var results = ActivityLogService.Search(_root, query: null, limit: 10, sinceDays: 7);

        Assert.Single(results);
        Assert.Equal("fresh", results[0].Message);
    }

    [Fact]
    public void Search_since_days_takes_priority_over_since_until()
    {
        WriteEntry(DateTime.Now.AddDays(-10), "old", "ancient");
        WriteEntry(DateTime.Now.AddDays(-2), "recent", "fresh");

        var results = ActivityLogService.Search(
            _root,
            query: null,
            limit: 10,
            sinceDays: 7,
            since: DateTime.Now.AddDays(-30));

        Assert.Single(results);
        Assert.Equal("fresh", results[0].Message);
    }

    [Fact]
    public void Export_filters_by_date_range()
    {
        WriteEntry(DateTime.Now.AddDays(-10), "old", "ancient");
        WriteEntry(DateTime.Now.AddDays(-2), "recent", "fresh");

        var export = ActivityLogService.Export(_root, sinceDays: 7);

        Assert.Equal(1, export.EntryCount);
        Assert.True(File.Exists(export.ExportPath));
    }

    [Fact]
    public void PreviewTrim_summary_includes_category_and_since_days_hints()
    {
        WriteEntry(DateTime.Now.AddDays(-5), "telegram", "in range old");
        WriteEntry(DateTime.Now.AddDays(-2), "telegram", "in range fresh");

        var preview = ActivityLogService.PreviewTrim(
            _root,
            keepDays: 3,
            category: "telegram",
            sinceDays: 7);

        Assert.Contains("分类 telegram", preview.SummaryText);
        Assert.Contains("近 7 天", preview.SummaryText);
        Assert.Contains("清理预览", preview.SummaryText);
    }

    [Fact]
    public void PreviewArchive_summary_includes_category_and_since_days_hints()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "telegram", "old tg");
        WriteEntry(DateTime.Now.AddDays(-2), "telegram", "fresh tg");

        var preview = ActivityLogService.PreviewArchive(
            _root,
            keepDays: 30,
            category: "telegram",
            sinceDays: 7);

        Assert.Contains("分类 telegram", preview.SummaryText);
        Assert.Contains("近 7 天", preview.SummaryText);
        Assert.Contains("归档预览", preview.SummaryText);
        Assert.Contains("将归档", preview.SummaryText);
    }

    [Fact]
    public void PreviewTrim_with_date_range_only_operates_on_in_range_entries()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "old", "out of range old");
        WriteEntry(DateTime.Now.AddDays(-5), "in-range", "in range old");
        WriteEntry(DateTime.Now.AddDays(-2), "in-range", "in range fresh");

        var preview = ActivityLogService.PreviewTrim(
            _root,
            keepDays: 3,
            category: "in-range",
            sinceDays: 7);

        Assert.Equal(2, preview.TotalCount);
        Assert.Equal(1, preview.WouldRemoveCount);
        Assert.Equal(2, preview.WouldRemainCount);
        Assert.Equal(3, ActivityLogService.ReadAll(_root).Count);
    }

    [Fact]
    public void Trim_with_date_range_preserves_out_of_range_entries()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "old", "out of range old");
        WriteEntry(DateTime.Now.AddDays(-5), "in-range", "in range old");
        WriteEntry(DateTime.Now.AddDays(-2), "in-range", "in range fresh");

        var result = ActivityLogService.Trim(
            _root,
            keepDays: 3,
            category: "in-range",
            sinceDays: 7);

        Assert.Equal(1, result.RemovedCount);
        Assert.Equal(2, result.RemainingCount);

        var remaining = ActivityLogService.ReadAll(_root);
        Assert.Contains(remaining, entry => entry.Message == "out of range old");
        Assert.Contains(remaining, entry => entry.Message == "in range fresh");
        Assert.DoesNotContain(remaining, entry => entry.Message == "in range old");
    }

    private void WriteEntry(DateTime timestamp, string category, string message)
    {
        var entry = new ActivityLogEntry
        {
            Timestamp = timestamp,
            Category = category,
            Message = message
        };
        var path = ActivityLogService.GetLogPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path,
            System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine);
    }
}
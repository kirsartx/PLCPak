using System.Text;
using System.Text.Json;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class ActivityLogService
{
    public const string LogFileName = "activity.log";

    private static readonly JsonSerializerOptions LineOptions = new(JsonHelper.Options)
    {
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions LogLineOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string GetLogPath(string workspaceRoot)
        => Path.Combine(workspaceRoot, "logs", LogFileName);

    public static void Append(string workspaceRoot, string category, string message)
    {
        var entry = new ActivityLogEntry
        {
            Timestamp = DateTime.Now,
            Category = category?.Trim() ?? string.Empty,
            Message = message?.Trim() ?? string.Empty
        };

        var logsDir = Path.Combine(workspaceRoot, "logs");
        Directory.CreateDirectory(logsDir);

        var line = JsonSerializer.Serialize(entry, LogLineOptions);
        File.AppendAllText(GetLogPath(workspaceRoot), line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static List<ActivityLogEntry> ReadRecent(string workspaceRoot, int limit = 50, string? category = null)
    {
        var options = new ActivityLogQueryOptions
        {
            Category = category,
            Limit = limit
        };
        return FilterEntries(ReadAll(workspaceRoot), options);
    }

    public static List<ActivityLogEntry> ReadAll(string workspaceRoot)
    {
        var path = GetLogPath(workspaceRoot);
        if (!File.Exists(path))
            return [];

        var entries = new List<ActivityLogEntry>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = TryParseLine(line);
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }

    public static List<ActivityLogEntry> Search(
        string workspaceRoot,
        string? query,
        int limit = 50,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var options = new ActivityLogQueryOptions
        {
            Query = query,
            Limit = limit,
            Category = category,
            SinceDays = sinceDays,
            Since = since,
            Until = until
        };
        return FilterEntries(ReadAll(workspaceRoot), options);
    }

    public static ActivityLogPageResult SearchPage(string workspaceRoot, ActivityLogQueryOptions options)
    {
        var filtered = FilterEntries(ReadAll(workspaceRoot), options, applyLimit: false);
        var offset = Math.Max(0, options.Offset);
        var limit = Math.Max(1, options.Limit);
        var total = filtered.Count;
        var page = filtered.Skip(offset).Take(limit).ToList();
        var hasMore = offset + page.Count < total;

        return new ActivityLogPageResult
        {
            Entries = page,
            TotalMatched = total,
            Offset = offset,
            Limit = limit,
            HasMore = hasMore,
            SummaryText = BuildPageSummaryText(total, offset, limit, page.Count, hasMore)
        };
    }

    public static IReadOnlyDictionary<string, int> GetCategoryCounts(
        string workspaceRoot,
        int? sinceDays = null,
        string? category = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var options = new ActivityLogQueryOptions
        {
            Category = category,
            SinceDays = sinceDays,
            Since = since,
            Until = until
        };
        var (sinceBound, untilBound) = ResolveDateRange(options);
        var categoryFilter = string.IsNullOrWhiteSpace(options.Category) ? null : options.Category.Trim();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var path = GetLogPath(workspaceRoot);
        if (!File.Exists(path))
            return counts;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = TryParseLine(line);
            if (entry is null)
                continue;

            if (categoryFilter is not null
                && !entry.Category.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsInDateRange(entry, sinceBound, untilBound))
                continue;

            var normalized = string.IsNullOrWhiteSpace(entry.Category) ? string.Empty : entry.Category.Trim();
            counts.TryGetValue(normalized, out var current);
            counts[normalized] = current + 1;
        }

        return counts;
    }

    public static IReadOnlyList<string> GetCategories(string workspaceRoot)
        => GetCategoryCounts(workspaceRoot).Keys
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> GetRecentCategories(string workspaceRoot, int limit = 5)
    {
        var take = Math.Max(1, limit);
        var latestByCategory = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var path = GetLogPath(workspaceRoot);
        if (!File.Exists(path))
            return [];

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = TryParseLine(line);
            if (entry is null)
                continue;

            var category = string.IsNullOrWhiteSpace(entry.Category) ? string.Empty : entry.Category.Trim();
            if (string.IsNullOrWhiteSpace(category))
                continue;

            if (!latestByCategory.TryGetValue(category, out var existing) || entry.Timestamp > existing)
                latestByCategory[category] = entry.Timestamp;
        }

        return latestByCategory
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(pair => pair.Key)
            .ToList();
    }

    public static ActivityLogExportResult Export(
        string workspaceRoot,
        string? outputPath = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var options = new ActivityLogQueryOptions
        {
            Category = category,
            SinceDays = sinceDays,
            Since = since,
            Until = until,
            Limit = int.MaxValue
        };
        var entries = FilterEntries(ReadAll(workspaceRoot), options, applyLimit: false);

        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, entries);

        return new ActivityLogExportResult
        {
            ExportPath = fullPath,
            EntryCount = entries.Count,
            CategoryFilter = string.IsNullOrWhiteSpace(category) ? null : category.Trim()
        };
    }

    public static ActivityLogTrimPreviewResult PreviewTrim(
        string workspaceRoot,
        int keepDays = 30,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var options = BuildTrimOptions(category, sinceDays, since, until);
        var (remaining, removing, keep, inRangeCount) = ComputeTrimEntries(workspaceRoot, keepDays, options);
        var categoryHint = string.IsNullOrWhiteSpace(category) ? string.Empty : $"（分类 {category.Trim()}）";
        var rangeHint = BuildRangeHint(options);

        return new ActivityLogTrimPreviewResult
        {
            TotalCount = inRangeCount,
            WouldRemoveCount = removing.Count,
            WouldRemainCount = remaining.Count,
            KeepDays = keep,
            SummaryText = BuildTrimPreviewSummaryText(inRangeCount, removing.Count, remaining.Count, keep, categoryHint, rangeHint)
        };
    }

    public static ActivityLogTrimPreviewResult PreviewArchive(
        string workspaceRoot,
        int keepDays = 30,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var options = BuildTrimOptions(category, sinceDays, since, until);
        var (remaining, removing, keep, inRangeCount) = ComputeTrimEntries(workspaceRoot, keepDays, options);
        var categoryHint = string.IsNullOrWhiteSpace(category) ? string.Empty : $"（分类 {category.Trim()}）";
        var rangeHint = BuildRangeHint(options);

        return new ActivityLogTrimPreviewResult
        {
            TotalCount = inRangeCount,
            WouldRemoveCount = removing.Count,
            WouldRemainCount = remaining.Count,
            KeepDays = keep,
            SummaryText = BuildArchivePreviewSummaryText(inRangeCount, removing.Count, remaining.Count, keep, categoryHint, rangeHint)
        };
    }

    public static ActivityLogArchiveResult ArchiveOlderThan(
        string workspaceRoot,
        int keepDays = 30,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var options = BuildTrimOptions(category, sinceDays, since, until);
        var (remaining, removing, keep, _) = ComputeTrimEntries(workspaceRoot, keepDays, options);
        string? archivePath = null;
        var categoryHint = string.IsNullOrWhiteSpace(category) ? string.Empty : $"，分类 {category.Trim()}";
        var rangeHint = BuildRangeHint(options);

        if (removing.Count > 0)
        {
            archivePath = ResolveArchivePath(workspaceRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
            JsonHelper.WriteFile(archivePath, removing);
            WriteRemainingEntries(workspaceRoot, remaining);
        }

        return new ActivityLogArchiveResult
        {
            ArchivedCount = removing.Count,
            RemainingCount = remaining.Count,
            KeepDays = keep,
            ArchivePath = archivePath,
            SummaryText = removing.Count == 0
                ? $"活动日志归档：暂无超过 {keep} 天的记录，主日志保留 {remaining.Count} 条（{keep} 天内{categoryHint}{rangeHint}）"
                : $"活动日志归档：{removing.Count} 条已移至 {archivePath}，主日志保留 {remaining.Count} 条（{keep} 天内{categoryHint}{rangeHint}）"
        };
    }

    public static ActivityLogTrimResult Trim(
        string workspaceRoot,
        int keepDays = 30,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var options = BuildTrimOptions(category, sinceDays, since, until);
        var (remaining, removing, keep, _) = ComputeTrimEntries(workspaceRoot, keepDays, options);
        var removed = removing.Count;

        WriteRemainingEntries(workspaceRoot, remaining);

        var categoryHint = string.IsNullOrWhiteSpace(category) ? string.Empty : $"，分类 {category.Trim()}";
        var rangeHint = BuildRangeHint(options);

        return new ActivityLogTrimResult
        {
            RemovedCount = removed,
            RemainingCount = remaining.Count,
            KeepDays = keep,
            SummaryText = $"活动日志清理：删除 {removed} 条，保留 {remaining.Count} 条（{keep} 天内{categoryHint}{rangeHint}）"
        };
    }

    private static List<ActivityLogEntry> FilterEntries(
        IReadOnlyList<ActivityLogEntry> entries,
        ActivityLogQueryOptions options,
        bool applyLimit = true)
    {
        var filtered = entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(options.Category))
        {
            var normalized = options.Category.Trim();
            filtered = filtered
                .Where(entry => entry.Category.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }

        var (since, until) = ResolveDateRange(options);
        if (since is not null)
            filtered = filtered.Where(entry => entry.Timestamp >= since.Value);
        if (until is not null)
            filtered = filtered.Where(entry => entry.Timestamp <= until.Value);

        var keyword = options.Query?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered
                .Where(entry => entry.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        if (!applyLimit)
            return list;

        var take = Math.Max(1, options.Limit);
        if (list.Count <= take)
            return list;

        return list
            .Skip(list.Count - take)
            .ToList();
    }

    private static (DateTime? Since, DateTime? Until) ResolveDateRange(ActivityLogQueryOptions options)
    {
        if (options.SinceDays is int days && days > 0)
            return (DateTime.Now.AddDays(-days), null);

        return (options.Since, options.Until);
    }

    private static bool HasDateRange(ActivityLogQueryOptions options)
        => options.SinceDays is > 0 || options.Since is not null || options.Until is not null;

    private static bool IsInDateRange(ActivityLogEntry entry, DateTime? since, DateTime? until)
    {
        if (since is not null && entry.Timestamp < since.Value)
            return false;
        if (until is not null && entry.Timestamp > until.Value)
            return false;
        return true;
    }

    private static ActivityLogQueryOptions BuildTrimOptions(
        string? category,
        int? sinceDays,
        DateTime? since,
        DateTime? until)
        => new()
        {
            Category = category,
            SinceDays = sinceDays,
            Since = since,
            Until = until,
            Limit = int.MaxValue
        };

    private static string BuildRangeHint(ActivityLogQueryOptions options)
    {
        if (!HasDateRange(options))
            return string.Empty;

        if (options.SinceDays is int days && days > 0)
            return $"，范围近 {days} 天";

        var parts = new List<string>();
        if (options.Since is not null)
            parts.Add($"自 {options.Since.Value:yyyy-MM-dd}");
        if (options.Until is not null)
            parts.Add($"至 {options.Until.Value:yyyy-MM-dd}");

        return parts.Count == 0 ? string.Empty : "，范围 " + string.Join(" ", parts);
    }

    private static string BuildTrimPreviewSummaryText(
        int inRangeCount,
        int removingCount,
        int remainingCount,
        int keepDays,
        string categoryHint,
        string rangeHint)
        => inRangeCount == 0
            ? $"活动日志清理预览：范围内暂无记录（保留 {keepDays} 天{categoryHint}{rangeHint}）"
            : $"活动日志清理预览：范围内 {inRangeCount} 条，将删除 {removingCount} 条（超过 {keepDays} 天{categoryHint}{rangeHint}），保留 {remainingCount} 条";

    private static string BuildArchivePreviewSummaryText(
        int inRangeCount,
        int archivingCount,
        int remainingCount,
        int keepDays,
        string categoryHint,
        string rangeHint)
        => inRangeCount == 0
            ? $"活动日志归档预览：范围内暂无记录（保留 {keepDays} 天{categoryHint}{rangeHint}）"
            : $"活动日志归档预览：范围内 {inRangeCount} 条，将归档 {archivingCount} 条（超过 {keepDays} 天{categoryHint}{rangeHint}），主日志保留 {remainingCount} 条";

    private static (List<ActivityLogEntry> Remaining, List<ActivityLogEntry> Removing, int KeepDays, int InRangeCount) ComputeTrimEntries(
        string workspaceRoot,
        int keepDays,
        ActivityLogQueryOptions? options = null)
    {
        var keep = Math.Max(1, keepDays);
        var cutoff = DateTime.Now.AddDays(-keep);
        var entries = ReadAll(workspaceRoot);
        options ??= new ActivityLogQueryOptions();
        var (since, until) = ResolveDateRange(options);
        var hasRange = HasDateRange(options);

        if (string.IsNullOrWhiteSpace(options.Category))
        {
            var removing = entries
                .Where(entry => (!hasRange || IsInDateRange(entry, since, until))
                    && entry.Timestamp < cutoff)
                .ToList();
            var remaining = entries
                .Where(entry => hasRange && !IsInDateRange(entry, since, until)
                    || entry.Timestamp >= cutoff)
                .ToList();
            var inRangeCount = hasRange
                ? entries.Count(entry => IsInDateRange(entry, since, until))
                : entries.Count;
            return (remaining, removing, keep, inRangeCount);
        }

        var normalized = options.Category.Trim();
        var removingFiltered = entries
            .Where(entry => entry.Category.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                && (!hasRange || IsInDateRange(entry, since, until))
                && entry.Timestamp < cutoff)
            .ToList();
        var remainingFiltered = entries
            .Where(entry => !entry.Category.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || (hasRange && !IsInDateRange(entry, since, until))
                || entry.Timestamp >= cutoff)
            .ToList();
        var inRangeFiltered = hasRange
            ? entries.Count(entry => IsInDateRange(entry, since, until)
                && entry.Category.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            : entries.Count;
        return (remainingFiltered, removingFiltered, keep, inRangeFiltered);
    }

    private static string BuildPageSummaryText(int total, int offset, int limit, int pageCount, bool hasMore)
    {
        if (total == 0)
            return $"活动日志：暂无匹配记录（偏移 {offset}，每页 {limit}）";

        var end = offset + pageCount;
        var moreHint = hasMore ? "，还有更多" : string.Empty;
        return $"活动日志：匹配 {total} 条，本页 {pageCount} 条（{offset + 1}-{end}，每页 {limit}）{moreHint}";
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"activity-log-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(reportsDir, fileName);
    }

    private static string ResolveArchivePath(string workspaceRoot)
    {
        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"activity-archive-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(reportsDir, fileName);
    }

    private static void WriteRemainingEntries(string workspaceRoot, IReadOnlyList<ActivityLogEntry> remaining)
    {
        var path = GetLogPath(workspaceRoot);
        if (remaining.Count == 0)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        var logsDir = Path.Combine(workspaceRoot, "logs");
        Directory.CreateDirectory(logsDir);
        var lines = remaining
            .Select(entry => JsonSerializer.Serialize(entry, LogLineOptions))
            .ToList();
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static ActivityLogEntry? TryParseLine(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<ActivityLogEntry>(line.Trim(), LogLineOptions);
        }
        catch
        {
            return TryParsePlainTextLine(line);
        }
    }

    private static ActivityLogEntry? TryParsePlainTextLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 20 || trimmed[0] != '[')
            return null;

        var close = trimmed.IndexOf(']');
        if (close <= 1)
            return null;

        var timestampText = trimmed[1..close];
        if (!DateTime.TryParse(timestampText, out var timestamp))
            return null;

        var remainder = trimmed[(close + 1)..].Trim();
        var colon = remainder.IndexOf(':');
        if (colon <= 0)
        {
            return new ActivityLogEntry
            {
                Timestamp = timestamp,
                Category = "general",
                Message = remainder
            };
        }

        return new ActivityLogEntry
        {
            Timestamp = timestamp,
            Category = remainder[..colon].Trim(),
            Message = remainder[(colon + 1)..].Trim()
        };
    }
}
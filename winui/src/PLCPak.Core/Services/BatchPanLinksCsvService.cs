using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchPanLinksCsvService
{
    private static readonly string[] HeaderMarkers = ["jobid", "job_id", "title"];

    public static IReadOnlyList<BatchPanLinksCsvRow> ParseLines(IEnumerable<string> lines)
    {
        var rows = new List<BatchPanLinksCsvRow>();
        var isFirst = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (isFirst && LooksLikeHeader(line))
            {
                isFirst = false;
                continue;
            }

            isFirst = false;
            var fields = ParseCsvLine(line);
            if (fields.Count == 0)
                continue;

            rows.Add(new BatchPanLinksCsvRow
            {
                JobId = GetField(fields, 0),
                Title = GetField(fields, 1),
                BaiduLink = GetField(fields, 2),
                BaiduPwd = GetField(fields, 3),
                QuarkLink = GetField(fields, 4),
                QuarkPwd = GetField(fields, 5),
                TelegramLink = GetField(fields, 6)
            });
        }

        return rows;
    }

    public static IReadOnlyList<BatchPanLinksCsvRow> ParseFile(string path)
        => ParseLines(File.ReadAllLines(path));

    public static BatchPanLinksImportResult ApplyToJobs(
        JobStore jobStore,
        JobRunner jobRunner,
        IEnumerable<BatchPanLinksCsvRow> rows)
    {
        var result = new BatchPanLinksImportResult { Success = true };
        var jobs = jobStore.List();

        foreach (var row in rows)
        {
            if (IsRowEmpty(row))
            {
                result.Failed++;
                result.Messages.Add("[失败] 空行或无有效字段");
                continue;
            }

            var job = ResolveJob(jobs, row);
            if (job is null)
            {
                result.Failed++;
                var key = !string.IsNullOrWhiteSpace(row.JobId) ? row.JobId : row.Title;
                result.Messages.Add($"[失败] 未找到任务: {key}");
                continue;
            }

            try
            {
                jobRunner.SavePublishLinks(
                    job.Id,
                    NullIfEmpty(row.BaiduLink),
                    NullIfEmpty(row.BaiduPwd),
                    NullIfEmpty(row.QuarkLink),
                    NullIfEmpty(row.QuarkPwd),
                    string.IsNullOrWhiteSpace(row.TelegramLink) ? null : row.TelegramLink.Trim());

                result.Applied++;
                result.Messages.Add($"[成功] {job.Title}");
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"[失败] {job.Title}: {ex.Message}");
            }
        }

        if (result.Applied == 0 && result.Failed > 0)
            result.Success = false;

        return result;
    }

    private static PublishJob? ResolveJob(IReadOnlyList<PublishJob> jobs, BatchPanLinksCsvRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.JobId))
        {
            var byId = jobs.FirstOrDefault(j => j.Id.Equals(row.JobId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(row.Title))
        {
            return jobs.FirstOrDefault(j => j.Title.Equals(row.Title.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static bool IsRowEmpty(BatchPanLinksCsvRow row)
        => string.IsNullOrWhiteSpace(row.JobId)
            && string.IsNullOrWhiteSpace(row.Title)
            && string.IsNullOrWhiteSpace(row.BaiduLink)
            && string.IsNullOrWhiteSpace(row.BaiduPwd)
            && string.IsNullOrWhiteSpace(row.QuarkLink)
            && string.IsNullOrWhiteSpace(row.QuarkPwd)
            && string.IsNullOrWhiteSpace(row.TelegramLink);

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool LooksLikeHeader(string line)
    {
        var first = ParseCsvLine(line).FirstOrDefault()?.Trim();
        return !string.IsNullOrWhiteSpace(first)
            && HeaderMarkers.Any(marker => first.Equals(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetField(IReadOnlyList<string> fields, int index)
        => index < fields.Count ? fields[index].Trim() : string.Empty;

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }
}
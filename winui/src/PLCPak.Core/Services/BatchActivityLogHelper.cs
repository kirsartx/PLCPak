namespace PLCPak.Core.Services;

public static class BatchActivityLogHelper
{
    public static string FormatJobIdSample(IEnumerable<string> jobIds, int maxSample = 5)
    {
        var ids = jobIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return string.Empty;

        if (ids.Count <= maxSample)
            return string.Join(", ", ids);

        var sample = string.Join(", ", ids.Take(maxSample));
        return $"{sample} 等 {ids.Count} 个";
    }
}
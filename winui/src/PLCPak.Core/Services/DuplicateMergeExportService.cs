using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class DuplicateMergeExportService
{
    public static DuplicateMergeExportResult Export(
        DuplicateMergeSuggestionResult suggestions,
        string workspaceRoot,
        string? outputPath = null)
    {
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, suggestions, utf8Bom: true);

        return new DuplicateMergeExportResult
        {
            ExportPath = fullPath,
            GroupCount = suggestions.GroupCount,
            MergeActionCount = suggestions.MergeActionCount,
            EntryCount = suggestions.Suggestions.Count
        };
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"duplicate-merge-suggestions-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(reportsDir, fileName);
    }
}
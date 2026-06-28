using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchDuplicateMergePreviewExportService
{
    public static BatchDuplicateMergePreviewExportResult Export(
        BatchDuplicateMergePreviewResult preview,
        string workspaceRoot,
        string? outputPath = null)
    {
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, preview, utf8Bom: true);

        return new BatchDuplicateMergePreviewExportResult
        {
            ExportPath = fullPath,
            GroupCount = preview.GroupCount,
            MergeActionCount = preview.MergeActionCount,
            ItemCount = preview.Items.Count
        };
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"batch-merge-preview-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(reportsDir, fileName);
    }
}
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class JobFolderRemovalService
{
    public static IReadOnlyList<string> GetRemovableDirectories(PublishJob job, WorkspaceService workspace)
    {
        var paths = new List<string>();
        foreach (var dir in new[] { job.Paths.Inbox, job.Paths.Extract, job.Paths.Output })
        {
            if (!string.IsNullOrWhiteSpace(dir))
                paths.Add(dir);
        }

        if (!string.IsNullOrWhiteSpace(job.Paths.Slug))
            paths.Add(Path.Combine(workspace.PublishedDirectory, job.Paths.Slug));

        return paths;
    }

    public static List<string> RemoveJobFolders(
        PublishJob job,
        WorkspaceService workspace,
        bool useRecycleBin)
    {
        var removed = new List<string>();
        foreach (var dir in GetRemovableDirectories(job, workspace))
        {
            if (!Directory.Exists(dir))
                continue;

            if (useRecycleBin)
                AdCleanupService.RemoveItem(dir, isDirectory: true, useRecycleBin: true);
            else
                Directory.Delete(dir, recursive: true);

            removed.Add(dir);
        }

        return removed;
    }
}
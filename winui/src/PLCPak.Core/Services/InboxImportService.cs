using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class InboxImportService
{
    public static IEnumerable<string> CollectArchivePaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            if (File.Exists(path))
            {
                if (WorkspaceService.IsArchiveFile(path))
                    yield return path;
                continue;
            }

            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (WorkspaceService.IsArchiveFile(file))
                    yield return file;
            }
        }
    }

    public static string ResolveUniquePath(string directory, string fileName)
    {
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "archive.bin" : fileName;
        var dest = Path.Combine(directory, safeName);
        if (!File.Exists(dest))
            return dest;

        var stem = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);
        var index = 2;
        while (true)
        {
            var candidate = Path.Combine(directory, $"{stem}_{index}{ext}");
            if (!File.Exists(candidate))
                return candidate;
            index++;
        }
    }

    public static int CopyArchivesToInbox(string inboxDirectory, IEnumerable<string> sourceArchives, List<string> copiedPaths)
    {
        Directory.CreateDirectory(inboxDirectory);
        var copied = 0;

        foreach (var source in sourceArchives)
        {
            if (!File.Exists(source))
                continue;

            var dest = ResolveUniquePath(inboxDirectory, Path.GetFileName(source));
            File.Copy(source, dest, overwrite: false);
            copiedPaths.Add(dest);
            copied++;
        }

        return copied;
    }
}
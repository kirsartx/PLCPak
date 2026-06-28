using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class JobBackupService
{
    public static JobBackupSnapshot ExportAll(IEnumerable<PublishJob> jobs)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        return new JobBackupSnapshot
        {
            Version = AppVersion.Current,
            ExportedAt = DateTime.Now,
            Count = list.Count,
            Jobs = list.ToList()
        };
    }

    public static string ExportToFile(string workspaceRoot, IEnumerable<PublishJob> jobs)
    {
        var snapshot = ExportAll(jobs);
        var backupsDir = Path.Combine(workspaceRoot, "backups");
        Directory.CreateDirectory(backupsDir);

        var path = Path.Combine(backupsDir, $"jobs-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        JsonHelper.WriteFile(path, snapshot, utf8Bom: true);
        return path;
    }

    public static JobBackupImportResult ImportFromFile(JobStore jobStore, string path, JobBackupImportMode mode)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"备份文件不存在: {fullPath}");

        var snapshot = JsonHelper.ReadFile<JobBackupSnapshot>(fullPath)
            ?? throw new InvalidOperationException("无法解析任务备份 JSON");

        var jobs = snapshot.Jobs ?? [];
        var result = new JobBackupImportResult();

        foreach (var job in jobs)
        {
            if (job is null)
                continue;

            var existing = string.IsNullOrWhiteSpace(job.Id) ? null : jobStore.Get(job.Id);

            if (mode == JobBackupImportMode.SkipExisting && existing is not null)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title} ({job.Id}): 已存在");
                continue;
            }

            var hadConflict = existing is not null;
            var imported = jobStore.Import(job);
            result.Imported++;

            if (hadConflict)
            {
                result.Updated++;
                result.Messages.Add($"[更新] {imported.Title}: 冲突 ID 已导入为新任务 {imported.Id}");
            }
            else
            {
                result.Messages.Add($"[导入] {imported.Title} ({imported.Id})");
            }
        }

        return result;
    }
}
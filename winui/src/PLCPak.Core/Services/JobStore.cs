using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class JobStore
{
    private readonly WorkspaceService _workspace;

    public JobStore(WorkspaceService workspace) => _workspace = workspace;

    public IReadOnlyList<PublishJob> List()
    {
        _workspace.EnsureLayout();
        var jobs = new List<PublishJob>();
        foreach (var file in Directory.EnumerateFiles(_workspace.JobsDirectory, "*.json"))
        {
            var job = JsonHelper.ReadFile<PublishJob>(file);
            if (job is not null)
                jobs.Add(job);
        }

        return jobs.OrderByDescending(j => j.UpdatedAt).ToList();
    }

    public PublishJob? Get(string id)
    {
        var path = GetJobPath(id);
        return File.Exists(path) ? JsonHelper.ReadFile<PublishJob>(path) : null;
    }

    public void Save(PublishJob job)
    {
        _workspace.EnsureLayout();
        job.UpdatedAt = DateTime.Now;
        JsonHelper.WriteFile(GetJobPath(job.Id), job);
    }

    public PublishJob Create(string title, JobSource? source = null, JobPlatform platform = JobPlatform.Both, string? slugOverride = null)
    {
        var existing = List();
        var slug = slugOverride ?? JobQueryService.EnsureUniqueSlug(title, existing);
        var paths = _workspace.BuildJobPaths(slug);
        Directory.CreateDirectory(paths.Inbox);
        Directory.CreateDirectory(paths.Extract);
        Directory.CreateDirectory(paths.Output);

        var job = new PublishJob
        {
            Title = title.Trim(),
            Source = source ?? new JobSource(),
            Platform = platform,
            Status = JobStatus.Draft,
            Paths = paths
        };
        job.AppendLog($"创建任务: {job.Title}");
        Save(job);
        return job;
    }

    public PublishJob Archive(string id)
    {
        var job = Get(id) ?? throw new InvalidOperationException($"任务不存在: {id}");
        if (job.Status != JobStatus.Archived)
            job.ArchivedFromStatus = job.Status;
        job.Status = JobStatus.Archived;
        job.Error = null;
        job.AppendLog("任务已归档");
        Save(job);
        return job;
    }

    public PublishJob Unarchive(string id)
    {
        var job = Get(id) ?? throw new InvalidOperationException($"任务不存在: {id}");
        if (job.Status != JobStatus.Archived)
            throw new InvalidOperationException("任务未归档，无需恢复");

        job.Artifacts.InboxArchives = _workspace.FindArchives(job.Paths.Inbox);
        if (Directory.Exists(job.Paths.Output))
        {
            job.Artifacts.OutputArchives = Directory.EnumerateFiles(job.Paths.Output, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        job.Status = InferStatusFromArtifacts(job);
        job.ArchivedFromStatus = null;
        job.Error = null;
        job.AppendLog($"任务已取消归档，恢复状态: {job.Status}");
        Save(job);
        return job;
    }

    public PublishJob Import(PublishJob job, bool createFolders = true)
    {
        _workspace.EnsureLayout();
        if (string.IsNullOrWhiteSpace(job.Id))
            job.Id = Guid.NewGuid().ToString("N");

        var existing = Get(job.Id);
        if (existing is not null)
        {
            job.Id = Guid.NewGuid().ToString("N");
            job.Paths.Slug = JobQueryService.EnsureUniqueSlug(job.Title, List());
            job.Paths = _workspace.BuildJobPaths(job.Paths.Slug);
        }
        else if (List().Any(j => j.Paths.Slug.Equals(job.Paths.Slug, StringComparison.OrdinalIgnoreCase)))
        {
            job.Paths.Slug = JobQueryService.EnsureUniqueSlug(job.Title, List());
            job.Paths = _workspace.BuildJobPaths(job.Paths.Slug);
        }

        if (createFolders)
        {
            Directory.CreateDirectory(job.Paths.Inbox);
            Directory.CreateDirectory(job.Paths.Extract);
            Directory.CreateDirectory(job.Paths.Output);
        }

        job.UpdatedAt = DateTime.Now;
        if (job.CreatedAt == default)
            job.CreatedAt = job.UpdatedAt;

        job.AppendLog(existing is null ? "任务已从 JSON 导入" : "任务已从 JSON 导入（已分配新 ID）");
        Save(job);
        return job;
    }

    private JobStatus InferStatusFromArtifacts(PublishJob job)
    {
        if (HasOutput(job))
            return JobStatus.Processed;

        var archives = job.Artifacts.InboxArchives.Count > 0
            ? job.Artifacts.InboxArchives
            : _workspace.FindArchives(job.Paths.Inbox);

        return archives.Count > 0 ? JobStatus.InboxReady : JobStatus.Draft;
    }

    private static bool HasOutput(PublishJob job)
    {
        if (job.Artifacts.OutputArchives.Count > 0)
            return true;

        return !string.IsNullOrWhiteSpace(job.Paths.Output)
            && Directory.Exists(job.Paths.Output)
            && Directory.EnumerateFiles(job.Paths.Output, "*", SearchOption.AllDirectories).Any();
    }

    public void Delete(string id)
    {
        var path = GetJobPath(id);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetJobPath(string id) => Path.Combine(_workspace.JobsDirectory, $"{id}.json");
}
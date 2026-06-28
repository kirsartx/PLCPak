using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class PipelineService
{
    private readonly AppPaths _paths;
    private readonly AdCleanupService _cleanup;
    private readonly Dictionary<string, PipelineTask> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _scanSemaphore = new(5, 5);
    private readonly List<Task> _backgroundScans = [];

    public PipelineService(AppPaths paths, AdCleanupService cleanup)
    {
        _paths = paths;
        _cleanup = cleanup;
    }

    public event Action? TasksChanged;

    public IReadOnlyDictionary<string, PipelineTask> Tasks => _tasks;

    public void NewTask(string path)
    {
        _tasks[path] = new PipelineTask
        {
            Path = path,
            State = PipelineTaskState.PendingScan
        };
    }

    public void SetTaskFromScan(string path, AdCleanupResult result)
    {
        if (!_tasks.ContainsKey(path))
            NewTask(path);

        var task = _tasks[path];
        task.ScanResult = result;
        task.Scanned = result.TotalScanned;
        task.Matched = result.TotalMatched;
        task.State = result.TotalMatched > 0
            ? PipelineTaskState.PendingConfirm
            : PipelineTaskState.NoAds;
        TasksChanged?.Invoke();
    }

    public Task StartPreviewScanAsync(string path, CompressConfig config, CancellationToken cancellationToken = default)
    {
        NewTask(path);
        var scanTask = Task.Run(async () =>
        {
            await _scanSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var result = _cleanup.Invoke(path, config, previewOnly: true, cancellationToken);
                SetTaskFromScan(path, result);
            }
            finally
            {
                _scanSemaphore.Release();
            }
        }, cancellationToken);

        lock (_backgroundScans)
            _backgroundScans.Add(scanTask);

        scanTask.ContinueWith(_ =>
        {
            lock (_backgroundScans)
                _backgroundScans.Remove(scanTask);
        }, TaskScheduler.Default);

        return scanTask;
    }

    public async Task WaitForBackgroundScansAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Task[] pending;
        lock (_backgroundScans)
            pending = _backgroundScans.Where(t => !t.IsCompleted).ToArray();

        if (pending.Length == 0)
            return;

        var all = Task.WhenAll(pending);
        var delay = Task.Delay(timeout, cancellationToken);
        await Task.WhenAny(all, delay).ConfigureAwait(false);
    }

    public void RemoveTask(string path)
    {
        if (_tasks.Remove(path))
            TasksChanged?.Invoke();
    }

    public string GetTaskSummary()
    {
        var lines = _tasks.Values.Select(t =>
            $"{StateLabel(t.State)} | 匹配{t.Matched} | {Path.GetFileName(t.Path)}");
        return string.Join(Environment.NewLine, lines);
    }

    public bool NeedsCleanupConfirmation(CompressConfig config, bool force)
    {
        var need = _tasks.Values
            .Where(t => t.Matched > 0 && !t.Cleaned)
            .ToList();

        if (need.Count == 0)
            return false;

        var total = need.Sum(t => t.Matched);
        if (force)
            return false;

        return config.PreviewBeforeClean || total >= config.AdConfirmThreshold;
    }

    public CleanupConfirmation BuildCleanupConfirmation(CompressConfig config, bool force = false)
    {
        var need = _tasks.Values
            .Where(t => t.Matched > 0 && !t.Cleaned)
            .ToList();

        var total = need.Sum(t => t.Matched);
        var requires = !force && need.Count > 0
            && (config.PreviewBeforeClean || total >= config.AdConfirmThreshold);

        return new CleanupConfirmation
        {
            FolderCount = need.Count,
            TotalMatched = total,
            RequiresConfirmation = requires,
            Summary = GetTaskSummary()
        };
    }

    public void CleanAll(CompressConfig config, CancellationToken cancellationToken = default)
    {
        foreach (var task in _tasks.Values.ToList())
        {
            if (task.Matched <= 0 || task.Cleaned)
                continue;

            if (!Directory.Exists(task.Path))
                continue;

            var result = _cleanup.Invoke(task.Path, config, previewOnly: false, cancellationToken);
            WriteSessionLog(task.Path, result);
            task.Cleaned = true;
            task.State = PipelineTaskState.Cleaned;
        }
    }

    public AdCleanupResult CleanPath(string path, CompressConfig config, CancellationToken cancellationToken = default)
    {
        var result = _cleanup.Invoke(path, config, previewOnly: false, cancellationToken);
        if (_tasks.TryGetValue(path, out var task))
        {
            task.Cleaned = true;
            task.State = PipelineTaskState.Cleaned;
            task.Matched = result.TotalMatched;
            task.Scanned = result.TotalScanned;
        }

        WriteSessionLog(path, result);
        return result;
    }

    public void WriteSessionLog(string targetPath, AdCleanupResult result)
    {
        var entry = new SessionLogEntry
        {
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Target = targetPath,
            Removed = [..result.RemovedFiles],
            Count = result.TotalRemoved
        };

        var list = new List<SessionLogEntry> { entry };

        if (File.Exists(_paths.SessionLogPath))
        {
            try
            {
                var old = ReadSessionEntries();
                if (old.Count > 0)
                    list = old.Concat(list).TakeLast(20).ToList();
            }
            catch
            {
                // ignore
            }
        }

        JsonHelper.WriteFile(_paths.SessionLogPath, list);
    }

    public void ClearTasks() => _tasks.Clear();

    private List<SessionLogEntry> ReadSessionEntries()
    {
        var json = File.ReadAllText(_paths.SessionLogPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<SessionLogEntry>>(json, JsonHelper.Options) ?? [];
        }

        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var single = System.Text.Json.JsonSerializer.Deserialize<SessionLogEntry>(json, JsonHelper.Options);
            return single is null ? [] : [single];
        }

        return [];
    }

    private static string StateLabel(PipelineTaskState state) => state switch
    {
        PipelineTaskState.PendingScan => "待扫描",
        PipelineTaskState.PendingConfirm => "待确认",
        PipelineTaskState.NoAds => "无广告",
        PipelineTaskState.Cleaned => "已清理",
        PipelineTaskState.Compressed => "已压缩",
        _ => state.ToString()
    };
}
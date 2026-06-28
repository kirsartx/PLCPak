using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchLogServiceTests : IDisposable
{
    private readonly string _root;

    public BatchLogServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batchlog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void WriteBatchLog_writes_to_workspace_logs_directory()
    {
        var path = BatchLogService.WriteBatchLog(_root, "batch-pipeline", ["[成功] job-a", "[失败] job-b"]);

        Assert.StartsWith(Path.Combine(_root, "logs", "batch-"), path);
        Assert.EndsWith(".txt", path);
        Assert.True(File.Exists(path));

        var content = File.ReadAllText(path);
        Assert.Contains("Operation: batch-pipeline", content);
        Assert.Contains("[成功] job-a", content);
        Assert.Contains("[失败] job-b", content);
    }
}
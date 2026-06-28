using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class InboxImportServiceTests : IDisposable
{
    private readonly string _dir;

    public InboxImportServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "plcpak-inbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ResolveUniquePath_appends_suffix_when_exists()
    {
        var inbox = Path.Combine(_dir, "inbox");
        Directory.CreateDirectory(inbox);
        File.WriteAllText(Path.Combine(inbox, "game.7z"), "a");

        var second = InboxImportService.ResolveUniquePath(inbox, "game.7z");
        Assert.Equal(Path.Combine(inbox, "game_2.7z"), second);
    }

    [Fact]
    public void CopyArchivesToInbox_copies_files()
    {
        var source = Path.Combine(_dir, "source.7z");
        File.WriteAllText(source, "payload");
        var inbox = Path.Combine(_dir, "inbox");
        var copied = new List<string>();

        var count = InboxImportService.CopyArchivesToInbox(inbox, [source], copied);

        Assert.Equal(1, count);
        Assert.True(File.Exists(copied[0]));
    }

    [Fact]
    public void CollectArchivePaths_finds_archives_in_folder()
    {
        var folder = Path.Combine(_dir, "downloads");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "a.zip"), "x");
        File.WriteAllText(Path.Combine(folder, "readme.txt"), "y");

        var archives = InboxImportService.CollectArchivePaths([folder]).ToList();

        Assert.Single(archives);
        Assert.EndsWith("a.zip", archives[0]);
    }
}
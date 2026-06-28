using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PasswordManifestServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly PasswordManifestService _manifest;

    public PasswordManifestServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "plcpak-pwd-manifest-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_dir);
        Directory.CreateDirectory(paths.PasswordSamplesDirectory);
        _manifest = new PasswordManifestService(paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void SyncFromFolder_imports_password_txt_files()
    {
        var root = new AppPaths(_dir).PasswordSamplesDirectory;
        File.WriteAllText(Path.Combine(root, "sample-game.txt"), "password-from-file");

        var stats = _manifest.SyncFromFolder();

        Assert.Equal(1, stats.Added);
        var manifest = _manifest.Read();
        Assert.Contains(manifest.Entries, e => e.Password == "password-from-file");
    }

    [Fact]
    public void SyncFromFolder_imports_folder_with_password_txt()
    {
        var root = new AppPaths(_dir).PasswordSamplesDirectory;
        var folder = Path.Combine(root, "game-folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "解压密码.txt"), "folder-password-99");

        var stats = _manifest.SyncFromFolder();

        Assert.Equal(1, stats.Added);
        var manifest = _manifest.Read();
        Assert.Contains(manifest.Entries, e => e.Password == "folder-password-99");
    }

    [Fact]
    public void SyncFromFolder_updates_existing_entry_when_password_changes()
    {
        var root = new AppPaths(_dir).PasswordSamplesDirectory;
        var file = Path.Combine(root, "repeat-entry.txt");
        File.WriteAllText(file, "old-password");
        _manifest.SyncFromFolder();

        File.WriteAllText(file, "new-password");
        var stats = _manifest.SyncFromFolder();

        Assert.Equal(1, stats.Updated);
        var manifest = _manifest.Read();
        Assert.Equal("new-password", manifest.Entries.Single(e => e.Name == "repeat-entry").Password);
    }
}
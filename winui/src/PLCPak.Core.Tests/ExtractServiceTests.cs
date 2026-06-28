using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ExtractServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly ExtractService _extract;

    public ExtractServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "plcpak-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _extract = new ExtractService(new SevenZipService(new AppPaths(_dir)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void DetectGameRoot_prefers_directory_with_exe()
    {
        var root = Path.Combine(_dir, "extracted");
        var game = Path.Combine(root, "GameFolder");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, "game.exe"), "");
        File.WriteAllText(Path.Combine(root, "readme.txt"), "");

        var detected = _extract.DetectGameRoot(root);

        Assert.Equal(game, detected);
    }
}
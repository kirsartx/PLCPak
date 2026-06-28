using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JsonHelperTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public JsonHelperTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "plcpak-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "test.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Round_trips_object_with_camel_case()
    {
        var sample = new SampleDto { Name = "demo", Count = 3 };
        JsonHelper.WriteFile(_file, sample);
        var loaded = JsonHelper.ReadFile<SampleDto>(_file);

        Assert.NotNull(loaded);
        Assert.Equal("demo", loaded!.Name);
        Assert.Equal(3, loaded.Count);
        Assert.Contains("\"name\"", File.ReadAllText(_file), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFile_returns_default_when_missing()
    {
        var missing = Path.Combine(_dir, "missing.json");
        var loaded = JsonHelper.ReadFile<SampleDto>(missing);

        Assert.Null(loaded);
    }

    [Fact]
    public void ReadFile_strips_utf8_bom()
    {
        File.WriteAllText(_file, "\uFEFF{\"name\":\"bom\",\"count\":1}");
        var loaded = JsonHelper.ReadFile<SampleDto>(_file);

        Assert.NotNull(loaded);
        Assert.Equal("bom", loaded!.Name);
    }

    private sealed class SampleDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
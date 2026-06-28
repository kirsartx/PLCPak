using System.Net;
using System.Text;
using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class VersionCheckServiceTests : IDisposable
{
    private readonly string _root;
    private readonly AppPaths _paths;

    public VersionCheckServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-version-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "version.json"), """
            {
              "version": "1.0.57",
              "latestVersion": "1.0.57",
              "channel": "stable"
            }
            """);
        _paths = new AppPaths(Path.Combine(_root, "app"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("8.0.0", "8.1.0", true)]
    [InlineData("8.0.0", "8.0.0", false)]
    [InlineData("8.0.0", "7.9.9", false)]
    [InlineData("7.2.0", "8.0.0", true)]
    [InlineData("1.0.44", "1.0.45", true)]
    [InlineData("1.0.45", "1.0.46", true)]
    [InlineData("1.0.46", "1.0.47", true)]
    [InlineData("1.0.47", "1.0.48", true)]
    [InlineData("1.0.48", "1.0.49", true)]
    [InlineData("1.0.49", "1.0.50", true)]
    [InlineData("1.0.50", "1.0.51", true)]
    [InlineData("1.0.51", "1.0.52", true)]
    [InlineData("1.0.52", "1.0.53", true)]
    [InlineData("1.0.53", "1.0.54", true)]
    [InlineData("1.0.54", "1.0.55", true)]
    [InlineData("1.0.55", "1.0.56", true)]
    [InlineData("1.0.56", "1.0.57", true)]
    [InlineData("1.0.57", "1.0.58", true)]
    [InlineData("1.0.58", "1.0.59", true)]
    [InlineData("1.0.59", "1.0.60", true)]
    [InlineData("1.0.60", "1.0.61", true)]
    [InlineData("1.0.61", "1.0.62", true)]
    [InlineData("1.0.62", "1.0.63", true)]
    [InlineData("1.0.63", "1.0.64", true)]
    [InlineData("1.0.64", "1.0.65", true)]
    [InlineData("1.0.65", "1.0.66", true)]
    [InlineData("1.0.66", "1.0.67", true)]
    [InlineData("1.0.67", "1.0.68", true)]
    [InlineData("1.0.68", "1.0.69", true)]
    [InlineData("1.0.69", "1.0.70", true)]
    [InlineData("1.0.70", "1.0.71", true)]
    [InlineData("1.0.57", "10.10.0", false)]
    public void IsRemoteNewer_compares_semver_parts(string local, string remote, bool expected)
    {
        Assert.Equal(expected, VersionCheckService.IsRemoteNewer(local, remote));
    }

    [Fact]
    public void LoadLocalVersion_reads_version_json()
    {
        var service = new VersionCheckService(_paths);

        Assert.Equal("1.0.57", service.LoadLocalVersion());
    }

    [Fact]
    public async Task CheckAsync_with_empty_url_returns_no_update()
    {
        var service = new VersionCheckService(_paths);

        var result = await service.CheckAsync(null);

        Assert.False(result.HasUpdate);
        Assert.Equal(AppVersion.Current, result.LocalVersion);
        Assert.Null(result.RemoteVersion);
        Assert.Contains("未配置", result.Message);
    }

    [Fact]
    public async Task CheckAsync_with_mock_http_detects_newer_remote_version()
    {
        var handler = new MockVersionHttpHandler("""
            {
              "version": "1.0.75",
              "latestVersion": "1.0.75",
              "channel": "stable"
            }
            """);
        var service = new VersionCheckService(_paths, new HttpClient(handler));

        var result = await service.CheckAsync("https://example.test/version.json");

        Assert.True(result.HasUpdate);
        Assert.Equal(AppVersion.Current, result.LocalVersion);
        Assert.Equal("1.0.75", result.RemoteVersion);
        Assert.Contains("1.0.75", result.Message);
    }

    private sealed class MockVersionHttpHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
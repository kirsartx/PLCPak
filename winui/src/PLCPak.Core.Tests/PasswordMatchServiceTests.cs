using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PasswordMatchServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly PasswordMatchService _match;

    public PasswordMatchServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "plcpak-pwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var paths = new AppPaths(_dir);
        Directory.CreateDirectory(paths.PasswordSamplesDirectory);
        var manifest = new PasswordManifestService(paths);
        manifest.Save(new PasswordManifest
        {
            Entries =
            [
                new PasswordManifestEntry
                {
                    Id = "laowang",
                    Name = "老王论坛",
                    Password = "上老王论坛当老王",
                    Sites = ["老王论坛"],
                    UrlPatterns = ["laowang.vip"],
                    FolderNames = ["上老王论坛当老王"],
                    Priority = 100
                }
            ]
        });

        var studio = new StudioConfigService(paths);
        _match = new PasswordMatchService(manifest, studio, new SevenZipService(paths));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MatchForJob_matches_forum_site()
    {
        var job = CreateJob(site: "老王论坛");
        var result = _match.MatchForJob(job);

        Assert.Contains(result.Hits, h => h.Password == "上老王论坛当老王");
        Assert.Equal("上老王论坛当老王", result.BestPassword);
    }

    [Fact]
    public void MatchForJob_matches_thread_url()
    {
        var job = CreateJob(threadUrl: "https://laowang.vip/forum.php?tid=123");
        var result = _match.MatchForJob(job);

        Assert.Contains(result.Hits, h => h.Reason.Contains("laowang.vip"));
    }

    [Fact]
    public void MatchForJob_reads_inbox_password_txt()
    {
        var job = CreateJob();
        var hint = Path.Combine(job.Paths.Inbox, "解压密码.txt");
        Directory.CreateDirectory(job.Paths.Inbox);
        File.WriteAllText(hint, "test-password-123");

        var result = _match.MatchForJob(job);

        Assert.Contains(result.Hits, h => h.Password == "test-password-123");
    }

    private static PublishJob CreateJob(string site = "", string threadUrl = "")
    {
        var slug = "test-game";
        var root = Path.Combine(Path.GetTempPath(), "plcpak-job-" + Guid.NewGuid().ToString("N"));
        return new PublishJob
        {
            Title = "测试游戏",
            Source = new JobSource
            {
                Site = site,
                ThreadUrl = threadUrl
            },
            Paths = new JobPaths
            {
                Slug = slug,
                Inbox = Path.Combine(root, "inbox", slug),
                Extract = Path.Combine(root, "extract", slug),
                Staging = Path.Combine(root, "extract", slug),
                Output = Path.Combine(root, "output", slug)
            }
        };
    }
}
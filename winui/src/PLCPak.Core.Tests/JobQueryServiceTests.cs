using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobQueryServiceTests
{
    [Fact]
    public void Search_matches_title_slug_and_thread_url()
    {
        var jobs = new[]
        {
            new PublishJob
            {
                Title = "妹妹使我强大",
                Paths = new JobPaths { Slug = "妹妹使我强大" },
                Source = new JobSource { ThreadUrl = "https://laowang.vip/thread/123" }
            },
            new PublishJob { Title = "其他游戏" }
        };

        Assert.Single(JobQueryService.Search(jobs, "妹妹"));
        Assert.Single(JobQueryService.Search(jobs, "thread/123"));
        Assert.Single(JobQueryService.Search(jobs, "其他"));
    }

    [Fact]
    public void NormalizeThreadUrl_strips_trailing_slash_and_port()
    {
        var a = JobQueryService.NormalizeThreadUrl("https://laowang.vip/thread/123/");
        var b = JobQueryService.NormalizeThreadUrl("https://laowang.vip:443/thread/123");
        Assert.Equal(a, b);
    }

    [Fact]
    public void CheckDuplicates_blocks_same_thread_url()
    {
        var existing = new[]
        {
            new PublishJob
            {
                Id = "abc",
                Title = "已有任务",
                Source = new JobSource { ThreadUrl = "https://laowang.vip/thread/99" }
            }
        };

        var check = JobQueryService.CheckDuplicates("新名字", "https://laowang.vip/thread/99/", existing);

        Assert.True(check.Blocked);
        Assert.Contains("同帖链接", check.Message);
    }

    [Fact]
    public void Sort_title_orders_alphabetically()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "Zulu", UpdatedAt = DateTime.Now },
            new PublishJob { Title = "Alpha", UpdatedAt = DateTime.Now.AddMinutes(-5) }
        };

        var sorted = JobQueryService.Sort(jobs, JobListSort.Title);

        Assert.Equal("Alpha", sorted[0].Title);
        Assert.Equal("Zulu", sorted[1].Title);
    }

    [Fact]
    public void ParseSort_accepts_aliases()
    {
        Assert.Equal(JobListSort.Title, JobQueryService.ParseSort("title"));
        Assert.Equal(JobListSort.Updated, JobQueryService.ParseSort("updated"));
    }

    [Fact]
    public void Sort_orders_by_title_and_updated()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "B", UpdatedAt = new DateTime(2026, 1, 2) },
            new PublishJob { Title = "A", UpdatedAt = new DateTime(2026, 1, 3) },
            new PublishJob { Title = "C", UpdatedAt = new DateTime(2026, 1, 1) }
        };

        var byTitle = JobQueryService.Sort(jobs, JobSortOrder.TitleAsc);
        Assert.Equal("A", byTitle[0].Title);
        Assert.Equal("B", byTitle[1].Title);

        var byUpdated = JobQueryService.Sort(jobs, JobSortOrder.UpdatedDesc);
        Assert.Equal("A", byUpdated[0].Title);
        Assert.Equal("C", byUpdated[2].Title);
    }

    [Fact]
    public void Search_matches_notes_generated_copy_and_baidu_slug()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "其他游戏", Notes = "特殊备注关键字" },
            new PublishJob
            {
                Title = "文案任务",
                Publish = new JobPublishState { GeneratedCopy = "开头内容 可搜索词 后续文字" }
            },
            new PublishJob
            {
                Title = "百度任务",
                Publish = new JobPublishState
                {
                    Baidu = new PublishChannelState { Link = "https://pan.baidu.com/s/abcSlug99" }
                }
            },
            new PublishJob
            {
                Title = "密码任务",
                Source = new JobSource { ArchivePassword = "secret123" }
            }
        };

        Assert.Single(JobQueryService.Search(jobs, "特殊备注"));
        Assert.Single(JobQueryService.Search(jobs, "可搜索词"));
        Assert.Single(JobQueryService.Search(jobs, "abcSlug99"));
        Assert.Empty(JobQueryService.Search(jobs, "secret123"));
    }

    [Fact]
    public void EnsureUniqueSlug_appends_suffix_for_conflicts()
    {
        var existing = new[]
        {
            new PublishJob { Paths = new JobPaths { Slug = "测试游戏" } },
            new PublishJob { Paths = new JobPaths { Slug = "测试游戏_2" } }
        };

        var slug = JobQueryService.EnsureUniqueSlug("测试游戏", existing);
        Assert.Equal("测试游戏_3", slug);
    }

    [Fact]
    public void Search_matches_tags()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "普通任务" },
            new PublishJob { Title = "标签任务", Tags = ["热门", "PC"] }
        };

        Assert.Single(JobQueryService.Search(jobs, "热门"));
        Assert.Single(JobQueryService.Search(jobs, "pc"));
        Assert.Empty(JobQueryService.Search(jobs, "移动端"));
    }

    [Fact]
    public void SortPinnedFirst_orders_pinned_jobs_before_updated_desc()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "最新未置顶", IsPinned = false, UpdatedAt = new DateTime(2026, 6, 25) },
            new PublishJob { Title = "旧置顶", IsPinned = true, UpdatedAt = new DateTime(2026, 6, 1) },
            new PublishJob { Title = "新置顶", IsPinned = true, UpdatedAt = new DateTime(2026, 6, 20) }
        };

        var sorted = JobQueryService.SortPinnedFirst(jobs);

        Assert.Equal("新置顶", sorted[0].Title);
        Assert.Equal("旧置顶", sorted[1].Title);
        Assert.Equal("最新未置顶", sorted[2].Title);
    }

    [Fact]
    public void Sort_supports_pinned_first_order()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "B", IsPinned = false, UpdatedAt = new DateTime(2026, 6, 25) },
            new PublishJob { Title = "A", IsPinned = true, UpdatedAt = new DateTime(2026, 6, 1) }
        };

        var sorted = JobQueryService.Sort(jobs, JobSortOrder.PinnedFirst);

        Assert.Equal("A", sorted[0].Title);
        Assert.Equal("B", sorted[1].Title);
    }

    [Fact]
    public void FilterByTag_returns_only_jobs_containing_tag()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "普通任务" },
            new PublishJob { Title = "热门任务", Tags = ["热门", "PC"] },
            new PublishJob { Title = "PC任务", Tags = ["PC"] }
        };

        var filtered = JobQueryService.FilterByTag(jobs, "热门");

        Assert.Single(filtered);
        Assert.Equal("热门任务", filtered[0].Title);
    }

    [Fact]
    public void FilterByTag_is_case_insensitive()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "标签任务", Tags = ["VIP"] }
        };

        var filtered = JobQueryService.FilterByTag(jobs, "vip");

        Assert.Single(filtered);
        Assert.Equal("标签任务", filtered[0].Title);
    }

    [Fact]
    public void Query_with_tagFilter_applies_tag_before_search_and_sort()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "Zulu", Tags = ["热门"], UpdatedAt = new DateTime(2026, 6, 1) },
            new PublishJob { Title = "Alpha", Tags = ["热门"], UpdatedAt = new DateTime(2026, 6, 3) },
            new PublishJob { Title = "其他", Tags = ["PC"], UpdatedAt = new DateTime(2026, 6, 2) }
        };

        var result = JobQueryService.Query(jobs, JobListFilter.All, null, JobListSort.Updated, "热门");

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Title);
        Assert.Equal("Zulu", result[1].Title);
    }

    [Fact]
    public void Query_with_tagFilter_and_search_narrows_results()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "热门Alpha", Tags = ["热门"] },
            new PublishJob { Title = "热门Beta", Tags = ["热门"] },
            new PublishJob { Title = "普通Beta", Tags = ["PC"] }
        };

        var result = JobQueryService.Query(jobs, JobListFilter.All, "Alpha", JobListSort.Title, "热门");

        Assert.Single(result);
        Assert.Equal("热门Alpha", result[0].Title);
    }

    [Fact]
    public void FilterByTag_with_comma_requires_all_tags()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "双标签", Tags = ["热门", "PC"] },
            new PublishJob { Title = "仅热门", Tags = ["热门"] },
            new PublishJob { Title = "仅PC", Tags = ["PC"] }
        };

        var filtered = JobQueryService.FilterByTag(jobs, "热门, PC");

        Assert.Single(filtered);
        Assert.Equal("双标签", filtered[0].Title);
    }
}
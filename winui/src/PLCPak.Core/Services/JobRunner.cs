using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class JobRunner
{
    private readonly JobStore _store;
    private readonly WorkspaceService _workspace;
    private readonly ExtractService _extract;
    private readonly PlcPakAppContext _app;
    private DuplicateOperationsBundle? _duplicateBundleCache;
    private string? _duplicateBundleCacheKey;
    private OperationsCenterSnapshot? _operationsSnapshotCache;
    private string? _operationsSnapshotCacheKey;

    public JobRunner(JobStore store, WorkspaceService workspace, ExtractService extract, PlcPakAppContext app)
    {
        _store = store;
        _workspace = workspace;
        _extract = extract;
        _app = app;
    }

    public PublishJob ScanInbox(string jobId)
    {
        var job = RequireJob(jobId);
        var archives = _workspace.FindArchives(job.Paths.Inbox);
        job.Artifacts.InboxArchives = archives;
        if (job.Status is JobStatus.Extracting or JobStatus.Processing)
        {
            job.AppendLog("扫描 inbox: 任务进行中，已更新压缩包列表，状态未变更");
        }
        else if (archives.Count > 0)
        {
            job.Status = job.Status is JobStatus.Extracted or JobStatus.Processed or JobStatus.Failed
                ? job.Status
                : JobStatus.InboxReady;
        }
        else
        {
            job.Status = JobStatus.Draft;
        }

        job.Error = null;

        if (archives.Count > 0)
        {
            var primary = WorkspaceService.PickPrimaryArchive(archives);
            job.AppendLog($"扫描 inbox: 发现 {archives.Count} 个压缩包，主包: {Path.GetFileName(primary)}");
            ApplyPasswordMatches(job, archives);
        }
        else
        {
            job.AppendLog("扫描 inbox: 未发现压缩包，请将下载文件放入 inbox 目录");
        }

        _store.Save(job);
        return job;
    }

    public PasswordMatchResult MatchPasswords(string jobId)
    {
        var job = RequireJob(jobId);
        var archives = job.Artifacts.InboxArchives.Count > 0
            ? job.Artifacts.InboxArchives
            : _workspace.FindArchives(job.Paths.Inbox);

        var result = _app.PasswordMatch.MatchForJob(job, archives);
        ApplyPasswordMatchResult(job, result);
        _store.Save(job);
        return result;
    }

    public PublishJob Extract(string jobId, CancellationToken cancellationToken = default)
    {
        var job = RequireJob(jobId);
        if (job.Artifacts.InboxArchives.Count == 0)
        {
            job = ScanInbox(jobId);
            if (job.Artifacts.InboxArchives.Count == 0)
                throw new InvalidOperationException("inbox 中没有可解压的压缩包，请先放入压缩文件并点击「扫描 inbox」");
        }

        job.Status = JobStatus.Extracting;
        job.Error = null;
        job.AppendLog("开始解压...");
        _store.Save(job);

        try
        {
            Directory.CreateDirectory(job.Paths.Extract);

            var archive = WorkspaceService.PickPrimaryArchive(job.Artifacts.InboxArchives)
                ?? job.Artifacts.InboxArchives[0];

            var result = _extract.ExtractArchive(
                archive,
                job.Paths.Extract,
                BuildPasswordCandidates(job),
                msg => { job.AppendLog(msg); _store.Save(job); },
                cancellationToken);

            if (!result.Success)
            {
                job.Status = JobStatus.Failed;
                job.Error = result.Error;
                job.AppendLog($"解压失败: {result.Error}");
                _store.Save(job);
                return job;
            }

            job.Artifacts.GameRoot = result.GameRoot;
            job.Paths.Staging = result.GameRoot ?? job.Paths.Extract;
            job.Status = JobStatus.Extracted;
            job.AppendLog($"解压成功，处理目录: {job.Paths.Staging}");
            _store.Save(job);
            return job;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            job.AppendLog($"解压异常: {ex.Message}");
            _store.Save(job);
            throw;
        }
    }

    public async Task<PublishJob> ProcessAsync(
        string jobId,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
    {
        var job = RequireJob(jobId);

        if (job.Status != JobStatus.Extracted || !ExtractService.HasExtractedContent(job.Paths.Extract))
        {
            throw new InvalidOperationException("请先成功解压游戏文件（extract 目录不能为空）。带密码的 RAR/7z 需填写解压密码。");
        }

        if (string.IsNullOrWhiteSpace(job.Paths.Staging) || !Directory.Exists(job.Paths.Staging))
        {
            if (!string.IsNullOrWhiteSpace(job.Artifacts.GameRoot) && Directory.Exists(job.Artifacts.GameRoot))
                job.Paths.Staging = job.Artifacts.GameRoot;
            else
                throw new InvalidOperationException("解压目录无效，请重新解压");
        }

        job.Status = JobStatus.Processing;
        job.Error = null;
        job.AppendLog("开始去广告 + 压缩...");
        _store.Save(job);

        try
        {
            Directory.CreateDirectory(job.Paths.Output);
            var config = _app.Config.Load();
            var sourcePath = job.Paths.Staging;

            void Log(string msg)
            {
                job.AppendLog(msg);
                _store.Save(job);
            }

            var request = new GuiExecuteRequest
            {
                SourcePaths = [sourcePath],
                Enable7z = job.Enable7z,
                EnableTarZst = job.EnableTarZst,
                SelfPurchase = job.SelfPurchase,
                SevenZOutputName = job.Paths.Slug,
                TarZstOutputName = DateTime.Now.ToString("yyMMdd_01"),
                VolumeSizeMB = config.VolumeSizeMB,
                OutputDirectory = job.Paths.Output
            };

            await _app.ExecuteOneClickAsync(
                request,
                confirmCleanupAsync ?? (_ => Task.FromResult(true)),
                Log,
                percent,
                cancellationToken).ConfigureAwait(false);

            job.Artifacts.OutputArchives = Directory.EnumerateFiles(job.Paths.Output, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
                .ToList();

            job.Status = JobStatus.Processed;
            job.AppendLog($"处理完成，输出 {job.Artifacts.OutputArchives.Count} 个压缩包");
            job.AppendLog("提示: 上传网盘后回填链接，再生成 TG 文案");
            _store.Save(job);

            var studio = _app.StudioConfig.Load();
            if (studio.AutoGenerateCopyAfterProcess)
            {
                try
                {
                    GeneratePublishCopy(jobId, export: true);
                }
                catch (Exception copyEx)
                {
                    job = RequireJob(jobId);
                    job.AppendLog($"自动文案生成失败: {copyEx.Message}");
                    _store.Save(job);
                }
            }

            return RequireJob(jobId);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            job.AppendLog($"处理失败: {ex.Message}");
            _store.Save(job);
            throw;
        }
    }

    private IEnumerable<string> BuildPasswordCandidates(PublishJob job)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var password in EnumeratePasswordCandidates(job))
        {
            if (seen.Add(password))
                yield return password;
        }
    }

    private IEnumerable<string> EnumeratePasswordCandidates(PublishJob job)
    {
        if (!string.IsNullOrWhiteSpace(job.Source.ArchivePassword))
            yield return job.Source.ArchivePassword.Trim();

        foreach (var pwd in job.Source.MatchedPasswords)
        {
            if (!string.IsNullOrWhiteSpace(pwd))
                yield return pwd.Trim();
        }

        var match = _app.PasswordMatch.MatchForJob(job, job.Artifacts.InboxArchives);
        foreach (var pwd in match.OrderedPasswords)
            yield return pwd;

        if (!string.IsNullOrWhiteSpace(job.Source.DownloadHint))
            yield return job.Source.DownloadHint.Trim();

        foreach (var pwd in _app.StudioConfig.Load().ExtractPasswords)
        {
            if (!string.IsNullOrWhiteSpace(pwd))
                yield return pwd.Trim();
        }
    }

    private void ApplyPasswordMatches(PublishJob job, List<string> archives)
        => ApplyPasswordMatchResult(job, _app.PasswordMatch.MatchForJob(job, archives));

    private void ApplyPasswordMatchResult(PublishJob job, PasswordMatchResult result)
    {
        job.Source.MatchedPasswords = result.OrderedPasswords.ToList();

        if (result.Hits.Count == 0)
        {
            job.AppendLog("密码匹配: 未命中密码库规则");
            return;
        }

        job.AppendLog($"密码匹配: 命中 {result.Hits.Count} 条规则");
        foreach (var hit in result.Hits)
            job.AppendLog($"[密码匹配] {hit.Name} -> {hit.Password} ({hit.Reason})");

        if (string.IsNullOrWhiteSpace(job.Source.ArchivePassword) && result.BestPassword is not null)
        {
            job.Source.ArchivePassword = result.BestPassword;
            job.AppendLog($"[密码匹配] 已自动填入解压密码: {result.BestPassword}");
        }
    }

    public PublishJob SavePublishLinks(
        string jobId,
        string? baiduLink,
        string? baiduPassword,
        string? quarkLink,
        string? quarkPassword,
        string? telegramLink = null)
    {
        var job = RequireJob(jobId);
        job.Publish.Baidu.Link = baiduLink?.Trim() ?? string.Empty;
        job.Publish.Baidu.Password = baiduPassword?.Trim() ?? string.Empty;
        job.Publish.Quark.Link = quarkLink?.Trim() ?? string.Empty;
        job.Publish.Quark.Password = quarkPassword?.Trim() ?? string.Empty;
        if (telegramLink is not null)
            job.Publish.Telegram.Link = telegramLink.Trim();

        foreach (var warning in PublishLinkValidator.ValidateAll(baiduLink, baiduPassword, quarkLink, quarkPassword))
            job.AppendLog($"[链接校验] {warning}");

        PublishStatusHelper.ApplyLinkStatus(job.Publish.Baidu);
        PublishStatusHelper.ApplyLinkStatus(job.Publish.Quark);
        if (telegramLink is not null)
            PublishStatusHelper.ApplyLinkStatus(job.Publish.Telegram);

        job.AppendLog("已保存发布链接");
        job.AppendLog($"发布进度: {PublishStatusHelper.BuildSummary(job.Publish)}");
        _store.Save(job);
        return job;
    }

    public PublishJob MarkChannelPublished(string jobId, string channel)
    {
        var job = RequireJob(jobId);
        switch (channel.ToLowerInvariant())
        {
            case "baidu":
                PublishStatusHelper.MarkPublished(job.Publish.Baidu);
                job.AppendLog("已标记: 百度已发布");
                break;
            case "quark":
                PublishStatusHelper.MarkPublished(job.Publish.Quark);
                job.AppendLog("已标记: 夸克已发布");
                break;
            case "telegram":
            case "tg":
                PublishStatusHelper.MarkPublished(job.Publish.Telegram, requireLink: false);
                job.AppendLog("已标记: TG 已发布");
                break;
            default:
                throw new ArgumentException($"未知渠道: {channel}", nameof(channel));
        }

        TryPromotePublishedStatus(job);
        job.AppendLog($"发布进度: {PublishStatusHelper.BuildSummary(job.Publish)}");
        _store.Save(job);
        return job;
    }

    public PublishJob RefreshOutputArtifacts(string jobId)
    {
        var job = RequireJob(jobId);
        if (!Directory.Exists(job.Paths.Output))
            return job;

        job.Artifacts.OutputArchives = Directory.EnumerateFiles(job.Paths.Output, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
            .ToList();
        _store.Save(job);
        return job;
    }

    public PublishCopyResult GeneratePublishCopy(string jobId, string? templateId = null, bool export = true)
    {
        var job = RefreshOutputArtifacts(jobId);
        var studio = _app.StudioConfig.Load();
        templateId ??= string.IsNullOrWhiteSpace(job.Publish.TemplateId)
            ? studio.DefaultPublishTemplateId
            : job.Publish.TemplateId;

        var result = _app.PublishTemplates.Render(job, studio, templateId);
        job.Publish.TemplateId = result.TemplateId;
        job.Publish.GeneratedCopy = result.Text;
        job.Publish.CopyGeneratedAt = DateTime.Now;
        job.AppendLog($"已生成发布文案: {result.TemplateName}");

        if (result.MissingFields.Count > 0)
            job.AppendLog($"文案提示: 尚未填写 {string.Join("、", result.MissingFields)}");

        if (export)
        {
            var exportDir = Path.Combine(_workspace.PublishedDirectory, job.Paths.Slug);
            Directory.CreateDirectory(exportDir);
            var copyPath = Path.Combine(exportDir, "发布文案.txt");
            File.WriteAllText(copyPath, result.Text, System.Text.Encoding.UTF8);
            JsonHelper.WriteFile(Path.Combine(exportDir, "publish-meta.json"), new
            {
                jobId = job.Id,
                title = job.Title,
                templateId = result.TemplateId,
                generatedAt = job.Publish.CopyGeneratedAt,
                publish = job.Publish
            });
            result.ExportPath = copyPath;
            job.AppendLog($"文案已导出: {copyPath}");
        }

        _store.Save(job);
        return result;
    }

    public string GetPublishedDirectory(string jobId)
    {
        var job = RequireJob(jobId);
        return Path.Combine(_workspace.PublishedDirectory, job.Paths.Slug);
    }

    public void SaveStudioTelegramChannel(string channelUrl)
    {
        var config = _app.StudioConfig.Load();
        config.TelegramChannelUrl = channelUrl.Trim();
        _app.StudioConfig.Save(config);
    }

    public PublishJob MarkAllChannelsPublished(string jobId)
    {
        var job = RequireJob(jobId);
        var errors = new List<string>();

        if (job.Publish.Baidu.Status != PublishStatusHelper.Published)
        {
            if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link))
                errors.Add("百度链接未填");
            else
                PublishStatusHelper.MarkPublished(job.Publish.Baidu);
        }

        if (job.Publish.Quark.Status != PublishStatusHelper.Published)
        {
            if (string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
                errors.Add("夸克链接未填");
            else
                PublishStatusHelper.MarkPublished(job.Publish.Quark);
        }

        if (job.Publish.Telegram.Status != PublishStatusHelper.Published)
            PublishStatusHelper.MarkPublished(job.Publish.Telegram, requireLink: false);

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join("；", errors));

        job.AppendLog("已标记: 三渠道全部已发布");
        TryPromotePublishedStatus(job);
        job.AppendLog($"发布进度: {PublishStatusHelper.BuildSummary(job.Publish)}");
        _store.Save(job);
        return job;
    }

    public BatchCopyResult BatchGenerateCopyForQueue(int limit = 50, string? templateId = null)
    {
        var queueJobs = GetPublishQueue(limit).Entries
            .Select(e => RequireJob(e.JobId))
            .ToList();

        var snapshot = PublishQueueService.BuildBatchCopySummary(
            queueJobs,
            job =>
            {
                GeneratePublishCopy(job.Id, templateId);
                return true;
            },
            job => !string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy) ? "已有文案" : null);

        return new BatchCopyResult
        {
            Success = snapshot.Success,
            Failed = snapshot.Failed,
            Skipped = snapshot.Skipped,
            Messages = snapshot.Messages
        };
    }

    public BatchCopyResult BatchGenerateCopy(JobListFilter filter = JobListFilter.PendingPublish, string? templateId = null)
    {
        var result = new BatchCopyResult();
        var jobs = PublishDashboardService.Filter(_store.List(), filter)
            .Where(j => j.Status is JobStatus.Processed or JobStatus.Published)
            .ToList();

        foreach (var job in jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link) || string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title}: 百度/夸克链接未齐");
                continue;
            }

            try
            {
                GeneratePublishCopy(job.Id, templateId);
                result.Success++;
                result.Messages.Add($"[成功] {job.Title}");
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"[失败] {job.Title}: {ex.Message}");
            }
        }

        return result;
    }

    public JobCreateCheckResult CheckCreateJob(string title, string? threadUrl)
        => JobQueryService.CheckDuplicates(title, threadUrl, _store.List());

    public PublishJob? TryCreateJob(string title, JobSource? source = null, bool allowDuplicateTitle = false)
    {
        var check = CheckCreateJob(title, source?.ThreadUrl);
        if (check.Blocked)
            return null;

        if (check.HasDuplicates && !allowDuplicateTitle)
            return null;

        var slug = check.HasDuplicates
            ? JobQueryService.EnsureUniqueSlug(title, _store.List())
            : null;

        var job = _store.Create(title, source, slugOverride: slug);
        if (check.HasDuplicates)
            job.AppendLog($"注意: 存在相似任务，目录 slug = {job.Paths.Slug}");

        return job;
    }

    public PublishJob CreateJob(string title, JobSource? source = null)
    {
        var job = TryCreateJob(title, source, allowDuplicateTitle: true)
            ?? throw new InvalidOperationException(CheckCreateJob(title, source?.ThreadUrl).Message);

        var studio = _app.StudioConfig.Load();
        if (studio.DefaultJobTags.Count > 0)
        {
            job.Tags = studio.DefaultJobTags
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _store.Save(job);
        }

        InvalidateDuplicateCache();
        return job;
    }

    public IReadOnlyList<PublishJob> QueryJobs(JobListFilter filter, string? searchText, JobListSort sort = JobListSort.Updated)
        => JobQueryService.Query(_store.List(), filter, searchText, sort);

    public IReadOnlyList<PublishJob> QueryJobsByTag(
        string tag,
        JobListFilter filter,
        string? searchText,
        JobListSort sort = JobListSort.Updated)
        => JobQueryService.Query(_store.List(), filter, searchText, sort, tag);

    public Task<ThreadTitleResult> FetchThreadTitleAsync(string url, CancellationToken cancellationToken = default)
        => _app.ThreadTitles.FetchTitleAsync(url, cancellationToken);

    public Task<ThreadParseResult> FetchThreadInfoAsync(string url, CancellationToken cancellationToken = default)
        => _app.ThreadParse.FetchAndParseAsync(url, cancellationToken);

    public async Task<ForumDownloadResult> DownloadThreadAttachmentsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = RequireJob(jobId);
        if (string.IsNullOrWhiteSpace(job.Source.ThreadUrl))
        {
            return new ForumDownloadResult
            {
                Success = false,
                Error = "任务未设置帖子链接",
                Job = job
            };
        }

        var parseResult = await FetchThreadInfoAsync(job.Source.ThreadUrl, cancellationToken).ConfigureAwait(false);
        if (parseResult.AttachmentLinks.Count == 0)
        {
            var empty = new ForumDownloadResult
            {
                Success = false,
                Error = "帖子中未找到直链附件",
                Job = job,
                Messages = ["帖子中未找到直链附件"]
            };
            job.AppendLog(empty.Error!);
            _store.Save(job);
            return empty;
        }

        var studio = _app.StudioConfig.Load();
        var downloadResult = await _app.ForumDownload.DownloadToInboxAsync(
            job,
            parseResult.AttachmentLinks,
            studio.ForumDownloadMaxSizeMB,
            cancellationToken).ConfigureAwait(false);

        foreach (var message in downloadResult.Messages)
            job.AppendLog(message);

        if (downloadResult.DownloadedCount > 0)
            job = ScanInbox(jobId);

        downloadResult.Job = job;
        _store.Save(job);
        return downloadResult;
    }

    public async Task<CreateJobFromThreadResult> CreateJobFromThreadAsync(
        string threadUrl,
        string? title = null,
        string? site = null,
        bool downloadAttachments = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadUrl))
        {
            return new CreateJobFromThreadResult
            {
                Success = false,
                Error = "帖子链接为空",
                Message = "帖子链接为空"
            };
        }

        var parseResult = await FetchThreadInfoAsync(threadUrl, cancellationToken).ConfigureAwait(false);
        var jobTitle = !string.IsNullOrWhiteSpace(title) ? title.Trim() : parseResult.Title.Trim();
        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            return new CreateJobFromThreadResult
            {
                Success = false,
                Error = parseResult.Error ?? "无法从帖子解析标题",
                ParseResult = parseResult,
                Message = parseResult.Error ?? "无法从帖子解析标题"
            };
        }

        var source = new JobSource
        {
            ThreadUrl = threadUrl.Trim(),
            Site = string.IsNullOrWhiteSpace(site) ? "老王论坛" : site.Trim()
        };

        var job = CreateJob(jobTitle, source);
        job = ApplyThreadInfoToJob(job.Id, parseResult);

        ForumDownloadResult? downloadResult = null;
        var studio = _app.StudioConfig.Load();
        if (downloadAttachments || studio.AutoDownloadThreadAttachments)
        {
            if (parseResult.AttachmentLinks.Count > 0)
            {
                downloadResult = await _app.ForumDownload.DownloadToInboxAsync(
                    job,
                    parseResult.AttachmentLinks,
                    studio.ForumDownloadMaxSizeMB,
                    cancellationToken).ConfigureAwait(false);

                foreach (var message in downloadResult.Messages)
                    job.AppendLog(message);

                if (downloadResult.DownloadedCount > 0)
                    job = ScanInbox(job.Id);

                downloadResult.Job = job;
                _store.Save(job);
            }
            else
            {
                job.AppendLog("帖子中未找到直链附件，跳过下载");
                _store.Save(job);
            }
        }

        return new CreateJobFromThreadResult
        {
            Success = true,
            Job = job,
            ParseResult = parseResult,
            DownloadResult = downloadResult,
            Message = $"已创建任务: {job.Title}"
        };
    }

    public async Task<TelegramSendResult> SendPublishCopyToTelegramAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = RequireJob(jobId);
        if (string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy))
        {
            return new TelegramSendResult
            {
                Success = false,
                Error = "请先生成发布文案",
                ChatId = string.Empty
            };
        }

        var studio = _app.StudioConfig.Load();
        var chatId = TelegramBotService.ResolveChatId(studio.TelegramChannelUrl);
        var result = await _app.TelegramBot.SendMessageAsync(
            studio.TelegramBotToken,
            chatId,
            job.Publish.GeneratedCopy,
            cancellationToken).ConfigureAwait(false);

        result.ChatId = chatId;
        if (result.Success)
        {
            job.AppendLog($"已发送到 Telegram (message_id={result.MessageId})");
            _store.Save(job);
        }
        else
        {
            job.AppendLog($"Telegram 发送失败: {result.Error}");
            _store.Save(job);
        }

        return result;
    }

    public PublishJob ApplyThreadInfoToJob(string jobId, ThreadParseResult info, bool applyPublishLinks = true)
    {
        var job = RequireJob(jobId);
        var applied = new List<string>();

        if (!string.IsNullOrWhiteSpace(info.ArchivePassword)
            && string.IsNullOrWhiteSpace(job.Source.ArchivePassword))
        {
            job.Source.ArchivePassword = info.ArchivePassword.Trim();
            applied.Add("解压密码");
        }

        if (!string.IsNullOrWhiteSpace(info.DownloadHint)
            && string.IsNullOrWhiteSpace(job.Source.DownloadHint))
        {
            job.Source.DownloadHint = info.DownloadHint.Trim();
            applied.Add("下载提示");
        }

        if (applyPublishLinks)
        {
            var baiduLink = string.IsNullOrWhiteSpace(job.Publish.Baidu.Link) ? info.BaiduLink : null;
            var baiduPassword = string.IsNullOrWhiteSpace(job.Publish.Baidu.Password) ? info.BaiduPassword : null;
            var quarkLink = string.IsNullOrWhiteSpace(job.Publish.Quark.Link) ? info.QuarkLink : null;
            var quarkPassword = string.IsNullOrWhiteSpace(job.Publish.Quark.Password) ? info.QuarkPassword : null;

            if (!string.IsNullOrWhiteSpace(baiduLink))
                applied.Add("百度链接");
            if (!string.IsNullOrWhiteSpace(baiduPassword))
                applied.Add("百度提取码");
            if (!string.IsNullOrWhiteSpace(quarkLink))
                applied.Add("夸克链接");
            if (!string.IsNullOrWhiteSpace(quarkPassword))
                applied.Add("夸克提取码");

            if (applied.Any(f => f.StartsWith("百度", StringComparison.Ordinal) || f.StartsWith("夸克", StringComparison.Ordinal)))
            {
                job = SavePublishLinks(
                    jobId,
                    baiduLink ?? job.Publish.Baidu.Link,
                    baiduPassword ?? job.Publish.Baidu.Password,
                    quarkLink ?? job.Publish.Quark.Link,
                    quarkPassword ?? job.Publish.Quark.Password);
            }
        }

        if (applied.Count > 0)
            job.AppendLog($"已应用帖子信息: {string.Join("、", applied)}");
        else if (!info.Success)
            job.AppendLog($"帖子解析未成功: {info.Error ?? "无可用字段"}");
        else
            job.AppendLog("帖子信息已检查，无需更新字段");

        _store.Save(job);
        return job;
    }

    public JobMergeResult PreviewMergeJobs(string targetJobId, string sourceJobId)
    {
        var target = RequireJob(targetJobId);
        var source = RequireJob(sourceJobId);
        return JobMergeService.PreviewMerge(target, source);
    }

    public JobMergeResult MergeJobs(string targetJobId, string sourceJobId, bool archiveSource = true)
    {
        var target = RequireJob(targetJobId);
        var source = RequireJob(sourceJobId);
        var result = JobMergeService.Merge(target, source, archiveSource);
        if (!result.Success)
            return result;

        target.AppendLog($"合并任务: 来源 {source.Title} ({source.Id})");
        if (result.MergedFields.Count > 0)
            target.AppendLog($"合并字段: {string.Join(", ", result.MergedFields)}");

        _store.Save(target);

        if (archiveSource)
            _store.Archive(sourceJobId);
        else
            _store.Delete(sourceJobId);

        InvalidateDuplicateCache();
        return result;
    }

    public DashboardSnapshot GetDashboardSnapshot(
        int publishQueueLimit = 20,
        int tgLimit = 50,
        int workflowLimit = 5)
        => DashboardSnapshotService.Build(_store.List(), publishQueueLimit, tgLimit, workflowLimit);

    public PublishQueueSnapshot GetPublishQueue(int limit = 50, IReadOnlyList<PublishJob>? jobs = null)
        => PublishQueueService.BuildQueue(ResolveJobs(jobs), limit);

    public TgPendingSnapshot GetTgPendingSnapshot(IReadOnlyList<PublishJob>? jobs = null)
        => TgPendingService.GetPending(ResolveJobs(jobs));

    public TgPendingSnapshot GetTgPendingQueue(int limit = 50, IReadOnlyList<PublishJob>? jobs = null)
        => TgPendingService.GetPending(ResolveJobs(jobs), limit);

    public BatchPanLinksImportResult ImportPanLinksCsv(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return new BatchPanLinksImportResult
            {
                Success = false,
                Failed = 1,
                Messages = [$"CSV 文件不存在: {fullPath}"]
            };
        }

        var rows = BatchPanLinksCsvService.ParseFile(fullPath);
        var result = BatchPanLinksCsvService.ApplyToJobs(_store, this, rows);
        LogActivity("import", $"批量导入网盘链接: 成功 {result.Applied}，失败 {result.Failed}");
        return result;
    }

    public void LogActivity(string category, string message)
        => ActivityLogService.Append(_workspace.GetWorkspaceRoot(), category, message);

    public async Task<BatchCopyResult> BatchSendTgPendingAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var result = new BatchCopyResult();
        var pending = GetTgPendingQueue(limit).List;

        foreach (var entry in pending)
        {
            if (!entry.HasCopy)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {entry.Title}: 无发布文案");
                continue;
            }

            try
            {
                var send = await SendPublishCopyToTelegramAsync(entry.JobId, cancellationToken).ConfigureAwait(false);
                if (send.Success)
                {
                    result.Success++;
                    result.Messages.Add($"[成功] {entry.Title}");
                }
                else
                {
                    result.Failed++;
                    result.Messages.Add($"[失败] {entry.Title}: {send.Error ?? "发送失败"}");
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"[失败] {entry.Title}: {ex.Message}");
            }
        }

        LogActivity("telegram", $"批量发送 TG 待发布: 成功 {result.Success}，跳过 {result.Skipped}，失败 {result.Failed}");
        return result;
    }

    public ImportInboxResult ImportInboxFiles(string jobId, IEnumerable<string> paths)
    {
        var job = RequireJob(jobId);
        var archives = InboxImportService.CollectArchivePaths(paths).ToList();
        var copiedPaths = new List<string>();
        var copied = InboxImportService.CopyArchivesToInbox(job.Paths.Inbox, archives, copiedPaths);
        var skipped = Math.Max(0, paths.Count() - copied);

        if (copied > 0)
            job.AppendLog($"拖入 inbox: 已复制 {copied} 个压缩包");

        job = copied > 0 ? ScanInbox(jobId) : job;
        _store.Save(job);

        return new ImportInboxResult
        {
            CopiedFiles = copied,
            SkippedPaths = skipped,
            CopiedPaths = copiedPaths,
            Job = job
        };
    }

    public PublishJob ArchiveJob(string jobId)
    {
        var job = _store.Archive(jobId);
        InvalidateDuplicateCache();
        return job;
    }

    public PublishJob UnarchiveJob(string jobId) => _store.Unarchive(jobId);

    public PublishLinksSnapshot GetPublishLinks(string jobId)
    {
        var job = RequireJob(jobId);
        return PublishLinkFormatter.Build(job);
    }

    public string CopyAllPublishLinksText(string jobId)
        => PublishLinkFormatter.Build(RequireJob(jobId)).FormattedText;

    public PublishJob SaveNotes(string jobId, string notes)
    {
        var job = RequireJob(jobId);
        job.Notes = notes?.Trim() ?? string.Empty;
        job.AppendLog(string.IsNullOrWhiteSpace(job.Notes) ? "已清空备注" : "已保存备注");
        _store.Save(job);
        return job;
    }

    public PublishJob ApplyParsedPanLinks(string jobId, PanLinkParseResult parsed)
    {
        var job = RequireJob(jobId);
        if (!parsed.Success)
        {
            job.AppendLog("网盘链接解析未成功");
            foreach (var message in parsed.Messages)
                job.AppendLog(message);
            _store.Save(job);
            return job;
        }

        var applied = new List<string>();
        var baiduLink = string.IsNullOrWhiteSpace(job.Publish.Baidu.Link) ? parsed.BaiduLink : null;
        var baiduPassword = string.IsNullOrWhiteSpace(job.Publish.Baidu.Password) ? parsed.BaiduPassword : null;
        var quarkLink = string.IsNullOrWhiteSpace(job.Publish.Quark.Link) ? parsed.QuarkLink : null;
        var quarkPassword = string.IsNullOrWhiteSpace(job.Publish.Quark.Password) ? parsed.QuarkPassword : null;
        var telegramLink = string.IsNullOrWhiteSpace(job.Publish.Telegram.Link) && parsed.TelegramLinks.Count > 0
            ? parsed.TelegramLinks[0]
            : null;

        if (!string.IsNullOrWhiteSpace(baiduLink))
            applied.Add("百度链接");
        if (!string.IsNullOrWhiteSpace(baiduPassword))
            applied.Add("百度提取码");
        if (!string.IsNullOrWhiteSpace(quarkLink))
            applied.Add("夸克链接");
        if (!string.IsNullOrWhiteSpace(quarkPassword))
            applied.Add("夸克密码");
        if (!string.IsNullOrWhiteSpace(telegramLink))
            applied.Add("Telegram 链接");

        if (applied.Count == 0)
        {
            job.AppendLog("解析成功，但发布字段均已填写，无需更新");
            _store.Save(job);
            return job;
        }

        job = SavePublishLinks(
            jobId,
            baiduLink ?? job.Publish.Baidu.Link,
            baiduPassword ?? job.Publish.Baidu.Password,
            quarkLink ?? job.Publish.Quark.Link,
            quarkPassword ?? job.Publish.Quark.Password,
            telegramLink);

        job.AppendLog($"已应用解析链接: {string.Join("、", applied)}");
        _store.Save(job);
        return job;
    }

    public PublishJob SaveJobTags(string jobId, string tags)
    {
        var job = RequireJob(jobId);
        job.Tags = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        job.AppendLog(job.Tags.Count == 0 ? "已清空标签" : $"已保存标签: {string.Join(", ", job.Tags)}");
        _store.Save(job);
        return job;
    }

    public PublishJob ToggleJobPin(string jobId)
    {
        var job = RequireJob(jobId);
        job.IsPinned = !job.IsPinned;
        job.AppendLog(job.IsPinned ? "已置顶" : "已取消置顶");
        _store.Save(job);
        return job;
    }

    public string ExportOperationsCenterReport()
    {
        var snapshot = GetOperationsCenterSnapshot();
        var reportsDir = Path.Combine(_workspace.GetWorkspaceRoot(), "reports");
        Directory.CreateDirectory(reportsDir);
        var dateKey = DateTime.Today.ToString("yyyy-MM-dd");
        var jsonPath = Path.Combine(reportsDir, $"operations-{dateKey}.json");
        JsonHelper.WriteFile(jsonPath, snapshot, utf8Bom: true);
        return jsonPath;
    }

    public string ExportJob(string jobId, string outputPath)
    {
        var job = RequireJob(jobId);
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, job);
        return fullPath;
    }

    public JobJsonExport ExportJobJson(string jobId, string outputPath)
    {
        var job = RequireJob(jobId);
        var fullPath = ExportJob(jobId, outputPath);
        return new JobJsonExport
        {
            JobId = job.Id,
            Title = job.Title,
            ExportPath = fullPath
        };
    }

    public JobImportResult ImportJob(string inputPath)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"导入文件不存在: {fullPath}");

        var job = JsonHelper.ReadFile<PublishJob>(fullPath)
            ?? throw new InvalidOperationException("无法解析任务 JSON");

        var existingId = _store.Get(job.Id)?.Id;
        var imported = _store.Import(job);
        InvalidateDuplicateCache();
        return new JobImportResult
        {
            Created = existingId is null,
            JobId = imported.Id,
            Title = imported.Title,
            Job = imported
        };
    }

    public JobJsonImportResult ImportJobJson(string inputPath)
    {
        var result = ImportJob(inputPath);
        return new JobJsonImportResult
        {
            Job = result.Job,
            WasExisting = !result.Created,
            Message = result.Created ? "任务已导入" : "任务已导入（已分配新 ID）"
        };
    }

    public WorkspaceHealthReport ScanWorkspaceHealth()
        => new WorkspaceHealthService(_workspace, _store).Scan();

    public IReadOnlyList<RecentActivityEntry> GetRecentActivity(int limit = 20, IReadOnlyList<PublishJob>? jobs = null)
        => PublishDashboardService.GetRecentActivity(ResolveJobs(jobs), limit);

    public PublishJob RetryJob(string jobId)
    {
        var job = RequireJob(jobId);
        if (job.Status != JobStatus.Failed)
            throw new InvalidOperationException("仅失败状态的任务可重试");

        job.Error = null;

        if (!string.IsNullOrWhiteSpace(job.Paths.Extract) && Directory.Exists(job.Paths.Extract)
            && ExtractService.HasExtractedContent(job.Paths.Extract))
        {
            job.Status = JobStatus.Extracted;
        }
        else
        {
            var archives = job.Artifacts.InboxArchives.Count > 0
                ? job.Artifacts.InboxArchives
                : _workspace.FindArchives(job.Paths.Inbox);

            job.Status = archives.Count > 0 ? JobStatus.InboxReady : JobStatus.Draft;
        }

        job.AppendLog($"重试: 状态已恢复为 {job.Status}");
        _store.Save(job);
        return job;
    }

    public JobHealthReport GetJobHealth(IReadOnlyList<PublishJob>? jobs = null)
        => JobHealthService.Compute(ResolveJobs(jobs));

    public PublishWorkflowSnapshot GetPublishWorkflow(int limit = 5, IReadOnlyList<PublishJob>? jobs = null)
        => PublishWorkflowService.BuildSnapshot(ResolveJobs(jobs), limit);

    public PublishWizardState GetWizardState(string? jobId = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            var primaryJobId = GetPublishWorkflow(1).Primary?.JobId;
            if (string.IsNullOrWhiteSpace(primaryJobId))
                return PublishWizardService.BuildState(null);

            jobId = primaryJobId;
        }

        return GetWizardStateForJob(jobId);
    }

    public PublishWizardState GetWizardStateForJob(string jobId)
    {
        var job = RequireJob(jobId);
        return PublishWizardService.BuildState(job);
    }

    public ScheduleBatchExport ExportScheduleBatchScript(
        string? cliExePath = null,
        string? filter = null,
        int? hour = null,
        int? minute = null)
    {
        var studio = _app.StudioConfig.Load();
        var options = new ScheduleBatchOptions
        {
            CliExePath = cliExePath ?? ScheduleBatchScriptService.ResolveDefaultCliExePath(_app.Paths),
            Filter = filter ?? studio.ScheduledBatchFilter,
            Hour = hour ?? studio.ScheduledBatchHour,
            Minute = minute ?? studio.ScheduledBatchMinute
        };

        return ScheduleBatchScriptService.Export(_workspace.GetWorkspaceRoot(), options);
    }

    public NightlyAutomationExport ExportNightlyAutomationScript(string? cliExePath = null)
    {
        var studio = _app.StudioConfig.Load();
        var options = new NightlyAutomationOptions
        {
            CliExePath = cliExePath ?? NightlyAutomationScriptService.ResolveDefaultCliExePath(_app.Paths),
            EnableTgBatchSend = studio.NightlyAutoSendTg,
            IncludeTgBatchSendComment = !studio.NightlyAutoSendTg,
            EnableScanDuplicates = studio.NightlyAutoScanDuplicates,
            IncludeScanDuplicatesComment = !studio.NightlyAutoScanDuplicates,
            EnableBatchMergeDuplicates = studio.NightlyAutoMergeDuplicates,
            IncludeBatchMergeDuplicatesComment = !studio.NightlyAutoMergeDuplicates,
            EnableActivityLogTrim = studio.NightlyAutoTrimActivityLog,
            IncludeActivityLogTrimComment = !studio.NightlyAutoTrimActivityLog,
            EnableActivityLogStatsExport = studio.NightlyExportActivityLogStatsBoth || studio.NightlyExportActivityLogStats,
            IncludeActivityLogStatsExportComment = !(studio.NightlyExportActivityLogStatsBoth || studio.NightlyExportActivityLogStats),
            EnableActivityLogStatsHtmlExport = studio.NightlyExportActivityLogStatsBoth || studio.NightlyExportActivityLogStatsHtml,
            IncludeActivityLogStatsHtmlExportComment = !(studio.NightlyExportActivityLogStatsBoth || studio.NightlyExportActivityLogStatsHtml),
            EnableActivityLogBatchStatsAllExport = studio.NightlyExportActivityLogBatchStatsAll
                || studio.NightlyExportActivityLogStatsBoth
                || studio.NightlyExportActivityLogStats
                || studio.NightlyExportActivityLogStatsHtml,
            IncludeActivityLogBatchStatsAllExportComment = !(studio.NightlyExportActivityLogBatchStatsAll
                || studio.NightlyExportActivityLogStatsBoth
                || studio.NightlyExportActivityLogStats
                || studio.NightlyExportActivityLogStatsHtml),
            EnablePinnedJobsCsvExport = studio.NightlyExportPinnedJobsCsv,
            IncludePinnedJobsCsvExportComment = !studio.NightlyExportPinnedJobsCsv,
            ActivityLogExportSinceDays = studio.ActivityLogKeepDays > 0 ? studio.ActivityLogKeepDays : null
        };

        return NightlyAutomationScriptService.Export(_workspace.GetWorkspaceRoot(), options);
    }

    public JobNextActionEntry? GetNextActionForJob(string jobId)
    {
        var job = RequireJob(jobId);
        return PublishWorkflowService.GetNextActionForJob(job);
    }

    public JobWizardStateSnapshot GetWizardTabState(string jobId)
        => PublishWizardTabService.BuildForJob(RequireJob(jobId));

    public JobWizardStateListSnapshot GetAllWizardTabStates()
        => PublishWizardTabService.BuildForJobs(_store.List());

    public Task<JobNextActionResult> ExecuteNextActionAsync(
        string jobId,
        string? actionOverride,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        CancellationToken ct = default)
    {
        JobNextActionType? action = string.IsNullOrWhiteSpace(actionOverride)
            ? null
            : PublishWorkflowService.ParseActionOverride(actionOverride);
        return ExecuteNextActionAsync(jobId, action, confirmCleanupAsync, percent: null, ct);
    }

    public async Task<JobNextActionResult> ExecuteNextActionAsync(
        string jobId,
        JobNextActionType? action = null,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        Action<int>? percent = null,
        CancellationToken ct = default)
    {
        var job = RequireJob(jobId);
        var entry = action is null ? GetNextActionForJob(jobId) : null;
        var resolvedAction = action ?? entry?.Action ?? JobNextActionType.None;

        if (resolvedAction == JobNextActionType.None)
        {
            return new JobNextActionResult
            {
                Success = false,
                Error = "暂无建议操作",
                Message = "暂无建议操作",
                Action = JobNextActionType.None,
                Job = job
            };
        }

        entry ??= new JobNextActionEntry
        {
            JobId = job.Id,
            Title = job.Title,
            Action = resolvedAction,
            ActionLabel = PublishWorkflowService.ActionLabel(resolvedAction),
            Reason = PublishWorkflowService.ActionLabel(resolvedAction)
        };

        try
        {
            switch (resolvedAction)
            {
                case JobNextActionType.RetryFailed:
                {
                    var updated = await RetryJobAsync(jobId, confirmCleanupAsync, percent, ct)
                        .ConfigureAwait(false);
                    return new JobNextActionResult
                    {
                        Success = updated.Status != JobStatus.Failed,
                        Error = updated.Status == JobStatus.Failed ? updated.Error ?? "重试失败" : null,
                        Message = updated.Status == JobStatus.Failed
                            ? updated.Error ?? "重试失败"
                            : "重试完成",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = updated
                    };
                }
                case JobNextActionType.DownloadInbox:
                {
                    var download = await DownloadThreadAttachmentsAsync(jobId, ct).ConfigureAwait(false);
                    return new JobNextActionResult
                    {
                        Success = download.Success,
                        Error = download.Error,
                        Message = download.Success
                            ? $"已下载 {download.DownloadedCount} 个附件"
                            : download.Error ?? "下载失败",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = download.Job
                    };
                }
                case JobNextActionType.RunPipeline:
                {
                    var pipeline = await RunFullPipelineAsync(jobId, confirmCleanupAsync, percent, ct)
                        .ConfigureAwait(false);
                    return new JobNextActionResult
                    {
                        Success = pipeline.Success,
                        Error = pipeline.Error,
                        Message = pipeline.Success ? "流水线完成" : pipeline.Error ?? "流水线失败",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = pipeline.Job
                    };
                }
                case JobNextActionType.FillLinks:
                    return new JobNextActionResult
                    {
                        Success = false,
                        NeedsUserInput = true,
                        Message = "请在任务工作台回填百度/夸克/TG 发布链接",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = RequireJob(jobId)
                    };
                case JobNextActionType.GenerateCopy:
                {
                    GeneratePublishCopy(jobId);
                    return new JobNextActionResult
                    {
                        Success = true,
                        Message = "已生成发布文案",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = RequireJob(jobId)
                    };
                }
                case JobNextActionType.MarkPublished:
                {
                    job = RequireJob(jobId);
                    if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link)
                        || string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
                    {
                        return new JobNextActionResult
                        {
                            Success = false,
                            Error = "链接未填齐，无法标记已发布",
                            Message = "链接未填齐，无法标记已发布",
                            Action = resolvedAction,
                            Entry = entry,
                            Job = job
                        };
                    }

                    var published = MarkAllChannelsPublished(jobId);
                    return new JobNextActionResult
                    {
                        Success = true,
                        Message = "已标记全部渠道已发布",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = published
                    };
                }
                case JobNextActionType.SendTelegram:
                {
                    var send = await SendPublishCopyToTelegramAsync(jobId, ct).ConfigureAwait(false);
                    return new JobNextActionResult
                    {
                        Success = send.Success,
                        Error = send.Error,
                        Message = send.Success ? "已发送到 Telegram" : send.Error ?? "发送失败",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = RequireJob(jobId)
                    };
                }
                default:
                    return new JobNextActionResult
                    {
                        Success = false,
                        Error = $"不支持的操作: {resolvedAction}",
                        Message = $"不支持的操作: {resolvedAction}",
                        Action = resolvedAction,
                        Entry = entry,
                        Job = job
                    };
            }
        }
        catch (Exception ex)
        {
            return new JobNextActionResult
            {
                Success = false,
                Error = ex.Message,
                Message = ex.Message,
                Action = resolvedAction,
                Entry = entry,
                Job = _store.Get(jobId)
            };
        }
    }

    public ScheduleRegisterResult RegisterScheduleBatch(
        string? cliExePath = null,
        string? filter = null,
        int? hour = null,
        int? minute = null,
        string? taskName = null)
    {
        var studio = _app.StudioConfig.Load();
        var options = new ScheduleBatchOptions
        {
            CliExePath = cliExePath ?? ScheduleBatchScriptService.ResolveDefaultCliExePath(_app.Paths),
            Filter = filter ?? studio.ScheduledBatchFilter,
            Hour = hour ?? studio.ScheduledBatchHour,
            Minute = minute ?? studio.ScheduledBatchMinute,
            TaskName = taskName ?? "PLCPak Nightly Batch"
        };

        var export = ScheduleBatchScriptService.Export(_workspace.GetWorkspaceRoot(), options);
        var registration = ScheduleBatchScriptService.TryRegisterScheduledTask(export, options.TaskName);
        export.Registration = registration;

        return new ScheduleRegisterResult
        {
            Success = registration.Success,
            Message = registration.Success
                ? $"{export.SummaryText}\n{registration.Message}"
                : $"{export.SummaryText}\nBAT: {export.BatPath}\n注册失败: {registration.Message}",
            Export = export,
            CommandOutput = registration.Message
        };
    }

    public ScheduleRegisterResult TryRegisterScheduledTask(string schTasksCommand)
        => ScheduleBatchScriptService.TryRegisterTask(schTasksCommand);

    public ScheduleRegisterResult ExportAndRegisterScheduleBatch(
        string? cliExePath = null,
        string? filter = null,
        int? hour = null,
        int? minute = null,
        string? taskName = null)
        => RegisterScheduleBatch(cliExePath, filter, hour, minute, taskName);

    public async Task<PublishJob> RetryJobAsync(
        string jobId,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
    {
        var job = RequireJob(jobId);
        if (job.Status != JobStatus.Failed)
            throw new InvalidOperationException("仅失败状态的任务可重试");

        job.Error = null;
        job.AppendLog("开始重试失败任务...");
        _store.Save(job);

        if (ExtractService.HasExtractedContent(job.Paths.Extract))
            return await ProcessAsync(jobId, confirmCleanupAsync, percent, cancellationToken).ConfigureAwait(false);

        job = ScanInbox(jobId);
        if (job.Artifacts.InboxArchives.Count == 0)
            throw new InvalidOperationException("inbox 中没有压缩包，无法重试");

        job = Extract(jobId, cancellationToken);
        if (job.Status == JobStatus.Failed)
            return job;

        return await ProcessAsync(jobId, confirmCleanupAsync, percent, cancellationToken).ConfigureAwait(false);
    }

    public Task<BatchPipelineResult> BatchRunPipeline(
        JobListFilter filter = JobListFilter.Active,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        CancellationToken cancellationToken = default)
        => RunBatchPipelineAsync(filter, force: false, confirmCleanupAsync, cancellationToken);

    public async Task<BatchPipelineResult> RunBatchPipelineAsync(
        JobListFilter filter = JobListFilter.Active,
        bool force = false,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchPipelineResult();
        var jobs = JobHealthService.FilterForBatchPipeline(_store.List(), filter);

        Func<CleanupConfirmation, Task<bool>> confirm = force
            ? (_ => Task.FromResult(true))
            : confirmCleanupAsync ?? (_ => Task.FromResult(true));

        foreach (var job in jobs)
        {
            if (job.Status == JobStatus.Extracted)
            {
                try
                {
                    var processed = await ProcessAsync(job.Id, confirm, cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (processed.Status == JobStatus.Processed)
                    {
                        result.Success++;
                        result.Messages.Add($"[成功] {job.Title}: 压缩完成");
                    }
                    else
                    {
                        result.Failed++;
                        result.Messages.Add($"[失败] {job.Title}: {processed.Error ?? "处理未完成"}");
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"[失败] {job.Title}: {ex.Message}");
                }

                continue;
            }

            if (job.Status is JobStatus.Processed or JobStatus.Published or JobStatus.Archived)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title}: 状态 {job.Status}");
                continue;
            }

            try
            {
                var pipeline = await RunFullPipelineAsync(job.Id, confirm, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (pipeline.Success)
                {
                    result.Success++;
                    result.Messages.Add($"[成功] {job.Title}");
                }
                else
                {
                    result.Failed++;
                    result.Messages.Add($"[失败] {job.Title}: {pipeline.Error ?? "流水线失败"}");
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Messages.Add($"[失败] {job.Title}: {ex.Message}");
            }
        }

        var logMessages = new List<string>
        {
            $"Success: {result.Success}",
            $"Failed: {result.Failed}",
            $"Skipped: {result.Skipped}"
        };
        logMessages.AddRange(result.Messages);
        result.BatchLogPath = BatchLogService.WriteBatchLog(
            _workspace.GetWorkspaceRoot(),
            "batch-pipeline",
            logMessages);

        return result;
    }

    public JobDeleteResult DeleteJob(string jobId, bool deleteFolders = false, bool useRecycleBin = false)
    {
        var job = RequireJob(jobId);
        var result = new JobDeleteResult
        {
            JobId = job.Id,
            Title = job.Title,
            DeletedFolders = deleteFolders,
            UseRecycleBin = deleteFolders && useRecycleBin
        };

        if (deleteFolders)
            result.RemovedPaths.AddRange(JobFolderRemovalService.RemoveJobFolders(job, _workspace, useRecycleBin));

        _store.Delete(jobId);
        InvalidateDuplicateCache();
        return result;
    }

    public async Task<PipelineRunResult> RunFullPipelineAsync(
        string jobId,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        Action<int>? percent = null,
        CancellationToken cancellationToken = default)
    {
        var result = new PipelineRunResult();
        var job = RequireJob(jobId);

        try
        {
            var studio = _app.StudioConfig.Load();
            if (!string.IsNullOrWhiteSpace(job.Source.ThreadUrl)
                && studio.AutoDownloadThreadAttachments
                && _workspace.FindArchives(job.Paths.Inbox).Count == 0)
            {
                await DownloadThreadAttachmentsAsync(jobId, cancellationToken).ConfigureAwait(false);
                job = RequireJob(jobId);
                result.Steps.Add("download-attachments");
            }

            job = ScanInbox(jobId);
            result.Steps.Add("scan");

            if (string.IsNullOrWhiteSpace(job.Source.ArchivePassword))
            {
                MatchPasswords(jobId);
                job = RequireJob(jobId);
                result.Steps.Add("match-passwords");
            }

            if (job.Artifacts.InboxArchives.Count == 0)
                throw new InvalidOperationException("inbox 中没有压缩包。请先拖入或放入压缩文件。");

            job = Extract(jobId, cancellationToken);
            result.Steps.Add("extract");
            if (job.Status == JobStatus.Failed)
            {
                result.Error = job.Error ?? "解压失败";
                result.Job = job;
                return result;
            }

            job = await ProcessAsync(jobId, confirmCleanupAsync, percent, cancellationToken).ConfigureAwait(false);
            result.Steps.Add("process");
            result.Success = job.Status == JobStatus.Processed;
            result.Job = job;
            if (!result.Success)
                result.Error = job.Error ?? "处理未完成";
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.Job = _store.Get(jobId);
            return result;
        }
    }

    public PublishHistoryExport ExportPublishHistory()
    {
        var entries = PublishDashboardService.BuildHistory(_store.List());
        var export = new PublishHistoryExport
        {
            Version = AppVersion.Current,
            Count = entries.Count,
            Entries = entries
        };

        Directory.CreateDirectory(_workspace.PublishedDirectory);
        var jsonPath = Path.Combine(_workspace.PublishedDirectory, "publish-history.json");
        var csvPath = Path.Combine(_workspace.PublishedDirectory, "publish-history.csv");
        JsonHelper.WriteFile(jsonPath, export, utf8Bom: true);
        File.WriteAllText(csvPath, PublishDashboardService.ToCsv(entries), Encoding.UTF8);

        export.ExportPath = csvPath;
        return export;
    }

    public async Task<WorkflowChainResult> RunAutomatableChainAsync(
        string jobId,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        CancellationToken ct = default)
    {
        var job = RequireJob(jobId);
        return await WorkflowChainService.RunAutomatableChainAsync(
            job,
            action => ExecuteNextActionAsync(jobId, action, confirmCleanupAsync, ct: ct),
            id => _store.Get(id),
            ct).ConfigureAwait(false);
    }

    public async Task<BatchChainResult> RunBatchAutomatableChainAsync(
        JobListFilter filter = JobListFilter.Active,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        CancellationToken ct = default)
        => await RunBatchAutomatableChainCoreAsync(
            JobHealthService.FilterForBatchPipeline(_store.List(), filter),
            confirmCleanupAsync,
            "batch-automatable-chain",
            ct).ConfigureAwait(false);

    public async Task<BatchChainResult> RunBatchChainAsync(
        JobListFilter filter = JobListFilter.Active,
        bool force = false,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync = null,
        CancellationToken ct = default)
        => await RunBatchAutomatableChainCoreAsync(
            JobHealthService.FilterForBatchChain(_store.List(), filter),
            force ? null : confirmCleanupAsync,
            "batch-chain",
            ct).ConfigureAwait(false);

    private async Task<BatchChainResult> RunBatchAutomatableChainCoreAsync(
        IReadOnlyList<PublishJob> jobs,
        Func<CleanupConfirmation, Task<bool>>? confirmCleanupAsync,
        string logOperation,
        CancellationToken ct)
    {
        var result = await WorkflowChainService.RunBatchAutomatableChainAsync(
            jobs,
            job => RunAutomatableChainAsync(job.Id, confirmCleanupAsync, ct),
            ct).ConfigureAwait(false);

        var logMessages = new List<string>
        {
            $"Success: {result.Success}",
            $"Failed: {result.Failed}",
            $"StoppedForManual: {result.StoppedForManual}",
            $"Skipped: {result.Skipped}"
        };
        logMessages.AddRange(result.Messages);
        result.BatchLogPath = BatchLogService.WriteBatchLog(
            _workspace.GetWorkspaceRoot(),
            logOperation,
            logMessages);

        return result;
    }

    public string ExportStudioConfig(string path)
        => new StudioConfigExportService(_app.StudioConfig).ExportStudioConfig(path);

    public ImportResult ImportStudioConfig(string path)
        => new StudioConfigExportService(_app.StudioConfig).ImportStudioConfig(path);

    public AllBackupExportResult ExportAllBackup(string? outputPath = null)
        => new AllBackupService(_store, _workspace, _app.StudioConfig).ExportAllBackup(outputPath);

    public AllBackupImportResult ImportAllBackup(string inputPath, bool merge = false)
        => new AllBackupService(_store, _workspace, _app.StudioConfig).ImportAllBackup(inputPath, merge);

    public DailyReportSnapshot ExportDailyReportSnapshot()
    {
        var snapshot = DailyReportService.BuildReport(_store.List());
        DailyReportService.Export(_workspace.GetWorkspaceRoot(), snapshot);
        return snapshot;
    }

    public string ExportDailyReport()
        => ExportDailyReportSnapshot().CsvPath!;

    public string ExportAllJobsBackup()
        => JobBackupService.ExportToFile(_workspace.GetWorkspaceRoot(), _store.List());

    public JobBackupImportResult ImportJobsBackup(string path, bool merge = true)
        => JobBackupService.ImportFromFile(
            _store,
            path,
            merge ? JobBackupImportMode.Merge : JobBackupImportMode.SkipExisting);

    public string GetQuickStatsOneLiner()
        => QuickStatsService.BuildOneLiner(_store.List());

    public WorkflowGuideSnapshot GetWorkflowGuide()
        => WorkflowGuideService.Build(_store.List());

    public MaintenanceReport GetMaintenanceReport(int staleDays = 7)
        => MaintenanceService.BuildReport(_store.List(), staleDays);

    public BulkArchiveResult BulkArchivePublishedJobs(int olderThanDays = 0)
    {
        var result = MaintenanceService.BulkArchivePublished(_store.List(), _store, olderThanDays);
        if (result.Archived > 0)
            InvalidateDuplicateCache();

        return result;
    }

    public BatchArchiveFilteredPreviewResult PreviewBatchArchiveFiltered(
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort)
        => BatchArchiveFilteredService.PreviewDetailed(_store.List(), filter, searchText, tagFilter, sort);

    public BatchPinFilteredPreviewResult PreviewBatchPinFiltered(
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        bool pin = true)
        => BatchPinFilteredService.Preview(_store.List(), filter, searchText, tagFilter, pin);

    public BatchPinFilteredResult BatchPinFilteredJobs(
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        bool pin = true)
    {
        var result = BatchPinFilteredService.BatchPinFilteredJobs(
            _store.List(),
            _store,
            filter,
            searchText,
            tagFilter,
            pin);

        if (result.Applied > 0)
            LogActivity("pin", $"批量{(pin ? "置顶" : "取消置顶")}筛选: 更新 {result.Applied}，跳过 {result.Skipped}");

        return result;
    }

    public BulkArchiveResult BatchArchiveFilteredJobs(
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort)
    {
        var result = BatchArchiveFilteredService.ArchiveJobs(
            _store.List(),
            _store,
            filter,
            searchText,
            tagFilter,
            sort);

        if (result.Archived > 0)
        {
            InvalidateDuplicateCache();
            LogActivity("archive", $"批量归档筛选: 成功 {result.Archived}，跳过 {result.Skipped}");
        }

        return result;
    }

    public OperationsCenterSnapshot GetOperationsCenterSnapshot()
        => GetOrBuildOperationsSnapshot();

    public DuplicateOperationsBundle GetDuplicateOperationsBundle()
        => GetOrBuildDuplicateBundle();

    public DuplicateScanResult ScanDuplicateJobs()
        => GetOrBuildDuplicateBundle().Scan;

    public DuplicateMergeSuggestionResult GetDuplicateMergeSuggestions()
        => GetOrBuildDuplicateBundle().Suggestions;

    public BatchDuplicateMergePreviewResult PreviewBatchMergeDuplicates()
        => BatchDuplicateMergePreviewService.PreviewAll(_store.List());

    public BatchDuplicateMergePreviewExportResult ExportBatchMergePreview(string? outputPath = null)
    {
        var preview = PreviewBatchMergeDuplicates();
        var export = BatchDuplicateMergePreviewExportService.Export(
            preview,
            _workspace.GetWorkspaceRoot(),
            outputPath);
        LogActivity("export", $"导出批量合并预览: {export.GroupCount} 组，{export.MergeActionCount} 个合并动作");
        return export;
    }

    public BatchDuplicateMergeResult BatchMergeDuplicateJobs()
    {
        var result = BatchDuplicateMergeService.MergeAll(
            _store.List(),
            (targetJobId, sourceJobId) => MergeJobs(targetJobId, sourceJobId, archiveSource: true));

        if (result.Merged > 0)
        {
            InvalidateDuplicateCache();
            LogActivity("merge", $"批量合并重复: 成功 {result.Merged}，跳过 {result.Skipped}，失败 {result.Failed}");
        }

        return result;
    }

    public int GetActivityLogKeepDays()
    {
        var configured = _app.StudioConfig.Load().ActivityLogKeepDays;
        return configured > 0 ? configured : 30;
    }

    public ActivityLogTrimPreviewResult PreviewActivityLogTrim(
        int? keepDays = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
        => ActivityLogService.PreviewTrim(
            _workspace.GetWorkspaceRoot(),
            keepDays ?? GetActivityLogKeepDays(),
            category,
            sinceDays,
            since,
            until);

    public ActivityLogTrimPreviewResult PreviewActivityLogArchive(
        int? keepDays = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
        => ActivityLogService.PreviewArchive(
            _workspace.GetWorkspaceRoot(),
            keepDays ?? GetActivityLogKeepDays(),
            category,
            sinceDays,
            since,
            until);

    public ActivityLogTrimResult TrimActivityLog(
        int? keepDays = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var keep = keepDays ?? GetActivityLogKeepDays();
        var summaryParts = new List<string>();

        if (_app.StudioConfig.Load().ArchiveBeforeTrimActivityLog)
        {
            var archive = ActivityLogService.ArchiveOlderThan(
                _workspace.GetWorkspaceRoot(),
                keep,
                category,
                sinceDays,
                since,
                until);
            if (archive.ArchivedCount > 0)
                summaryParts.Add(archive.SummaryText);
        }

        var result = ActivityLogService.Trim(
            _workspace.GetWorkspaceRoot(),
            keep,
            category,
            sinceDays,
            since,
            until);

        summaryParts.Add(result.SummaryText);
        result.SummaryText = string.Join(" ", summaryParts);
        LogActivity("maintenance", result.SummaryText);
        return result;
    }

    public ActivityLogArchiveResult ArchiveActivityLog(
        int? keepDays = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var keep = keepDays ?? GetActivityLogKeepDays();
        var result = ActivityLogService.ArchiveOlderThan(
            _workspace.GetWorkspaceRoot(),
            keep,
            category,
            sinceDays,
            since,
            until);
        LogActivity("maintenance", result.SummaryText);
        return result;
    }

    public BatchTagResult BatchApplyTags(
        string tags,
        BatchTagMode mode = BatchTagMode.Append,
        JobListFilter filter = JobListFilter.All,
        string? tagFilter = null,
        string? searchText = null,
        IEnumerable<string>? jobIds = null)
    {
        var jobs = _store.List();
        var result = BatchTagService.Apply(jobs, tags, mode, tagFilter, filter, searchText, jobIds);

        foreach (var jobId in result.UpdatedJobIds)
        {
            var job = jobs.FirstOrDefault(candidate => candidate.Id == jobId);
            if (job is not null)
                _store.Save(job);
        }

        if (result.Applied > 0)
        {
            var sample = BatchActivityLogHelper.FormatJobIdSample(result.UpdatedJobIds);
            LogActivity(
                "tags",
                string.IsNullOrWhiteSpace(sample)
                    ? $"批量标签: 更新 {result.Applied}，跳过 {result.Skipped}"
                    : $"批量标签: 更新 {result.Applied}，跳过 {result.Skipped}；任务 {sample}");
        }

        return result;
    }

    public BatchTagResult BatchApplyTagsToJobIds(
        IEnumerable<string> jobIds,
        string tags,
        BatchTagMode mode = BatchTagMode.Append)
        => BatchApplyTags(tags, mode, jobIds: jobIds);

    public BulkArchiveResult BatchArchiveJobIds(IEnumerable<string> jobIds)
    {
        var result = BatchArchiveJobIdsService.Archive(jobIds, _store);
        if (result.Archived > 0)
        {
            InvalidateDuplicateCache();
            var sample = BatchActivityLogHelper.FormatJobIdSample(result.AffectedJobIds);
            LogActivity(
                "archive",
                string.IsNullOrWhiteSpace(sample)
                    ? $"批量归档选中: 成功 {result.Archived}，跳过 {result.Skipped}"
                    : $"批量归档选中: 成功 {result.Archived}，跳过 {result.Skipped}；任务 {sample}");
        }

        return result;
    }

    public BulkUnarchiveResult BatchUnarchiveJobIds(IEnumerable<string> jobIds)
    {
        var result = BatchUnarchiveJobIdsService.Unarchive(jobIds, _store);
        if (result.Unarchived > 0)
        {
            var sample = BatchActivityLogHelper.FormatJobIdSample(result.AffectedJobIds);
            LogActivity(
                "unarchive",
                string.IsNullOrWhiteSpace(sample)
                    ? $"批量恢复选中: 成功 {result.Unarchived}，跳过 {result.Skipped}"
                    : $"批量恢复选中: 成功 {result.Unarchived}，跳过 {result.Skipped}；任务 {sample}");
        }

        return result;
    }

    public BatchDeleteJobIdsPreviewResult PreviewBatchDeleteJobIds(IEnumerable<string> jobIds)
        => BatchDeleteJobIdsService.Preview(jobIds, _store, _workspace);

    public BatchDeleteJobIdsResult BatchDeleteJobIds(
        IEnumerable<string> jobIds,
        bool deleteFolders = false,
        bool useRecycleBin = false)
    {
        var result = BatchDeleteJobIdsService.Delete(
            jobIds,
            _store,
            _workspace,
            deleteFolders,
            useRecycleBin);
        if (result.Deleted > 0)
        {
            InvalidateDuplicateCache();
            var sample = BatchActivityLogHelper.FormatJobIdSample(result.DeletedJobIds);
            var detail = $"批量删除选中: 成功 {result.Deleted}，跳过 {result.Skipped}，删目录={deleteFolders}，回收站={useRecycleBin}";
            LogActivity(
                "delete",
                string.IsNullOrWhiteSpace(sample) ? detail : $"{detail}；任务 {sample}");
        }

        return result;
    }

    public GlobalShortcutProfileExportResult ExportShortcutProfile(string? outputPath = null)
    {
        var export = GlobalShortcutProfileService.Export(
            _app.UiPreferences.Load(),
            outputPath,
            _workspace.GetWorkspaceRoot());
        LogActivity("export", $"导出快捷键配置: {export.OverrideCount} 映射，{export.DisabledCount} 禁用");
        return export;
    }

    public GlobalShortcutProfileImportResult ImportShortcutProfile(string inputPath, bool merge = true)
    {
        var prefs = _app.UiPreferences.Load();
        var result = GlobalShortcutProfileService.Import(inputPath, prefs, merge);
        if (result.Success)
        {
            _app.UiPreferences.Save(prefs);
            LogActivity("import", result.SummaryText);
        }

        return result;
    }

    public TgPreviewSnapshot GetTgPreviewSnapshot(int limit = 50)
        => TgPreviewService.BuildPreview(_store.List(), limit);

    public ActivityLogExportResult ExportActivityLog(
        string? outputPath = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var export = ActivityLogService.Export(
            _workspace.GetWorkspaceRoot(),
            outputPath,
            category,
            sinceDays,
            since,
            until);
        LogActivity("export", $"导出活动日志: {export.EntryCount} 条");
        return export;
    }

    public ActivityLogCsvExportResult ExportActivityLogCsv(
        string? outputPath = null,
        string? query = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var export = ActivityLogCsvExportService.Export(
            _workspace.GetWorkspaceRoot(),
            outputPath,
            query,
            category,
            sinceDays,
            since,
            until);
        var hint = string.IsNullOrWhiteSpace(export.QueryFilter) ? string.Empty : $"，关键词「{export.QueryFilter}」";
        LogActivity("export", $"导出活动日志 CSV: {export.EntryCount} 条{hint}");
        return export;
    }

    public MachineProfilePreviewResult PreviewMachineProfile(string inputPath, bool merge = true)
        => new MachineProfileService(_workspace, _app.StudioConfig, _app.UiPreferences)
            .PreviewMachineProfile(inputPath, merge);

    public IReadOnlyDictionary<string, int> GetActivityLogCategoryCounts(int? sinceDays = null, string? category = null)
        => ActivityLogService.GetCategoryCounts(_workspace.GetWorkspaceRoot(), sinceDays, category);

    public ActivityLogStatsResult GetActivityLogStats(int? sinceDays = null, string? category = null)
        => ActivityLogStatsService.BuildStats(_workspace.GetWorkspaceRoot(), sinceDays, category);

    public ActivityLogStatsExportResult ExportActivityLogStats(string? outputPath = null, int? sinceDays = null)
    {
        var export = ActivityLogStatsService.Export(
            _workspace.GetWorkspaceRoot(),
            outputPath,
            sinceDays);
        LogActivity("export", $"导出活动日志统计: {export.Stats.TotalCount} 条，{export.Stats.Items.Count} 个分类");
        return export;
    }

    public ActivityLogStatsCsvExportResult ExportActivityLogStatsCsv(string? outputPath = null, int? sinceDays = null)
    {
        var export = ActivityLogStatsService.ExportCsv(
            _workspace.GetWorkspaceRoot(),
            outputPath,
            sinceDays);
        LogActivity("export", $"导出活动日志统计 CSV: {export.Stats.TotalCount} 条，{export.Stats.Items.Count} 个分类");
        return export;
    }

    public ActivityLogStatsHtmlExportResult ExportActivityLogStatsHtml(string? outputPath = null, int? sinceDays = null)
    {
        var stats = GetActivityLogStats(sinceDays);
        var dailyStats = GetActivityLogDailyStats(days: 7);
        var prefs = _app.UiPreferences.Load();
        var batchSummary = GetActivityLogBatchSummary(sinceDays);
        var export = ActivityLogStatsHtmlExportService.Export(
            stats,
            _workspace.GetWorkspaceRoot(),
            outputPath,
            dailyStats,
            prefs.HtmlReportTheme,
            batchSummary,
            sinceDays);
        var batchHint = batchSummary.TotalCount > 0 ? $"，批量 {batchSummary.TotalCount} 条" : string.Empty;
        LogActivity("export", $"导出活动日志统计 HTML: {export.Stats.TotalCount} 条，{export.Stats.Items.Count} 个分类{batchHint}");
        return export;
    }

    public ActivityLogDailyStatsResult GetActivityLogDailyStats(int days = 7, string? category = null)
        => ActivityLogDailyStatsService.GetDailyCounts(_workspace.GetWorkspaceRoot(), days, category);

    public ActivityLogDailyStatsCsvExportResult ExportActivityLogDailyStatsCsv(string? outputPath = null, int days = 7)
    {
        var export = ActivityLogDailyStatsService.ExportCsv(
            _workspace.GetWorkspaceRoot(),
            outputPath,
            days);
        LogActivity("export", $"导出活动日志每日统计 CSV: 近 {export.Stats.Days} 天 {export.Stats.TotalCount} 条");
        return export;
    }

    public FilteredJobsCsvExportResult ExportFilteredJobsCsv(
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort,
        int? limit = null,
        string? outputPath = null)
    {
        var export = FilteredJobsCsvExportService.Export(
            _store.List(),
            _workspace.GetWorkspaceRoot(),
            filter,
            searchText,
            tagFilter,
            sort,
            limit,
            outputPath);
        LogActivity("export", $"导出筛选任务 CSV: {export.EntryCount} 条");
        return export;
    }

    public FilteredJobsJsonExportResult ExportFilteredJobsJson(
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort,
        int? limit = null,
        string? outputPath = null)
    {
        var export = FilteredJobsJsonExportService.Export(
            _store.List(),
            _workspace.GetWorkspaceRoot(),
            filter,
            searchText,
            tagFilter,
            sort,
            limit,
            outputPath);
        LogActivity("export", $"导出筛选任务 JSON: {export.EntryCount} 条");
        return export;
    }

    public FilteredJobsCsvExportResult ExportSelectedJobsCsv(
        IEnumerable<string> jobIds,
        string? outputPath = null)
    {
        var export = SelectedJobsCsvExportService.Export(
            _store.List(),
            jobIds,
            _workspace.GetWorkspaceRoot(),
            outputPath);
        LogActivity("export", $"导出选中任务 CSV: {export.EntryCount} 条");
        return export;
    }

    public FilteredJobsCsvExportResult ExportPinnedJobsCsv(string? outputPath = null)
    {
        var export = PinnedJobsCsvExportService.Export(
            _store.List(),
            _workspace.GetWorkspaceRoot(),
            outputPath);
        LogActivity("export", $"导出置顶任务 CSV: {export.EntryCount} 条");
        return export;
    }

    public MachineProfileExportResult ExportMachineProfile(string? outputPath = null)
    {
        var snapshot = GetOperationsCenterSnapshot();
        var export = new MachineProfileService(_workspace, _app.StudioConfig, _app.UiPreferences)
            .ExportMachineProfile(outputPath, snapshot.SummaryText);
        if (!string.IsNullOrWhiteSpace(snapshot.SummaryText))
            export.SummaryText = $"{export.SummaryText} | 运维快照：{snapshot.SummaryText}";
        return export;
    }

    public MachineProfileImportResult ImportMachineProfile(string inputPath, bool merge = true)
    {
        var preview = PreviewMachineProfile(inputPath, merge);
        if (!preview.Valid)
        {
            return new MachineProfileImportResult
            {
                Success = false,
                Message = string.IsNullOrWhiteSpace(preview.Message) ? preview.SummaryText : preview.Message,
                Merged = merge
            };
        }

        var result = new MachineProfileService(_workspace, _app.StudioConfig, _app.UiPreferences)
            .ImportMachineProfile(inputPath, merge);

        if (result.Success)
        {
            InvalidateDuplicateCache();
            LogActivity("import", result.Message);
        }

        return result;
    }

    public ActivityLogBatchSummaryResult GetActivityLogBatchSummary(int? sinceDays = null)
        => ActivityLogBatchSummaryService.Build(_workspace.GetWorkspaceRoot(), sinceDays);

    public ActivityLogBatchSummaryExportResult ExportActivityLogBatchStats(
        string? outputPath = null,
        int? sinceDays = null)
    {
        var export = ActivityLogBatchSummaryExportService.Export(
            _workspace.GetWorkspaceRoot(),
            outputPath,
            sinceDays);
        LogActivity("export", $"导出批量操作统计 JSON: {export.Stats.TotalCount} 条");
        return export;
    }

    public ActivityLogBatchSummaryCsvExportResult ExportActivityLogBatchStatsCsv(
        string? outputPath = null,
        int? sinceDays = null)
    {
        var export = ActivityLogBatchSummaryCsvExportService.Export(
            _workspace.GetWorkspaceRoot(),
            outputPath,
            sinceDays);
        LogActivity("export", $"导出批量操作统计 CSV: {export.Stats.TotalCount} 条");
        return export;
    }

    public ActivityLogBatchBundleExportResult ExportActivityLogBatchStatsAll(int? sinceDays = null)
    {
        var export = ActivityLogBatchSummaryExportService.ExportAll(
            _workspace.GetWorkspaceRoot(),
            sinceDays);
        LogActivity("export", $"导出批量操作统计 JSON+CSV: {export.Stats.TotalCount} 条");
        return export;
    }

    public QueueCsvExportResult ExportTgPendingCsv(int limit = 50, string? outputPath = null)
    {
        var export = QueueCsvExportService.ExportTgPending(
            _store.List(),
            _workspace.GetWorkspaceRoot(),
            limit,
            outputPath);
        LogActivity("export", $"导出 TG 待发 CSV: {export.EntryCount} 条");
        return export;
    }

    public QueueCsvExportResult ExportPublishQueueCsv(int limit = 50, string? outputPath = null)
    {
        var export = QueueCsvExportService.ExportPublishQueue(
            _store.List(),
            _workspace.GetWorkspaceRoot(),
            limit,
            outputPath);
        LogActivity("export", $"导出待发布队列 CSV: {export.EntryCount} 条");
        return export;
    }

    public DuplicateReportExportResult ExportDuplicateReport(string? outputPath = null)
    {
        var bundle = GetOrBuildDuplicateBundle();
        var export = DuplicateScanService.ExportReport(bundle.Scan, _workspace.GetWorkspaceRoot(), outputPath);
        LogActivity("export", $"导出重复任务报告: {export.GroupCount} 组");
        return export;
    }

    public DuplicateMergeExportResult ExportDuplicateMergeSuggestions(string? outputPath = null)
    {
        var bundle = GetOrBuildDuplicateBundle();
        var export = DuplicateMergeExportService.Export(
            bundle.Suggestions,
            _workspace.GetWorkspaceRoot(),
            outputPath);
        LogActivity("export", $"导出重复合并建议: {export.GroupCount} 组，{export.MergeActionCount} 个合并动作");
        return export;
    }

    public List<ActivityLogEntry> SearchActivityLog(
        string? query,
        int limit = 50,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
        => ActivityLogService.Search(
            _workspace.GetWorkspaceRoot(),
            query,
            limit,
            category,
            sinceDays,
            since,
            until);

    public ActivityLogPageResult SearchActivityLogPage(
        string? query,
        int? limit = null,
        int? offset = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var studio = _app.StudioConfig.Load();
        var options = new ActivityLogQueryOptions
        {
            Query = query,
            Limit = limit ?? studio.ActivityLogPageSize,
            Offset = offset ?? 0,
            Category = category,
            SinceDays = sinceDays,
            Since = since,
            Until = until
        };

        return ActivityLogService.SearchPage(_workspace.GetWorkspaceRoot(), options);
    }

    private void TryPromotePublishedStatus(PublishJob job)
    {
        if (!PublishStatusHelper.IsFullyPublished(job.Publish))
            return;

        if (job.Status is JobStatus.Processed or JobStatus.Published)
            job.Status = JobStatus.Published;

        var studio = _app.StudioConfig.Load();
        if (studio.AutoArchiveOnPublished && job.Status == JobStatus.Published)
        {
            job.ArchivedFromStatus = JobStatus.Published;
            job.Status = JobStatus.Archived;
            job.AppendLog("已自动归档（三渠道全部发布后）");
        }
    }

    public void InvalidateDuplicateCache()
    {
        _duplicateBundleCache = null;
        _duplicateBundleCacheKey = null;
        _operationsSnapshotCache = null;
        _operationsSnapshotCacheKey = null;
    }

    private OperationsCenterSnapshot GetOrBuildOperationsSnapshot()
    {
        var jobs = _store.List();
        var cacheKey = BuildOperationsSnapshotCacheKey(jobs);
        if (_operationsSnapshotCache is not null && _operationsSnapshotCacheKey == cacheKey)
            return _operationsSnapshotCache;

        var snapshot = OperationsCenterService.BuildSnapshotFromJobs(jobs);
        EnrichWithActivityLogSummaries(snapshot);
        _operationsSnapshotCache = snapshot;
        _operationsSnapshotCacheKey = cacheKey;
        return _operationsSnapshotCache;
    }

    private void EnrichWithActivityLogSummaries(OperationsCenterSnapshot snapshot)
    {
        var sinceDays = GetActivityLogKeepDays();
        var root = _workspace.GetWorkspaceRoot();

        var stats = ActivityLogStatsService.BuildStats(root, sinceDays);
        snapshot.ActivityLogStatsSummary = stats;
        if (stats.TotalCount > 0)
        {
            snapshot.SummaryText = $"{snapshot.SummaryText} | {stats.SummaryText}";
            snapshot.Sections.Add(new OperationsCenterSection
            {
                Key = "activity-log-stats",
                Title = "活动日志统计",
                SummaryText = stats.SummaryText
            });
        }

        var batchSummary = ActivityLogBatchSummaryService.Build(root, sinceDays);
        snapshot.ActivityLogBatchSummary = batchSummary;
        if (batchSummary.TotalCount == 0)
            return;

        snapshot.SummaryText = $"{snapshot.SummaryText} | {batchSummary.SummaryText}";
        snapshot.Sections.Add(new OperationsCenterSection
        {
            Key = "activity-log-batch",
            Title = "批量操作统计",
            SummaryText = batchSummary.SummaryText
        });
    }

    private string BuildOperationsSnapshotCacheKey(IReadOnlyList<PublishJob> jobs)
        => $"{BuildJobListFingerprint(jobs)}|{BuildActivityLogFingerprint()}|{GetActivityLogKeepDays()}";

    private string BuildActivityLogFingerprint()
    {
        var path = ActivityLogService.GetLogPath(_workspace.GetWorkspaceRoot());
        if (!File.Exists(path))
            return "0";

        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private DuplicateOperationsBundle GetOrBuildDuplicateBundle()
    {
        var jobs = _store.List();
        var cacheKey = BuildJobListFingerprint(jobs);
        if (_duplicateBundleCache is not null && _duplicateBundleCacheKey == cacheKey)
            return _duplicateBundleCache;

        _duplicateBundleCache = DuplicateOperationsService.BuildBundle(jobs);
        _duplicateBundleCacheKey = cacheKey;
        return _duplicateBundleCache;
    }

    private IReadOnlyList<PublishJob> ResolveJobs(IReadOnlyList<PublishJob>? jobs)
        => jobs ?? _store.List();

    private static string BuildJobListFingerprint(IReadOnlyList<PublishJob> jobs)
    {
        var key = new StringBuilder(jobs.Count * 24);
        key.Append(jobs.Count);
        foreach (var job in jobs)
            key.Append('|').Append(job.Id).Append(':').Append(job.UpdatedAt.Ticks);

        return key.ToString();
    }

    private PublishJob RequireJob(string jobId)
    {
        var job = _store.Get(jobId) ?? throw new InvalidOperationException($"任务不存在: {jobId}");
        return job;
    }
}
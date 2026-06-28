using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class UiStringTableTests
{
    [Fact]
    public void Get_returns_chinese_by_default()
    {
        Assert.Equal("发布看板", UiStringTable.Get("dashboard.title", "zh"));
    }

    [Fact]
    public void Get_returns_english_when_en()
    {
        Assert.Equal("Publish dashboard", UiStringTable.Get("dashboard.title", "en"));
    }

    [Theory]
    [InlineData("dark", "dark")]
    [InlineData("DARK", "dark")]
    [InlineData("", "light")]
    public void NormalizeTheme_maps_values(string input, string expected)
    {
        Assert.Equal(expected, UiStringTable.NormalizeTheme(input));
    }

    [Theory]
    [InlineData("jobs", "zh", "任务工作台")]
    [InlineData("jobs", "en", "Jobs")]
    [InlineData("dashboard", "zh", "发布看板")]
    [InlineData("operations", "en", "Ops")]
    public void GetNavLabel_returns_localized_page_label(string page, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.GetNavLabel(page, lang));
    }

    [Theory]
    [InlineData("metric.tip.total", "zh", "点击查看全部任务")]
    [InlineData("metric.tip.total", "en", "Click to view all jobs")]
    [InlineData("metric.tip.pending", "zh", "点击查看待发布任务")]
    [InlineData("metric.tip.failed", "en", "Click to view failed jobs")]
    [InlineData("metric.tip.tg", "zh", "点击查看 TG 待发并刷新")]
    [InlineData("metric.tip.health", "en", "Click to open Ops for health issues")]
    public void Get_returns_metric_tip_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("jobs.collapse.moreSettings", "zh", "更多设置")]
    [InlineData("jobs.collapse.moreSettings", "en", "More settings")]
    [InlineData("jobs.wizard.title", "zh", "分步发布向导")]
    [InlineData("jobs.wizard.title", "en", "Step-by-step publish")]
    [InlineData("wizard.step.prepare", "zh", "① 素材准备")]
    [InlineData("wizard.step.links", "en", "② Fill links")]
    [InlineData("wizard.status.active", "zh", "进行中")]
    [InlineData("wizard.status.done", "en", "Done")]
    [InlineData("jobs.wizard.execute", "zh", "执行当前步骤")]
    [InlineData("jobs.wizard.stepsLoading", "zh", "步骤更新中…")]
    [InlineData("jobs.wizard.stepsLoading", "en", "Updating steps…")]
    [InlineData("jobs.wizard.noJob", "en", "Select or create a job first")]
    [InlineData("jobs.nextAction.title", "zh", "下一步")]
    [InlineData("jobs.nextAction.fallback", "en", "Select a job to see the suggested action")]
    [InlineData("jobs.selectJobPrompt", "zh", "请选择一个任务")]
    [InlineData("jobs.emptyList", "en", "No jobs yet. Enter a game name and tap New job, or use Create from thread.")]
    [InlineData("jobs.filterEmpty", "zh", "没有符合筛选条件的任务。试试放宽筛选或清空搜索。")]
    [InlineData("jobs.clearFilters", "en", "Clear filters")]
    [InlineData("jobs.emptyDetail", "zh", "从左侧选择任务")]
    [InlineData("dashboard.emptyRecentJobs", "en", "No recent activity. Create or update jobs to see them here.")]
    [InlineData("dashboard.refreshing", "zh", "正在刷新看板…")]
    [InlineData("dashboard.refreshing", "en", "Refreshing dashboard…")]
    [InlineData("jobs.listCountFiltered", "zh", "显示 {0} / {1} 个任务")]
    [InlineData("jobs.multiSelectCount", "en", "{0} jobs selected")]
    [InlineData("jobs.refreshing", "zh", "正在刷新任务列表…")]
    [InlineData("quick.page.sourceCountLoading", "zh", "统计源文件…")]
    [InlineData("quick.page.sourceCountLoading", "en", "Counting sources…")]
    [InlineData("ops.page.staleJobsLoading", "zh", "陈旧任务更新中…")]
    [InlineData("ops.page.staleJobsLoading", "en", "Updating stale jobs…")]
    [InlineData("guide.refreshing", "zh", "全局建议更新中…")]
    [InlineData("guide.refreshing", "en", "Updating global next step…")]
    [InlineData("ops.page.refreshing", "en", "Refreshing ops center…")]
    [InlineData("ops.page.duplicateGroupsEmpty", "zh", "未发现重复任务组。")]
    [InlineData("quick.page.sourceEmpty", "en", "Drop folders/files here, or tap Add folder on the right.")]
    [InlineData("wizard.page.stepProgress", "zh", "已完成 {0} / {1} 步")]
    [InlineData("ops.page.activityLogMatchBadge", "en", "{0} matching entries")]
    [InlineData("feedback.refreshInProgress", "zh", "正在刷新，请稍候…")]
    [InlineData("ops.page.activityLogLoaded", "en", "Showing {0} entries")]
    [InlineData("ops.page.activityLogLoadingMore", "zh", "正在加载更多活动日志…")]
    [InlineData("shell.commandPalette.recent", "zh", "最近使用")]
    [InlineData("shell.commandPalette.keyboardHint", "zh", "↑↓ 选择 · Enter 执行 · ↑ 回到筛选")]
    [InlineData("dashboard.metricsLoading", "en", "Updating metrics…")]
    [InlineData("dashboard.statsLoading", "zh", "统计更新中…")]
    [InlineData("dashboard.statsLoading", "en", "Updating stats…")]
    [InlineData("dashboard.queuesLoading", "zh", "队列更新中…")]
    [InlineData("shell.commandPalette.resultCount", "en", "{0} commands")]
    [InlineData("wizard.page.jobCount", "zh", "共 {0} 个任务")]
    [InlineData("ops.page.filterSummary", "zh", "筛选：{0}")]
    [InlineData("ops.page.staleJobsCount", "en", "{0} stale jobs")]
    [InlineData("ops.page.duplicateGroupsCount", "zh", "{0} 组重复")]
    [InlineData("quick.page.dropAdded", "zh", "已添加 {0} 项到源列表")]
    [InlineData("dashboard.recentJobsLoading", "en", "Updating recent jobs…")]
    [InlineData("dashboard.recentJobsCount", "zh", "{0} 条记录")]
    [InlineData("dashboard.smartNextLoading", "zh", "智能建议更新中…")]
    [InlineData("jobs.detail.inboxDropAdded", "en", "Imported {0} files to inbox")]
    [InlineData("ops.page.activityLogSearchPending", "zh", "正在筛选活动日志…")]
    [InlineData("jobs.filterSummary", "en", "Filters: {0}")]
    [InlineData("jobs.filterPart.search", "zh", "搜索「{0}」")]
    [InlineData("nav.commandPalette", "zh", "命令")]
    [InlineData("nav.commandPalette", "en", "Commands")]
    [InlineData("settings.replayOnboarding", "zh", "重新播放新手引导")]
    [InlineData("settings.section.workspace", "en", "Workspace & compress")]
    [InlineData("nav.help", "zh", "帮助")]
    [InlineData("jobs.log.title", "zh", "任务日志")]
    [InlineData("jobs.proShortcuts.line", "en", "F5 Refresh  Ctrl+R Refresh  Ctrl+S Save links  Ctrl+Enter Pipeline  Ctrl+Alt+←/→ Wizard steps  Ctrl+Alt+Enter Run step  Ctrl+Shift+E Export JSON  Ctrl+Shift+C Export filtered CSV  Ctrl+Shift+A Batch archive filtered  Ctrl+Shift+T Multi-select batch tags")]
    public void Get_returns_wizard_step_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("mode.simpleHintDismiss", "zh", "知道了")]
    [InlineData("mode.simpleHintDismiss", "en", "Got it")]
    public void Get_returns_simple_mode_hint_dismiss_key(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("settings.wizardFeedback", "zh", "向导步骤完成时播放声音与触觉反馈")]
    [InlineData("settings.wizardFeedback", "en", "Sound & haptic feedback when a wizard step completes")]
    public void Get_returns_wizard_feedback_setting_key(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("wizard.page.title", "zh", "发布向导")]
    [InlineData("wizard.page.title", "en", "Wizard")]
    [InlineData("wizard.page.simpleHero", "zh", "傻瓜式发布")]
    [InlineData("wizard.page.simpleFlow", "en", "① Pick a job → ② See highlighted step → ③ Tap Run current step")]
    [InlineData("wizard.page.otherSuggestions", "zh", "其他建议任务")]
    [InlineData("wizard.page.selectJob", "en", "Select job")]
    [InlineData("wizard.page.selectJobPlaceholder", "zh", "选择要查看的发布任务")]
    [InlineData("wizard.page.publishSteps", "en", "Publish steps")]
    [InlineData("wizard.page.refresh", "zh", "刷新")]
    [InlineData("wizard.page.stepsLoading", "zh", "步骤更新中…")]
    [InlineData("wizard.page.stepsLoading", "en", "Updating steps…")]
    [InlineData("wizard.page.autoRunUntilManual", "en", "Auto-run (until manual)")]
    [InlineData("wizard.page.needsManual", "zh", "需要手动操作")]
    [InlineData("wizard.page.executeFailed", "en", "Execution failed")]
    [InlineData("wizard.page.autoRun", "zh", "自动执行")]
    [InlineData("wizard.page.autoRunFailed", "en", "Auto-run failed")]
    public void Get_returns_wizard_page_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("jobs.sidebar.newGameHeader", "zh", "游戏名称")]
    [InlineData("jobs.sidebar.newGameHeader", "en", "Game name")]
    [InlineData("jobs.sidebar.newJobPlaceholder", "zh", "新建任务")]
    [InlineData("jobs.sidebar.threadUrlHeader", "en", "Forum thread URL")]
    [InlineData("jobs.sidebar.pasteLink", "zh", "粘贴链接")]
    [InlineData("jobs.sidebar.fetchThreadInfo", "en", "Fetch thread info")]
    [InlineData("jobs.sidebar.sourceForum", "zh", "来源论坛")]
    [InlineData("jobs.sidebar.createFromThread", "en", "Create from thread")]
    [InlineData("jobs.sidebar.searchJobs", "zh", "搜索任务")]
    [InlineData("jobs.sidebar.searchPlaceholder", "en", "Title / slug / thread URL / ID / notes / tags")]
    [InlineData("jobs.sidebar.jobFilter", "zh", "任务筛选")]
    [InlineData("jobs.sidebar.sort", "en", "Sort")]
    [InlineData("jobs.sidebar.tagFilter", "zh", "标签筛选")]
    [InlineData("jobs.sidebar.batchAppendTags", "en", "Batch append tags")]
    [InlineData("jobs.sidebar.batchArchiveSelected", "zh", "批量归档选中")]
    [InlineData("jobs.sidebar.batchUnarchiveSelected", "en", "Batch restore selected")]
    [InlineData("jobs.sidebar.batchDeleteSelected", "zh", "批量删除选中")]
    [InlineData("jobs.sidebar.batchArchiveFiltered", "en", "Batch archive filtered")]
    [InlineData("jobs.sidebar.batchPinFiltered", "zh", "批量置顶")]
    [InlineData("jobs.sidebar.batchUnpinFiltered", "en", "Batch unpin")]
    [InlineData("jobs.sidebar.archiveJob", "zh", "归档任务")]
    [InlineData("jobs.sidebar.unarchiveJob", "en", "Restore job")]
    [InlineData("jobs.sidebar.deleteJob", "zh", "删除任务")]
    [InlineData("jobs.sidebar.exportJobJson", "en", "Export job JSON")]
    [InlineData("jobs.sidebar.exportFilteredCsv", "zh", "导出筛选 CSV")]
    [InlineData("jobs.sidebar.exportFilteredJson", "en", "Export filtered JSON")]
    [InlineData("jobs.sidebar.exportSelectedCsv", "zh", "导出列表 CSV")]
    [InlineData("jobs.sidebar.exportPinnedCsv", "en", "Export pinned CSV")]
    [InlineData("jobs.sidebar.importJobJson", "zh", "导入任务JSON")]
    [InlineData("jobs.sidebar.importPanLinksCsv", "en", "Import cloud links CSV")]
    [InlineData("jobs.sidebar.refreshList", "zh", "刷新列表")]
    [InlineData("jobs.sidebar.exportPublishHistory", "en", "Export publish history")]
    [InlineData("jobs.sidebar.batchGenerateCopy", "zh", "批量生成文案")]
    [InlineData("jobs.sidebar.batchPipeline", "en", "Batch pipeline")]
    [InlineData("jobs.sidebar.batchAutoChain", "zh", "批量自动链")]
    [InlineData("jobs.sidebar.openWorkspace", "en", "Open workspace")]
    public void Get_returns_jobs_sidebar_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("jobs.detail.executeSuggestedAction", "zh", "执行建议操作")]
    [InlineData("jobs.detail.executeSuggestedAction", "en", "Run suggested action")]
    [InlineData("jobs.detail.runFullPipeline", "zh", "一键流水线")]
    [InlineData("jobs.detail.autoRunUntilManual", "en", "Auto-run (until manual)")]
    [InlineData("jobs.detail.jobNotes", "zh", "任务备注")]
    [InlineData("jobs.detail.saveNotes", "en", "Save notes")]
    [InlineData("jobs.detail.archivePassword", "zh", "解压密码（可自动匹配密码库）")]
    [InlineData("jobs.detail.inboxDropHint", "en", "Drop archives into job inbox (7z/zip/rar)")]
    [InlineData("jobs.detail.passwordLibrary", "zh", "解压密码库")]
    [InlineData("jobs.detail.matchPassword", "en", "Match password")]
    [InlineData("jobs.detail.openThread", "zh", "打开帖子")]
    [InlineData("jobs.detail.process", "en", "Clean ads + compress")]
    [InlineData("jobs.detail.advancedActions", "zh", "高级操作")]
    [InlineData("jobs.detail.mergeIntoCurrent", "en", "Merge into current")]
    [InlineData("jobs.detail.baiduLink", "zh", "百度链接")]
    [InlineData("jobs.detail.extractCode", "en", "Extract code")]
    [InlineData("jobs.detail.telegramPostLink", "zh", "TG 帖子链接（发布后回填）")]
    [InlineData("jobs.detail.saveChannel", "en", "Save channel")]
    [InlineData("jobs.detail.tags", "zh", "标签(逗号分隔)")]
    [InlineData("jobs.detail.pin", "en", "Pin")]
    [InlineData("jobs.detail.unpin", "zh", "取消置顶")]
    [InlineData("jobs.detail.saveLinks", "en", "Save links")]
    [InlineData("jobs.detail.parseClipboardLinks", "zh", "粘贴解析网盘链接")]
    [InlineData("jobs.detail.blurbTemplate", "en", "Blurb template")]
    [InlineData("jobs.detail.generateCopy", "zh", "生成文案")]
    [InlineData("jobs.detail.copyTgCopy", "en", "Copy TG copy")]
    [InlineData("jobs.detail.markAllPublished", "zh", "一键全部已发布")]
    [InlineData("jobs.detail.botSendToTg", "en", "Send via bot")]
    [InlineData("jobs.detail.copyLog", "zh", "复制日志")]
    [InlineData("jobs.detail.exportLog", "en", "Export log")]
    public void Get_returns_jobs_detail_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("quick.page.title", "zh", "快速处理")]
    [InlineData("quick.page.title", "en", "Quick")]
    [InlineData("quick.page.simpleHint", "zh", "拖入游戏文件夹，点「一键执行」完成去广告与压缩。日常发布任务请用「任务工作台」。")]
    [InlineData("quick.page.heroFlow", "en", "① Add source folders → ② Check total size → ③ Tap Run")]
    [InlineData("quick.page.addFolder", "zh", "添加文件夹")]
    [InlineData("quick.page.execute", "en", "Run")]
    [InlineData("quick.page.taskStatus.line", "zh", "{0} | 匹配{1} | {2}")]
    [InlineData("quick.page.totalSize.both", "en", "Total: PC: {0}   Android: {1}")]
    [InlineData("quick.page.adSampleCount", "zh", "清单: {0} 个签名 / {1} 个文件夹")]
    [InlineData("quick.page.progress.scanningSamples", "en", "Scanning samples...")]
    [InlineData("quick.page.log.sevenZipNotFound", "zh", "[X] 未找到 7-Zip, 7z-zstd 压缩不可用")]
    [InlineData("quick.page.error.volumeRange", "en", "Volume size must be 100–10000 MB!")]
    public void Get_returns_quick_page_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("ops.page.title", "zh", "运维中心")]
    [InlineData("ops.page.title", "en", "Ops center")]
    [InlineData("ops.page.simpleHint", "zh", "简易模式：看任务概览和陈旧任务，点「批量归档已发布」即可日常维护。活动日志、重复扫描等请开专业模式。")]
    [InlineData("ops.page.proHint", "en", "F5 / Ctrl+R refresh · Ctrl+5 shortcut")]
    [InlineData("ops.page.staleDaysFormat", "zh", "{0} — {1}（{2} 天）")]
    [InlineData("ops.page.staleDaysFormat", "en", "{0} — {1} ({2} days)")]
    [InlineData("ops.page.staleDaysNotUpdated", "zh", "已 {0} 天未更新")]
    [InlineData("ops.page.staleDaysNotUpdated", "en", "{0} days since update")]
    [InlineData("ops.page.activityLogAndStats", "zh", "活动日志与统计")]
    [InlineData("ops.page.filterBatchArchive", "en", "Batch archive")]
    [InlineData("ops.page.bulkArchivePublished", "zh", "批量归档已发布")]
    [InlineData("ops.page.bulkArchiveButtonCount", "zh", "批量归档已发布 ({0})")]
    [InlineData("ops.page.bulkArchiveButtonCount", "en", "Bulk archive published ({0})")]
    [InlineData("ops.page.duplicateSuggestionsCount", "zh", "{0} 条合并建议")]
    [InlineData("ops.page.duplicateSuggestionsCount", "en", "{0} merge suggestions")]
    [InlineData("ops.page.exportNightlyScript", "en", "Export nightly script")]
    [InlineData("ops.page.mergePreviewEmpty", "zh", "暂无合并预览。当前没有可自动合并的重复任务；可点击「预览合并」生成预览，或刷新后重试。")]
    public void Get_returns_ops_page_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("settings.configPathUnknown", "zh", "studio-config.json 路径未知")]
    [InlineData("settings.configPathUnknown", "en", "studio-config.json path unknown")]
    [InlineData("settings.configPathFormat", "zh", "配置文件路径：{0}")]
    [InlineData("settings.configPathFormat", "en", "Config path: {0}")]
    [InlineData("settings.general.exportConfig", "zh", "导出配置")]
    [InlineData("settings.general.importConfig", "en", "Import config")]
    [InlineData("settings.general.workspaceRoot", "zh", "工作区根目录")]
    [InlineData("settings.general.recycleBin", "en", "Delete to Recycle Bin")]
    [InlineData("settings.general.whitelist", "zh", "白名单(每行一条)")]
    [InlineData("settings.jobs.sectionTitle", "zh", "任务工作台")]
    [InlineData("settings.jobs.professionalMode", "en", "Pro mode (all advanced features; off = simple guided mode)")]
    [InlineData("settings.shortcuts.disableGlobal", "zh", "禁用全局快捷键")]
    [InlineData("settings.shortcuts.batchArchiveFiltered", "en", "Ctrl+Shift+A Batch archive filtered")]
    [InlineData("settings.shortcuts.htmlReportTheme", "zh", "活动日志 HTML 报告默认主题")]
    [InlineData("settings.shortcuts.showHelp", "en", "View shortcut reference")]
    [InlineData("settings.shortcuts.overrideHint", "zh", "留空表示使用默认快捷键。格式示例：Ctrl+Shift+G")]
    [InlineData("settings.shortcuts.invalidFormatTitle", "en", "Invalid shortcut format")]
    [InlineData("settings.shortcuts.conflictTitle", "zh", "快捷键冲突")]
    [InlineData("settings.telegram.autoDownloadAttachments", "en", "Auto-download attachments when creating from thread")]
    [InlineData("settings.publish.autoArchiveAfterPublish", "zh", "发布后自动归档")]
    [InlineData("settings.nightly.sectionTitle", "zh", "定时批量流水线")]
    [InlineData("settings.nightly.autoSendTg", "en", "Nightly script: auto batch send TG")]
    [InlineData("settings.nightly.exportPinnedJobsCsv", "zh", "夜间全流程脚本自动导出置顶任务 CSV")]
    [InlineData("settings.activityLog.keepDays", "en", "Activity log keep days")]
    [InlineData("settings.activityLog.scheduledBatchEnable", "zh", "启用定时批量（导出脚本供任务计划程序使用）")]
    [InlineData("settings.activityLog.scheduledFilter", "en", "Batch filter (active/failed/all)")]
    [InlineData("settings.close", "zh", "关闭")]
    [InlineData("settings.confirm", "en", "OK")]
    public void Get_returns_settings_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Theory]
    [InlineData("jobs.msg.title.duplicateJob", "zh", "重复任务")]
    [InlineData("jobs.msg.title.duplicateJob", "en", "Duplicate job")]
    [InlineData("jobs.msg.selectJobFirst", "zh", "请先选择任务。")]
    [InlineData("jobs.msg.selectJobFirst", "en", "Select a job first.")]
    [InlineData("jobs.msg.title.pipelineDone", "zh", "流水线完成")]
    [InlineData("jobs.msg.title.pipelineDone", "en", "Pipeline complete")]
    [InlineData("common.msg.title.copied", "zh", "已复制")]
    [InlineData("common.msg.title.saved", "en", "Saved")]
    [InlineData("dashboard.msg.title.batchSendTg", "zh", "批量发送 TG")]
    [InlineData("dashboard.msg.noTgPreviewJobs", "en", "No TG pending jobs to preview.")]
    [InlineData("ops.msg.title.bulkArchivePublished", "zh", "批量归档已发布")]
    [InlineData("ops.msg.noAutoMergeDuplicates", "en", "No duplicate jobs to auto-merge.")]
    public void Get_returns_message_dialog_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Fact]
    public void Get_returns_settings_shortcuts_helpText_for_both_languages()
    {
        var zh = UiStringTable.Get("settings.shortcuts.helpText", "zh");
        var en = UiStringTable.Get("settings.shortcuts.helpText", "en");

        Assert.Contains("Ctrl+K  命令面板", zh);
        Assert.Contains("Ctrl+Alt+Enter  执行当前向导步骤（任务工作台）", zh);
        Assert.Contains("Ctrl+K  Command palette", en);
        Assert.Contains("Ctrl+Alt+Enter  Run current wizard step (Jobs)", en);
    }

    [Theory]
    [InlineData("feedback.versionUpdate", "zh", "版本更新")]
    [InlineData("feedback.versionUpdate", "en", "Update available")]
    [InlineData("feedback.fillLinks", "zh", "回填链接")]
    [InlineData("feedback.executionComplete", "en", "Completed")]
    [InlineData("feedback.exportConfigFailed", "zh", "导出配置失败")]
    [InlineData("feedback.dashboardSnapshot", "en", "Dashboard snapshot")]
    [InlineData("shell.versionUpdate.message", "zh", "发现新版本 {0}（当前 {1}）")]
    [InlineData("shell.versionUpdate.download", "en", "Download: {0}")]
    [InlineData("shell.fillLinks.message", "zh", "请在工作台回填发布链接后按 Ctrl+S 保存。")]
    [InlineData("shell.titleBar.jobCount", "en", "{0} jobs")]
    [InlineData("shell.commandPalette.title", "zh", "命令面板 (Ctrl+K)")]
    [InlineData("shell.commandPalette.filterPlaceholder", "en", "Filter commands…")]
    [InlineData("shell.help.title", "zh", "快捷键")]
    [InlineData("shell.welcome.startButton", "en", "Get started")]
    [InlineData("shell.commandPalette.noResults", "zh", "无匹配命令，请换个关键词试试。")]
    [InlineData("shell.commandPalette.noResults", "en", "No matching commands—try another keyword.")]
    [InlineData("shell.palette.navOps.title", "zh", "跳转运维中心")]
    [InlineData("shell.palette.exportFilteredCsv.desc", "en", "Export job list CSV by current filters")]
    [InlineData("shell.palette.batchTagsHint", "zh", "请先在任务列表多选至少 2 个任务，并在标签框填写要追加的标签。")]
    public void Get_returns_shell_and_feedback_keys(string key, string lang, string expected)
    {
        Assert.Equal(expected, UiStringTable.Get(key, lang));
    }

    [Fact]
    public void Get_returns_shell_help_text_with_batch_shortcuts_for_both_languages()
    {
        var zh = UiStringTable.Get("shell.help.text", "zh");
        var en = UiStringTable.Get("shell.help.text", "en");

        Assert.Contains("Ctrl+Shift+?  导出批量统计 CSV", zh);
        Assert.Contains("Ctrl+Alt+Enter  执行当前向导步骤", zh);
        Assert.Contains("Ctrl+Shift+?  Export batch stats CSV", en);
        Assert.Contains("Ctrl+Alt+Enter  Run current wizard step", en);
    }

    [Fact]
    public void Get_returns_shell_welcome_body_with_version_placeholder()
    {
        var zh = UiStringTable.Get("shell.welcome.body", "zh");
        var en = UiStringTable.Get("shell.welcome.body", "en");

        Assert.Contains("PLCPak v{0} WinUI", zh);
        Assert.Contains("Recommended workflow:", en);
        Assert.Contains("Ctrl+Shift+O  导出批量统计 JSON+CSV", zh);
    }
}
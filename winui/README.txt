PLCPak v1.0.74 WinUI（.NET 10 + Windows App SDK 2.2.0）
=======================================================

全量 C# 重写，保留 v2.0 全部功能，不再依赖 PowerShell 主程序。

版本号说明：自 v1.0.11 起采用语义化版本（major.minor.patch），承接原 v10.10 功能基线；
历史 v4.x～v10.x 变更记录仍保留于下文供查阅。
发行版更新日志（Markdown，v10.8.0 起）：../releases/CHANGELOG.md

v1.0.74 向导统一与看板精简（WinUI）
------------------------------------------------------------------------
GUI：
  1. 「发布向导」导航/Ctrl+4 统一进入任务工作台内嵌向导，不再打开独立向导页
  2. 启动恢复「向导」页时自动落点任务工作台并聚焦向导区
  3. 简易模式下看板隐藏「智能下一步」卡片，避免与底部全局引导栏重复
  4. 全局建议跳转「向导」时一律走任务内嵌向导
  5. 修复启动崩溃：主题在 App 构造函数 InitializeComponent 之前从 ui-prefs 读取并设置 RequestedTheme
  6. 运行时切换主题改走主窗口 RootGrid，避免 OnLaunched 后设置 Application.RequestedTheme 触发 COM 异常

版本：
  1. AppVersion 1.0.74；VersionCheck 远程示例 1.0.75

v1.0.73 简易模式流程简化（WinUI）
------------------------------------------------------------------------
GUI：
  1. 简易模式顶部导航常驻「快速处理」，拖文件即可压缩，无需切换专业模式
  2. 启动时不再误恢复向导/运维页；默认落点任务工作台
  3. 全局建议「向导」在简易模式下直达任务工作台内嵌向导
  4. Ctrl+K 命令面板在简易模式仅保留 8 项常用操作（导航、流水线、设置等）
  5. 新手引导文案与真实简易流程对齐（快速处理 + 任务工作台 + 看板）
  6. 快速处理页移除重复设置按钮（统一用顶栏设置）

版本：
  1. AppVersion 1.0.73；VersionCheck 远程示例 1.0.74

v1.0.72 修复新手引导与暗色主题（WinUI）
------------------------------------------------------------------------
GUI：
  1. 引导卡片：动画失败时强制 Opacity=1，避免部分机器整块不可见（含跳过/下一步按钮）
  2. 暗色主题：Plc* 画刷改为 ThemeDictionaries + RequestedTheme，卡片/标题/正文随主题实时切换
  3. 修复暗色模式下快速处理等页面白卡片、文字不可见的问题
  4. 引导底栏增加 Esc 跳过提示；跳过/下一步按钮设置最小尺寸

版本：
  1. AppVersion 1.0.72；VersionCheck 远程示例 1.0.73

v1.0.71 修复命令面板崩溃（WinUI）
------------------------------------------------------------------------
GUI：
  1. 命令面板 DataTemplate 补充 xmlns:x 声明，修复打开 Ctrl+K 时 XamlParseException（undeclared prefix）

版本：
  1. AppVersion 1.0.71；VersionCheck 远程示例 1.0.72

v1.0.70 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 快速处理：刷新时源列表计数显示「统计源文件…」
  2. 运维中心：刷新时陈旧任务区显示加载提示并暂隐列表
  3. 命令面板：筛选时用 accent 高亮匹配关键词
  4. 全局引导栏：页面刷新时显示「全局建议更新中…」并暂禁按钮

版本：
  1. AppVersion 1.0.70；VersionCheck 远程示例 1.0.71

v1.0.69 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 任务工作台：刷新时向导步骤区显示「步骤更新中…」并暂隐步骤/标签页
  2. 发布看板：刷新时任务统计区显示加载提示并暂禁筛选按钮
  3. 命令面板：筛选无结果时显示「无匹配命令」提示
  4. 命令面板：轻量操作反馈改用 1.5 秒短 InfoBar 自动关闭

版本：
  1. AppVersion 1.0.69；VersionCheck 远程示例 1.0.70

v1.0.68 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 发布向导：刷新时步骤区显示「步骤更新中…」并暂隐列表、禁用执行/自动执行
  2. 运维中心：有可归档任务时高亮提示区；按钮显示「批量归档已发布 (N)」
  3. 运维中心：重复合并建议标题旁显示「N 条合并建议」计数
  4. 全局 InfoBar：Success 3 秒、Informational 2.5 秒自动关闭；Warning/Error 保持

版本：
  1. AppVersion 1.0.68；VersionCheck 远程示例 1.0.69

v1.0.67 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 发布看板：刷新时「智能下一步」卡片显示加载提示并暂禁操作
  2. 任务工作台：inbox/密码库拖放高亮；导入后短暂成功提示
  3. 任务工作台：刷新时「下一步建议」卡片显示加载提示并暂禁
  4. 运维中心：活动日志关键词输入时显示「正在筛选…」进度指示

版本：
  1. AppVersion 1.0.67；VersionCheck 远程示例 1.0.68

v1.0.66 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 快速处理：源列表/广告样本拖放区高亮反馈；添加后显示短暂成功提示
  2. 发布看板：刷新时最近任务与健康问题显示加载提示并暂禁列表
  3. 发布看板：最近任务标题旁显示记录数
  4. 运维中心：重复扫描标题旁显示「N 组重复」计数

版本：
  1. AppVersion 1.0.66；VersionCheck 远程示例 1.0.67

v1.0.65 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 运维中心：活动日志筛选激活时显示摘要 + 「清空筛选」（对齐任务页）
  2. 发布看板：刷新时 TG/发布队列显示「队列更新中…」并暂禁列表
  3. 命令面板：显示匹配命令数；列表首项按 ↑ 回到筛选框
  4. 发布向导：任务下拉旁显示「共 N 个任务」；陈旧任务区显示计数

版本：
  1. AppVersion 1.0.65；VersionCheck 远程示例 1.0.66

v1.0.64 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 发布看板：刷新时指标卡显示「…」占位并禁用点击，附「指标更新中…」提示
  2. 命令面板：↑↓ 选择、Enter 执行；打开时自动聚焦筛选框
  3. 全局工作流引导栏：执行/打开任务按钮显示动作与任务名；有待办时 Pro 模式也高亮边框
  4. 机器配置/快捷键配置克隆时同步 RecentCommandPaletteIds

版本：
  1. AppVersion 1.0.64；VersionCheck 远程示例 1.0.65

v1.0.63 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 命令面板：记录最近 5 条命令，无筛选时置顶「最近使用」
  2. 任务工作台：筛选激活时显示摘要（搜索/状态/排序/标签）
  3. 有筛选时列表上方显示「清空筛选」按钮（不限于空结果）

版本：
  1. AppVersion 1.0.63；VersionCheck 远程示例 1.0.64

v1.0.62 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. F5/Ctrl+R：刷新成功时依赖页面进度条，不再弹「刷新完成」；忙碌时提示「正在刷新…」
  2. 活动日志：「已显示 N 条」计数；加载更多时 ProgressRing 指示
  3. 发布向导：无步骤时隐藏步骤列表，仅显示空状态提示

版本：
  1. AppVersion 1.0.62；VersionCheck 远程示例 1.0.63

v1.0.61 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 快速处理：刷新指示、源列表计数与空状态提示
  2. 发布向导：步骤完成进度条、刷新指示、无任务/未选任务空状态
  3. 运维活动日志：筛选结果「匹配 N 条记录」高亮徽章

版本：
  1. AppVersion 1.0.61；VersionCheck 远程示例 1.0.62

v1.0.60 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 运维中心刷新时顶部进度条与「正在刷新运维中心…」
  2. 任务列表刷新指示；显示「共 N 个任务」或「显示 X / Y 个任务」
  3. 多选任务时显示「已选 N 个任务」提示
  4. 运维重复扫描空状态；重复组摘要双语本地化

版本：
  1. AppVersion 1.0.60；VersionCheck 远程示例 1.0.61

v1.0.59 UI/UX 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 设置页改为 Expander 分组（外观/工作区/任务/快捷键/集成/夜间/高级）
  2. 设置内「重新播放新手引导」；首次 onboarding 时跳过欢迎对话框
  3. 顶栏新增命令面板按钮（Ctrl+K）；帮助按钮文案与快捷键提示
  4. 全局忙碌遮罩显示流水线进度条；InfoBar 错误/警告不自动关闭
  5. 看板刷新时顶部显示不确定进度条与「正在刷新看板…」

版本：
  1. AppVersion 1.0.59；VersionCheck 远程示例 1.0.60

v1.0.58 UI/UX 优化（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 任务详情/筛选空状态与「清空筛选」；筛选条件记忆 LastJobFilter
  2. 向导 Tab 始终有标题；简易模式隐藏竖向步骤列表
  3. 导航当前页高亮 + 简易模式推荐页淡色提示
  4. 快速处理消息改 InfoBar；看板最近任务/健康问题空状态
  5. 页面切换动画优化；导航预加载减少重复刷新

版本：
  1. AppVersion 1.0.58；VersionCheck 远程示例 1.0.59

v1.0.57 稳定版（性能 UX + 崩溃修复，WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 性能：列表未变时跳过重建；日志轮询去重；减少动画设置
  2. 崩溃修复：任务列表 Extended 模式仅用 SelectedItems 同步；页面可见后再刷新/选中；切换工作台延迟 RefreshJobs
  3. 简易模式仅显示竖向向导步骤；状态与发布摘要双语
  4. 启动日志记录 HResult 与调度堆栈

版本：
  1. AppVersion 1.0.57；VersionCheck 远程示例 1.0.58

v1.0.56 性能 UX 与状态双语（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 任务列表增量同步（筛选/排序时避免 Clear+全量重建，保留选中与滚动位置）
  2. 任务日志轮询去重：文本未变时不刷新绑定，减少滚动抖动
  3. 向导步骤列表结构未变时仅就地更新，减少闪烁
  4. 设置新增「减少动画」：跳过页面切换淡入淡出与向导步骤动效（保留完成音效）
  5. 语言切换时刷新任务列表/看板最近任务的状态与发布摘要显示

Core：
  1. 任务状态与发布渠道摘要双语（job.status.* / publish.channel.*）；UiDisplayContext 与 LocalizationService 同步
  2. UiPreferences.ReduceMotion

版本：
  1. AppVersion 1.0.56；VersionCheck 远程示例 1.0.57

v1.0.55 壳层本地化、消息双语与简易模式打磨（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. MainWindow 壳层全面本地化：导航、InfoBar、命令面板、帮助/欢迎对话框（shell.* / feedback.* 110+ 项）
  2. ViewModel 消息对话框标题与静态文案双语（Jobs/Dashboard/Operations ~195 处；jobs.msg.* / common.msg.*）
  3. 简易模式：隐藏快速处理/发布向导/运维导航；引导条隐藏冗余「去推荐页」；可关闭顶部提示；无待办时弱化引导条

Core：
  1. UiPreferences.HasDismissedSimpleModeHint

版本：
  1. AppVersion 1.0.55；VersionCheck 远程示例 1.0.56

v1.0.54 快速处理/设置本地化与看板筛选聚焦（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 快速处理页（MainPage）全面中英双语（quick.page.* 81 项）；状态/进度文案随语言刷新
  2. 设置对话框剩余控件全面本地化（settings.* 73+ 项）；语言预览实时更新标签
  3. 简易模式：看板指标（失败/待发布/归档/全部）跳转后自动选中筛选首条任务并聚焦向导

Core：
  1. UiStringTable quick.page.* / settings.general|jobs|shortcuts|nightly|activityLog.*

版本：
  1. AppVersion 1.0.54；VersionCheck 远程示例 1.0.55

v1.0.53 详情区/运维页本地化与新建聚焦（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 任务页右侧详情区全面中英双语（英雄卡片、密码/inbox、向导 Tab、日志等 63 项）
  2. 运维中心页面全面中英双语（ops.page.* 70 项）；语言切换实时刷新
  3. 新建任务 / 从帖子创建 / 导入 JSON 成功后自动滚到分步向导

Core：
  1. UiStringTable jobs.detail.* / ops.page.*

版本：
  1. AppVersion 1.0.53；VersionCheck 远程示例 1.0.54

v1.0.52 侧栏本地化、运维聚焦与完成反馈（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 任务页左侧栏全面中英双语（新建任务、搜索筛选、批量/导出按钮等 36 项）
  2. 简易模式：运维中心跳转任务后聚焦分步向导（与看板/引导条一致）
  3. 向导步骤完成时播放系统音效 + 可选触觉反馈；设置中可关闭

Core：
  1. UiPreferences.EnableWizardCompletionFeedback（默认开启）
  2. UiStringTable jobs.sidebar.* / settings.wizardFeedback

版本：
  1. AppVersion 1.0.52；VersionCheck 远程示例 1.0.53

v1.0.51 向导页本地化与导航聚焦（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 发布向导页（WizardPage）全面中英双语；语言切换实时刷新
  2. 任务页剩余文案本地化：下一步卡片、空列表、任务日志、专业快捷键行
  3. 简易模式：全局引导条「打开任务」跳转后聚焦向导；发布向导页跳转同样聚焦
  4. 发布向导页步骤列表自动滚到当前高亮步骤

Core：
  1. UiStringTable 扩展 wizard.page.* / jobs.nextAction.* / jobs.proShortcuts.*

版本：
  1. AppVersion 1.0.51；VersionCheck 远程示例 1.0.52

v1.0.50 向导 UX 三连优化（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 简易模式选中任务后，「更多设置」Expander 默认折叠，突出分步向导
  2. 看板点击任务跳转后延迟聚焦向导区（focusWizard 参数，仅 Dashboard 来源）
  3. 向导步骤完成时：进度条 + 向导卡片 + 已完成步骤按钮脉冲动画（链式 Storyboard）

Core：
  1. UiStringTable jobs.collapse.moreSettings

版本：
  1. AppVersion 1.0.50；VersionCheck 远程示例 1.0.51

v1.0.49 向导键盘快捷键与提示本地化（WinUI + Core）
------------------------------------------------------------------------
GUI：
  1. 任务页向导：Ctrl+Alt+←/→ 上一步/下一步，Ctrl+Alt+Enter 执行当前步骤
  2. 简易/专业模式显示向导快捷键提示；专业模式显示 jobs.wizard.hint
  3. 素材准备、文案生成 Tab 提示文案中英双语

Core：
  1. UiStringTable 扩展 jobs.wizard.shortcuts / wizard.step.*.hint

版本：
  1. AppVersion 1.0.49；VersionCheck 远程示例 1.0.50

v1.0.48 向导步骤自动滚动（WinUI）
------------------------------------------------------------------------
GUI：
  1. 切换向导步骤（上一步/下一步/点击步骤/TabView）时，右侧详情区自动滚到向导区域
  2. 选中任务时滚到当前步骤；执行步骤推进后随 Tab 切换同步滚动
  3. 双帧延迟布局 + BringIntoView，合并同帧多次滚动请求

版本：
  1. AppVersion 1.0.48；VersionCheck 远程示例 1.0.49

v1.0.47 任务页面分步向导（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 任务工作台嵌入「分步发布向导」：进度条、横向步骤条、纵向步骤列表（简易模式）
  2. WizardSteps 由 PublishWizardTabService 状态驱动；步骤可点击切换 TabView
  3. 执行当前步骤 / 上一步 / 下一步；TabView 与 SelectedWizardTabIndex 双向同步
  4. 简易模式突出向导；专业模式保留 Tab 标题 + 紧凑步骤条
  5. 工作台标题与简易提示本地化（jobs.workbench / jobs.simpleHint）

Core：
  1. UiStringTable 扩展 jobs.wizard.* / wizard.step.* / wizard.status.*

版本：
  1. AppVersion 1.0.47；VersionCheck 远程示例 1.0.48

v1.0.46 用户体验 (UX) 增强（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. GlobalInfoBar：成功/信息 4 秒自动关闭；错误保持至手动关闭
  2. 看板空状态 CTA：待发布队列空→「去任务工作台」；TG 空→提示+刷新
  3. 指标卡 Tooltip（metric.tip.*）与 PointerOver/Pressed 视觉反馈
  4. 顶栏快览可点击→打开发布看板并刷新
  5. 导航切换内容区 150ms 淡入淡出（链式 Storyboard，避免同属性冲突）
  6. 设置语言实时预览；取消时还原
  7. Esc 关闭 InfoBar；Esc 跳过引导
  8. 简易模式有下一步时引导条 PlcAccentBrush 2px 边框强调

Core：
  1. UiStringTable 扩展 metric.tip / feedback / dashboard.goToJobs / tooltip.quickStats

版本：
  1. AppVersion 1.0.46；VersionCheck 远程示例 1.0.47

v1.0.45 看板/UI 本地化与主题优化（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 看板指标卡片可点击筛选（总任务/待发布/失败/TG/健康）
  2. 专业模式按钮与空队列提示全面本地化（中/英）
  3. 导航按钮 Tooltip、全局忙碌文案、引导条无待办提示本地化
  4. 暗色/亮色标题栏与导航高亮随主题刷新；设置内主题可预览、取消还原
  5. PlcNavHighlightBrush / PlcTitleBarTextBrush 主题资源

Core：
  1. UiStringTable 扩展 dashboard/tooltip/busy/guide 键与 GetNavLabel()

版本：
  1. AppVersion 1.0.45；VersionCheck 远程示例 1.0.46

v1.0.44 修复首次引导动画步骤切换崩溃（WinUI）
------------------------------------------------------------------------
GUI：
  1. OnboardingOverlay 步骤切换改为先淡出再淡入，避免同一 Storyboard 重复动画 Opacity

版本：
  1. AppVersion 1.0.44；VersionCheck 远程示例 1.0.45

v1.0.43 修复启动初始化失败（WinUI）
------------------------------------------------------------------------
GUI：
  1. 主题/语言加载从 App 构造函数移至 OnLaunched，避免访问 Resources 时 COM 异常

版本：
  1. AppVersion 1.0.43；VersionCheck 远程示例 1.0.44

v1.0.42 看板卡片化、引导动画、暗色主题与中英文（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 发布看板指标卡片（总任务/待发布/失败/TG/健康问题）与双列队列/TG 卡片布局
  2. 首次启动动画引导（4 步淡入切换，可跳过）
  3. 暗色主题：设置切换，运行时刷新 Plc* 画刷
  4. 中英文界面：设置切换，导航/看板/引导条等关键文案本地化

Core：
  1. UiStringTable — 中英文字典；UiPreferences.AppTheme / UiLanguage / HasSeenOnboarding

版本：
  1. AppVersion 1.0.42；VersionCheck 远程示例 1.0.43

v1.0.41 全局工作流引导与导航智能推荐（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 底部「全局下一步」引导条：摘要 / 去推荐页 / 打开任务 / 一键执行
  2. 导航角标：任务工作台(待办)、发布看板(TG)、运维中心(陈旧)
  3. 简易模式下推荐页面高亮；Ctrl+Shift+P 切换简易/专业
  4. 任务工作台简易模式显示「当前步骤」发布向导提示

Core：
  1. WorkflowGuideService — 聚合首要待办、推荐页面与角标计数

版本：
  1. AppVersion 1.0.41；VersionCheck 远程示例 1.0.42

v1.0.40 简易/专业模式与全站 UI/UX 更迭（WinUI + Core + Tests）
------------------------------------------------------------------------
GUI：
  1. 顶栏「简易 | 专业」ToggleSwitch；UiPreferences.ProfessionalMode 持久化
  2. 默认简易模式：各页突出「下一步」英雄卡片，隐藏批量导出/日志统计等高级区
  3. 专业模式：恢复 v1.0.39 完整功能（运维活动日志、重复扫描、任务批量操作等）
  4. 全站统一 PlcPageTitle / PlcHeroCard / PlcContentCard / PlcPrimaryButton 样式
  5. 设置页可勾选专业模式；机器配置/快捷键配置导入导出含 ProfessionalMode

Core：
  1. UiModeService — 全局模式切换与 ui-prefs.json 同步

版本：
  1. AppVersion 1.0.40；VersionCheck 远程示例 1.0.41

v1.0.39 运维中心 UI/UX 优化（WinUI）
------------------------------------------------------------------------
GUI：
  1. 运维分区卡片化展示；任务概览高亮；完整摘要可折叠
  2. 活动日志 Expander + 筛选摘要/清空/天数说明；陈旧任务空状态
  3. 底部操作分组（常用运维 / 备份 / 日志维护）；顶栏刷新与快捷键提示
  4. 主导航 ToolTip 显示 Ctrl+1~5；新增 PlcCardBackgroundBrush 等主题色

版本：
  1. AppVersion 1.0.39；VersionCheck 远程示例 1.0.40

v1.0.38 运维报告分区输出与夜间 readme 说明（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. OperationsCenterService.FormatSectionLines — 统一格式化 Sections 文本行
  2. 夜间 readme 注明运维报告 JSON 含 activity-log-stats / activity-log-batch

GUI：
  1. 导出运维报告消息显示摘要与各分区行

CLI：
  1. -JobExportOperationsReport 文本模式输出全部分区（与 -JobOperationsCenter 一致）

版本：
  1. AppVersion 1.0.38；VersionCheck 远程示例 1.0.39

v1.0.37 机器配置运维快照持久化与导入联动（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. MachineProfileBundle 写入 OperationsCenterSummary；预览/导出结果贯通
  2. 导入机器配置成功后刷新运维快照缓存

GUI：
  1. 导入确认框显示导出时运维快照
  2. 导入成功后同步活动日志天数筛选与快捷键提示

CLI：
  1. -JobExportMachineProfile / -JobPreviewMachineProfile 文本模式输出运维快照行

版本：
  1. AppVersion 1.0.37；VersionCheck 远程示例 1.0.38

v1.0.36 机器配置运维快照摘要与设置保存刷新（Core + WinUI + Tests）
------------------------------------------------------------------------
Core API：
  1. MachineProfileExportResult 含 OperationsCenterSummary；导出摘要附带运维快照

GUI：
  1. 保存设置后自动刷新运维中心（同步天数筛选与概览摘要）
  2. 移除运维概览批量摘要重复拼接

版本：
  1. AppVersion 1.0.36；VersionCheck 远程示例 1.0.37

v1.0.35 运维快照活动日志统计与缓存刷新（Core + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. OperationsCenterSnapshot 含 ActivityLogStatsSummary 与 activity-log-stats 分区
  2. 运维快照缓存纳入活动日志文件指纹，写入日志后自动刷新批量/分类统计

CLI：
  1. -JobOperationsCenter 文本模式输出活动日志统计分区（有记录时）

版本：
  1. AppVersion 1.0.35；VersionCheck 远程示例 1.0.36

v1.0.34 空天数筛选回退保留天数与运维分区（Core + WinUI + Tests）
------------------------------------------------------------------------
Core API：
  1. 运维快照 Sections 增加 activity-log-batch 分区（有批量记录时）
  2. GlobalShortcutKeyHelper 修复 Ctrl+Shift+? 快捷键 VirtualKey 映射

GUI：
  1. 天数筛选留空时按 ActivityLogKeepDays 筛选（与占位提示一致）
  2. 空状态提示显示「默认保留 N 天」

版本：
  1. AppVersion 1.0.34；VersionCheck 远程示例 1.0.35

v1.0.33 活动日志统计 SinceDays 与运维快照批量摘要（Core + WinUI + Tests）
------------------------------------------------------------------------
Core API：
  1. ActivityLogStatsResult/JSON/CSV/HTML 导出结果含 SinceDays；摘要统一「最近 N 天」
  2. OperationsCenterSnapshot 含 ActivityLogBatchSummary；快照与 JSON 报告自动填充
  3. JobRunner 运维快照按 ActivityLogKeepDays 附带批量操作统计摘要

GUI：
  1. 运维中心概览摘要显示批量操作统计（有记录时）

版本：
  1. AppVersion 1.0.33；VersionCheck 远程示例 1.0.34

v1.0.32 保存设置后同步活动日志天数筛选（WinUI）
------------------------------------------------------------------------
GUI：
  1. 修改「活动日志保留天数」并保存后，若天数筛选仍为旧默认值则自动同步
  2. 天数输入框占位提示「空=默认保留天数」

版本：
  1. AppVersion 1.0.32；VersionCheck 远程示例 1.0.33

v1.0.31 批量统计 SinceDays 贯通与运维默认天数（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. ActivityLogBatchSummaryResult/导出结果含 SinceDays；摘要统一显示「最近 N 天」
  2. NightlyAutomationExport 含 ActivityLogExportSinceDays、批量导出标记与增强 SummaryText

GUI：
  1. 运维中心首次加载默认填入 ActivityLogKeepDays 作为天数筛选
  2. 夜间脚本导出消息使用 SummaryText

CLI：
  1. -JobExportNightlyAutomation 文本模式先输出 SummaryText

版本：
  1. AppVersion 1.0.31；VersionCheck 远程示例 1.0.32

v1.0.30 设置夜间双导出联动与导入预览增强（WinUI + Tests）
------------------------------------------------------------------------
GUI：
  1. 勾选「同时导出 CSV 与 HTML」自动开启统计 CSV/HTML 与批量 JSON+CSV
  2. 机器配置导入确认框显示夜间批量统计与 -SinceDays 提示

版本：
  1. AppVersion 1.0.30；VersionCheck 远程示例 1.0.31

v1.0.29 机器配置夜间摘要与 bundle SinceDays（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. MachineProfile 导出/预览携带夜间批量统计与 ActivityLogKeepDays 摘要
  2. ActivityLogBatchBundleExportResult.SinceDays；ExportAll 摘要含天数
  3. 夜间 readme 头部显示活动日志导出筛选天数

GUI：
  1. 保存设置后运维中心快捷键提示即时刷新
  2. 机器配置导出消息使用 SummaryText

版本：
  1. AppVersion 1.0.29；VersionCheck 远程示例 1.0.30

v1.0.28 夜间批量统计独立开关与动态快捷键提示（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. StudioConfig.NightlyExportActivityLogBatchStatsAll — 可独立于活动日志统计开启夜间批量导出

GUI：
  1. 设置页「夜间全流程脚本自动导出批量操作统计 JSON+CSV」
  2. 运维中心批量快捷键提示随自定义映射动态更新

版本：
  1. AppVersion 1.0.28；VersionCheck 远程示例 1.0.29

v1.0.27 批量 bundle 同步时间戳与运维快捷键说明（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. ExportAll 为 JSON/CSV 使用同一 BundleStamp 文件名后缀
  2. ActivityLogBatchBundleExportResult.BundleStamp 字段
  3. 夜间 readme 步骤说明同步显示 -SinceDays
  4. HTML 批量操作区块显示「筛选最近 N 天」

GUI：
  1. 欢迎/帮助对话框补全 Ctrl+Shift+B/J/?/O 批量快捷键
  2. 运维中心批量导出区显示快捷键提示行

版本：
  1. AppVersion 1.0.27；VersionCheck 远程示例 1.0.28

v1.0.26 夜间 SinceDays 联动与批量一键导出快捷键（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. 夜间脚本统计/批量导出自动附加 -SinceDays（取自 ActivityLogKeepDays）
  2. GlobalShortcutRegistry：Ctrl+Shift+O 导出批量 JSON+CSV

GUI：
  1. 设置页可禁用 Ctrl+Shift+O 批量一键导出快捷键

版本：
  1. AppVersion 1.0.26；VersionCheck 远程示例 1.0.27

v1.0.25 夜间批量一键导出、HTML 天数标注与 CSV 快捷键（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. 夜间脚本启用统计导出时改用 -JobExportActivityLogBatchStatsAll（替代分步 JSON+CSV）
  2. ActivityLogStatsHtmlExportService 在报告 meta 中显示「筛选最近 N 天」
  3. GlobalShortcutRegistry：Ctrl+Shift+? 导出批量操作统计 CSV

GUI：
  1. 设置页可禁用 Ctrl+Shift+? 批量 CSV 快捷键
  2. HTML 报告与运维中心 SinceDays 筛选联动显示

版本：
  1. AppVersion 1.0.25；VersionCheck 远程示例 1.0.26

v1.0.24 批量统计 JSON+CSV 一键导出（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. ActivityLogBatchSummaryExportService.ExportAll — 同时写入 JSON 与 CSV
  2. JobRunner.ExportActivityLogBatchStatsAll 复用 ExportAll 并记录活动日志

GUI：
  1. 运维中心「导出批量 JSON+CSV」主按钮；命令面板新增一键导出项
  2. -SinceDays 与运维中心活动日志天数筛选联动

CLI：
  1. -JobExportActivityLogBatchStatsAll [-SinceDays <N>] [-Json]

版本：
  1. AppVersion 1.0.24；VersionCheck 远程示例 1.0.25

v1.0.23 批量操作快捷键与夜间 CSV 导出（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. GlobalShortcutRegistry：Ctrl+Shift+B 筛选批量日志、Ctrl+Shift+J 导出批量 JSON
  2. 夜间脚本在统计导出启用时追加 -JobExportActivityLogBatchStatsCsv

GUI：
  1. 设置页可单独禁用 Ctrl+Shift+B / Ctrl+Shift+J
  2. 运维中心快捷键说明已更新

版本：
  1. AppVersion 1.0.23；VersionCheck 远程示例 1.0.24

v1.0.22 批量统计 CSV、夜间 JSON 导出与条带点击筛选（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. ActivityLogBatchSummaryCsvExportService — 批量操作统计 CSV 导出
  2. 夜间脚本在启用统计 CSV/HTML/双导出时自动 -JobExportActivityLogBatchStats

GUI：
  1. 批量操作条带行可点击，快速筛选对应分类 +「批量」关键词
  2. 导出批量 JSON / CSV 双按钮；命令面板新增批量筛选与导出

CLI：
  1. -JobExportActivityLogBatchStatsCsv [-SinceDays <N>] [-Path path.csv] [-Json]

版本：
  1. AppVersion 1.0.22；VersionCheck 远程示例 1.0.23

v1.0.21 批量统计 CLI/JSON 导出、HTML 柱状图与夜间 CSV+HTML 双导出（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. -JobActivityLogBatchStats / ActivityLogBatchSummaryExportService 批量统计查询与 JSON 导出
  2. ActivityLogStatsHtmlExportService 批量操作 SVG 柱状图（batch-chart）
  3. StudioConfig.NightlyExportActivityLogStatsBoth 夜间脚本一键 CSV+HTML

GUI：
  1. 运维中心批量操作分类条带列表 +「导出批量操作统计 JSON」
  2. 设置页「同时导出活动日志统计 CSV 与 HTML」

CLI：
  1. -JobActivityLogBatchStats [-SinceDays <N>] [-Json]
  2. -JobExportActivityLogBatchStats [-SinceDays <N>] [-Path path.json] [-Json]

版本：
  1. AppVersion 1.0.21；VersionCheck 远程示例 1.0.22

v1.0.20 导入预览校验、HTML 批量统计区块与运维批量摘要（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. ActivityLogBatchSummaryService — 按分类统计含「批量」关键词的活动日志
  2. ActivityLogStatsHtmlExportService 增加「批量操作统计」表格区块
  3. JobRunner.ImportMachineProfile 导入前自动执行 PreviewMachineProfile 校验

GUI：
  1. 运维中心活动日志区显示批量操作统计摘要行

CLI：
  1. -JobImportMachineProfile 在冲突时自动拒绝（与 Preview 一致）

版本：
  1. AppVersion 1.0.20；VersionCheck 远程示例 1.0.21

v1.0.19 活动日志批量筛选、搜索 CSV 导出与机器配置 HTML 主题（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. ActivityLogCsvExportService 支持 -Query 关键词筛选
  2. ActivityLogBatchFilterService 批量操作快捷筛选常量
  3. MachineProfileExportResult.HtmlReportTheme 导出时携带主题信息

GUI：
  1. 运维中心「批量归档/删除/标签/恢复」快捷筛选按钮
  2. 导出 CSV 自动应用当前搜索关键词与分类筛选

CLI：
  1. -JobExportActivityLogCsv 增加 -Query 参数

版本：
  1. AppVersion 1.0.19；VersionCheck 远程示例 1.0.20

v1.0.18 机器配置导入预览与冲突检测（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. MachineProfileService.PreviewMachineProfile — 导入前预览 studio/UI/快捷键
  2. 预览检测快捷键冲突并报告 HtmlReportTheme

GUI：
  1. 导入机器配置前弹出预览确认对话框

CLI：
  1. -JobPreviewMachineProfile -Path path.json [-Merge] [-Replace] [-Json]

版本：
  1. AppVersion 1.0.18；VersionCheck 远程示例 1.0.19

v1.0.17 夜间脚本自动导出 HTML 统计报告（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. StudioConfig.NightlyExportActivityLogStatsHtml
  2. NightlyAutomationScriptService 增加 -JobExportActivityLogStatsHtml 步骤（主题跟随 UI 偏好）

GUI：
  1. 设置页「夜间全流程脚本自动导出活动日志统计 HTML」

CLI：
  1. nightly-full.bat 可选执行 -JobExportActivityLogStatsHtml

版本：
  1. AppVersion 1.0.17；VersionCheck 远程示例 1.0.18

v1.0.16 机器配置含快捷键、HTML 主题跟随偏好与批量操作日志增强（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. MachineProfileBundle.ShortcutProfileJson — 导出/导入机器配置时一并携带快捷键映射与禁用列表
  2. UiPreferences.HtmlReportTheme — HTML 统计报告默认主题（light/dark）
  3. BatchActivityLogHelper + 批量归档/恢复/删除/标签日志附带任务 ID 样本

GUI：
  1. 设置页「活动日志 HTML 报告默认主题」下拉框
  2. 运维中心导出机器配置提示含快捷键统计

CLI：
  1. -JobExportMachineProfile 输出含快捷键映射/禁用计数

版本：
  1. AppVersion 1.0.16；VersionCheck 远程示例 1.0.17

v1.0.15 批量删除回收站模式、快捷键导入导出与 HTML 暗色主题（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. JobFolderRemovalService + 批量/单条删除支持 useRecycleBin（AdCleanupService 回收站）
  2. GlobalShortcutProfileService.Export / Import（映射+禁用列表 JSON）
  3. ActivityLogStatsHtmlExportService 暗色主题切换（localStorage 记忆）

GUI：
  1. 删除/批量删除对话框三选一：仅删记录 / 永久删目录 / 目录移到回收站
  2. 设置页「导出快捷键」「导入快捷键」（保存前冲突检测）
  3. HTML 报告页「切换暗色主题」按钮

CLI：
  1. -JobDelete / -JobBatchDeleteJobIds 增加 -RecycleBin（需配合 -DeleteFolders）
  2. -JobExportShortcutProfile / -JobImportShortcutProfile

版本：
  1. AppVersion 1.0.15；VersionCheck 远程示例 1.0.16

v1.0.14 多选批量删除预览、HTML 打印导出 PDF 与快捷键冲突检测（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. BatchDeleteJobIdsService.Preview / Delete — JobRunner.PreviewBatchDeleteJobIds / BatchDeleteJobIds
  2. ActivityLogStatsHtmlExportService 增加「打印/导出 PDF」按钮与 @media print 样式
  3. GlobalShortcutConflictService.FindConflicts 保存映射前冲突检测

GUI：
  1. 任务工作台多选 ≥2：「批量删除选中」+ Ctrl+Shift+D（预览后可选仅删记录/删记录+目录）
  2. 设置页自定义快捷键保存时检测冲突并阻止
  3. 命令面板：多选批量删除

CLI：
  1. -JobPreviewBatchDeleteJobIds -JobIds id1,id2 [-Json]
  2. -JobBatchDeleteJobIds -JobIds id1,id2 -Force [-DeleteFolders] [-Json]

版本：
  1. AppVersion 1.0.14；VersionCheck 远程示例 1.0.15

v1.0.13 多选批量恢复、统计 HTML 交互图表与快捷键自定义映射（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. BatchUnarchiveJobIdsService.Unarchive — JobRunner.BatchUnarchiveJobIds(jobIds)
  2. ActivityLogStatsHtmlExportService 增加 SVG 柱状图与悬停 tooltip
  3. GlobalShortcutBindingService + UiPreferences.ShortcutOverrides 快捷键自定义

GUI：
  1. 任务工作台多选 ≥2：「批量恢复选中」+ Ctrl+Shift+U
  2. 设置页「自定义快捷键映射」对话框（覆盖默认组合键）
  3. 命令面板：多选批量恢复

CLI：
  1. -JobBatchUnarchiveJobIds -JobIds id1,id2 [-Json]

版本：
  1. AppVersion 1.0.13；VersionCheck 远程示例 1.0.14

v1.0.12 多选批量归档、统计 HTML 每日图表与单项快捷键禁用（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. BatchArchiveJobIdsService.Archive — JobRunner.BatchArchiveJobIds(jobIds)
  2. ActivityLogStatsHtmlExportService 增加近 7 天每日条数图表区块
  3. GlobalShortcutRegistry + UiPreferences.DisabledShortcuts 单项快捷键禁用

GUI：
  1. 任务工作台多选 ≥2：「批量归档选中」按钮 + Ctrl+Shift+G
  2. 设置页可单独禁用 Ctrl+Shift+A/G/T/H 等快捷键
  3. 命令面板：多选批量归档

CLI：
  1. -JobBatchArchiveJobIds -JobIds id1,id2 [-Json]

版本：
  1. AppVersion 1.0.12；VersionCheck 远程示例 1.0.13

v1.0.11 按任务 ID 批量标签与活动日志统计 HTML 导出（Core + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. BatchTagService.Apply(..., jobIds?) 已有；JobRunner.BatchApplyTags 透传 jobIds
  2. ActivityLogStatsHtmlExportService.Export
     JobRunner.ExportActivityLogStatsHtml(outputPath?, sinceDays?)

CLI：
  1. -JobBatchTagsJobIds -JobIds id1,id2 -Tags "a,b" [-Mode append] [-Json]
  2. -JobExportActivityLogStatsHtml [-SinceDays N] [-Path html] [-Json]

版本：
  1. AppVersion 1.0.11；VersionCheck 远程示例 1.0.12；PrintHelp v1.0.11

v10.10 活动日志归档筛选透传与批量置顶筛选（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------
Core API：
  1. ActivityLogService.ArchiveOlderThan(workspace, keepDays, category?, sinceDays?)
     与 Trim 一致支持分类/近 N 天范围筛选
  2. JobRunner.ArchiveActivityLog 透传 category/sinceDays（GUI 归档使用当前筛选）
  3. BatchPinFilteredService.Preview / BatchPinFilteredJobs(filter, search, tag, pin)
     按任务列表同款筛选批量置顶或取消置顶

GUI：
  1. 任务工作台「批量置顶」「批量取消置顶」按钮
  2. 运维中心活动日志分类统计改用 ActivityLogStatsService.BuildBarItems（Core）

CLI：
  1. -JobArchiveActivityLog [-KeepDays 30] [-Category 分类] [-SinceDays <N>] [-Json]
  2. -JobBatchPinFiltered -Pin true|false -Force [-Filter] [-Search] [-Tag] [-Json]

版本：
  1. AppVersion 10.10.0；VersionCheck 远程示例 10.11.0；358+ 测试全通过

v10.9 全局快捷键开关与活动日志每日统计 CSV 定稿（Core + WinUI + CLI + Tests）
------------------------------------------------------------------------------
Core API：
  1. UiPreferences.DisableGlobalShortcuts（bool，Studio 无关，存 ui-prefs.json）
  2. ActivityLogDailyStatsService.GetDailyCounts / ExportCsv
     JobRunner.ExportActivityLogDailyStatsCsv(days=7)
     按日历日聚合条数（近 N 天补零），CSV 列 date,count

GUI：
  1. 设置页「禁用全局快捷键」CheckBox
  2. MainWindow RootGrid_KeyDown 开头检查 prefs.DisableGlobalShortcuts
  3. 运维中心「近 7 天」每日条数条带 +「导出每日统计 CSV」按钮

CLI：
  1. -JobExportActivityLogDailyStatsCsv [-Days 7] [-Path csv] [-Json]
     默认 reports/activity-log-daily-stats-*.csv

版本：
  1. AppVersion 10.9.0；VersionCheck 远程示例 10.10.0；测试全通过

v10.8 活动日志日统计与筛选批量归档（Core + WinUI + CLI + README）
------------------------------------------------------------------
Core API：
  1. ActivityLogDailyStatsService.BuildDailyStats / ExportCsv
     按日历日聚合活动日志条数（近 N 天补零；GUI 可选分类筛选）
  2. MaintenanceService.PreviewBatchArchiveFiltered / BatchArchiveFiltered
     按任务列表同款 Filter / Search / Tag / Sort 预览与批量归档
  3. UiPreferences.DisableGlobalShortcuts（bool，存 ui-prefs.json）

GUI：
  1. 运维中心活动日志「近 7 天」每日条数条带 +「导出每日统计 CSV」
  2. 设置页「禁用全局快捷键」CheckBox

CLI：
  1. -JobActivityLogDailyStats [-Days 7] [-Json]
     查询活动日志每日条数；JSON 含 Days / TotalCount / Items（Date、Count）
  2. -JobExportActivityLogDailyStatsCsv [-Days 7] [-Path csv] [-Json]
     导出 date,count CSV；默认 reports/activity-log-daily-stats-*.csv
  3. -JobPreviewBatchArchiveFiltered [-Filter] [-Search] [-Tag] [-Sort] [-Json]
     预览筛选条件下可归档任务（TotalMatched / ArchivableCount / SampleTitles）
  4. -JobBatchArchiveFiltered -Force [-Filter] [-Search] [-Tag] [-Sort] [-Json]
     按筛选批量归档；跳过已归档项；需 -Force 确认
  5. PrintHelp 整理为 v10.8 版本号与说明

版本：
  1. AppVersion 10.8.0

v10.7 最后一轮抛光（Core + WinUI + CLI + Tests + README）
----------------------------------------------------------
Core API：
  1. QuickStatsService.StaleCount 已纳入标题栏快览与运维中心 QuickStatsLine
  2. ActivityLogService.PreviewArchive：归档预览 SummaryText 含分类 + sinceDays 范围提示
  3. ActivityLogService.PreviewTrim 清理预览 SummaryText 同步增强（分类 + 近 N 天）

GUI：
  1. 任务工作台「导出筛选 JSON」按钮（ExportFilteredJobsJsonCommand）
  2. 命令面板新增：导出置顶 CSV、导出筛选 JSON、归档活动日志（归档入口延续 v10.4）

CLI：
  1. PrintHelp 整理为 v10.7 版本号与说明

版本：
  1. AppVersion 10.7.0；VersionCheck 远程示例 10.8.0

v10.6 最近活跃分类与筛选任务 JSON 导出（Core + WinUI + CLI + Tests）
--------------------------------------------------------------------
Core API：
  1. ActivityLogService.GetRecentCategories(workspace, limit=5)
     按各分类最近活动时间倒序返回活跃分类
  2. FilteredJobsJsonExportService / JobRunner.ExportFilteredJobsJson
     按任务列表同款筛选/排序导出完整 PublishJob JSON 数组（非 CSV）
  3. StudioConfig.NightlyExportPinnedJobsCsv
     夜间全流程脚本可选自动导出置顶任务 CSV

GUI：
  1. 运维中心活动日志「最近活跃分类」chips，点击设置 ActivityCategoryFilter
  2. 设置：夜间全流程脚本自动导出置顶任务 CSV CheckBox

CLI：
  1. -JobExportFilteredJobsJson [-Filter active|failed|all|pending|published|processed|archived]
     [-Search 关键词] [-Tag 标签] [-Sort updated|title|pinned|...] [-Limit <N>] [-Path path.json] [-Json]
     参数与 -JobExportFilteredJobsCsv 相同，输出 JSON 任务数组

v10.5 置顶任务 CSV 导出与运维快照 CLI（Core + WinUI + CLI）
------------------------------------------------------------
Core API：
  1. PinnedJobsCsvExportService：仅导出 IsPinned 任务，复用 JobsCsvExportHelper
  2. JobRunner.ExportPinnedJobsCsv(outputPath?)
  3. StudioConfig.ArchiveBeforeTrimActivityLog：Trim 前先 Archive（JobRunner.TrimActivityLog 实现）

GUI：
  1. 任务工作台「导出置顶 CSV」按钮

CLI：
  1. -JobExportPinnedJobsCsv [-Path csv] [-Json]
     仅导出置顶任务 CSV（默认 reports/pinned-jobs-*.csv）
  2. -JobExportOperationsSnapshot [-Json]
     输出 GetOperationsCenterSnapshot 摘要（SummaryText / Sections / QuickStatsLine 等）

v10.4 活动日志归档（GUI + CLI）
-------------------------------
Core API：
  1. ActivityLogService.ArchiveOlderThan(workspace, keepDays)
     将超过保留期的条目写入 reports/activity-archive-{ts}.json，并从主 logs/activity.log 删除
  2. JobRunner.ArchiveActivityLog(keepDays?)

GUI：
  1. 运维中心「归档旧活动日志」按钮 + 确认对话框（与清理不同：归档保留 JSON 副本）
  2. 命令面板：归档活动日志

CLI：
  1. -JobArchiveActivityLog [-KeepDays 30] [-Json]
     默认读取 studio-config 活动日志保留天数

v10.3 活动日志分类计数性能优化（Core + CLI 文档）
--------------------------------------------------
Core API：
  1. ActivityLogService.GetCategoryCounts(workspaceRoot, sinceDays?, category?, since?, until?)
     流式逐行扫描，单次遍历聚合各分类条数，无需构建完整 ActivityLogEntry 列表
  2. GetCategories 改为基于 GetCategoryCounts（GUI 分类下拉共用 sinceDays / category 筛选逻辑）

性能：
  1. 运维中心活动日志分类列表加载更省内存，大体积 activity-log 文件下更高效

CLI（-JobActivityLogStats 语义不变）：
  1. -JobActivityLogStats [-SinceDays <N>] [-Json]
     按分类聚合活动日志条数与最近时间；JSON 含 TotalCount / Items（Category、Count、LatestTimestamp）
  2. -JobExportActivityLogStats [-SinceDays <N>] [-Path <json>] [-Json]
  3. -JobExportActivityLogStatsCsv [-SinceDays <N>] [-Path <csv>] [-Json]
     统计导出与查询共用 -SinceDays 时间范围筛选（默认全量）

v10.2 选定任务 CSV 导出与夜间活动日志统计（GUI + CLI）
------------------------------------------------------
Core API：
  1. ExportSelectedJobsCsv(jobIds, outputPath?)
  2. StudioConfig.NightlyExportActivityLogStats

GUI：
  1. 任务工作台「导出列表 CSV」（当前可见列表全部 jobId）
  2. 设置：夜间全流程脚本自动导出活动日志统计 CheckBox
  3. 运维中心 Ctrl+Shift+L 加载更多活动日志（LoadMoreActivityLogCommand）

CLI：
  1. -JobExportSelectedJobsCsv -JobIds id1,id2 [-Path csv] [-Json]

v10.1 活动日志统计 CSV 导出与筛选任务增强（CLI）
------------------------------------------------
CLI：
  1. -JobExportActivityLogStatsCsv [-SinceDays <N>] [-Path <csv>] [-Json]
     按分类聚合活动日志条数与最近时间，导出 CSV（category、count、latestTimestamp；默认 reports/activity-log-stats-*.csv）
  2. -JobExportFilteredJobsCsv [-Filter active|failed|all|pending|published|processed|archived]
     [-Search 关键词] [-Tag 标签] [-Sort updated|title|pinned|...] [-Limit <N>] [-Path <csv>] [-Json]
     按任务列表同款筛选/排序导出 CSV（含 jobId、title、status、tags、发布链接等列）

v10.0 活动日志统计与筛选任务 CSV 导出（GUI + CLI）
--------------------------------------------------
GUI：
  1. 运维中心活动日志分类统计面板 +「导出统计 JSON」
  2. 任务工作台「导出筛选 CSV」（当前筛选/搜索/标签/排序）
  3. 全局快捷键：F5 看板刷新、Ctrl+R 刷新当前页、Ctrl+Shift+E 导出任务 JSON
  4. 命令面板分组重排 + 看板快照/活动日志统计/筛选 CSV 入口

CLI：
  1. -JobActivityLogStats [-SinceDays <N>] [-Json]
     按分类聚合活动日志条数与最近时间；JSON 含 TotalCount / Items（Category、Count、LatestTimestamp）
  2. -JobExportActivityLogStats [-SinceDays <N>] [-Path <json>] [-Json]
     导出活动日志统计 JSON（默认 reports/activity-log-stats-*.json）
  3. -JobExportFilteredJobsCsv [-Filter active|failed|all|pending|published|processed|archived]
     [-Search 关键词] [-Tag 标签] [-Sort updated|title|pinned|...] [-Limit <N>] [-Path <csv>] [-Json]
     按任务列表同款筛选/排序导出 CSV（jobId、title、status、tags 等）

v9.9 看板快照与活动日志分页（GUI + CLI）
----------------------------------------
GUI：
  1. 活动日志「加载更多」分页，设置可配置每页条数
  2. 运维中心合并预览可视化列表（点击跳转主任务）
  3. 看板单次快照刷新 + TG 发送后局部刷新
  4. 看板/TG/发布队列空状态友好提示

CLI：
  1. -JobActivityLogSearch 增强：-Offset <N> 分页；JSON 含 TotalMatched / HasMore
  2. -JobDashboardSnapshot [-PublishQueueLimit 20] [-TgLimit 50] [-WorkflowLimit 5] [-Json]

性能：
  1. GetDashboardSnapshot 一次 List() 聚合看板数据
  2. 重复任务 bundle 在 merge/delete/archive 后自动失效缓存

v9.8 批量合并预览导出与活动日志时间范围（GUI + CLI）
----------------------------------------------------
GUI：
  1. 运维中心「导出批量合并预览」：将批量合并预览结果导出 JSON
  2. 活动日志搜索/导出/清理支持「近 N 天」范围筛选（搜索 400ms 防抖）
  3. 设置：夜间全流程脚本自动扫描重复任务
  4. 命令面板新增：导出批量合并预览

CLI：
  1. -JobExportBatchMergePreview [-Path <json>] [-Json] 导出批量合并预览 JSON
  2. -JobActivityLogSearch / -JobExportActivityLogCsv / -JobPreviewActivityLogTrim / -JobTrimActivityLog 支持 -SinceDays <N>

性能：
  1. 重复扫描与合并建议共用单次扫描（DuplicateOperationsBundle 缓存）
  2. 活动日志 ReadAll 流式逐行读取，大文件更省内存
  3. 运维中心活动日志区独立刷新，减少全页重绘

v9.7 活动日志 CSV 与批量合并预览（GUI + CLI）
----------------------------------------------
GUI：
  1. 运维中心活动日志「导出 CSV」：按当前分类筛选导出 timestamp/category/message
  2. 运维中心「预览合并」：批量合并重复前预览将合并的组与目标任务
  3. 设置：夜间全流程脚本自动合并重复任务
  4. 命令面板新增：导出活动日志 CSV、预览批量合并重复
  5. 重复任务区「预览合并」与「自动合并重复」并列，先预览再执行

CLI：
  1. -JobExportActivityLogCsv [-Category <分类>] [-Path <csv>] [-Json] 导出活动日志 CSV
  2. -JobPreviewBatchMergeDuplicates [-Json] 预览批量合并重复（每组一行摘要）

v9.6 合并建议导出与活动日志分类清理（GUI + CLI）
------------------------------------------------
GUI：
  1. 运维中心「导出合并建议」：重复任务合并建议导出 JSON
  2. 活动日志清理支持当前分类筛选（选中分类时仅清理该分类旧记录）
  3. 设置：夜间全流程脚本自动清理活动日志
  4. 命令面板新增：导出合并建议

CLI：
  1. -JobExportDuplicateMergeSuggestions [-Path <json>] [-Json] 导出合并建议 JSON
  2. -JobPreviewActivityLogTrim [-KeepDays 30] [-Category <分类>] [-Json] 按分类预览清理
  3. -JobTrimActivityLog [-KeepDays 30] [-Category <分类>] [-Json] 按分类清理活动日志

v9.5 活动日志与合并建议增强（GUI + CLI）
----------------------------------------
GUI：
  1. 活动日志清理前预览 + 确认对话框
  2. 设置：活动日志保留天数（默认 30 天）
  3. 运维中心展示重复合并建议明细列表（点击跳转主任务）

CLI：
  1. -JobPreviewActivityLogTrim [-KeepDays 30] [-Json] 预览清理影响
  2. -JobTrimActivityLog 默认读取 studio-config 保留天数

v9.4 重复合并与快览增强（GUI + CLI）
------------------------------------
GUI：
  1. 标题栏快览增强：待发布 / TG待发 / 失败 / 重复 / 陈旧
  2. 运维中心「自动合并重复」：按建议保留主任务、归档重复项
  3. 运维中心「清理旧活动日志」（默认保留 30 天）
  4. 设置：夜间全流程脚本自动批量发送 TG 开关

CLI：
  1. -JobSuggestDuplicateMerges [-Json] 重复合并建议
  2. -JobBatchMergeDuplicates -Force [-Json] 自动合并重复
  3. -JobTrimActivityLog [-KeepDays 30] [-Json] 清理活动日志

v9.3 队列导出与夜间 TG 流水线（GUI + CLI）
------------------------------------------
GUI：
  1. 看板 TG 待发「导出 CSV」+ 列表内单条「发送」按钮
  2. 看板「导出队列 CSV」（待发布队列）
  3. 运维中心活动日志关键词搜索
  4. 运维中心「导出重复报告」JSON
  5. 夜间全流程脚本新增 -JobTgPreview 与可选 -JobBatchSendTg（注释行）

CLI：
  1. -JobExportTgPendingCsv [-Limit 50] [-Path <csv>] [-Json]
  2. -JobExportPublishQueueCsv [-Limit 50] [-Path <csv>] [-Json]
  3. -JobExportDuplicateReport [-Path <json>] [-Json]
  4. -JobActivityLogSearch [-Query 关键词] [-Category <分类>] [-Limit 50] [-Json]

v9.2 运维增强与 TG 预览（GUI + CLI）
------------------------------------
GUI：
  1. 看板「预览 TG 文案」：批量发送前查看分条与字数
  2. 运维中心重复任务扫描（同帖/同标题），点击跳转
  3. 活动日志分类筛选与导出 JSON
  4. 机器配置导出/导入（studio-config + UI 偏好，不含任务）
  5. 任务工作台「批量追加」标签（对当前筛选列表）
  6. 命令面板新增：扫描重复、导出活动日志、导出机器配置、预览 TG

CLI：
  1. -JobTgPreview [-Limit 50] [-Json] 预览待发 TG 文案
  2. -JobScanDuplicates [-Json] 扫描全部重复任务
  3. -JobBatchTags -Tags "a,b" [-Mode append|replace] [-Filter] [-Tag] [-Search] [-Json]
  4. -JobExportActivityLog [-Category <分类>] [-Path <path>] [-Json]
  5. -JobExportMachineProfile [-Path <path>] [-Json]
  6. -JobImportMachineProfile -Path <json> [-Merge|-Replace] [-Json]

v9.1 TG 待发队列与网盘 CSV 批量导入（CLI）
------------------------------------------
1. CLI 查询待发 TG 队列：-JobTgPending [-Limit 50] [-Json]
   （百度/夸克已发布、文案已生成、TG 未发布的任务）
2. CLI 批量 Bot 发送 TG：-JobBatchSendTg [-Limit 50] -Force [-Json]
   （需 -Force 确认；依赖 studio-config 中 Bot Token 与频道 ID）
3. CLI 网盘链接 CSV 导入：-JobImportPanLinksCsv -Path <csv> [-Json]
   （列：jobId, title, baiduLink, baiduPwd, quarkLink, quarkPwd, telegramLink；可按 jobId 或 title 匹配任务）

v9.0 里程碑综合摘要
-------------------
GUI：
  1. 任务标签筛选与置顶优先排序（任务工作台 ComboBox + 置顶按钮）
  2. 网盘分享文本粘贴解析（百度/夸克链接与密码自动回填）
  3. 全局命令面板 Ctrl+K（跳转各页面、导出夜间脚本等）
  4. 运维中心 Ctrl+5（概览、维护检查、陈旧任务、批量归档、报告导出）
  5. 首次启动欢迎引导（设置可关闭「启动时显示欢迎引导」）
  6. 运维中心 / 任务工作台一键导出夜间全流程脚本 nightly-full.bat

CLI：
  1. -JobList [-Tag <标签>] 按标签筛选任务列表（配合 -Filter / -Search / -Sort）
  2. -JobSearch -Query 关键词 [-Tag <标签>] 标签 + 关键词联合搜索
  3. -JobExportNightlyAutomation [-CliPath ...] [-Json] 导出夜间全自动流水线脚本
     （批量自动链 → 队列文案 → 日报 → 运维报告）
  4. -JobParsePanLinks / -JobSaveTags / -JobTogglePin / -JobExportOperationsReport（v8.5 延续）

夜间脚本 workspace\scripts\nightly-full.bat 执行顺序：
  -JobRunBatchChain -Filter active -Force
  -JobBatchQueueCopy -Limit 50
  -JobDailyReport
  -JobExportOperationsReport
  -JobTgPreview -Limit 50
  (可选) -JobBatchSendTg -Limit 50 -Force

v8.5 网盘链接解析、标签置顶与运维报告导出（CLI）
------------------------------------------------
1. CLI 解析分享文本：-JobParsePanLinks -Text "..." [-JobId <id>] [-Json]（省略 -Text 时从 stdin 读取）
2. CLI 保存任务标签：-JobSaveTags -JobId <id> -Tags "a,b" [-Json]
3. CLI 切换置顶：-JobTogglePin -JobId <id> [-Json]
4. CLI 导出运维报告：-JobExportOperationsReport [-Json]（写入 workspace\reports\operations-YYYY-MM-DD.json）

v8.4 运维中心与维护 CLI（GUI + CLI）
------------------------------------
1. GUI 运维中心：概览、维护检查、陈旧任务、批量归档已发布（Ctrl+5）
2. CLI 维护报告：-JobMaintenanceReport [-StaleDays 7] [-Json]
3. CLI 批量归档已发布：-JobBulkArchivePublished [-OlderThanDays 0] [-Json]
4. CLI 运维中心快照：-JobOperationsCenter [-Json]

v8.3 全量备份导入导出（CLI）
----------------------------
1. CLI 导出全量备份：-JobExportAllBackup [-Path <path>] [-Json]（省略 -Path 时写入 workspace\backups\）
2. CLI 导入全量备份：-JobImportAllBackup -Path <path> [-Merge] [-Json]（-Merge 合并 studio-config 并跳过重复任务）

v8.2 批量自动链与 studio-config 导入导出（CLI）
-----------------------------------------------
1. CLI 批量自动链：-JobRunBatchChain [-Filter active|failed|all] [-Force] [-Json]
2. CLI 导出 studio-config：-JobExportStudioConfig -Path <path> [-Json]
3. CLI 导入 studio-config：-JobImportStudioConfig -Path <path> [-Json]

v8.1 工作流自动链与日报（GUI + CLI）
------------------------------------
1. 自动执行链：连续执行可自动化步骤，遇回填链接/标记发布/TG 发送时停止（GUI 向导页）
2. CLI 自动链：-JobRunChain -JobId <id> [-Force] [-Json]（-Force 跳过清理确认）
3. CLI 日报导出：-JobDailyReport [-Json]，导出 workspace\reports\daily-YYYY-MM-DD.csv/.json

v8.0 发布向导与版本检查（GUI + CLI）
------------------------------------
1. 发布向导四 Tab：素材准备 → 链接回填 → 文案生成 → 渠道发布（任务工作台）
2. CLI 查询向导状态：-JobWizardState [-JobId <id>] [-Json]（-JobId 可选，省略则取工作流首要任务）
3. 启动时自动读取 data\version.json 检查新版本（InfoBar 提示）

v7.2 工作流执行与计划任务注册（GUI + CLI）
------------------------------------------
1. 执行建议下一步：按推荐或指定操作自动执行（-JobExecuteNextAction -JobId <id>）
2. 可选 -Action：retry|download|pipeline|copy|mark|telegram
3. 计划任务一键注册：导出 bat 并尝试 schtasks 注册（-JobScheduleRegister）
4. 支持 -Force / -CliPath / -Filter / -Hour / -Minute / -Json 参数

v7.1 发布工作流与定时批量（GUI + CLI）
--------------------------------------
1. 发布工作流建议：按任务状态推荐下一步操作（-JobNextAction）
2. 定时批量脚本导出：生成 Windows 计划任务用 bat（-JobScheduleExport）
3. 支持 -Limit / -CliPath / -Filter / -Hour / -Minute / -Json 参数

v7.0 生产环境打磨（GUI + CLI）
------------------------------
1. 批量流水线清理确认：批量执行前弹出删除确认，避免误删；IsBusy 防并发，长任务期间禁用重复点击
2. 看板交互增强：最近任务/待发布队列点击跳转到任务工作台；支持一键打开百度/夸克链接
3. 网络与 TG 可靠性：论坛 HTTP 请求自动重试；TG Bot 长文案按 4096 字符分条发送
4. 合并预览确认：合并任务前预览将合并的字段，确认后再执行（GUI）
5. 智能流水线：从帖子创建可自动下载附件；一键流水线含匹配解压密码步骤
6. 状态中文显示：任务状态、发布渠道状态统一中文标签
7. 设置面板补全：Bot Token、自动下载附件、发布后归档、处理后自动生成文案等
8. 全局忙碌遮罩：任务工作台长操作期间显示忙碌状态，防止误操作

v6.2 新增（GUI + CLI）
----------------------
1. 论坛附件下载：从帖子直链下载压缩包到 inbox（-JobDownloadAttachments）
2. 从帖子创建任务：拉取标题/链接/密码并可选下载附件（-JobCreateFromThread）
3. TG Bot 发送文案：通过 Bot API 将发布文案发到频道（-JobSendTelegram，需配置 Bot Token）
4. 待发布队列批量文案：对队列任务批量生成发布文案（-JobBatchQueueCopy）

v6.1 新增（GUI + CLI）
----------------------
1. 帖子信息拉取：从论坛帖自动提取网盘链接与解压密码（-JobFetchThreadInfo）
2. 任务合并：将源任务 inbox/链接/备注合并到目标任务（-JobMerge）
3. 待发布队列：查看已处理待回填链接的任务列表（-JobPublishQueue）
4. TG 频道快捷打开（GUI 设置面板）

v6.0 任务工作流（GUI + CLI）
----------------------------
1. 创建任务 → 拖入/导入 inbox 压缩包 → 扫描 → 解压 → 去广告压缩
2. 回填百度/夸克/TG 链接 → 生成发布文案 → 标记渠道已发布
3. 批量流水线（-JobBatchPipeline）、失败重试（-JobRetry）、健康检查（-JobHealth）
4. 任务备注（-JobSaveNotes）、单任务导入导出（-JobImport / -JobExport）
5. 归档与取消归档（-JobArchive / -JobUnarchive）、列表排序（-JobList -Sort title|updated）

技术栈
------
- .NET SDK 10.0.301（运行时 10.0.x）
- Windows App SDK 2.2.0
- WinUI 3 解包应用（无需 MSIX 安装）

解决方案结构
------------
src/PLCPak.Core      业务逻辑（广告清理、压缩、流水线、清单）
src/PLCPak.WinUI     WinUI 3 图形界面
src/PLCPak.Cli       命令行（兼容 -Clean -Preview -Compress -NoGui）

功能对照（与 v2.0 PowerShell 版）
--------------------------------
✓ 预览 → 确认 → 清理 → 压缩 流水线
✓ 7z-zstd + tar.zst 双格式
✓ 广告哈希清单 + 样本库导入/同步
✓ 并行 SHA256、扫描缓存、白名单
✓ 设置面板、最近项目、任务状态
✓ 归档校验、分卷、-ms=on、临时目录
✓ CLI 全流程

构建
----
  cd F:\BaiduNetdiskDownload\PLCPak\winui
  dotnet build PLCPak.sln -c Release -p:Platform=x64

发布（含资源复制 + zip）
------------------------
  powershell -File scripts\Publish-Release.ps1

运行 GUI
--------
  dist\PLCPak.WinUI\PLCPak.WinUI.exe

CLI 示例
--------
  dist\PLCPak.Cli\PLCPak.Cli.exe -Preview -Path "D:\游戏" -NoGui
  dist\PLCPak.Cli\PLCPak.Cli.exe -Clean -Compress -Path "D:\游戏" -NoGui -Force

说明
----
运行时需与 exe 同目录放置：7-Zip-Zstandard、AD-Samples、compress-config.json
Publish-Release.ps1 会从 dev\ 自动复制这些文件。

旧版 PowerShell 仍保留在 ..\dev\ 供参考对比。
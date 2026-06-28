# PLCPak WinUI 更新日志

**最新版本：** v1.0.74
**技术栈：** .NET 10 + Windows App SDK 2.2.0（自包含发布，一般无需单独安装 .NET）

本文件汇总自 `winui/README.txt`，涵盖 **v10.8.0** 起至当前正式发行版的完整变更记录。

> **版本号说明：** 自 v1.0.11 起采用语义化版本（major.minor.patch），承接原 v10.10 功能基线；v10.x 为过渡期编号。

发行包命名：`PLCPak_v{版本号}_WinUI.zip`（v1.0.11 起）；早期包见 `releases/新建文件夹/`。

---

## 版本索引（新 → 旧）

| 版本 | 摘要 |
|------|------|
| v1.0.74 | 向导统一与看板精简 |
| v1.0.73 | 简易模式流程简化 |
| v1.0.72 | 修复新手引导与暗色主题 |
| v1.0.71 | 修复命令面板崩溃 |
| v1.0.70 | UI/UX 增强 |
| v1.0.69 | UI/UX 增强 |
| v1.0.68 | UI/UX 增强 |
| v1.0.67 | UI/UX 增强 |
| v1.0.66 | UI/UX 增强 |
| v1.0.65 | UI/UX 增强 |
| v1.0.64 | UI/UX 增强 |
| v1.0.63 | UI/UX 增强 |
| v1.0.62 | UI/UX 增强 |
| v1.0.61 | UI/UX 增强 |
| v1.0.60 | UI/UX 增强 |
| v1.0.59 | UI/UX 增强 |
| v1.0.58 | UI/UX 优化 |
| v1.0.57 | 稳定版 |
| v1.0.56 | 性能 UX 与状态双语 |
| v1.0.55 | 壳层本地化、消息双语与简易模式打磨 |
| v1.0.54 | 快速处理/设置本地化与看板筛选聚焦 |
| v1.0.53 | 详情区/运维页本地化与新建聚焦 |
| v1.0.52 | 侧栏本地化、运维聚焦与完成反馈 |
| v1.0.51 | 向导页本地化与导航聚焦 |
| v1.0.50 | 向导 UX 三连优化 |
| v1.0.49 | 向导键盘快捷键与提示本地化 |
| v1.0.48 | 向导步骤自动滚动 |
| v1.0.47 | 任务页面分步向导 |
| v1.0.46 | 用户体验 (UX) 增强 |
| v1.0.45 | 看板/UI 本地化与主题优化 |
| v1.0.44 | 修复首次引导动画步骤切换崩溃 |
| v1.0.43 | 修复启动初始化失败 |
| v1.0.42 | 看板卡片化、引导动画、暗色主题与中英文 |
| v1.0.41 | 全局工作流引导与导航智能推荐 |
| v1.0.40 | 简易/专业模式与全站 UI/UX 更迭 |
| v1.0.39 | 运维中心 UI/UX 优化 |
| v1.0.38 | 运维报告分区输出与夜间 readme 说明 |
| v1.0.37 | 机器配置运维快照持久化与导入联动 |
| v1.0.36 | 机器配置运维快照摘要与设置保存刷新 |
| v1.0.35 | 运维快照活动日志统计与缓存刷新 |
| v1.0.34 | 空天数筛选回退保留天数与运维分区 |
| v1.0.33 | 活动日志统计 SinceDays 与运维快照批量摘要 |
| v1.0.32 | 保存设置后同步活动日志天数筛选 |
| v1.0.31 | 批量统计 SinceDays 贯通与运维默认天数 |
| v1.0.30 | 设置夜间双导出联动与导入预览增强 |
| v1.0.29 | 机器配置夜间摘要与 bundle SinceDays |
| v1.0.28 | 夜间批量统计独立开关与动态快捷键提示 |
| v1.0.27 | 批量 bundle 同步时间戳与运维快捷键说明 |
| v1.0.26 | 夜间 SinceDays 联动与批量一键导出快捷键 |
| v1.0.25 | 夜间批量一键导出、HTML 天数标注与 CSV 快捷键 |
| v1.0.24 | 批量统计 JSON+CSV 一键导出 |
| v1.0.23 | 批量操作快捷键与夜间 CSV 导出 |
| v1.0.22 | 批量统计 CSV、夜间 JSON 导出与条带点击筛选 |
| v1.0.21 | 批量统计 CLI/JSON 导出、HTML 柱状图与夜间 CSV+HTML 双导出 |
| v1.0.20 | 导入预览校验、HTML 批量统计区块与运维批量摘要 |
| v1.0.19 | 活动日志批量筛选、搜索 CSV 导出与机器配置 HTML 主题 |
| v1.0.18 | 机器配置导入预览与冲突检测 |
| v1.0.17 | 夜间脚本自动导出 HTML 统计报告 |
| v1.0.16 | 机器配置含快捷键、HTML 主题跟随偏好与批量操作日志增强 |
| v1.0.15 | 批量删除回收站模式、快捷键导入导出与 HTML 暗色主题 |
| v1.0.14 | 多选批量删除预览、HTML 打印导出 PDF 与快捷键冲突检测 |
| v1.0.13 | 多选批量恢复、统计 HTML 交互图表与快捷键自定义映射 |
| v1.0.12 | 多选批量归档、统计 HTML 每日图表与单项快捷键禁用 |
| v1.0.11 | 按任务 ID 批量标签与活动日志统计 HTML 导出 |
| v10.10 | 活动日志归档筛选透传与批量置顶筛选 |
| v10.9 | 全局快捷键开关与活动日志每日统计 CSV 定稿 |
| v10.8 | 活动日志日统计与筛选批量归档 |

---

## v1.0.74 — 向导统一与看板精简（WinUI）

**发行包：** `PLCPak_v1.0.74_WinUI.zip`

### GUI
- 「发布向导」导航/Ctrl+4 统一进入任务工作台内嵌向导，不再打开独立向导页
- 启动恢复「向导」页时自动落点任务工作台并聚焦向导区
- 简易模式下看板隐藏「智能下一步」卡片，避免与底部全局引导栏重复
- 全局建议跳转「向导」时一律走任务内嵌向导

### 脚本
- `Publish-Release.ps1` 从 `AppVersion.cs` 自动读取版本号；ZIP 文件名动态生成

### 版本信息
- AppVersion 1.0.74；VersionCheck 远程示例 1.0.75

---

## v1.0.73 — 简易模式流程简化（WinUI）

**发行包：** `PLCPak_v1.0.73_WinUI.zip`（未单独发行；变更已并入 v1.0.74）

### GUI
- 简易模式顶部导航常驻「快速处理」，拖文件即可压缩，无需切换专业模式
- 启动时不再误恢复向导/运维页；默认落点任务工作台
- 全局建议「向导」在简易模式下直达任务工作台内嵌向导
- Ctrl+K 命令面板在简易模式仅保留 8 项常用操作（导航、流水线、设置等）
- 新手引导文案与真实简易流程对齐（快速处理 + 任务工作台 + 看板）
- 快速处理页移除重复设置按钮（统一用顶栏设置）

### 版本信息
- AppVersion 1.0.73；VersionCheck 远程示例 1.0.74

---

## v1.0.72 — 修复新手引导与暗色主题（WinUI）

**发行包：** `PLCPak_v1.0.72_WinUI.zip`

### GUI
- 引导卡片：动画失败时强制 Opacity=1，避免部分机器整块不可见（含跳过/下一步按钮）
- 暗色主题：Plc* 画刷改为 ThemeDictionaries + RequestedTheme，卡片/标题/正文随主题实时切换
- 修复暗色模式下快速处理等页面白卡片、文字不可见的问题
- 引导底栏增加 Esc 跳过提示；跳过/下一步按钮设置最小尺寸

### 版本信息
- AppVersion 1.0.72；VersionCheck 远程示例 1.0.73

---

## v1.0.71 — 修复命令面板崩溃（WinUI）

**发行包：** `PLCPak_v1.0.71_WinUI.zip`（未单独发行）

### GUI
- 命令面板 DataTemplate 补充 xmlns:x 声明，修复打开 Ctrl+K 时 XamlParseException（undeclared prefix）

### 版本信息
- AppVersion 1.0.71；VersionCheck 远程示例 1.0.72

---

## v1.0.70 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.70_WinUI.zip`

### GUI
- 快速处理：刷新时源列表计数显示「统计源文件…」
- 运维中心：刷新时陈旧任务区显示加载提示并暂隐列表
- 命令面板：筛选时用 accent 高亮匹配关键词
- 全局引导栏：页面刷新时显示「全局建议更新中…」并暂禁按钮

### 版本信息
- AppVersion 1.0.70；VersionCheck 远程示例 1.0.71

---

## v1.0.69 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.69_WinUI.zip`

### GUI
- 任务工作台：刷新时向导步骤区显示「步骤更新中…」并暂隐步骤/标签页
- 发布看板：刷新时任务统计区显示加载提示并暂禁筛选按钮
- 命令面板：筛选无结果时显示「无匹配命令」提示
- 命令面板：轻量操作反馈改用 1.5 秒短 InfoBar 自动关闭

### 版本信息
- AppVersion 1.0.69；VersionCheck 远程示例 1.0.70

---

## v1.0.68 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.68_WinUI.zip`

### GUI
- 发布向导：刷新时步骤区显示「步骤更新中…」并暂隐列表、禁用执行/自动执行
- 运维中心：有可归档任务时高亮提示区；按钮显示「批量归档已发布 (N)」
- 运维中心：重复合并建议标题旁显示「N 条合并建议」计数
- 全局 InfoBar：Success 3 秒、Informational 2.5 秒自动关闭；Warning/Error 保持

### 版本信息
- AppVersion 1.0.68；VersionCheck 远程示例 1.0.69

---

## v1.0.67 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.67_WinUI.zip`

### GUI
- 发布看板：刷新时「智能下一步」卡片显示加载提示并暂禁操作
- 任务工作台：inbox/密码库拖放高亮；导入后短暂成功提示
- 任务工作台：刷新时「下一步建议」卡片显示加载提示并暂禁
- 运维中心：活动日志关键词输入时显示「正在筛选…」进度指示

### 版本信息
- AppVersion 1.0.67；VersionCheck 远程示例 1.0.68

---

## v1.0.66 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.66_WinUI.zip`

### GUI
- 快速处理：源列表/广告样本拖放区高亮反馈；添加后显示短暂成功提示
- 发布看板：刷新时最近任务与健康问题显示加载提示并暂禁列表
- 发布看板：最近任务标题旁显示记录数
- 运维中心：重复扫描标题旁显示「N 组重复」计数

### 版本信息
- AppVersion 1.0.66；VersionCheck 远程示例 1.0.67

---

## v1.0.65 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.65_WinUI.zip`

### GUI
- 运维中心：活动日志筛选激活时显示摘要 + 「清空筛选」（对齐任务页）
- 发布看板：刷新时 TG/发布队列显示「队列更新中…」并暂禁列表
- 命令面板：显示匹配命令数；列表首项按 ↑ 回到筛选框
- 发布向导：任务下拉旁显示「共 N 个任务」；陈旧任务区显示计数

### 版本信息
- AppVersion 1.0.65；VersionCheck 远程示例 1.0.66

---

## v1.0.64 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.64_WinUI.zip`

### GUI
- 发布看板：刷新时指标卡显示「…」占位并禁用点击，附「指标更新中…」提示
- 命令面板：↑↓ 选择、Enter 执行；打开时自动聚焦筛选框
- 全局工作流引导栏：执行/打开任务按钮显示动作与任务名；有待办时 Pro 模式也高亮边框
- 机器配置/快捷键配置克隆时同步 RecentCommandPaletteIds

### 版本信息
- AppVersion 1.0.64；VersionCheck 远程示例 1.0.65

---

## v1.0.63 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.63_WinUI.zip`

### GUI
- 命令面板：记录最近 5 条命令，无筛选时置顶「最近使用」
- 任务工作台：筛选激活时显示摘要（搜索/状态/排序/标签）
- 有筛选时列表上方显示「清空筛选」按钮（不限于空结果）

### 版本信息
- AppVersion 1.0.63；VersionCheck 远程示例 1.0.64

---

## v1.0.62 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.62_WinUI.zip`

### GUI
- F5/Ctrl+R：刷新成功时依赖页面进度条，不再弹「刷新完成」；忙碌时提示「正在刷新…」
- 活动日志：「已显示 N 条」计数；加载更多时 ProgressRing 指示
- 发布向导：无步骤时隐藏步骤列表，仅显示空状态提示

### 版本信息
- AppVersion 1.0.62；VersionCheck 远程示例 1.0.63

---

## v1.0.61 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.61_WinUI.zip`

### GUI
- 快速处理：刷新指示、源列表计数与空状态提示
- 发布向导：步骤完成进度条、刷新指示、无任务/未选任务空状态
- 运维活动日志：筛选结果「匹配 N 条记录」高亮徽章

### 版本信息
- AppVersion 1.0.61；VersionCheck 远程示例 1.0.62

---

## v1.0.60 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.60_WinUI.zip`

### GUI
- 运维中心刷新时顶部进度条与「正在刷新运维中心…」
- 任务列表刷新指示；显示「共 N 个任务」或「显示 X / Y 个任务」
- 多选任务时显示「已选 N 个任务」提示
- 运维重复扫描空状态；重复组摘要双语本地化

### 版本信息
- AppVersion 1.0.60；VersionCheck 远程示例 1.0.61

---

## v1.0.59 — UI/UX 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.59_WinUI.zip`

### GUI
- 设置页改为 Expander 分组（外观/工作区/任务/快捷键/集成/夜间/高级）
- 设置内「重新播放新手引导」；首次 onboarding 时跳过欢迎对话框
- 顶栏新增命令面板按钮（Ctrl+K）；帮助按钮文案与快捷键提示
- 全局忙碌遮罩显示流水线进度条；InfoBar 错误/警告不自动关闭
- 看板刷新时顶部显示不确定进度条与「正在刷新看板…」

### 版本信息
- AppVersion 1.0.59；VersionCheck 远程示例 1.0.60

---

## v1.0.58 — UI/UX 优化（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.58_WinUI.zip`

### GUI
- 任务详情/筛选空状态与「清空筛选」；筛选条件记忆 LastJobFilter
- 向导 Tab 始终有标题；简易模式隐藏竖向步骤列表
- 导航当前页高亮 + 简易模式推荐页淡色提示
- 快速处理消息改 InfoBar；看板最近任务/健康问题空状态
- 页面切换动画优化；导航预加载减少重复刷新

### 版本信息
- AppVersion 1.0.58；VersionCheck 远程示例 1.0.59

---

## v1.0.57 — 稳定版（性能 UX + 崩溃修复，WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.57_WinUI.zip`

### GUI
- 性能：列表未变时跳过重建；日志轮询去重；减少动画设置
- 崩溃修复：任务列表 Extended 模式仅用 SelectedItems 同步；页面可见后再刷新/选中；切换工作台延迟 RefreshJobs
- 简易模式仅显示竖向向导步骤；状态与发布摘要双语
- 启动日志记录 HResult 与调度堆栈

### 版本信息
- AppVersion 1.0.57；VersionCheck 远程示例 1.0.58

---

## v1.0.56 — 性能 UX 与状态双语（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.56_WinUI.zip`

### GUI
- 任务列表增量同步（筛选/排序时避免 Clear+全量重建，保留选中与滚动位置）
- 任务日志轮询去重：文本未变时不刷新绑定，减少滚动抖动
- 向导步骤列表结构未变时仅就地更新，减少闪烁
- 设置新增「减少动画」：跳过页面切换淡入淡出与向导步骤动效（保留完成音效）
- 语言切换时刷新任务列表/看板最近任务的状态与发布摘要显示

### Core
- 任务状态与发布渠道摘要双语（job.status.* / publish.channel.*）；UiDisplayContext 与 LocalizationService 同步
- UiPreferences.ReduceMotion

### 版本信息
- AppVersion 1.0.56；VersionCheck 远程示例 1.0.57

---

## v1.0.55 — 壳层本地化、消息双语与简易模式打磨（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.55_WinUI.zip`

### GUI
- MainWindow 壳层全面本地化：导航、InfoBar、命令面板、帮助/欢迎对话框（shell.* / feedback.* 110+ 项）
- ViewModel 消息对话框标题与静态文案双语（Jobs/Dashboard/Operations ~195 处；jobs.msg.* / common.msg.*）
- 简易模式：隐藏快速处理/发布向导/运维导航；引导条隐藏冗余「去推荐页」；可关闭顶部提示；无待办时弱化引导条

### Core
- UiPreferences.HasDismissedSimpleModeHint

### 版本信息
- AppVersion 1.0.55；VersionCheck 远程示例 1.0.56

---

## v1.0.54 — 快速处理/设置本地化与看板筛选聚焦（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.54_WinUI.zip`

### GUI
- 快速处理页（MainPage）全面中英双语（quick.page.* 81 项）；状态/进度文案随语言刷新
- 设置对话框剩余控件全面本地化（settings.* 73+ 项）；语言预览实时更新标签
- 简易模式：看板指标（失败/待发布/归档/全部）跳转后自动选中筛选首条任务并聚焦向导

### Core
- UiStringTable quick.page.* / settings.general|jobs|shortcuts|nightly|activityLog.*

### 版本信息
- AppVersion 1.0.54；VersionCheck 远程示例 1.0.55

---

## v1.0.53 — 详情区/运维页本地化与新建聚焦（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.53_WinUI.zip`

### GUI
- 任务页右侧详情区全面中英双语（英雄卡片、密码/inbox、向导 Tab、日志等 63 项）
- 运维中心页面全面中英双语（ops.page.* 70 项）；语言切换实时刷新
- 新建任务 / 从帖子创建 / 导入 JSON 成功后自动滚到分步向导

### Core
- UiStringTable jobs.detail.* / ops.page.*

### 版本信息
- AppVersion 1.0.53；VersionCheck 远程示例 1.0.54

---

## v1.0.52 — 侧栏本地化、运维聚焦与完成反馈（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.52_WinUI.zip`

### GUI
- 任务页左侧栏全面中英双语（新建任务、搜索筛选、批量/导出按钮等 36 项）
- 简易模式：运维中心跳转任务后聚焦分步向导（与看板/引导条一致）
- 向导步骤完成时播放系统音效 + 可选触觉反馈；设置中可关闭

### Core
- UiPreferences.EnableWizardCompletionFeedback（默认开启）
- UiStringTable jobs.sidebar.* / settings.wizardFeedback

### 版本信息
- AppVersion 1.0.52；VersionCheck 远程示例 1.0.53

---

## v1.0.51 — 向导页本地化与导航聚焦（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.51_WinUI.zip`

### GUI
- 发布向导页（WizardPage）全面中英双语；语言切换实时刷新
- 任务页剩余文案本地化：下一步卡片、空列表、任务日志、专业快捷键行
- 简易模式：全局引导条「打开任务」跳转后聚焦向导；发布向导页跳转同样聚焦
- 发布向导页步骤列表自动滚到当前高亮步骤

### Core
- UiStringTable 扩展 wizard.page.* / jobs.nextAction.* / jobs.proShortcuts.*

### 版本信息
- AppVersion 1.0.51；VersionCheck 远程示例 1.0.52

---

## v1.0.50 — 向导 UX 三连优化（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.50_WinUI.zip`

### GUI
- 简易模式选中任务后，「更多设置」Expander 默认折叠，突出分步向导
- 看板点击任务跳转后延迟聚焦向导区（focusWizard 参数，仅 Dashboard 来源）
- 向导步骤完成时：进度条 + 向导卡片 + 已完成步骤按钮脉冲动画（链式 Storyboard）

### Core
- UiStringTable jobs.collapse.moreSettings

### 版本信息
- AppVersion 1.0.50；VersionCheck 远程示例 1.0.51

---

## v1.0.49 — 向导键盘快捷键与提示本地化（WinUI + Core）

**发行包：** `PLCPak_v1.0.49_WinUI.zip`

### GUI
- 任务页向导：Ctrl+Alt+←/→ 上一步/下一步，Ctrl+Alt+Enter 执行当前步骤
- 简易/专业模式显示向导快捷键提示；专业模式显示 jobs.wizard.hint
- 素材准备、文案生成 Tab 提示文案中英双语

### Core
- UiStringTable 扩展 jobs.wizard.shortcuts / wizard.step.*.hint

### 版本信息
- AppVersion 1.0.49；VersionCheck 远程示例 1.0.50

---

## v1.0.48 — 向导步骤自动滚动（WinUI）

**发行包：** `PLCPak_v1.0.48_WinUI.zip`

### GUI
- 切换向导步骤（上一步/下一步/点击步骤/TabView）时，右侧详情区自动滚到向导区域
- 选中任务时滚到当前步骤；执行步骤推进后随 Tab 切换同步滚动
- 双帧延迟布局 + BringIntoView，合并同帧多次滚动请求

### 版本信息
- AppVersion 1.0.48；VersionCheck 远程示例 1.0.49

---

## v1.0.47 — 任务页面分步向导（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.47_WinUI.zip`

### GUI
- 任务工作台嵌入「分步发布向导」：进度条、横向步骤条、纵向步骤列表（简易模式）
- WizardSteps 由 PublishWizardTabService 状态驱动；步骤可点击切换 TabView
- 执行当前步骤 / 上一步 / 下一步；TabView 与 SelectedWizardTabIndex 双向同步
- 简易模式突出向导；专业模式保留 Tab 标题 + 紧凑步骤条
- 工作台标题与简易提示本地化（jobs.workbench / jobs.simpleHint）

### Core
- UiStringTable 扩展 jobs.wizard.* / wizard.step.* / wizard.status.*

### 版本信息
- AppVersion 1.0.47；VersionCheck 远程示例 1.0.48

---

## v1.0.46 — 用户体验 (UX) 增强（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.46_WinUI.zip`

### GUI
- GlobalInfoBar：成功/信息 4 秒自动关闭；错误保持至手动关闭
- 看板空状态 CTA：待发布队列空→「去任务工作台」；TG 空→提示+刷新
- 指标卡 Tooltip（metric.tip.*）与 PointerOver/Pressed 视觉反馈
- 顶栏快览可点击→打开发布看板并刷新
- 导航切换内容区 150ms 淡入淡出（链式 Storyboard，避免同属性冲突）
- 设置语言实时预览；取消时还原
- Esc 关闭 InfoBar；Esc 跳过引导
- 简易模式有下一步时引导条 PlcAccentBrush 2px 边框强调

### Core
- UiStringTable 扩展 metric.tip / feedback / dashboard.goToJobs / tooltip.quickStats

### 版本信息
- AppVersion 1.0.46；VersionCheck 远程示例 1.0.47

---

## v1.0.45 — 看板/UI 本地化与主题优化（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.45_WinUI.zip`

### GUI
- 看板指标卡片可点击筛选（总任务/待发布/失败/TG/健康）
- 专业模式按钮与空队列提示全面本地化（中/英）
- 导航按钮 Tooltip、全局忙碌文案、引导条无待办提示本地化
- 暗色/亮色标题栏与导航高亮随主题刷新；设置内主题可预览、取消还原
- PlcNavHighlightBrush / PlcTitleBarTextBrush 主题资源

### Core
- UiStringTable 扩展 dashboard/tooltip/busy/guide 键与 GetNavLabel()

### 版本信息
- AppVersion 1.0.45；VersionCheck 远程示例 1.0.46

---

## v1.0.44 — 修复首次引导动画步骤切换崩溃（WinUI）

**发行包：** `PLCPak_v1.0.44_WinUI.zip`

### GUI
- OnboardingOverlay 步骤切换改为先淡出再淡入，避免同一 Storyboard 重复动画 Opacity

### 版本信息
- AppVersion 1.0.44；VersionCheck 远程示例 1.0.45

---

## v1.0.43 — 修复启动初始化失败（WinUI）

**发行包：** `PLCPak_v1.0.43_WinUI.zip`

### GUI
- 主题/语言加载从 App 构造函数移至 OnLaunched，避免访问 Resources 时 COM 异常

### 版本信息
- AppVersion 1.0.43；VersionCheck 远程示例 1.0.44

---

## v1.0.42 — 看板卡片化、引导动画、暗色主题与中英文（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.42_WinUI.zip`

### GUI
- 发布看板指标卡片（总任务/待发布/失败/TG/健康问题）与双列队列/TG 卡片布局
- 首次启动动画引导（4 步淡入切换，可跳过）
- 暗色主题：设置切换，运行时刷新 Plc* 画刷
- 中英文界面：设置切换，导航/看板/引导条等关键文案本地化

### Core
- UiStringTable — 中英文字典；UiPreferences.AppTheme / UiLanguage / HasSeenOnboarding

### 版本信息
- AppVersion 1.0.42；VersionCheck 远程示例 1.0.43

---

## v1.0.41 — 全局工作流引导与导航智能推荐（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.41_WinUI.zip`

### GUI
- 底部「全局下一步」引导条：摘要 / 去推荐页 / 打开任务 / 一键执行
- 导航角标：任务工作台(待办)、发布看板(TG)、运维中心(陈旧)
- 简易模式下推荐页面高亮；Ctrl+Shift+P 切换简易/专业
- 任务工作台简易模式显示「当前步骤」发布向导提示

### Core
- WorkflowGuideService — 聚合首要待办、推荐页面与角标计数

### 版本信息
- AppVersion 1.0.41；VersionCheck 远程示例 1.0.42

---

## v1.0.40 — 简易/专业模式与全站 UI/UX 更迭（WinUI + Core + Tests）

**发行包：** `PLCPak_v1.0.40_WinUI.zip`

### GUI
- 顶栏「简易 | 专业」ToggleSwitch；UiPreferences.ProfessionalMode 持久化
- 默认简易模式：各页突出「下一步」英雄卡片，隐藏批量导出/日志统计等高级区
- 专业模式：恢复 v1.0.39 完整功能（运维活动日志、重复扫描、任务批量操作等）
- 全站统一 PlcPageTitle / PlcHeroCard / PlcContentCard / PlcPrimaryButton 样式
- 设置页可勾选专业模式；机器配置/快捷键配置导入导出含 ProfessionalMode

### Core
- UiModeService — 全局模式切换与 ui-prefs.json 同步

### 版本信息
- AppVersion 1.0.40；VersionCheck 远程示例 1.0.41

---

## v1.0.39 — 运维中心 UI/UX 优化（WinUI）

**发行包：** `PLCPak_v1.0.39_WinUI.zip`

### GUI
- 运维分区卡片化展示；任务概览高亮；完整摘要可折叠
- 活动日志 Expander + 筛选摘要/清空/天数说明；陈旧任务空状态
- 底部操作分组（常用运维 / 备份 / 日志维护）；顶栏刷新与快捷键提示
- 主导航 ToolTip 显示 Ctrl+1~5；新增 PlcCardBackgroundBrush 等主题色

### 版本信息
- AppVersion 1.0.39；VersionCheck 远程示例 1.0.40

---

## v1.0.38 — 运维报告分区输出与夜间 readme 说明（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.38_WinUI.zip`

### Core API
- OperationsCenterService.FormatSectionLines — 统一格式化 Sections 文本行
- 夜间 readme 注明运维报告 JSON 含 activity-log-stats / activity-log-batch

### GUI
- 导出运维报告消息显示摘要与各分区行

### CLI
- -JobExportOperationsReport 文本模式输出全部分区（与 -JobOperationsCenter 一致）

### 版本信息
- AppVersion 1.0.38；VersionCheck 远程示例 1.0.39

---

## v1.0.37 — 机器配置运维快照持久化与导入联动（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.37_WinUI.zip`

### Core API
- MachineProfileBundle 写入 OperationsCenterSummary；预览/导出结果贯通
- 导入机器配置成功后刷新运维快照缓存

### GUI
- 导入确认框显示导出时运维快照
- 导入成功后同步活动日志天数筛选与快捷键提示

### CLI
- -JobExportMachineProfile / -JobPreviewMachineProfile 文本模式输出运维快照行

### 版本信息
- AppVersion 1.0.37；VersionCheck 远程示例 1.0.38

---

## v1.0.36 — 机器配置运维快照摘要与设置保存刷新（Core + WinUI + Tests）

**发行包：** `PLCPak_v1.0.36_WinUI.zip`

### Core API
- MachineProfileExportResult 含 OperationsCenterSummary；导出摘要附带运维快照

### GUI
- 保存设置后自动刷新运维中心（同步天数筛选与概览摘要）
- 移除运维概览批量摘要重复拼接

### 版本信息
- AppVersion 1.0.36；VersionCheck 远程示例 1.0.37

---

## v1.0.35 — 运维快照活动日志统计与缓存刷新（Core + CLI + Tests）

**发行包：** `PLCPak_v1.0.35_WinUI.zip`

### Core API
- OperationsCenterSnapshot 含 ActivityLogStatsSummary 与 activity-log-stats 分区
- 运维快照缓存纳入活动日志文件指纹，写入日志后自动刷新批量/分类统计

### CLI
- -JobOperationsCenter 文本模式输出活动日志统计分区（有记录时）

### 版本信息
- AppVersion 1.0.35；VersionCheck 远程示例 1.0.36

---

## v1.0.34 — 空天数筛选回退保留天数与运维分区（Core + WinUI + Tests）

**发行包：** `PLCPak_v1.0.34_WinUI.zip`

### Core API
- 运维快照 Sections 增加 activity-log-batch 分区（有批量记录时）
- GlobalShortcutKeyHelper 修复 Ctrl+Shift+? 快捷键 VirtualKey 映射

### GUI
- 天数筛选留空时按 ActivityLogKeepDays 筛选（与占位提示一致）
- 空状态提示显示「默认保留 N 天」

### 版本信息
- AppVersion 1.0.34；VersionCheck 远程示例 1.0.35

---

## v1.0.33 — 活动日志统计 SinceDays 与运维快照批量摘要（Core + WinUI + Tests）

**发行包：** `PLCPak_v1.0.33_WinUI.zip`

### Core API
- ActivityLogStatsResult/JSON/CSV/HTML 导出结果含 SinceDays；摘要统一「最近 N 天」
- OperationsCenterSnapshot 含 ActivityLogBatchSummary；快照与 JSON 报告自动填充
- JobRunner 运维快照按 ActivityLogKeepDays 附带批量操作统计摘要

### GUI
- 运维中心概览摘要显示批量操作统计（有记录时）

### 版本信息
- AppVersion 1.0.33；VersionCheck 远程示例 1.0.34

---

## v1.0.32 — 保存设置后同步活动日志天数筛选（WinUI）

**发行包：** `PLCPak_v1.0.32_WinUI.zip`

### GUI
- 修改「活动日志保留天数」并保存后，若天数筛选仍为旧默认值则自动同步
- 天数输入框占位提示「空=默认保留天数」

### 版本信息
- AppVersion 1.0.32；VersionCheck 远程示例 1.0.33

---

## v1.0.31 — 批量统计 SinceDays 贯通与运维默认天数（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.31_WinUI.zip`

### Core API
- ActivityLogBatchSummaryResult/导出结果含 SinceDays；摘要统一显示「最近 N 天」
- NightlyAutomationExport 含 ActivityLogExportSinceDays、批量导出标记与增强 SummaryText

### GUI
- 运维中心首次加载默认填入 ActivityLogKeepDays 作为天数筛选
- 夜间脚本导出消息使用 SummaryText

### CLI
- -JobExportNightlyAutomation 文本模式先输出 SummaryText

### 版本信息
- AppVersion 1.0.31；VersionCheck 远程示例 1.0.32

---

## v1.0.30 — 设置夜间双导出联动与导入预览增强（WinUI + Tests）

**发行包：** `PLCPak_v1.0.30_WinUI.zip`

### GUI
- 勾选「同时导出 CSV 与 HTML」自动开启统计 CSV/HTML 与批量 JSON+CSV
- 机器配置导入确认框显示夜间批量统计与 -SinceDays 提示

### 版本信息
- AppVersion 1.0.30；VersionCheck 远程示例 1.0.31

---

## v1.0.29 — 机器配置夜间摘要与 bundle SinceDays（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.29_WinUI.zip`

### Core API
- MachineProfile 导出/预览携带夜间批量统计与 ActivityLogKeepDays 摘要
- ActivityLogBatchBundleExportResult.SinceDays；ExportAll 摘要含天数
- 夜间 readme 头部显示活动日志导出筛选天数

### GUI
- 保存设置后运维中心快捷键提示即时刷新
- 机器配置导出消息使用 SummaryText

### 版本信息
- AppVersion 1.0.29；VersionCheck 远程示例 1.0.30

---

## v1.0.28 — 夜间批量统计独立开关与动态快捷键提示（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.28_WinUI.zip`

### Core API
- StudioConfig.NightlyExportActivityLogBatchStatsAll — 可独立于活动日志统计开启夜间批量导出

### GUI
- 设置页「夜间全流程脚本自动导出批量操作统计 JSON+CSV」
- 运维中心批量快捷键提示随自定义映射动态更新

### 版本信息
- AppVersion 1.0.28；VersionCheck 远程示例 1.0.29

---

## v1.0.27 — 批量 bundle 同步时间戳与运维快捷键说明（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.27_WinUI.zip`

### Core API
- ExportAll 为 JSON/CSV 使用同一 BundleStamp 文件名后缀
- ActivityLogBatchBundleExportResult.BundleStamp 字段
- 夜间 readme 步骤说明同步显示 -SinceDays
- HTML 批量操作区块显示「筛选最近 N 天」

### GUI
- 欢迎/帮助对话框补全 Ctrl+Shift+B/J/?/O 批量快捷键
- 运维中心批量导出区显示快捷键提示行

### 版本信息
- AppVersion 1.0.27；VersionCheck 远程示例 1.0.28

---

## v1.0.26 — 夜间 SinceDays 联动与批量一键导出快捷键（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.26_WinUI.zip`

### Core API
- 夜间脚本统计/批量导出自动附加 -SinceDays（取自 ActivityLogKeepDays）
- GlobalShortcutRegistry：Ctrl+Shift+O 导出批量 JSON+CSV

### GUI
- 设置页可禁用 Ctrl+Shift+O 批量一键导出快捷键

### 版本信息
- AppVersion 1.0.26；VersionCheck 远程示例 1.0.27

---

## v1.0.25 — 夜间批量一键导出、HTML 天数标注与 CSV 快捷键（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.25_WinUI.zip`

### Core API
- 夜间脚本启用统计导出时改用 -JobExportActivityLogBatchStatsAll（替代分步 JSON+CSV）
- ActivityLogStatsHtmlExportService 在报告 meta 中显示「筛选最近 N 天」
- GlobalShortcutRegistry：Ctrl+Shift+? 导出批量操作统计 CSV

### GUI
- 设置页可禁用 Ctrl+Shift+? 批量 CSV 快捷键
- HTML 报告与运维中心 SinceDays 筛选联动显示

### 版本信息
- AppVersion 1.0.25；VersionCheck 远程示例 1.0.26

---

## v1.0.24 — 批量统计 JSON+CSV 一键导出（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.24_WinUI.zip`

### Core API
- ActivityLogBatchSummaryExportService.ExportAll — 同时写入 JSON 与 CSV
- JobRunner.ExportActivityLogBatchStatsAll 复用 ExportAll 并记录活动日志

### GUI
- 运维中心「导出批量 JSON+CSV」主按钮；命令面板新增一键导出项
- -SinceDays 与运维中心活动日志天数筛选联动

### CLI
- -JobExportActivityLogBatchStatsAll [-SinceDays <N>] [-Json]

### 版本信息
- AppVersion 1.0.24；VersionCheck 远程示例 1.0.25

---

## v1.0.23 — 批量操作快捷键与夜间 CSV 导出（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.23_WinUI.zip`

### Core API
- GlobalShortcutRegistry：Ctrl+Shift+B 筛选批量日志、Ctrl+Shift+J 导出批量 JSON
- 夜间脚本在统计导出启用时追加 -JobExportActivityLogBatchStatsCsv

### GUI
- 设置页可单独禁用 Ctrl+Shift+B / Ctrl+Shift+J
- 运维中心快捷键说明已更新

### 版本信息
- AppVersion 1.0.23；VersionCheck 远程示例 1.0.24

---

## v1.0.22 — 批量统计 CSV、夜间 JSON 导出与条带点击筛选（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.22_WinUI.zip`

### Core API
- ActivityLogBatchSummaryCsvExportService — 批量操作统计 CSV 导出
- 夜间脚本在启用统计 CSV/HTML/双导出时自动 -JobExportActivityLogBatchStats

### GUI
- 批量操作条带行可点击，快速筛选对应分类 +「批量」关键词
- 导出批量 JSON / CSV 双按钮；命令面板新增批量筛选与导出

### CLI
- -JobExportActivityLogBatchStatsCsv [-SinceDays <N>] [-Path path.csv] [-Json]

### 版本信息
- AppVersion 1.0.22；VersionCheck 远程示例 1.0.23

---

## v1.0.21 — 批量统计 CLI/JSON 导出、HTML 柱状图与夜间 CSV+HTML 双导出（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.21_WinUI.zip`

### Core API
- -JobActivityLogBatchStats / ActivityLogBatchSummaryExportService 批量统计查询与 JSON 导出
- ActivityLogStatsHtmlExportService 批量操作 SVG 柱状图（batch-chart）
- StudioConfig.NightlyExportActivityLogStatsBoth 夜间脚本一键 CSV+HTML

### GUI
- 运维中心批量操作分类条带列表 +「导出批量操作统计 JSON」
- 设置页「同时导出活动日志统计 CSV 与 HTML」

### CLI
- -JobActivityLogBatchStats [-SinceDays <N>] [-Json]
- -JobExportActivityLogBatchStats [-SinceDays <N>] [-Path path.json] [-Json]

### 版本信息
- AppVersion 1.0.21；VersionCheck 远程示例 1.0.22

---

## v1.0.20 — 导入预览校验、HTML 批量统计区块与运维批量摘要（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.20_WinUI.zip`

### Core API
- ActivityLogBatchSummaryService — 按分类统计含「批量」关键词的活动日志
- ActivityLogStatsHtmlExportService 增加「批量操作统计」表格区块
- JobRunner.ImportMachineProfile 导入前自动执行 PreviewMachineProfile 校验

### GUI
- 运维中心活动日志区显示批量操作统计摘要行

### CLI
- -JobImportMachineProfile 在冲突时自动拒绝（与 Preview 一致）

### 版本信息
- AppVersion 1.0.20；VersionCheck 远程示例 1.0.21

---

## v1.0.19 — 活动日志批量筛选、搜索 CSV 导出与机器配置 HTML 主题（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.19_WinUI.zip`

### Core API
- ActivityLogCsvExportService 支持 -Query 关键词筛选
- ActivityLogBatchFilterService 批量操作快捷筛选常量
- MachineProfileExportResult.HtmlReportTheme 导出时携带主题信息

### GUI
- 运维中心「批量归档/删除/标签/恢复」快捷筛选按钮
- 导出 CSV 自动应用当前搜索关键词与分类筛选

### CLI
- -JobExportActivityLogCsv 增加 -Query 参数

### 版本信息
- AppVersion 1.0.19；VersionCheck 远程示例 1.0.20

---

## v1.0.18 — 机器配置导入预览与冲突检测（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.18_WinUI.zip`

### Core API
- MachineProfileService.PreviewMachineProfile — 导入前预览 studio/UI/快捷键
- 预览检测快捷键冲突并报告 HtmlReportTheme

### GUI
- 导入机器配置前弹出预览确认对话框

### CLI
- -JobPreviewMachineProfile -Path path.json [-Merge] [-Replace] [-Json]

### 版本信息
- AppVersion 1.0.18；VersionCheck 远程示例 1.0.19

---

## v1.0.17 — 夜间脚本自动导出 HTML 统计报告（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.17_WinUI.zip`

### Core API
- StudioConfig.NightlyExportActivityLogStatsHtml
- NightlyAutomationScriptService 增加 -JobExportActivityLogStatsHtml 步骤（主题跟随 UI 偏好）

### GUI
- 设置页「夜间全流程脚本自动导出活动日志统计 HTML」

### CLI
- nightly-full.bat 可选执行 -JobExportActivityLogStatsHtml

### 版本信息
- AppVersion 1.0.17；VersionCheck 远程示例 1.0.18

---

## v1.0.16 — 机器配置含快捷键、HTML 主题跟随偏好与批量操作日志增强（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.16_WinUI.zip`

### Core API
- MachineProfileBundle.ShortcutProfileJson — 导出/导入机器配置时一并携带快捷键映射与禁用列表
- UiPreferences.HtmlReportTheme — HTML 统计报告默认主题（light/dark）
- BatchActivityLogHelper + 批量归档/恢复/删除/标签日志附带任务 ID 样本

### GUI
- 设置页「活动日志 HTML 报告默认主题」下拉框
- 运维中心导出机器配置提示含快捷键统计

### CLI
- -JobExportMachineProfile 输出含快捷键映射/禁用计数

### 版本信息
- AppVersion 1.0.16；VersionCheck 远程示例 1.0.17

---

## v1.0.15 — 批量删除回收站模式、快捷键导入导出与 HTML 暗色主题（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.15_WinUI.zip`

### Core API
- JobFolderRemovalService + 批量/单条删除支持 useRecycleBin（AdCleanupService 回收站）
- GlobalShortcutProfileService.Export / Import（映射+禁用列表 JSON）
- ActivityLogStatsHtmlExportService 暗色主题切换（localStorage 记忆）

### GUI
- 删除/批量删除对话框三选一：仅删记录 / 永久删目录 / 目录移到回收站
- 设置页「导出快捷键」「导入快捷键」（保存前冲突检测）
- HTML 报告页「切换暗色主题」按钮

### CLI
- -JobDelete / -JobBatchDeleteJobIds 增加 -RecycleBin（需配合 -DeleteFolders）
- -JobExportShortcutProfile / -JobImportShortcutProfile

### 版本信息
- AppVersion 1.0.15；VersionCheck 远程示例 1.0.16

---

## v1.0.14 — 多选批量删除预览、HTML 打印导出 PDF 与快捷键冲突检测（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.14_WinUI.zip`

### Core API
- BatchDeleteJobIdsService.Preview / Delete — JobRunner.PreviewBatchDeleteJobIds / BatchDeleteJobIds
- ActivityLogStatsHtmlExportService 增加「打印/导出 PDF」按钮与 @media print 样式
- GlobalShortcutConflictService.FindConflicts 保存映射前冲突检测

### GUI
- 任务工作台多选 ≥2：「批量删除选中」+ Ctrl+Shift+D（预览后可选仅删记录/删记录+目录）
- 设置页自定义快捷键保存时检测冲突并阻止
- 命令面板：多选批量删除

### CLI
- -JobPreviewBatchDeleteJobIds -JobIds id1,id2 [-Json]
- -JobBatchDeleteJobIds -JobIds id1,id2 -Force [-DeleteFolders] [-Json]

### 版本信息
- AppVersion 1.0.14；VersionCheck 远程示例 1.0.15

---

## v1.0.13 — 多选批量恢复、统计 HTML 交互图表与快捷键自定义映射（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.13_WinUI.zip`

### Core API
- BatchUnarchiveJobIdsService.Unarchive — JobRunner.BatchUnarchiveJobIds(jobIds)
- ActivityLogStatsHtmlExportService 增加 SVG 柱状图与悬停 tooltip
- GlobalShortcutBindingService + UiPreferences.ShortcutOverrides 快捷键自定义

### GUI
- 任务工作台多选 ≥2：「批量恢复选中」+ Ctrl+Shift+U
- 设置页「自定义快捷键映射」对话框（覆盖默认组合键）
- 命令面板：多选批量恢复

### CLI
- -JobBatchUnarchiveJobIds -JobIds id1,id2 [-Json]

### 版本信息
- AppVersion 1.0.13；VersionCheck 远程示例 1.0.14

---

## v1.0.12 — 多选批量归档、统计 HTML 每日图表与单项快捷键禁用（Core + WinUI + CLI + Tests）

**发行包：** `PLCPak_v1.0.12_WinUI.zip`

### Core API
- BatchArchiveJobIdsService.Archive — JobRunner.BatchArchiveJobIds(jobIds)
- ActivityLogStatsHtmlExportService 增加近 7 天每日条数图表区块
- GlobalShortcutRegistry + UiPreferences.DisabledShortcuts 单项快捷键禁用

### GUI
- 任务工作台多选 ≥2：「批量归档选中」按钮 + Ctrl+Shift+G
- 设置页可单独禁用 Ctrl+Shift+A/G/T/H 等快捷键
- 命令面板：多选批量归档

### CLI
- -JobBatchArchiveJobIds -JobIds id1,id2 [-Json]

### 版本信息
- AppVersion 1.0.12；VersionCheck 远程示例 1.0.13

---

## v1.0.11 — 按任务 ID 批量标签与活动日志统计 HTML 导出（Core + CLI + Tests）

**发行包：** `PLCPak_v1.0.11_WinUI.zip`

### Core API
- BatchTagService.Apply(..., jobIds?) 已有；JobRunner.BatchApplyTags 透传 jobIds
- ActivityLogStatsHtmlExportService.Export
     JobRunner.ExportActivityLogStatsHtml(outputPath?, sinceDays?)

### CLI
- -JobBatchTagsJobIds -JobIds id1,id2 -Tags "a,b" [-Mode append] [-Json]
- -JobExportActivityLogStatsHtml [-SinceDays N] [-Path html] [-Json]

### 版本信息
- AppVersion 1.0.11；VersionCheck 远程示例 1.0.12；PrintHelp v1.0.11

---

## v10.10 — 活动日志归档筛选透传与批量置顶筛选（Core + WinUI + CLI + Tests）

### Core API
- ActivityLogService.ArchiveOlderThan(workspace, keepDays, category?, sinceDays?)
     与 Trim 一致支持分类/近 N 天范围筛选
- JobRunner.ArchiveActivityLog 透传 category/sinceDays（GUI 归档使用当前筛选）
- BatchPinFilteredService.Preview / BatchPinFilteredJobs(filter, search, tag, pin)
     按任务列表同款筛选批量置顶或取消置顶

### GUI
- 任务工作台「批量置顶」「批量取消置顶」按钮
- 运维中心活动日志分类统计改用 ActivityLogStatsService.BuildBarItems（Core）

### CLI
- -JobArchiveActivityLog [-KeepDays 30] [-Category 分类] [-SinceDays <N>] [-Json]
- -JobBatchPinFiltered -Pin true|false -Force [-Filter] [-Search] [-Tag] [-Json]

### 版本信息
- AppVersion 10.10.0；VersionCheck 远程示例 10.11.0；358+ 测试全通过

---

## v10.9 — 全局快捷键开关与活动日志每日统计 CSV 定稿（Core + WinUI + CLI + Tests）

### Core API
- UiPreferences.DisableGlobalShortcuts（bool，Studio 无关，存 ui-prefs.json）
- ActivityLogDailyStatsService.GetDailyCounts / ExportCsv
     JobRunner.ExportActivityLogDailyStatsCsv(days=7)
     按日历日聚合条数（近 N 天补零），CSV 列 date,count

### GUI
- 设置页「禁用全局快捷键」CheckBox
- MainWindow RootGrid_KeyDown 开头检查 prefs.DisableGlobalShortcuts
- 运维中心「近 7 天」每日条数条带 +「导出每日统计 CSV」按钮

### CLI
- -JobExportActivityLogDailyStatsCsv [-Days 7] [-Path csv] [-Json]
     默认 reports/activity-log-daily-stats-*.csv

### 版本信息
- AppVersion 10.9.0；VersionCheck 远程示例 10.10.0；测试全通过

---

## v10.8 — 活动日志日统计与筛选批量归档（Core + WinUI + CLI + README）

### Core API
- ActivityLogDailyStatsService.BuildDailyStats / ExportCsv
     按日历日聚合活动日志条数（近 N 天补零；GUI 可选分类筛选）
- MaintenanceService.PreviewBatchArchiveFiltered / BatchArchiveFiltered
     按任务列表同款 Filter / Search / Tag / Sort 预览与批量归档
- UiPreferences.DisableGlobalShortcuts（bool，存 ui-prefs.json）

### GUI
- 运维中心活动日志「近 7 天」每日条数条带 +「导出每日统计 CSV」
- 设置页「禁用全局快捷键」CheckBox

### CLI
- -JobActivityLogDailyStats [-Days 7] [-Json]
     查询活动日志每日条数；JSON 含 Days / TotalCount / Items（Date、Count）
- -JobExportActivityLogDailyStatsCsv [-Days 7] [-Path csv] [-Json]
     导出 date,count CSV；默认 reports/activity-log-daily-stats-*.csv
- -JobPreviewBatchArchiveFiltered [-Filter] [-Search] [-Tag] [-Sort] [-Json]
     预览筛选条件下可归档任务（TotalMatched / ArchivableCount / SampleTitles）
- -JobBatchArchiveFiltered -Force [-Filter] [-Search] [-Tag] [-Sort] [-Json]
     按筛选批量归档；跳过已归档项；需 -Force 确认
- PrintHelp 整理为 v10.8 版本号与说明

### 版本信息
- AppVersion 10.8.0

---

## 更早版本（v10.7 及以前）

| 版本 | 摘要 |
|------|------|
| v10.7 | 最后一轮抛光 |
| v10.6 | 最近活跃分类与筛选任务 JSON 导出 |
| v10.5 | 置顶任务 CSV 导出与运维快照 CLI |
| v10.4 | 活动日志归档 |
| v10.3 | 活动日志分类计数性能优化 |
| v10.2 | 选定任务 CSV 导出与夜间活动日志统计 |
| v10.1 | 活动日志统计 CSV 导出与筛选任务增强 |
| v10.0 | 活动日志统计与筛选任务 CSV 导出 |
| v9.9 | 看板快照与活动日志分页 |
| v9.8 | 批量合并预览导出与活动日志时间范围 |
| v9.7 | 活动日志 CSV 与批量合并预览 |
| v9.6 | 合并建议导出与活动日志分类清理 |
| v9.5 | 活动日志与合并建议增强 |
| v9.4 | 重复合并与快览增强 |
| v9.3 | 队列导出与夜间 TG 流水线 |
| v9.2 | 运维增强与 TG 预览 |
| v9.1 | TG 待发队列与网盘 CSV 批量导入 |
| v9.0 | 里程碑综合摘要 |
| v8.5 | 网盘链接解析、标签置顶与运维报告导出 |
| v8.4 | 运维中心与维护 CLI |
| v8.3 | 全量备份导入导出 |
| v8.2 | 批量自动链与 studio-config 导入导出 |
| v8.1 | 工作流自动链与日报 |
| v8.0 | 发布向导与版本检查 |
| v7.2 | 工作流执行与计划任务注册 |
| v7.1 | 发布工作流与定时批量 |
| v7.0 | 生产环境打磨 |
| v6.2 | 新增 |
| v6.1 | 新增 |
| v6.0 | 任务工作流 |

完整 CLI 参数与细节见 `winui/README.txt` 对应章节。

---

## 升级说明

1. 解压新版 ZIP 覆盖旧 dist 目录，或解压到新文件夹后迁移 `workspace` / `data`
2. 请保持 dist 完整（`PLCPak.WinUI`、`PLCPak.Cli`、`data`、`scripts`、`workspace`）
3. 推荐用 `PLCPak.WinUI\启动 PLCPak.bat` 启动
4. 若无法启动，查看 `logs\plcpak-startup.log`


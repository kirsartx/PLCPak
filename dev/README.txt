===============================================
    PLCPak v2.0.0（文件压缩打包工具）
===============================================

【简介】
支持 7z-zstd 和 tar.zst 双格式压缩，专为大文件批量打包设计。
✓ 预览 → 确认 → 清理 → 压缩 流水线（拖入不立刻删）
✓ 设置面板、任务状态、最近项目
✓ 并行哈希、扫描缓存、归档校验
✓ 完整 CLI（-Clean -Preview -Compress -NoGui）
✓ 广告哈希清单 + 样本库面板

【使用方法】
1. 解压到任意目录
2. 双击 PLCPak_v2.0.0.exe（或 .bat / .vbs / PLCPak.ps1）
3. 拖入或添加要压缩的文件夹（自动预览扫描）
4. 查看任务状态，点击「一键执行」：确认 → 清理 → 压缩
5. 可通过「设置」调整白名单、阈值、临时目录等

【CLI 示例】
  PLCPak.ps1 -Clean -Preview -Path "D:\游戏文件夹" -NoGui
  PLCPak.ps1 -Clean -Path "D:\游戏文件夹" -NoGui
  PLCPak.ps1 -Clean -Compress -Path "D:\游戏文件夹" -NoGui -Force

【配置文件 compress-config.json】
  VolumeSizeMB         分卷大小（默认 1900）
  AdScanTimeoutSec     广告扫描超时（默认 300 秒）
  AdConfirmThreshold   批量删除确认阈值
  UseRecycleBin        删除到回收站（true/false）
  PreviewBeforeClean   一键执行前预览确认（默认 true）
  ParallelHashWorkers  并行哈希线程数（默认 4）
  UseScanCache         扫描缓存（true/false）
  HashMaxFileMB        超过此大小仅文件名匹配时才算哈希
  CompressThreads      7z 线程数（0=自动）
  TempDir              tar.zst 临时目录（空=系统 TEMP）
  SkipStoreCompress    7z 存储模式 -ms=on
  Whitelist            白名单路径（支持通配符）
  ManifestStaleDays    清单过期提示天数

【文件结构】
PLCPak_v2.0.0.exe / .bat / .vbs
PLCPak.ps1                    主程序源码
PLCPak-AdEngine.ps1           广告清理引擎
PLCPak-Pipeline.ps1           流水线（预览/确认/清理）
PLCPak-Settings.ps1           设置对话框
Remove-AdFiles.ps1            界面增强 + 样本库
version.json                  版本与更新信息
UpdateManifest.ps1 / .bat
更新广告清单.bat
导出广告清单更新包.ps1        生成 ad-manifest-update.zip
7-Zip-Zstandard/
AD-Samples/（含 ad-manifest.json）
compress-config.json

【版本历史】
v2.0.0 (2026-06-24)  流水线重构、设置面板、完整CLI、并行哈希、扫描缓存、归档校验
v1.8.0 (2026-06-24)  广告引擎优化、清理预览、白名单、配置扩展、CLI、日志导出
v1.7.4 (2026-06-24)  PLCPak 命名与版本号显示
v1.7.3 (2026-06-24)  精简包体、广告哈希清单、新版 UI

===============================================
            Perfect Life CLUB · Niku
            更新日期: 2026-06-24
===============================================
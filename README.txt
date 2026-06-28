PLCPak 版本目录
================

根目录：F:\BaiduNetdiskDownload\PLCPak

子目录说明
----------
winui\      v3.0 WinUI 3 全量 C# 重写（.NET 10 + WASDK 2.2.0）
dev\        v2.0 PowerShell 版（保留参考）
releases\   已打包发布的 zip
v1.7.4\     v1.7.4 解压快照（只读参考）
v1.8.0\     v1.8.0 解压快照（只读参考）
v2.0.0\     v2.0.0 解压快照（只读参考）

后续更新约定
------------
1. 日常改代码、测试 → 只在 dev\ 里进行
2. 发新版本时：
   - 在 dev\ 编译/测试通过后，打包为 PLCPak_vX.Y.Z.zip
   - zip 放入 releases\
   - 可选：解压一份到 vX.Y.Z\ 作版本快照
3. 不要在外层 F:\BaiduNetdiskDownload\ 再散落 PLCPak 文件

当前状态
--------
- winui\dist\PLCPak.WinUI\   v3.0 GUI（主开发线）
- releases\PLCPak_v3.0.0_WinUI.zip
- releases\PLCPak_v2.0.0.zip  （旧 PowerShell 版）
- dev\                        v2.0 PowerShell 源码
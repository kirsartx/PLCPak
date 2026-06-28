# PLCPak

游戏资源发布工具：压缩、去广告、回填链接、TG 发送。当前主开发线为 **WinUI 3**（`.NET 10` + Windows App SDK）。

## 仓库结构

| 目录 | 说明 |
|------|------|
| `winui/` | WinUI 3 主程序（C#） |
| `dev/` | 共享 data 配置、脚本、样本（发布脚本会复制到 dist） |
| `releases/` | 发行说明与 `CHANGELOG.md`（ZIP 不进 git，走 GitHub Releases） |

## 本地开发

**环境：** Windows 10/11、.NET SDK 10（见 `winui/global.json`）、Windows App SDK 2.2

```powershell
cd winui
dotnet restore PLCPak.sln
dotnet test src/PLCPak.Core.Tests/PLCPak.Core.Tests.csproj -c Release
dotnet build src/PLCPak.WinUI/PLCPak.WinUI.csproj -c Release -p:Platform=x64
```

**打开发行包：**

```powershell
powershell -File winui/scripts/Publish-Release.ps1
```

输出：`winui/dist/` 与 `releases/PLCPak_v{版本}_WinUI.zip`

## 协作流程

1. 从 GitHub 克隆仓库
2. 新建分支 → 改代码 → 本地 `dotnet test` 通过
3. 提交 Pull Request 到 `main`
4. CI 会自动编译与跑测试（`.github/workflows/ci.yml`）
5. 合并后由维护者在 Actions 里手动跑 **Release** 工作流上传 ZIP

## 邀请协作者

仓库 **Settings → Collaborators → Add people**，对方接受邀请后即可 `git push`。

## 更新日志

完整记录见 [`releases/CHANGELOG.md`](releases/CHANGELOG.md)。
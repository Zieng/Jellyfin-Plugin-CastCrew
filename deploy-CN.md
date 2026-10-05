# CastCrew 2.0 部署说明

## 环境要求

- Jellyfin 10.10.x：.NET 8 / `net8.0`
- Jellyfin 10.11.x：.NET 9 / `net9.0`
- Git 2.30+
- 构建时需要访问 NuGet；启用元数据源后需要访问对应网站/API

## 安装 .NET SDK

Linux/macOS：

```bash
./scripts/install-dotnet.sh
export PATH="$HOME/.dotnet:$PATH"
```

Windows PowerShell：

```powershell
.\scripts\install-dotnet.ps1
$env:PATH="$HOME\.dotnet;$env:PATH"
```

## 编译和测试

```bash
./scripts/build.sh
```

Windows：

```powershell
.\scripts\build.ps1
```

## 创建插件安装包

```bash
./scripts/package.sh 2.0.0
```

会分别生成 Jellyfin 10.10.x 和 10.11.x 对应的插件 ZIP，并生成 SHA-256 校验文件。

## 安装插件

停止 Jellyfin，把对应版本 ZIP 解压到 Jellyfin plugins 目录，然后重新启动 Jellyfin。插件目录中 DLL 应位于目录根部。

## 配置元数据源

进入 **Dashboard → Plugins → CastCrew**。TMDB、MASex、Netflav 可以分别独立启用。启用 TMDB 时填写 Bearer Token。MASex 和 Netflav 的演员 URL 可配置，以便网站路径变化时无需修改代码。

演员元数据搜索支持：姓名、年龄、身高、胸围、腰围、臀围、影片数量；导演和片商/制作公司搜索支持名称搜索。

## Git 2.0 发布

本项目保留完整 `.git` 历史。需要把当前 `main` 发布为 `2.0` 分支并创建 `2.0` tag 时：

```bash
./scripts/release-push.sh
```

脚本不会保存 GitHub Token。建议使用 Git Credential Manager、SSH 或已认证的 GitHub CLI。

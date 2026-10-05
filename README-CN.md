# Jellyfin Plugin CastCrew 2.0

CastCrew 是 Jellyfin 的 Cast & Crew 插件。2.0 在保留原有本地演员/导演/片商浏览功能的基础上，增加可独立启用的外部元数据源和演员高级筛选。

## 2.0 功能

- Jellyfin 10.10.x / `net8.0`
- Jellyfin 10.11.x / `net9.0`
- TMDB、MASex、Netflav 元数据源独立开关
- TMDB 演员、导演、片商/公司搜索
- MASex 演员来源
- Netflav 演员来源
- 演员姓名搜索
- 年龄范围
- 身高范围
- 胸围范围
- 腰围范围
- 臀围范围
- 影片数量范围
- 多来源按姓名聚合和去重
- 中英文管理界面
- 原有 Jellyfin 本地 Cast & Crew API 保持兼容

## 配置

**Dashboard → Plugins → CastCrew**。

每个元数据源都可以单独启用。停用来源不会产生网络请求。TMDB 需要 Bearer Token；MASex/Netflav URL 可以在配置页修改。

## 元数据搜索

安装插件后进入 CastCrew Metadata 页面，可以在 Actor、Director、Studio/Manufacturer 三种类型之间切换。演员可以使用所有高级筛选；导演和片商/制作公司使用名称搜索。

API：

`GET /CastCrew/Metadata/Search`

主要参数：`type`、`name`、`minAge`、`maxAge`、`minHeight`、`maxHeight`、`minBust`、`maxBust`、`minWaist`、`maxWaist`、`minHip`、`maxHip`、`minVideoCount`、`maxVideoCount`、`startIndex`、`limit`。

## 构建

```bash
./scripts/install-dotnet.sh
./scripts/build.sh
./scripts/package.sh 2.0.0
```

Windows：

```powershell
.\scripts\install-dotnet.ps1
.\scripts\build.ps1
```

## Git

本项目发布包可以保留完整 `.git` 历史。发布 `2.0` 分支和 tag：

```bash
./scripts/release-push.sh
```

详见 [deploy-CN.md](deploy-CN.md)、[deploy.md](deploy.md)、[DESIGN.md](DESIGN.md)。

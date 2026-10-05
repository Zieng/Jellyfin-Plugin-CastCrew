# CastCrew 2.0 设计说明

## 架构

```text
Jellyfin Web
   │
   ├── CastCrew 本地页面/API
   │
   └── CastCrew Metadata 页面
          │
          └── /CastCrew/Metadata/Search
                    │
             MetadataSearchService
                    │
       ┌────────────┼────────────┐
       │            │            │
      TMDB        MASex       Netflav
       │            │            │
      JSON         HTML          HTML
```

## Provider 模型

所有外部来源实现 `IMetadataProvider`。Provider 不直接修改 Jellyfin 数据库，也不会覆盖原有 CastCrew 本地查询。

`MetadataSearchService` 根据配置决定调用哪些 Provider，并负责：

1. 并行/顺序来源聚合的统一入口；
2. 失败隔离；
3. 姓名标准化去重；
4. 最终范围过滤；
5. 分页。

## 数据模型

`MetadataPerson` 包含：

- Id
- Source
- Name
- ProfileUrl
- ImageUrl
- Gender
- Age
- HeightCm
- BustCm
- WaistCm
- HipCm
- VideoCount
- Country
- Overview
- ExternalId

并非每个来源都会提供全部字段；缺失字段保持 `null`。

## Jellyfin 兼容

原有：

- `/CastCrew/Actors`
- `/CastCrew/Directors`
- `/CastCrew/Producers`
- `/CastCrew/Libraries`

保持不变。

新增：

- `/CastCrew/Metadata/Search`

项目继续同时构建 `net8.0` 和 `net9.0`，分别对应 Jellyfin 10.10.x 和 10.11.x。

## 外部站点策略

TMDB 使用官方 JSON API。MASex 和 Netflav 当前没有作为本插件依赖引入额外 HTML 解析库，因此使用轻量级 HTML/文本提取器。由于第三方页面结构可能变化，URL 可配置，解析失败不会阻止其他 Provider。

## 安全

TMDB Token 存在 Jellyfin 插件配置中，不写入 Git。发布脚本不接受 Token 参数，也不把认证信息写入源码。

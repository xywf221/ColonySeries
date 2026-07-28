# ColonySeries — RimWorld 1.6 工程级模组 monorepo

一套**有代价、可设置、轻松模式、事件驱动**的殖民地模组。  
气质：维护循环 + 玩家动词（赶工 / 预支 / 接灰单），**不做心情税农场**。

> **源码真相**在本仓库 `mods/`。游戏加载路径仍是 `<RimWorld>/Mods/<Name>/`，用 deploy 同步。

| | |
|--|--|
| 目标游戏 | RimWorld **1.6** |
| 包数量 | **14**（见 [`catalog.json`](catalog.json)） |
| 作者品牌 | `ColonySeries` |
| 构建 | `Directory.Build.props` + identity-only csproj |
| 校验 | `scripts/validate-mods` → 0 error 为门禁 |

---

## 30 秒上手

```bash
# 推荐：把本仓库放在游戏根旁
#   RimWorld-v1.6.4850/
#   ├── RimWorldWin64_Data/
#   ├── Mods/
#   └── ColonySeries/     ← 这里

cd ColonySeries
./scripts/validate-mods.sh
./scripts/build-all.sh
./scripts/deploy.sh
```

游戏不在同级时：

```bash
export RIMWORLD_DIR="D:/Games/RimWorld"
./scripts/build-all.sh
./scripts/deploy.sh "$RIMWORLD_DIR/Mods"
```

---

## 仓库布局

```
ColonySeries/
├── Directory.Build.props / .targets   ← 共享 TFM、RimWorldDir、Harmony、引用
├── catalog.json                       ← 机读包清单（sync-catalog 生成）
├── CONTRIBUTING.md
├── CHANGELOG.md
├── docs/
│   ├── ENGINEERING.md                 ← 构建契约
│   ├── REPO_LAYOUT.md
│   ├── design/                        ← 可玩性评审 + 状态表
│   └── archive/NOT_SHIPPED.md
├── mods/<Name>/                       ← 一文件夹 = 一个 packageId
│   ├── About/  LoadFolders.xml  README.md
│   └── 1.6/{Assemblies,Defs,Languages,Source}
└── scripts/
    ├── build-all / deploy / validate-mods
    ├── new-mod / sync-catalog / list-mods
```

工程细节：[`docs/ENGINEERING.md`](docs/ENGINEERING.md)  
设计权威：[`docs/design/mod-design-review.md`](docs/design/mod-design-review.md)

---

## 模组一览

### 既有系列
| 模组 | packageId |
|------|-----------|
| 垦壤 Landworks | `landworks.terraforming` |
| 田作 Fieldcraft | `fieldcraft.soilbudget` |
| PersonalKit | `personal.kit` |
| Trait Extractor | `landworks.traitextractor` |

### S / A
| 模组 | packageId |
|------|-----------|
| Dead Drop | `deaddrop.greymarket` |
| Artisan Mark | `artisanmark.maker` |
| Workshop Wear | `workshopwear.maintenance` |
| Rumor Mill | `rumormill.reputation` |
| Ledger | `ledger.colonybooks` |

### B / C
| 模组 | packageId |
|------|-----------|
| Apprentice Bond | `apprenticebond.mentor` |
| Quarantine Line | `quarantineline.isolation` |
| Salvage Atlas | `salvageatlas.reverse` |
| Watch Posts | `watchroster.nightwatch` |
| Ford Rights | `fordrights.river` |

完整字段见 `catalog.json`。D 档不独立包见 `docs/archive/NOT_SHIPPED.md`。

---

## 构建契约（摘要）

每个 `mods/Foo/1.6/Source/Foo/Foo.csproj` **只写身份**：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Foo</AssemblyName>
    <RootNamespace>Foo</RootNamespace>
    <ColonySeriesNeedsHarmony>true</ColonySeriesNeedsHarmony>
  </PropertyGroup>
</Project>
```

- `TargetFramework=net472`、输出到 `1.6/Assemblies/`、游戏程序集引用 → `Directory.Build.*`
- `ColonySeriesNeedsHarmony=false` 仅用于无 Harmony 代码的包（PersonalKit、TraitExtractor）
- RimWorldDir 解析：`-p` / `RIMWORLD_DIR` / 同级游戏根 / 部署后的四级上溯

---

## 常用脚本

| 命令 | 作用 |
|------|------|
| `./scripts/validate-mods.sh` | 结构 + 公约门禁 |
| `./scripts/build-all.sh` | Release 编译全部 |
| `./scripts/deploy.sh` | 镜像到 `../Mods` |
| `./scripts/new-mod.sh Name id.pkg` | 脚手架新包 |
| `./scripts/sync-catalog.sh` | 重写 `catalog.json` |
| `./scripts/list-mods.sh` | 打印 packageId 表 |

PowerShell 孪生：`build-all.ps1` / `deploy.ps1` / `validate-mods.ps1`。

---

## 设计公约

1. 玩家主动决策  
2. 决策改变局面，失败可读  
3. 默认设置不与原版双重征税  
4. 总开关 + 轻松模式  
5. 事件/有界脉冲，禁止热路径 `map.AllCells`  
6. soft-link，不硬绑全系列  
7. 中英 Keyed  
8. 有意偏离写 `DEVIATIONS.md`  

---

## 许可

见 `LICENSE`（发布前请换成你选定的正式许可证）。  
Harmony / RimWorld 本体遵循各自条款；本仓库不附带游戏程序集。

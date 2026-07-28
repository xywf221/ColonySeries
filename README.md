# ColonySeries — RimWorld 1.6 模组系列仓库

一套**有代价、可设置、轻松模式、事件驱动**的殖民地模组。  
气质：维护循环 + 玩家动词（赶工 / 预支 / 接灰单），**不做心情税农场**。

> 本仓库是 **源码 monorepo**。游戏实际加载路径仍是  
> `<RimWorld>/Mods/<ModName>/`  
> 用 `scripts/deploy.ps1` / `scripts/deploy.sh` 同步过去。

---

## 仓库布局

```
ColonySeries/
├── README.md                 ← 本文件
├── LICENSE                   ← 占位（按需改）
├── .gitignore
├── docs/
│   ├── design/
│   │   ├── mod-design-review.md   ← 可玩性权威评审
│   │   └── todo.md                ← 实现状态表
│   └── archive/
│       └── NOT_SHIPPED.md         ← D 档不独立包说明
├── mods/                     ← 每个子目录 = 一个可加载 mod
│   ├── Landworks/
│   ├── Fieldcraft/
│   ├── PersonalKit/
│   ├── TraitExtractor/
│   ├── DeadDrop/
│   ├── ArtisanMark/
│   ├── WorkshopWear/
│   ├── RumorMill/
│   ├── Ledger/
│   ├── QuarantineLine/
│   ├── SalvageAtlas/
│   ├── ApprenticeBond/
│   ├── FordRights/
│   └── WatchRoster/
├── scripts/
│   ├── build-all.sh / .ps1        ← 编译全部
│   ├── deploy.sh / .ps1           ← 同步到游戏 Mods/
│   └── list-mods.sh
└── tools/                    ← 可选脚手架 / 贴图工具（空可放）
```

### 每个 `mods/<Name>/` 内部（标准 RimWorld 包）

```
About/                 packageId、描述
LoadFolders.xml
README.md
DEVIATIONS.md          （若有设计偏差）
1.6/
  Assemblies/*.dll
  Defs/ …
  Languages/English|ChineseSimplified/…
  Source/<Name>/*.csproj + *.cs
Textures/              （部分包）
```

**不要**把整个 `RimWorld/Mods`（含 Steam 数字 ID 工坊包）推进本仓库。

---

## 模组一览（14）

### 既有系列
| 模组 | packageId | 定位 |
|------|-----------|------|
| 垦壤 Landworks | `landworks.terraforming` | 有代价地形改造 |
| 田作 Fieldcraft | `fieldcraft.soilbudget` | 地力维护 |
| PersonalKit | `personal.kit` | 个人 QoL / 谈价 |
| Trait Extractor | `landworks.traitextractor` | 特性提取与血清 |

### S / A
| 模组 | packageId | 定位 |
|------|-----------|------|
| Dead Drop | `deaddrop.greymarket` | 边缘灰单 |
| Artisan Mark | `artisanmark.maker` | 匠人署名叙事 |
| Workshop Wear | `workshopwear.maintenance` | 工坊赶工/磨损 |
| Rumor Mill | `rumormill.reputation` | 事迹口碑 |
| Ledger | `ledger.colonybooks` | 信用预支杠杆 |

### B / C
| 模组 | packageId | 定位 |
|------|-----------|------|
| Apprentice Bond | `apprenticebond.mentor` | 1:1 学徒 XP |
| Quarantine Line | `quarantineline.isolation` | 有病才醒的检疫 |
| Salvage Atlas | `salvageatlas.reverse` | 械战残骸图谱 |
| Watch Posts | `watchroster.nightwatch` | 岗哨预警 |
| Ford Rights | `fordrights.river` | 有河渡口彩蛋 |

设计权威：[`docs/design/mod-design-review.md`](docs/design/mod-design-review.md)

---

## 依赖

- RimWorld **1.6**
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)（`brrainz.harmony`）
- 编译：.NET SDK（`net472` 目标），引用游戏 `Assembly-CSharp` + Harmony

各 csproj 可移植。仓库根有 `Directory.Build.props`：

- 若 `ColonySeries/` 与游戏根**同级**（推荐），自动找到 `../RimWorldWin64_Data`
- 或设 `RIMWORLD_DIR` / `-p:RimWorldDir=...`（路径会自动补尾斜杠）

```bash
# 通常直接：
dotnet build mods/DeadDrop/1.6/Source/DeadDrop/DeadDrop.csproj -c Release

# 游戏不在同级时：
export RIMWORLD_DIR="D:/Games/RimWorld"
./scripts/build-all.sh
```

Deploy 回 `Mods/<Name>/` 后，旧的「向上四级」相对路径仍可用。

---

## 常用命令

### 编译全部

```bash
# Git Bash / WSL
./scripts/build-all.sh

# PowerShell
./scripts/build-all.ps1
```

### 部署到游戏 Mods 目录

默认目标：`../Mods`（即与 `ColonySeries` 同级的游戏 `Mods/`）。

```bash
./scripts/deploy.sh
# 或
./scripts/deploy.ps1 -GameModsDir "E:/RimWorld-v1.6.4850/Mods"
```

部署为 **镜像同步**（robocopy/rsync 风格）：用仓库 `mods/<Name>` 覆盖游戏侧同名文件夹。  
**不会**动 Steam 数字 ID 目录。

### 开发工作流（推荐）

1. 在本仓库改 `mods/Foo/...`
2. `build-all` 或单包 `dotnet build`
3. `deploy` → 游戏 `Mods/Foo`
4. 重启 / 热更读档测试

也可把 `ColonySeries/mods` **junction/symlink** 进游戏 Mods（高级，Windows 需管理员或开发者模式）。

---

## 设计公约（摘要）

1. 玩家有主动决策（按钮 / 指定 / 接单）  
2. 决策改变局面，失败可读  
3. 不与原版双重征税（默认设置）  
4. 总开关 + 轻松模式  
5. 事件/有界脉冲，禁止 `map.AllCells` 主路径  
6. soft-link，不硬依赖兄弟包  
7. 中英 Keyed + 必要 DefInject  
8. 各包 README；有意偏离写 DEVIATIONS  

明确不做：免费超级装备、无代价传送、暗扣银、暗开门、只报表无决策。

---

## Git

```bash
cd ColonySeries
git status
```

建议 remote 自建（GitHub/Gitea）。  
**提交 DLL**：当前选择 **提交已编译 DLL**，方便不编译直接部署；若只想要源码，从 `.gitignore` 取消 Assemblies 注释并删掉已跟踪 dll。

---

## 版本

- 目标游戏：RimWorld 1.6.x  
- 系列文档日期：2026-07-28  
- Ledger v2 预支入口修复：同日后续提交

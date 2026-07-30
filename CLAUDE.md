# ColonySeries — Claude / agent 工程公约

本文件约束在本仓库内工作的 Claude Code 与其它代理。  
**先读完再改代码。** 更细的构建契约见 `docs/ENGINEERING.md`，可玩性权威见 `docs/design/mod-design-review.md`。

---

## 0. 仓库是什么

- **RimWorld 1.6 模组 monorepo**，不是游戏安装目录，也不是 Steam 工坊镜像。
- 源码真相：`mods/<Name>/`
- 游戏加载：`<RimWorld>/Mods/<Name>/`（用 `scripts/deploy` 同步）
- 当前 **14** 个可加载包；清单：`catalog.json`
- 作者品牌：`ColonySeries`
- 目标游戏：**仅 1.6**（未接 1.5/1.4 分支）

拓扑（推荐）：

```
<RimWorld>/
  RimWorldWin64_Data/
  Mods/                 ← deploy 目标；含 Steam 数字 ID（不要进 git）
  ColonySeries/         ← 本仓库
```

---

## 1. 绝对不要做

| 禁止 | 原因 |
|------|------|
| 把整个 `RimWorld/Mods` 或 Steam 数字 ID 目录塞进本仓库 | 体积/许可/diff 灾难 |
| 在 csproj 里再写 `TargetFramework` / 游戏 `Reference` / `OutputPath` / 完整 portable 模板 | 由 `Directory.Build.*` 统一；复制旧模板 = 回归 |
| 热路径 `map.AllCells` / 无界全图扫 | 系列性能公约；`validate-mods` 会报错 |
| 暗扣银、暗开门、抬高袭击点数、永久全局伤害树 | 设计红线（公平/可读失败） |
| 无代价传送、免费超级装备、全自动无维护农场 | 设计「不做」列表 |
| 把 DreamDebt / LogisticsCold 重新做成独立包 | D 档；见 `docs/archive/NOT_SHIPPED.md` |
| 硬 `modDependencies` 绑死全系列 | 只用 soft-link（`GetNamedSilentFail` 等） |
| 提交 `obj/`、`bin/`、`_decomp/`、游戏 exe/Managed | `.gitignore` 已挡；不要 force add |
| 批量生成一堆半成品 mod「清清单」 | 用户已明确：可玩性第一，先设计再实现 |
| 在 XML 注释里写 `--` 或尾部单独 `-` | 会弄坏 MSBuild / XML 解析（已踩坑） |

---

## 2. 设计五关（改玩法或新包必过）

1. **玩家有主动决策吗？**（按钮 / 指定 / 接单，不是只有被动 debuff）
2. **决策能改变局面吗？**（可感知，不是 ±1% 空气）
3. **失败公平可读吗？**（信/消息/检视，不藏第三方日志）
4. **会不会和原版双重征税？**（默认设置下：饿+储备心情、困+精神税等）
5. **关掉 / 轻松模式后还像 RimWorld 吗？**

每个正式系统还要有：

- 玩家动词 · 资源/风险代价 · 成功反馈 · 失败反馈  
- 设置：强度 + **总开关** + **轻松模式**  
- 休眠条件（无河/无病/无单 ≈ 0 工作）  
- 中英 Keyed（+ 必要 DefInject）  
- 有意偏离 → 该包 `DEVIATIONS.md`

权威文档：`docs/design/mod-design-review.md`（不要凭记忆改平衡哲学）。

---

## 3. 目录与包边界

```
mods/<Name>/
  About/About.xml          author=ColonySeries；唯一 packageId；supportedVersions 含 1.6
  About/Manifest.xml       建议有
  LoadFolders.xml          v1.6 → / + 1.6
  README.md                必有
  DEVIATIONS.md            有偏离才写
  1.6/Assemblies/<Name>.dll
  1.6/Defs|Languages|Source/<Name>/
  Textures/                 按需
```

- **一个文件夹 = 一个 `packageId` = 模组列表一行**
- 文件夹名 = `AssemblyName` = 主 DLL 名
- 新包：`./scripts/new-mod.sh FolderName package.id ["显示名"] [true|false]`
- 改 About 后跑：`./scripts/sync-catalog.sh`

---

## 4. 构建公约（MSBuild）

### csproj 只允许身份

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>Foo</AssemblyName>
    <RootNamespace>Foo</RootNamespace>
    <ColonySeriesNeedsHarmony>true</ColonySeriesNeedsHarmony>
  </PropertyGroup>
</Project>
```

- `ColonySeriesNeedsHarmony=false` **仅当** 源码零 Harmony（现：TraitExtractor）
- 共享：`Directory.Build.props` + `Directory.Build.targets`
  - TFM `net472`，`LangVersion` 12，输出 `1.6/Assemblies/`
  - 引用 `Assembly-CSharp` + Unity 模块；按需 `0Harmony`
- **RimWorldDir** 顺序：`-p:RimWorldDir` → `RIMWORLD_DIR` → 同级游戏根 → 部署后四级上溯  
  尾斜杠由 props 规范化；缺程序集应失败并打印路径
- **不要**往 Assemblies 里拷 `0Harmony.dll`（运行时用玩家的 Harmony mod）

### 常用命令

```bash
./scripts/validate-mods.sh    # 结构/公约门禁；改包后先跑
./scripts/build-all.sh        # 全量 Release
./scripts/deploy.sh           # 镜像到 ../Mods（不动 Steam 数字目录）
./scripts/sync-catalog.sh     # 重写 catalog.json
./scripts/list-mods.sh

# 单包
dotnet build mods/DeadDrop/1.6/Source/DeadDrop/DeadDrop.csproj -c Release
```

游戏不在同级：

```bash
export RIMWORLD_DIR="D:/Games/RimWorld"
```

改完 C#：**编译被改的包**（或 build-all），确认 0 error 再收工。  
只改 XML/Keyed 可不编译，但仍建议 `validate-mods`。

---

## 5. 代码口味（RimWorld 1.6）

- 事件驱动 / 有界脉冲 / `listerThings` / 跟踪集合；MapComponent 无事早退
- Harmony：补丁面尽量小；API 不确定时用 `AccessTools` + 存在性检查，避免整包炸加载
- 设置：`ModSettings` + 总开关；轻松模式软化惩罚，不删核心动词
- 本地化：玩家可见字符串走 Keyed/DefInject，不硬编码中文在 C# 里（开发用 Message 可例外）
- 命名：命名空间 = 文件夹名；Def 前缀用包缩写（如 `DD_` `WR_` `LD_`）避免冲突
- 存档：`IExposable` / Scribe 字段变更要可迁移或有默认值
- 读反编译仅作参考，放 `**/_decomp/`（已 gitignore），**永不**编进 csproj

### 已知坑（本系列已踩）

- `TexCommand` 字段因版本而异 → 用已知存在的图标或 `ContentFinder`
- `IncidentParms` 不要假设自定义字段；拷贝要稳或挂起原引用
- 原版 `TradeDeal` **不能**银不够成交 → 经济杠杆用玩家动词（如 Ledger 预支），不要事后 `spent > start` 开债
- XML/MSBuild 注释禁止 `--`
- monorepo 下旧 csproj「向上四级」会落到 `ColonySeries` 而非游戏根 → 必须靠 `Directory.Build.props`

---

## 6. 工作流（代理默认）

1. **定位**：`catalog.json` / `scripts/list-mods.sh` / `rg` 在 `mods/` 下搜  
2. **改设计向内容前**：打开 `docs/design/mod-design-review.md` 对应章节  
3. **实现**：只动相关 `mods/<Name>/`；共享构建只改根 `Directory.Build.*`  
4. **有意偏离设计**：更新该包 `DEVIATIONS.md` + 必要时 `docs/design/todo.md`  
5. **验证**：
   ```bash
   ./scripts/validate-mods.sh
   dotnet build mods/<Name>/1.6/Source/<Name>/<Name>.csproj -c Release
   ```
6. **部署试玩**（用户要实机时）：`./scripts/deploy.sh`  
7. **提交**（仅当用户要求 commit）：在 `ColonySeries/` 下 git；message 用英文或中英清晰句；不要提交游戏本体

### 并行与范围

- 多包独立改动可并行；**共享 props/脚本/设计文档** 避免多代理同时写
- 不要擅自扩 scope 到「再做 12 个新 mod」；新包必须先过设计五关并问用户
- 用户说「继续」且上下文是工程/修复时：修门禁与已知断点，不要重新 bulk 脚手架

---

## 7. 文档何时更新

| 变更 | 更新 |
|------|------|
| 新包 / 删包 / packageId | `catalog.json`（跑 sync-catalog）、README 表、设计 todo |
| 构建系统 | `docs/ENGINEERING.md` + 本文件相关节 |
| 玩法偏离设计 | 包内 `DEVIATIONS.md` |
| 用户可见版本故事 | `CHANGELOG.md` |
| 协作流程 | `CONTRIBUTING.md` |

不要把长设计论文写进 About.xml；About 保持玩家向短描述。

---

## 8. 安全与权限

- 本仓库可写；**不要**删除或改写 `../Mods/<数字ID>/` 工坊内容
- `deploy` 只镜像**本系列具名文件夹**
- 不提交密钥、本地存档、`RIMWORLD_DIR.local`
- 不要求、不生成对他人服务器或账号的攻击性内容

---

## 9. 一句话

> **可玩性第一，工程可重复第二：身份-only csproj、验证再编译、设计五关、soft-link、不暗坑玩家。**

不确定时：先读 `docs/ENGINEERING.md` 与对应包 README/DEVIATIONS，再改。

# Monorepo 布局说明

## 为什么不直接 git 整个 `RimWorld/Mods`？

游戏 `Mods/` 里混着：

- Steam 工坊数字 ID 目录（上百个第三方包）
- 本系列 14 个具名包
- 可能的存档配置、本地覆盖

把整棵塞进 git 会：体积爆炸、许可混乱、diff 不可读、误提交他人 DLL。

## 推荐拓扑

```
E:/RimWorld-v1.6.4850/          ← 游戏根（不进 git）
├── RimWorldWin64_Data/
├── Mods/                       ← 游戏加载处
│   ├── 2009463077/             ← Harmony 等工坊（不进 git）
│   ├── Landworks/              ← deploy 同步目标
│   ├── DeadDrop/
│   └── …
└── ColonySeries/               ← 本 git 仓库
    ├── docs/
    ├── mods/Landworks/…        ← 源码真相
    └── scripts/deploy.*
```

## 两种开发姿势

### A. 编辑仓库 → deploy（默认、最安全）

1. 改 `ColonySeries/mods/Foo`
2. `scripts/build-all.*`
3. `scripts/deploy.*` 覆盖 `Mods/Foo`
4. 进游戏测

### B. 目录联接（少一步复制）

管理员 / 开发者模式下：

```bat
mklink /J "E:\RimWorld-v1.6.4850\Mods\DeadDrop" "E:\RimWorld-v1.6.4850\ColonySeries\mods\DeadDrop"
```

注意：联接后游戏与 git 共用同一文件夹；`obj/` 仍应被 gitignore。

## csproj 与路径

历史 csproj 假设：

```
<RimWorld>/Mods/<Name>/1.6/Source/<Name>/<Name>.csproj
→ 向上四级 = RimWorld 根
```

在 monorepo 中变为：

```
ColonySeries/mods/<Name>/1.6/Source/<Name>/
→ 向上四级 = ColonySeries，不是游戏根
```

因此 **从 monorepo 编译必须**：

```bash
dotnet build -p:RimWorldDir="E:/RimWorld-v1.6.4850"
```

`scripts/build-all.*` 已自动处理。

Deploy 之后若在 `Mods/<Name>/...` 下直接 build，旧相对路径仍可用。

## 包边界

- **一个文件夹 = 一个 `packageId` = 模组列表一行**
- soft-link 用 `GetNamedSilentFail` / 可选依赖，禁止强 `modDependencies` 绑死全系列
- 系列文档在 `docs/`，不进单个 mod 的 About（About 保持玩家向短描述）

## 不入库

见 `docs/archive/NOT_SHIPPED.md`（DreamDebt / LogisticsCold）。

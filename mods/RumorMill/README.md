# 流言口碑 Rumor Mill

RimWorld 1.6。**事迹驱动**的轻量口碑标签。与 [Trait Extractor](../TraitExtractor) **软联动**（可选）。

## 理念

> 标签来自事迹，不是 RNG 贴条。玩家能表彰、能压闲话；关模组后仍是 RimWorld。

不做每小时全员传播扫描，不直接用标签触发精神崩溃。

## 启用

1. **Harmony**
2. 模组列表启用 **流言口碑 Rumor Mill**
3. 可选 **Trait Extractor**（「被抽过神经」污名）

路径：`Mods/RumorMill`

## 标签（最多 2 个/人）

| 标签 | 事迹 | 效果倾向 |
|------|------|----------|
| 可靠 Reliable | 多次成功护理（阈值） | 招募↑ 贸易↑ 社交↑ |
| 手黑 Bloody hands | 处决、器官/违规切除（阈值） | 招募↓ 贸易↓ 社交↓（嗜血者略↑） |
| 怕血 Squeamish | 目睹人类死亡（阈值，轻） | 社交略↓ 招募微↓ |
| 被抽过神经 Nerved | TE 神经创伤/疤（soft-link） | 贸易↓ 社交↓ |
| 战英雄 War hero | 袭击语境击杀（阈值） | 招募↑ 社交↑ |
| 逃兵 Deserter | 威胁下恐慌逃跑（阈值） | 招募↓ 社交↓ |

**抠门 Stingy**：跳过（难以公平检测，见 `DEVIATIONS.md`）。

**衰减**：默认约 45 天无强化则降级/消失（设置 15～90）。

## 玩家动词

- **表彰 Commend**（殖民者 Gizmo）：发言人走过去说好话  
  - 优先削弱一档负面流言  
  - 若无负面则强化「可靠」  
  - 双方短心情
- **闲聊**：原版 Chitchat / DeepTalk / KindWords 有小概率强化已知流言时间戳（延迟衰减）

## 设置

选项 → 模组设置 → 流言口碑：

- 总开关、轻松模式
- 衰减天数、招募/贸易/社交幅度
- 表彰 / 闲聊开关
- 各事迹挂钩开关

## 性能

- `GameComponent` 字典存 pawn→tags
- **每日**衰减检查；TE 疤 **隔日**只扫玩家主地图人类
- Harmony **事迹 postfix**，无 `AllCells`、无每小时全员 mesh

## 编译

```bash
dotnet build "E:\RimWorld-v1.6.4850\Mods\RumorMill\1.6\Source\RumorMill\RumorMill.csproj" -c Release
```

`RimWorldDir` / `RIMWORLD_DIR` 可移植。输出：`1.6/Assemblies/RumorMill.dll`

## 兼容

- Harmony 必需
- Soft-link Trait Extractor（hediff defName）
- 不抢原版特质的精神崩溃职责

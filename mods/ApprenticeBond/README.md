# 学徒契约 Apprentice Bond

RimWorld 1.6。玩家手动指定 **1 导师 + 1 学徒 + 1 技能**；同房间工作时分享 XP，带**每日硬帽**。

## 理念

> 中前期双人成长，导师阵亡有创伤。不是双倍升级外挂，也不是多对多师徒网。

## 启用

1. **Harmony**
2. 模组列表启用 **学徒契约 Apprentice Bond**

路径：`Mods/ApprenticeBond`

## 玩法

1. 选中殖民者 → **订立学徒契约** → 点选学徒 → 选择一项导师更高的技能。
2. 导师在该技能获得 XP 时，若学徒**同地图、同房间**（室外则近距），学徒获得本次 XP 的 **15%～25%**（默认 20%），导师另得 **5%** 教学加成。
3. 学徒每日从契约获得的 XP 有**硬帽**（默认 2000，可调）。
4. 绑定双方 **+2** 心情；玩家拆约临时 **−3**；导师死亡学徒 **−8～−12**（默认 −10，衰减）；出师双方小 buff。
5. 学徒技能 ≥ 导师 −2 **或** 满设定天数 → 可 **出师**（提示 + Gizmo）。
6. 设置内可 **一键解散全部**；殖民地人数超阈值会轻提示（偏中前期工具）。

**不做**：全局光环、跨地图传 XP、一人多徒/多师网。

## 设置

选项 → 模组设置 → 学徒契约：

| 项 | 默认 |
|----|------|
| 总开关 / 轻松模式 | 开 / 关 |
| 学徒分享 | 20%（滑条 15–25） |
| 导师教学 | 5% |
| 日帽 | 2000 |
| 出师天数 / 等级差 | 30 天 / 2 级 |
| 心情 | 绑定 +2；拆约 −3；导师死 −10；出师 +4 |
| 人数提示阈值 | 12 |

## 性能

- `GameComponent` 存少量契约条目（每人最多一条）。
- XP 只在 `SkillRecord.Learn` postfix + re-entry 守卫中处理。
- 约每 6 小时脉冲清理/出师提示，无全图 mesh。

## 编译

```bash
dotnet build "E:\RimWorld-v1.6.4850\Mods\ApprenticeBond\1.6\Source\ApprenticeBond\ApprenticeBond.csproj" -c Release
```

`RimWorldDir` 默认为 csproj 上溯五级；可用 `-p:RimWorldDir=...` 或 `RIMWORLD_DIR`。  
输出：`Mods/ApprenticeBond/1.6/Assemblies/ApprenticeBond.dll`

## 设计依据

见 `Mods/mod-design-review.md` §2.6。与设计稿差异见 `DEVIATIONS.md`。

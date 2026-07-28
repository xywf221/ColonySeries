# 边防岗哨 Watch Posts

RimWorld 1.6。由「边防更鼓」**降级**为岗哨建筑：夜间有人站岗 → 更好的预警；空岗只减弱火灾察觉。

## 理念

> 岗位，不是 HR 排班表。  
> 有人值更有回报；空岗的代价必须**可读且公平**——绝不偷偷开门，绝不抬高袭击点数。

## 启用

1. **Harmony**
2. 模组列表启用 **边防岗哨 Watch Posts**

路径：`Mods/WatchRoster`

## 玩法

1. 在 **Security** 分类建造 **岗哨 / watch post**（默认最多 **3** 座，可设）。
2. 夜间（默认 20:00–05:00），**Hunting** 工种殖民者会自动去空岗站岗；也可右键强制。
3. **有人值更**
   - 敌对袭击到来前发出 **预警信件**，实际袭击延迟约 **0.5～1.5 小时**（袭击点数**不变**）。
   - `FireWatcher` 观察更快 → 火灾危险评估更灵敏。
4. **空岗（夜间有岗哨但无人）**
   - **仅**减慢火灾观察节奏（发现更慢）。
   - **绝不**自动开锁/开门。
   - **绝不**提高袭击强度/点数。
5. 白天默认不站岗（可在设置里允许白天值更）。

## 设置

选项 → 模组设置 → 边防岗哨：

| 项 | 默认 |
|----|------|
| 总开关 | 开 |
| 岗哨上限 | 3 |
| 夜间时段 | 20–5 |
| 预警小时 | 0.5–1.5 |
| 火灾倍率（有人/空岗） | 0.45 / 1.6 |
| 袭击预警 / 火灾节奏 | 开 |
| 白天站岗 | 关 |
| 轻松模式 | 关（预警更长；空岗不再拖慢火灾） |

## 性能

- 岗哨列表走 `listerThings.ThingsOfDef`，**不**扫 `AllCells`。
- Harmony 仅贴：`IncidentWorker_RaidEnemy.TryExecuteWorker`、`FireWatcher.FireWatcherTick`。
- 无全员更鼓表、无每 tick 全图门扫描。

## 编译

```bash
dotnet build "E:\RimWorld-v1.6.4850\Mods\WatchRoster\1.6\Source\WatchRoster\WatchRoster.csproj" -c Release
```

可移植：`RimWorldDir` / `RIMWORLD_DIR`。输出：`Mods/WatchRoster/1.6/Assemblies/WatchRoster.dll`

## 不做（有意）

- 全员更鼓 / 时间表 UI（与原版 Schedule 撞车）
- 无人值更 → 门未锁（不公平）
- 提高袭击点数 / 做难度税

## 设计依据

见 `Mods/mod-design-review.md` §2.9。差异见 `DEVIATIONS.md`。

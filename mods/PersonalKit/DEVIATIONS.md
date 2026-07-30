# PersonalKit — DEVIATIONS.md

## Harmony 使用

PersonalKit 需要 Harmony 来 patch 多个原版方法。

### 补丁清单

| # | 目标方法 | 类型 | 说明 |
|---|---------|------|------|
| 1 | `TradeUtility.GetPricePlayerBuy` | Transpiler | 替换硬编码 0.5f 买入价地板为用户可配置值 |
| 2 | `SkillRecord.Learn` | Prefix | 当 xp < 0 时乘以技能衰减倍率 |
| 3 | `SkillRecord.LearnRateFactor` | Postfix | 乘以激情/学习速率倍率 |
| 4 | `Pawn_InteractionsTracker.SocialFightChance` | Postfix | 乘以社交冲突频率倍率 |
| 5 | `InspirationHandler.get_StartInspirationMTBDays` | Postfix | MTB 除以灵感频率倍率 |
| 6 | `InteractionWorker_RecruitAttempt.Interacted` | Transpiler | 在抗性削减计算后插入乘以招募倍率 |
| 7 | `Pawn_HealthTracker.MakeDowned` | Postfix | 倒地后将掉落武器收回背包 |
| 8 | `Recipe_Surgery.CheckSurgeryFail` | Prefix | 手术失败时按保底概率强制成功 |
| 9 | `QualityUtility.GenerateQualityCreatedByPawn` | Postfix | 强制最低制作品质 |
| 10 | — (ThingDef swap) | — | 运行时替换 Shuttle ThingDef 的 thingClass 为 `Building_PassengerShuttle_Aggro`（实现 `IAttackTarget`，TargetPriorityFactor=0.4f） |

### 为什么用 Transpiler 做 #1 和 #6

- #1: 原版 `GetPricePlayerBuy` 硬编码 `Mathf.Max(num, 0.5f)`，0.5f 是编译时常量，Postfix 无法覆盖（result 已被钳过）
- #6: 原版 `Interacted` 中抗性削减量是局部变量，需要在 `Mathf.Min` 之前插入乘法

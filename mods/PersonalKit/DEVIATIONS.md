# PersonalKit — DEVIATIONS.md

## Harmony 使用

PersonalKit 需要 Harmony 来 patch 多个原版方法。About.xml 声明了 `brrainz.harmony` 依赖与 `loadAfter`。

### 补丁清单

| # | 目标方法 | 类型 | 说明 |
|---|---------|------|------|
| 1 | `TradeUtility.GetPricePlayerBuy` | Transpiler | 替换硬编码 0.5f 买入价地板为用户可配置值（关时回 0.5） |
| 2 | `SkillRecord.Learn` | Prefix | 当 xp < 0 且 direct=false 时乘以技能衰减倍率 |
| 3 | `SkillRecord.LearnRateFactor` | Postfix | 乘以激情/学习速率倍率 |
| 4 | `Pawn_InteractionsTracker.SocialFightChance` | Postfix | 乘以社交冲突频率倍率 |
| 5 | `InspirationHandler.get_StartInspirationMTBDays` | Postfix | MTB 除以灵感频率倍率 |
| 6 | `InteractionWorker_RecruitAttempt.Interacted` | Prefix+Postfix | 按倍率缩放实际抗性削减量（不依赖脆弱 IL） |
| 7 | `Recipe_Surgery.CheckSurgeryFail` | Prefix | 按保底概率强制成功（跳过失败判定） |
| 8 | `Pawn_HealthTracker.MakeDowned` | Postfix | 倒地后将 `droppedWeapon` 收回背包并清空引用 |
| 9 | `QualityUtility.GenerateQualityCreatedByPawn(int,bool)` | Postfix | 强制最低制作品质 |
| 10 | ThingDef.thingClass swap | runtime | Shuttle → `Building_PassengerShuttle_Aggro`（`IAttackTarget`，0.4f） |

### 设计说明

- **每个功能独立开关**：关 = 补丁 early-return / 回原版常量，不改行为。
- **默认只开交易两项**（谈判上限 + 买入价地板）；其余默认关，避免“装了就改全游戏”。
- **#6 不用 Transpiler**：`guest.resistance` 在方法内出现多次，脆弱匹配会乘错位置；改用 Prefix 记 before、Postfix 按 delta 缩放。
- **穿梭机**：只改 `ThingDef.thingClass`；已生成实例运行时类型不变，需重载地图/新建造才完全一致。若他 mod 已替换 thingClass 则不覆盖。
- **手术保底**：语义是“有 X% 概率强制成功”，不是“成功率下限 clamp”。设置文案已说明。

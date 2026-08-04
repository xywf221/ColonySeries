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
| 10 | ThingDef.thingClass swap | runtime | Odyssey `PassengerShuttle` → `Building_PassengerShuttle_Aggro`（`IAttackTarget`，0.4f） |
| 11 | `HediffCompProperties_PsychicHarmonizer.range` | runtime mutate | 同调范围；关时回 30 |
| 12 | `Thought_PsychicHarmonizer.MoodOffset` | Postfix | 乘以心情加成比例（好坏都乘） |
| 13 | `HediffComp_PsychicHarmonizer.AffectPawns` | Prefix | 开启叠加时重写施加逻辑，去掉「接收者已有同调植入则跳过」 |
| 14 | `Thought_PsychicHarmonizer.get_ShouldDiscard` | Prefix | 开启叠加时丢弃判定同样忽略接收者自身植入 |
| 15 | `PK_ExtractPsylinkLevel` RecipeDef | new surgery | 降启灵 1 级并生成 `PsychicAmplifier` 物品；设置关则 `AvailableOnNow=false` |
| 16 | `IdeoStyleTracker.StyleForThingDef` | Prefix | 开启后跳过 Morbid 分类；并清理缓存/已生成物 StyleDef |

### 设计说明

- **每个功能独立开关**：关 = 补丁 early-return / 回原版常量，不改行为。
- **默认只开交易两项**（谈判上限 + 买入价地板）；其余默认关，避免“装了就改全游戏”。
- **#6 不用 Transpiler**：`guest.resistance` 在方法内出现多次，脆弱匹配会乘错位置；改用 Prefix 记 before、Postfix 按 delta 缩放。
- **穿梭机**：只改 `ThingDef.thingClass`；已生成实例运行时类型不变，需重载地图/新建造才完全一致。若他 mod 已替换 thingClass 则不覆盖。
- **手术保底**：语义是“有 X% 概率强制成功”，不是“成功率下限 clamp”。设置文案已说明。
- **同调范围**：直接改 def 上 `HediffCompProperties_PsychicHarmonizer.range`；施加与 `ShouldDiscard` 都读 Props.range，无需双补丁。无 Royalty 时 def 不存在则跳过。
- **同调心情比例**：对最终 MoodOffset 乘倍率；同调者心情差时负面也会被放大。
- **同调叠加**：原版 `HasHediff(PsychicHarmonizer)` 让携带者互不接收；开启后 AffectPawns / ShouldDiscard 都绕过该门，仍按 `harmonizer == parent` 去重同源记忆。N 人同调可叠 N−1 条。
- **拆除启灵**：现版无拆除/降级手术（启灵装置是消耗品）。恢复旧玩法用 `Recipe_ExtractPsylinkLevel`：`ChangeLevel(-1)` + 生成 neuroformer；1→0 时 `Hediff_Level.ShouldRemove` 清掉 hediff。已学灵能不会自动收回。
- **病态风格**：原版仅能在意识形态「风格分类」调整；模因可强制 Morbid。无全局关闭。本选项在 `StyleForThingDef` 跳过 Morbid，并在 ApplyAll 清缓存 + 已生成物 `StyleDef`。不删模因本身。

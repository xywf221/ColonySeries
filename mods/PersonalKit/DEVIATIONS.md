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
| 17 | `Apparel.get_WornByCorpse` | Prefix | 「视为干净」开启时 getter 永远返回 false——亡字、掉心情、折价全消失；不改字段/存档，关即还原 |
| 18 | New building `PK_Washer` | building+work | 洗衣机：`JobDriver_PKWashApparel` 把真正污损的衣物搬进机器，洗完 `Apparel.WornByCorpse=false`（走 setter）。检测污损用反射读私有字段 `wornByCorpseInt`（避开 #17 的 getter 覆盖） |
| 19 | `Building_Bed.GetGizmos` | Postfix | 双人床追加「强制滚床单」命令：给床上/床外清醒恋人下发 `JobDefOf.Lovin`，清 `canLovinTick` 冷却。只下发工作，不建关系、不改判定 |
| 20 | `Thought.get_BaseMoodOffset` | Postfix | 一个 postfix 覆盖三处心灵抚慰来源：`PsychicEmanatorSoothe`（放射器，原版 +5）、`PsychicDrone`（0 档 +16 / 1-4 档 -12~-40）、`ArtifactMoodBoost`（抚慰脉冲，原版 +15）。按 def 分派**绝对心情值**（非倍率，允许负数），各来源独立开关；关 = early-return 走原版 |
| 21 | `ThoughtWorker_PsychicEmanatorSoothe.CurrentStateInternal` | Prefix | 放射器半径覆盖。原版半径是 worker 里的 `const float Radius = 15f`，无 def 可改；开启时用设置半径重跑同样逻辑（保留「未接电则不生效」判定与穿墙特性），关闭时 early-return 走原版 |
| 22 | `ThoughtDef ArtifactMoodBoost` 的 `durationDays` / `stackLimit` | runtime mutate | 抚慰脉冲的持续时间与叠加上限无 per-instance 钩子，只能改 def。原版值缓存一次作基线，每次 Apply 从基线重算（不复合叠加）；关 = 回基线 |
| 23 | 新 ThingDef `PK_PsychicShockPulser` | artifact | 新神器：全图敌对/无阵营单位陷入心灵冲击倒地。用 `CompTargetable_AllPawnsOnTheMap` 的变体 + 自定义 `CompTargetEffect`，不 patch 任何原版方法 |
| 24 | `CompTargetable_AllHostilePawnsOnTheMap`（新类，继承原版） | new comp | 原版只有 `ignorePlayerFactionPawns`，会连中立商队一起放倒。此类改为「敌对 **或** 无阵营」才算目标，中立/访客免疫 |
| 25 | `CompTargetEffect_PsychicShockDown`（新类，继承原版 `CompTargetEffect`） | new comp | 复用原版 `PsychicShock` hediff（意识 `setMax 0.1` → 倒地），只在 `AddHediff` 后用 `HediffComp_Disappears.SetDuration(ticks)` 改时长，使倒地时长可设置 |
| 26 | `ThingSetMaker_RandomOption.Option.weight` | runtime mutate | 分组权重覆盖。`Option.weight` 是 public 字段且全游戏无写入方，直接改 + 从缓存的原版值还原，**不需要 Harmony** |
| 27 | `Option.thingSetMaker` 换成 `WeightedStackCount`（新类，继承 `ThingSetMaker_StackCount`） | runtime swap | 组内逐条权重。原版组内是**均匀**抽取，无权重钩子；只替换被玩家编辑过的分组，未编辑的完全走原版 |

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
- **亡者衣物**：
  - #17（视为干净）只 patch getter。原版「打标」在 `Apparel.Notify_PawnKilled` 写私有字段 `wornByCorpseInt`，存档也存该字段；getter 覆盖后所有读它处（`GenLabel.LabelExtras`「亡」、心情、售价）都变成“干净”，但字段仍是 true，关开关一秒还原。
  - #18（洗衣机）读私有字段判断“真污损”不受 getter 影响；洗时移除污损走的是 setter。开 #17 时全图无污损可洗，洗衣机只剩“磨损耐久”的用途——两者互补，README 已注明建议只开其一。
  - **洗衣机接受任意衣物**：设计器 `Designator_PKWashClothes` + `DesignationDef PK_Wash`（`Designations_PKWasher.xml`），直接沿用原版 `Designator_Haul` 模式（`CanDesignateThing` 限定 Apparel）。工作链不再要求“真污损”。
  - **耐久损耗**：洗完在 `FinishWash` 按 `washerDurabilityLoss`（% of MaxHitPoints）扣 `HitPoints`，下限 1。设置滑条 0%–50%，默认 10%。
  - **防重复**：洗完立即 `des.Delete()` 清掉该衣物的洗衣指定，否则会无限重洗。
  - 洗衣机不引入「水电」假资源；代价 = 耗电（CompPowerTrader）+ 工时 + 建造/占地 + 耐久损耗，符合系列“有代价”约定。断电位衣物留在机内不丢，恢复供电可继续。
- **强制滚床单（#19）**：
  - 只在殖民者自有的非医疗/非囚犯/非奴隶双人床上出命令；需至少一名清醒、有在世恋人的主人。
  - 机制：若已有恋人躺在床中 → 把 `Lovin` 工作下发给躺着的这位（其 `JobDriver_Lovin.initAction` 会自动让另一位也上床，走原版流程）；否则下发给床主。清空双方 `canLovinTick` 冷却。
  - 不主动制造恋爱关系，也不绕过原版判定；只像“命令”一样催他们现在去滚。
- **心灵抚慰三来源（#20-#22）**：
  - **为什么 #20 patch 基类 `Thought.get_BaseMoodOffset` 而不是三个子类**：`Thought_Situational` 没有 override 它（直接继承基类），而 `Thought_Memory` 的 override 内部会调 `base.MoodOffset()`（后者读的正是这个 getter）。所以 patch 基类一个点即可覆盖三种来源——放射器（situational）、抚慰脉冲（memory）、事件（situational），补丁面最小且不会漏。同调（`Thought_PsychicHarmonizer`）是另一个独立 override，沿用已有的 #12，不受影响。
  - **为什么是 `BaseMoodOffset` 而不是 `MoodOffset`**（初版踩的坑）：三者都带 `<effectMultiplyingStat>PsychicSensitivity</effectMultiplyingStat>`，而该乘数在 `Thought.MoodOffset()` 里作用于 `BaseMoodOffset` **之后**。若在最终 `MoodOffset()` 上直接赋绝对值，就绕过了心灵敏感度——心灵敏感度 0 的小人也会被强制扣心情，破坏原版免疫。设在 `BaseMoodOffset` 则原版乘数照常叠加，**心灵敏感度 0 依然完全免疫，负数也强加不上**。
  - **为什么用绝对值而非倍率**：用户明确要求「具体数值」且「允许调成负数」——倍率无法把 +15 变成 -30。三条滑条统一 -100 ~ +100。
  - **灵能低语四档只给一条滑条**：原版四档是 -12/-22/-30/-40，用 0.3/0.55/0.75/1.0 的比例从「最高档」滑条派生（见 `PersonalKitMod.DroneStageRatios`）。拖一条即可整体翻转符号，面板实时回显四档实际值。
  - **#22 持续时间的生效时机**：改 `def.durationDays` 只影响**新获得**的记忆——`Thought_Memory.age > DurationTicks` 的丢弃判定读的是当前 def。已存在的记忆不会因为调大时长而“续命”，也不会因为调小而立刻消失（age 已超新时长时会消失）。设置文案已写明「下一次脉冲生效」。
  - **#21 放射器半径**：原版写作 worker 内的 `const float Radius = 15f`，连 `InHorDistOf` 调用点都内联了该常量，没有任何 def 字段。只能整体替换 worker 逻辑。已保留原版的「有 `CompPowerTrader` 且未通电则跳过」与水平距离判定（穿墙特性来自 `InHorDistOf` 本身，不判视线）。
  - **#20 事件档位区分**：`PsychicDrone` 的 5 档共用同一个 ThoughtDef，靠 `CurStageIndex` 区分。0 档（`PsychicDroneLevel.GoodMedium`）是正面抚慰，1-4 档（`BadLow`..`BadExtreme`）是负面低语。倍率一分为二，玩家可只保留想要的那一半。
- **心灵冲击脉冲发生器（#23-#25，新神器）**：
  - **零 Harmony**：整个功能不 patch 任何原版方法，全部靠继承 + 新 ThingDef。三个新类都继承原版（`ArtifactBase` ThingDef、`CompTargetable_AllPawnsOnTheMap`、`CompTargetEffect`），原版机制（战斗日志、好感惩罚、前摇、心灵敏感度过滤）全部照搬。
  - **为什么要自定义 targetable（#24）**：原版 `CompProperties_Targetable` 只提供 `ignorePlayerFactionPawns`，即「非我方全打」——**会把来访的中立商队一起放倒**，然后对每个派系 -200 好感，一次使用即外交全灭。故改为「`Faction == null || Faction.HostileTo(OfPlayer)`」才算目标，中立/同盟免疫。
  - **为什么自定义 target effect（#25）**：原版 `CompTargetEffect_PsychicShock` 用固定 7500 tick（3h）的 hediff 且无时长钩子。我们不改 hediff def（会影响冲击枪/神经热等其它来源），而是 `AddHediff` 后立即 `TryGetComp<HediffComp_Disappears>().SetDuration(ticks)`——该方法原版就有，正是为此设计。
  - **倒地是靠意识而非血量**：`PsychicShock` hediff 对 Consciousness `setMax 0.1`，`Pawn_HealthTracker.ShouldBeDowned()` 判 `capacities.CanBeAwake` 失败即倒地。所以是「无损倒地」，不扣血、不留疤。
  - **总开关语义**：关 = 神器仍会出现在奖励池、仍可拾取，但激活时 `DoEffectOn` 直接 return（物品照样消耗）。符合系列「关 = 该功能走原版/不生效」约定，且不给玩家一个“存着没用”的死物品。
  - **不进 `DisableAll()`**：`DisableAll` 的语义是「关掉所有**对原版的覆盖**」，而本神器是本模组自带内容，不是覆盖。故只进 `ResetToDefaults()`（默认开）。
  - **贴图复用**：`texPath` 直接指向原版 `Things/Item/Artifact/PsychicShockLance/PsychicShockLance`，音效复用 `PsychicShockLanceCast` / `PsychicArtifactWarmupSustained`。本仓库不搬运游戏美术资源（见 CLAUDE.md §1）。
  - **平衡取舍**：比原版冲击枪强在全图 + 无视线 + 无脑损伤，弱在一次性 + 只能打敌对/无阵营 + 激活前摇 132 tick 可被中断 + 好感 -200。
  - **掉落池（易漏，已验证）**：分两类机制，别想当然——
    - **靠标签自动生效**（`ArtifactBase` 已带，无需 patch）：商队/轨道/定居点的 `StockGenerator_Tag`（`tradeTag=Artifact`）、`MapGen_AncientTempleContents` 远古神殿（按 `thingCategories=Artifacts` 筛，**不是** defName 白名单）、通用 `RewardStandardHighFreq` 奖励池。
    - **靠显式 defName 白名单**（必须 patch，见 `Patches/PK_ThingSetMakers.xml`）。用脚本扫全部游戏 XML 核对出的**完整 6 个**（之前写的「3 个」是漏的）：Ideology 的 `MapGen_AncientComplexRoomLoot_Better` / `MapGen_AncientComplex_SecurityCrate`、Anomaly 的 `MapGen_FleshSackLoot` / `Reward_GrayBox`、Odyssey 的 `Reward_AncientSafe` / `MapGen_HighValueCrate`。这些是 `<thingDefs>` 逐个列举的，新 def 不写进去就永远刷不出来。
    - xpath 带 `[li="PsychicSoothePulser"]` 限定，只命中真正列出神器的 `thingDefs` 列表，不会误插到食物/武器等其它列表。
  - **XML 注释禁止 `--`**（CLAUDE.md §1 已列，本次踩到）：注释里写了破折号导致解析失败。注释一律用中文全角或改写句式。
  - **Patch 文件的三个硬性要求（曾全部踩到，见下）**：路径必须是 `Patches/`，根元素必须是 `<Patch>`，`<Operation>` 必须是根的直接子元素。
- **`PK_ThingSetMakers.xml` 放错位置导致加载报错（已修）**：
  - 现象：`Type PatchOperationSequence is not a Def type or could not be found, in file PK_ThingSetMakers.xml`。
  - 根因不止一个，三个错叠在一起：
    1. **位置错**：文件放在 `Defs/ThingSetMakerDefs/`。`ModContentPack.LoadPatches()` 只扫 `Patches/`（`Verse/ModContentPack.cs:362`），放在 Defs 下会被 `DirectXmlToObjectNew.DefFromNodeNew` 当 Def 解析 → `PatchOperationSequence` 当然不是 Def 类型。
    2. **根元素错**：根写的是 `<Defs>`。Patch 文件必须是 `<Patch>`（`ModContentPack.cs:366`，不符会打 `expected 'Patch'`）。这也是它在原位置报错而非静默失效的原因。
    3. **`MayRequire` 对 Patch 无效**：`MayRequire` 只在 def 节点被处理（`LoadedModManager.cs:395`），`PatchOperation` 类完全没有该字段 —— 写成属性等于没写，DLC 缺失时 xpath 不命中，`PatchOperation.Complete()` 照样打 `patch operation failed`（`Verse/PatchOperation.cs:61`）。
  - 修法：移到 `Patches/PK_ThingSetMakers.xml`，根改 `<Patch>`，用 **`PatchOperationConditional`** 做 DLC 守卫 —— 外层 xpath 先探测目标 def 是否存在，不存在就整个跳过且**不打错误**。这比 `PatchOperationFindMod` 猜 mod 名字可靠，`HasActiveModWithName` 是按显示名精确匹配的。
  - **内层 xpath 必须锚到 defName**：`PatchOperationAdd.Apply` 会给每个命中的节点都 append 一份。六个 Conditional 若都用未锚定的全局 xpath，全 DLC 齐全时就是每个池被插 6 遍。已逐条用脚本核对每个锚定 xpath 恰好命中 1 个节点。
  - **修一次还会复发（曾二次出现）**：第一次「修复」后错误照旧。真因是**只删了游戏目录那份，源码那份 `ColonySeries/mods/PersonalKit/1.6/Defs/ThingSetMakerDefs/PK_ThingSetMakers.xml` 一直还在** —— 而 `deploy.sh` 是源码→游戏的镜像同步，每次部署都把它又写回去。误以为是 rsync 不清孤儿（`--delete` 其实是开的），真实教训是：**重定位文件时必须在源码树里删，然后 `find . -name <file>` 全局确认只剩新位置那份，再 deploy。** 检验手段：`find . -name PK_ThingSetMakers.xml` 应只返回 2 条（源码 + 部署，都在 `Patches/` 下）。
- **战利品表编辑器（#26-#27）**：
  - **两层结构，语义不同**：`ThingSetMakerDef` 的 `root` 是 `ThingSetMaker_RandomOption` 时，每个 `Option` 有 `weight`（分组权重，相对权重掷骰）；`Option.thingSetMaker` 通常是 `ThingSetMaker_StackCount`，其 `fixedParams.filter` 列出具体物品。
  - **为什么只收 `RandomOption` 根的池**：`ThingSetMaker_Sum` 用的是 `chance`（每项独立掷骰，**不是**相对权重），语义不同，故下拉里不列。扫描 `DefDatabase<ThingSetMakerDef>` 自动发现，**不写死名单** —— 装新 DLC / 其它模组的池自动纳管。
  - **下拉里显示 defName 的原因（不是翻译没调用）**：`ThingSetMakerDef` 在原版**一个 `<label>` 都没有**（脚本核对：28 个 RandomOption 池，0 个带 label），而 `Def.LabelCap` 在 label 为空时返回 null（`Verse/Def.cs:68`）—— 没有可翻译的原文。官方中文包的 `DefInjected` **也不含** `ThingSetMakerDef` 目录，故我们在 `1.6/Languages/ChineseSimplified/DefInjected/ThingSetMakerDef/ThingSetMakers.xml` 自建 28 条 `.label` 注入（**不要去改游戏 def**，会与其它模组打架）。已脚本核对 28 个 key 与实际池名一一对应，无未知项无遗漏。
  - **读 `pool.label` 而不是 `LabelCap`**：`Def.LabelCap` 会缓存首字母大写的结果，虽 `ClearCachedData()` 会清、且缓存是懒加载，但**直接读注入落点**在语言热重载后不可能拿到陈旧值。最初显示为「中文名（defName）」，后按用户要求**去掉了 defName**，只留可读名称；英文环境下无注入则兜底显示 defName。
  - **物品名一直是对的，分组标题曾经不是**：物品行走 `ThingDef.LabelCap`（官方有完整翻译）。但分组标题的头项我原先误用 defName 拼字符串，已改用 `LabelCap`。两者情况不同：物品名本来就没问题，只有分组标题需要修。
  - **分组标题的数量后缀**：初版对**所有**分组都加「（共 N 项）」，但脚本统计显示 189 个分组里 **119 个只装 1 件物品**（占 63%），13 个池更是每组都只有 1 件 —— 对它们来说物品名就是组名，挂个「1 项」纯属噪音。现改为**仅多物品分组**才标注数量。
  - **动态分组差点变成隐形行**：并非所有分组都是列举 defName 的 `StackCount`。远古建筑里的**设计图**分组是 `ThingSetMaker_Techprints`（`MapGen_AncientComplexRoomLoot_Better` 第 11 组，原版权重 0.05）—— 它**没有 `ThingFilter`**，内容在抽取时按研究进度动态决定（`TechprintUtility.TryGetTechprintDefToGenerate_NewTemp`）。原先 `VanillaItemNames` 只读 filter，对它返回空表，`GroupLabel` 就退化成光秃秃的 `#11`，混在一堆物品名里看不出是什么 —— 用户报「所有池子找过都没有」即此。
    - 修法：`VanillaItemNames` 在无 filter 时改用 `AllGeneratableThingsDebug()` 取该 maker 能产出的物品；标签显示为「动态生成（N 项）」，不假装某一个物品能代表全组。
    - **必须设上限**：这类列表规模运行时可变且探不明（techprint 的 def 与 `CompTechprint` 都不在静态 XML 里逐个声明，扫了全部 `ThingDef` 得到 0 条匹配），故加了 `MaxDebugItems = 200` 枚举上限；它只用于显示与 tooltip，超了截断即可。
    - **能力边界**：增删物品与组内权重都依赖 `ThingFilter`，所以这类分组**只能改分组权重**。UI 不再显示点了没用的「添加物品」按钮，改为一行灰色说明。想让设计图更容易出，把它那组的权重从 0.05 调高。
    - **`ApplyAll` 的连带 bug（同批修）**：原先 `if (...filter == null) continue;` 守卫整个循环，等于连**分组权重覆盖**也跳过了 —— 而那恰恰是唯一可用且最常见的改法。现已把分组权重提到 filter 判断之前；无 filter 的路径也会登记 `applied`，保证还原依旧精确。
  - **为什么组内权重必须换 maker**：原版 `StackCount` / `Count` 都走 `ThingSetMakerUtility.TryGetRandomThingWhichCanWeighNoMoreThan`，内部是 `TryRandomElement`（**均匀**）。没有 per-group 钩子。另一个选项是 patch 那个 static 方法，但它被全游戏所有 `Count`/`StackCount` 共用，影响面过宽 —— 故只替换被编辑分组的 maker 实例（#27），未编辑的分组保持原版类。
  - **为什么分组权重不需要 Harmony**（与原计划不同）：计划里打算 patch `ThingSetMaker_RandomOption.GetSelectionWeight`（private）。实际写时发现 `Option.weight` 是 **public 字段**且全游戏没有其它写入方，直接改 + 从首次扫描缓存的原版值还原即可，补丁面从 1 个 Harmony patch 降到 0。
  - **还原是精确的**：`ApplyAll` 开头先 `RestoreAll()`，按 `applied` 字典逐项回退（filter 的 SetAllow 反向操作 + `option.weight` 回缓存值 + `thingSetMaker` 回原实例），再重新应用。所以「关总开关」= 完全原版，不会残留。每帧设置页重绘都会跑一次，`ThingFilter.SetAllow` 开头有 `if (allow == Allows(...)) return;` 幂等，开销可忽略。
  - **原版值只缓存一次**：`Discover()` 里首次扫描时记下每个分组的 `weight`，之后**永不**重读（否则反复 Apply 会复合叠加）。
  - **`vanillaDefNames` 只在 override 首次创建时填充**：一旦我们开始 `SetAllow` 改 filter，它的内容就不再是「原版内容」了。若每次都重播种，被玩家移除的物品会悄悄从名单里消失，下一帧又被当成「没覆盖过」而重新启用。故只在新建时种子化，之后视为存档事实。
  - **移除是软删除**：原版物品点 `×` 只标 `removed`（UI 上行变红、可点 `+` 恢复），不删条目 —— 否则无法区分「玩家删了」和「原版本来就没有」。玩家自己添加的物品点 `×` 第二次才真正删掉条目。
  - **物品选择器**：开独立 Window（`Dialog_Search<ThingDef>` 子类，原版通用可搜索窗口，内建分帧过滤 500 项/帧），数据源 `ThingSetMakerUtility.allGeneratableItems`（原版预计算的「可作为战利品」列表，零枚举开销，自动含其它模组的物品）。**不在设置页内嵌套 `BeginScrollView`**（原版仅一处嵌套且靠 flag 特判）。
  - **滚动高度反馈**：`scrollHeight` 由上一帧绘制结果写入、下一帧用于 `viewRect`，照抄 `Dialog_AnomalySettings` 的标准写法。
  - **`WeightedStackCount` 里 `countRange` 语义照抄原版**：`remaining` 是数量预算，被**实际入栈数量**递减（不是循环计数），所以 stackLimit 大的物品一次即可满足 countRange。原版 `try..finally` 里 `num4 -= (thing.stackCount = ...)` 的写法已核对。

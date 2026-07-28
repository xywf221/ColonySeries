# 残骸图谱 Salvage Atlas

RimWorld 1.6。械战长期沉洞：从机械残骸提取**碎片**，在残骸台分析后投入**有界图谱分支**——不是研究条替代品。

## 理念

> 碎片不等于研究点。解锁一次性蓝图 / 短时局部强化 / 拆解配方。失败可读。无械图几乎休眠。

## 启用

1. **Harmony**
2. 模组列表启用 **残骸图谱 Salvage Atlas**

路径：`Mods/SalvageAtlas`

## 玩法循环（Phase 1）

1. 研究 **残骸基础 / salvage basics**（与原版研究并行，不给 ResearchManager 点数）
2. 建造 **残骸台**（Salvage 分类）
3. 指定 **搜刮机械体**：械尸 / 飞船块 / 残骸 → Crafting 工种拆出 **机械碎片** 物品
4. 殖民者将碎片扛到残骸台 **分析** → 存入「已分析碎片」银行
5. 在残骸台 Gizmo **选择解锁** 五个有界分支（3～4 级）
6. 解锁 **拆解** 后可用 **拆解残骸** 指定：成功拿材料+碎片；失败浪费碎片 + 小火/小爆 + 信件

### 碎片来源

| 来源 | 说明 |
|------|------|
| 指定搜刮械尸 | 主体：体型/战力 → 约 1～8 片（设置倍率） |
| 指定搜刮残骸/飞船块 | 较少，按市值估算 |
| 械死亡时小概率掉 1 片 | 点缀；完整收益仍靠指定搜刮 |
| 拆解成功奖励 | 额外碎片 |

### 五个分支（有界）

| 分支 | 级数 | 回报类型 |
|------|------|----------|
| Blueprints 蓝图 | 4 | 一次性示意图额度 → 打印物品 → 解码材料包 |
| Field kit 野战 | 3 | 临时护甲 hediff（单人） |
| Teardown 拆解 | 4 | 拆解指定与材料表 |
| Optics 光学 | 3 | 临时瞄准 hediff（单人） |
| Coolant 冷却 | 3 | 临时耐热 hediff（单人） |

**不做**：永久 +20% 全局伤害树。

### 轻松模式

- 碎片 x1.5、分析/搜刮更快
- 拆解风险 x0.4、失败不起火
- 仍需研究 + 残骸台 + Job

## 设置

选项 → 模组设置 → 残骸图谱：总开关、产出/工时/风险倍率、起火/爆炸、解锁信件、轻松模式。

## 性能

- 无 AllCells 扫描
- 工作靠 Designation / Job；MapComponent 几乎空转
- 无械、无指定时接近 0 成本

## 编译

```bash
dotnet build "E:/RimWorld-v1.6.4850/Mods/SalvageAtlas/1.6/Source/SalvageAtlas/SalvageAtlas.csproj" -c Release
```

可移植 `RimWorldDir` / `RIMWORLD_DIR`。输出：`1.6/Assemblies/SalvageAtlas.dll`

## 兼容

- Harmony 必需
- 中英 Keyed + DefInject
- 残骸目标以 defName 启发式识别（Core 飞船块 + 名称含 Mech/Wreck 等）

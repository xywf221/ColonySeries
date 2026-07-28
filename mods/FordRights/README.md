# 河权渡口 Ford Rights

RimWorld 1.6。有水地图的**渡口经济**彩蛋：小额过路费、慢速渔获、罕见洪水。无水则休眠。

权威设计：`mod-design-review.md` §2.10。

## 理念

> 有河才亮，不当主菜。过路费有帽，防印钞；渔获是点缀；洪水可读可修。

## 启用

1. **Harmony**
2. 模组列表启用 **河权渡口 Ford Rights**

路径：`Mods/FordRights`

## 玩法循环（Phase 1）

1. 研究 **河权 / river rights**
2. 在**紧邻水域**处建造 **河权渡口**（杂项分类；PlaceWorker 强制邻水）
3. **过路费**：贸易商队 / 访客 / 旅人到达且渡口运作时，掉落 **50～150** 银（可调）
   - **日次数帽**（默认 2）
   - **日银帽**（默认 300）
   - **季窗银帽**（默认 15 日滚动 1800）
4. **钓鱼**：渡口 Gizmo / 右键 → **狩猎**工种 Job；默认约 18h 冷却，每次 1 条河鱼
5. **洪水**：罕见事件砸伤渡口 HP（不删建筑）；&lt;35% HP 时停收/停钓，原版修理恢复
6. **无水**：地图粗采样无水 → MapComponent 几乎空转；建筑不邻水 → 该渡口休眠

### 轻松模式

- 过路费 ×0.85
- 钓鱼冷却 ×0.65
- 洪水伤害 ×0.35

## 设置

选项 → 模组设置 → 河权渡口：总开关、轻松模式、过路费区间与帽、钓鱼、洪水。

## 性能

- **不**扫 `map.AllCells`（粗网格 + 邻域半径 2）
- `MapComponent` 每小时稀有脉冲；无水/无建筑即休眠
- 收费只在到达事件 Postfix，不每 tick 搜商队

## 编译

```bash
dotnet build "E:\RimWorld-v1.6.4850\Mods\FordRights\1.6\Source\FordRights\FordRights.csproj" -c Release
```

可移植 `RimWorldDir` / `RIMWORLD_DIR`。输出：`1.6/Assemblies/FordRights.dll`

## 不做（有意 / 二期）

- 垦壤改河道 soft-link（二期）
- 自动挂机渔场 / 无限过路费
- 无水硬 debuff 全图税

## 兼容

- Harmony 必需
- 中英 Keyed + DefInject

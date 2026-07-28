# 后续 Mod 备忘（已按可玩性重审 + 实现状态）

> 完整评审：[`mod-design-review.md`](./mod-design-review.md)（2026-07-28）  
> 原则：有代价、可设置、轻松模式；事件驱动；soft-link；**不做心情税农场**。

---

## 已完成并编译通过（2026-07-28）

### S / A（第一批 + 账房）
| 模组 | 路径 | 要点 |
|------|------|------|
| 黑市邮筒 Dead Drop | `Mods/DeadDrop` | 边缘灰单、交货 Job、暴露/搜查、声望 |
| 匠人署名 Artisan Mark | `Mods/ArtisanMark` | 物记匠人、遗物心情；战斗加成默认关 |
| 工坊损耗 Workshop Wear | `Mods/WorkshopWear` | 赶工核心、马虎/精修、白名单 |
| 流言口碑 Rumor Mill | `Mods/RumorMill` | 事迹标签、衰减、表彰 |
| 聚落账房 Ledger **v2** | `Mods/Ledger` | 赊账杠杆；无电税；默认无储备心情 |

### B / C（第二批）
| 模组 | 路径 | 要点 |
|------|------|------|
| 学徒契约 Apprentice Bond | `Mods/ApprenticeBond` | 1:1 技能、同室 XP、日帽 |
| 疫线检疫 Quarantine Line | `Mods/QuarantineLine` | 无病休眠；检疫区；不暗锁门 |
| 残骸图谱 Salvage Atlas | `Mods/SalvageAtlas` | 碎片分支；拆解风险 |
| 边防岗哨 Watch Roster | `Mods/WatchRoster` | 岗哨预警；**不**暗开门、不涨袭击点 |
| 河权渡口 Ford Rights | `Mods/FordRights` | 邻水渡口、过路费有帽、无水休眠 |

### 既有系列（更早）
- 垦壤 Landworks · 田作 Fieldcraft · PersonalKit · Trait Extractor

---

## 明确不做独立包（D）

| 原名 | 处理 |
|------|------|
| 梦债 Dream Debt | 不独立；若做「自愿通宵」并入 PersonalKit |
| 冷链 Logistics Cold | 不独立；血清温敏并入 Trait Extractor |

空壳目录 `DreamDebt` / `LogisticsCold` 可忽略或日后删除。

---

## PersonalKit 小功能（仍建议并入 Kit，未开新包）

- 谈判备忘 · 工作峰值预设 · 囚犯/提取预览 · 地质简报（只读 soft-link）

---

## 实现约定（不变）

- 有代价、可设置、轻松模式  
- 事件驱动 / 有界脉冲，不扫 `map.AllCells`  
- 可移植 csproj（`RimWorldDir` / `RIMWORLD_DIR`）  
- 中英文本 · soft-link 不硬依赖  
- 玩家动词 / 代价 / 成败反馈 / 休眠条件  
- 各包 `README.md` + `DEVIATIONS.md`（若有偏差）

## 不想做的类型（不变）

- 免费超级装备 · 无代价传送 · 全自动无维护农场 · 纯数值 OP 种族  
- 暗扣银、暗开门、只报表无决策的管理模拟  

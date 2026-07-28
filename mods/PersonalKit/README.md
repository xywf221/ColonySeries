# 个人小工具 PersonalKit

个人向小功能集合（RimWorld 1.6）。后续功能会往这里加。

## 当前功能

### 交易谈判加成上限

原版 1.6 在 `TradePriceImprovement` 上写了：

```xml
<maxValue>0.395</maxValue>
```

约等于 **+40%** 硬顶。更早版本没有这个上限。

**选项 → 模组设置 → 个人小工具：**

| 选项 | 说明 |
|------|------|
| 不设上限（默认） | 接近旧版，社交带来的交易加成完整生效 |
| 自定义上限 | 关闭「不设上限」后，滑条 40%–300% |
| 恢复原版 40% | 一键回到原版硬顶 |

修改即时生效，一般不必重启游戏。

## 启用

模组列表启用 **个人小工具 PersonalKit**。不依赖 Harmony。

## 编译

```bash
dotnet build Mods/PersonalKit/1.6/Source/PersonalKit/PersonalKit.csproj -c Release
```

路径可移植：默认从 csproj 推断游戏根目录；也可用 `-p:RimWorldDir=...` 或环境变量 `RIMWORLD_DIR`。

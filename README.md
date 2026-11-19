# TRUE Auto Nutrient Paste (真·自动营养膏机) — 持久化 Bug 修复版

为 RimWorld 营养膏机添加智能制作和连续制作模式。

- 原作者：`xiaotuzaizi`
- 持久化修复：`WorldLeader`
- Package ID：`worldleader.autonutrientpaste`
- 目标游戏版本：RimWorld 1.6 / 1.7

## 本版本修复

修复“智能制作”和“连续制作”状态没有正确跨存档读取阶段保留的问题。选择生产模式、保存并退出游戏后，重新进入存档仍会保持选中状态。

## 功能

- 智能模式：按库存上下限自动生产和停止。
- 连续模式：只要有原料和电力就持续生产。
- 可选的智能储料斗控制。
- 独立的模式切换按钮。

## Build

```powershell
dotnet build .\Source\AutoNutrientPaste.csproj -c Release
dotnet build .\Source\AutoNutrientPaste.1.7.csproj -c Release
```

编译结果分别输出到 `1.6/Assemblies` 和 `1.7/Assemblies`。游戏会通过 `LoadFolders.xml` 只加载当前版本的 DLL。

## License

本仓库暂未附加新的开源许可证。原 Mod 的原始授权状态未在随附文件中说明；在进一步使用或再分发前，请确认原作者的授权。

---

Adds Smart and Continuous production modes to the RimWorld nutrient paste dispenser. This fork fixes mode selections being lost after saving, exiting, and reloading a game.

Original author: `xiaotuzaizi`  
Persistence fix: `WorldLeader`


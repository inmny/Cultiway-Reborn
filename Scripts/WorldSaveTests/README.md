# 世界存档检查

先在仓库根执行开发构建，再检查纯数据转换：

```bash
dotnet build Cultiway.csproj -c Debug
dotnet run --project Scripts/WorldSaveTests/WorldSaveTests.csproj
```

检查连续转换、原输入保留、缺失与重复转换、过新或损坏版本、局部恢复，以及既有全局资料的备份恢复。这里使用测试资料的第 1、2、3 版演示转换，不能冒充已经发行过三个正式世界存档版本。

入口使用 `Program.test`，编译中间文件和产物写进 `Temp/.world-save-tests/`，避免游戏加载器把检查程序当作模组源码。

游戏内通过 DebugToolbox 在独立验收世界运行 `Scripts/WorldSaveTests/runtime/verify_world_save.script`，检查五种人物资料、完整原版存读档、四处局部失败、原档与备份字节一致，以及再次保存后不残留损坏部分。测试会创建人物并切换世界，不在玩家正在玩的存档里运行。

`runtime/verify_world_save_snapshots.script` 只检查实际境界成果的嵌套结构；退出游戏并重启后运行 `runtime/verify_world_save_restart.script` 检查上一进程的保存结果；`runtime/verify_world_save_protection.script` 模拟备份失败，确认原位置不写入而另存可用。

## 日后怎样更新

1. 新增可选字段并且旧档缺失时默认值合理，可以保持当前数据版本。
2. 字段改名、结构重组或编号改变时，提高 `WorldSaveContent` 中对应部分的版本，并加入实现 `ISaveMigration` 的相邻版本转换。
3. 转换方法修改传入的数据副本并返回它，不读取游戏对象、不重新随机成长、不直接写文件。
4. 加入旧版正式数据样本，检查转换后的实际数值和后续保存；旧转换保留，不保留旧玩法运行路径。
5. 新玩法先定义稳定资料和恢复顺序，再登记 `SaveSection<上下文, 数据>`。人物上下文使用 `ActorExtend`，世界上下文使用 `MapBox`。


完整范围另外运行：

- `runtime/verify_full_world_save.script`：动态功法、丹方与原版书册、血脉、师徒、法宝与克隆、背包和宗门所有权、持续状态、半成品与材料、元神及其器灵、实际改变的小世界地图和局部时钟；比较完整部分及受伤生命、魔力、体力与饱食。
- `runtime/verify_full_world_restart.script`：退出游戏并重启后读取完整样板，比较全部世界部分与人物生命、魔力、体力、饱食。
- `runtime/verify_transport_world_save.script`：使用原版建造检查可接受的车站位置，比较铁路、列车位置、行程与乘客关系。
- `runtime/verify_transport_world_restart.script`：重启后读取上一进程的铁路样本，核对行程、行驶距离和车上乘客。
- `runtime/verify_world_save_scale.script`：增加 512 名人物、256 件物品和两个 48×48 小世界，记录真实保存耗时、压缩文件大小，并核对恢复数量。
- `runtime/verify_full_world_recovery.script`：让单件法宝来自较新版本并加入已删除世界部分，检查局部丢弃、原档字节保留、另一件法宝正常，以及再次保存后能够干净读取。

组件与关系的版本在 `WorldObjectSaveService.Parts.cs` 登记，世界部分在 `WorldEnvironmentSaveService` 登记。涉及原版元资料时使用 `StableSaveJson.NativeSettings`，不要使用只写字段的组件规则。

本轮实际第 1 版样板见 [Fixtures/README.md](Fixtures/README.md)，后续升级时保留原文件。最近一轮验收结果在 `artifacts/full_world_save/` 的 `*-result.json`，构建与纯数据检查输出分别为 `build.log`、`framework-tests.log`。

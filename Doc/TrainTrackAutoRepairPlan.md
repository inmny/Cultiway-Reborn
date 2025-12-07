## 目标
- 在地形被灾害/地形编辑破坏后，自动恢复铁路连通性，优先保证已有站点间的轨道快速回补，避免手动维护。

## 破坏场景梳理
- `terraformTop`/`setTop`/`setType`/爆炸/火灾/洪水/山崩等改写了 top_type 或地表类型，导致轨道变为其它地表。
- 车站被摧毁或变为废墟，导致原轨道终点失效。
- 周边地形被抬升/填海，轨道格子被阻挡或替换。

## 设计原则
- 事件驱动 + 批处理：监听地形变化，按批次调度修复，避免逐格实时高频开销。
- 最小化重建：优先“回填原路径”，失败再尝试重新选路。
- 安全保护：当灾害持续（火/熔岩/海水未退）时延后修复，避免无效重复刷。
- 数据轻量：轨道链路记录存内存即可，不强依赖存档序列化；必要时可按站点对重新扫描。

## 核心数据结构（建议）
- `TrainTrackRegistry`
  - `LinkId`：`(stationAId, stationBId)` 排序后的组合键。
  - `TileIds`：链路经过的 tile_id 列表（按顺序）。
  - `LastRepairTime` / `FailCount`：节流与降级用。
  - `Status`：Normal / PendingRepair / Disabled（终点缺失或多次失败）。
- `TrainTrackDamageEvent`
  - `tile_id`，`reason`（Fire/Lava/Explosion/Terraform/BuildingRemoved），`time`.
- `TrainTrackRepairJob`
  - 绑定一个 `LinkId`，包含待修复 tile 列表与调度时间。

## 事件挂点
- Patch `MapAction.terraformTop` / `WorldTile.setTop` / `WorldTile.setType`：当新 top_type 不是 `TrainTrack` 时，触发 `TrainTrackDamageEvent`。
- Patch 重大灾害（爆炸、火、海啸、山崩）效果里对 tile 的写操作，调用同一事件入口。
- Patch `Building.setState`：当 `TrainStation` 进入 Ruins/Removed，标记关联链路 `Disabled`。
- 站点新建/轨道新建时（BuildTrainTrack 成功后）向 `TrainTrackRegistry` 注册链路与路径。

## 修复流程（批处理）
1) **采集**：事件入口只做入队与去重，将 `tile_id` 写入并标记所在 `LinkId` 为 `PendingRepair`，记录触发时间。  
2) **调度**：每隔 `N` 秒（如 2~5s）运行一次修复例程，对积累的 `PendingRepair` 链路批量处理，单次上限避免卡顿。  
3) **判定**：
   - 如果任一终点站不存在/废墟 → 标记 `Disabled`，跳过修复。
   - 如果目标格仍处于危险（`tile.isOnFire`、`lava`、`ocean` 且不应铺设）→ 延后，下次再试。
4) **快速回填**：按 `TileIds` 顺序将 top_type 回填为 `TrainTrack`，必要时调用 `MapAction.terraformTop(tile, TopTileTypes.TrainTrack, Terraforms.TrainTrack, false)`。
5) **失败重建**：若回填中断（目标 tile 为 null 或非法）：
   - 调用现有 `GetTrainTrackDirection(stationA.tile, stationB.tile)` 重新求路径。
   - 成功则更新 `TileIds` 并回填；失败则 `FailCount++`，达到阈值（如 3）后暂时 `Disabled`，并在日志提示。
6) **收尾**：修复成功后重置 `FailCount`，刷新邻接贴图由现有 `WorldTilemap.getVariation` 逻辑自动完成。

## 节流与防抖
- 同一 `LinkId` 在冷却期内（如 10s）仅处理一次修复请求。
- 修复例程每帧仅处理固定数量链路（如 3 条），剩余下个 tick 继续。
- 遇到持续灾害的格子，记录 `NextRetryTime`，避免在灾害未结束前高频写入。

## 边界与降级
- 终点站被彻底移除：保持 `Disabled`，等待新的建站事件触发重新注册。
- 如果两个站点之一被更换位置，重新注册时覆盖旧 `TileIds`。
- 若玩家故意改造地形不希望恢复，可提供开关（全局 mod 设置或 DebugConfig 标记）来关闭自动修复。

## 最小落地步骤（实施优先级）
1) 添加 `TrainTrackRegistry` + `TrainTrackRepairScheduler`（内存单例），支持注册、标记损坏、批处理修复。  
2) Patch 事件挂点（`terraformTop` / `setTop` / `setType` / `Building.setState`）写入损坏事件。  
3) 在 `BuildTrainTrack.action` 成功后注册链路与 `TileIds`。  
4) 修复例程：先回填原路径，失败再调用 `GetTrainTrackDirection` 重新算路。  
5) 日志与开关：提供 Debug 日志，加入“自动修复开关”配置项以便测试。

## 可选增强
- **持久化**：将 `TrainTrackRegistry` 序列化入存档，避免读档后丢失轨道路径缓存。  
- **健康监测**：定期全图扫描 `TrainTrack` 区域，发现孤立/断头轨道自动比对邻近站点进行修正。  
- **成本化修复**：修复时消耗少量资源或时间，避免灾害下无限瞬时重铺。  
- **多样化路径**：重建时优先选择更平滑/更短路径，并预留对障碍绕行（结合寻路）支持。  
- **可视提示**：修复中在轨道上显示“维修中”特效，增强反馈。


# 寻路自动纠错方案

## 现状梳理
- **自研寻路流**：`Actor.goTo` 被前缀改写为提交 `PathFinder.RequestPath`，后台线程用 `PortalAwarePathGenerator` 生成路径，主线程在 `updatePathMovement` 通过 `TryPeekStep/ConsumeStep` 驱动移动。步不可达时直接 `Abort`，仅取消任务与行为，不会自动重算。
- **传送/乘船链路**：`PathStep.Method == Portal` 时调用 `PortalManager.NewRequest` 生成 `PortalRequest`（包含门户链、ToLoad/ToUnload 集合）。船的行为树前缀改写为读取 `PortalRequest`，但 `AssignNewRequestForDriver` 未实现，`PortalManager` 也未在循环中做健康检查或补救。
- **门户/船舶拓扑**：码头建筑在 `setState` 正常时注册为 `PortalDefinition`；`WaterConnectivitySystem` 基于水域连通性为同一水体的门户生成 `PortalConnection`，并写回 `Portal.Neighbours/ConnectedPortals` 供 BFS 寻径。
- **原版参考**：原版 `ActorMove` 失败即返回 False，不会自修复；Taxi 体系以 `TaxiRequest`（Pending/Assigned/Loading/Transporting）+ `BehBoat*` 行为处理上下客，超时/合法性由 `TaxiManager.update` 做定期清理。

## 现存问题场景
- **路径被环境破坏**：前方格子着火、变阻挡或被挖空后，`TryMove` 返回 Abort 直接终止行为，未自动从当前位置重算。
- **门户链失效**：码头被拆、所在海域断开或路径延伸的门户被移除时，`PortalRequest` 仍维持旧链路，船/乘客反复等待或继续驶向无效点。
- **船只错送/偏移**：船找不到码头时只会跳过首节点或卸载到附近任意可登岸格，乘客仍停留在“等待传送”状态，无法自动继续前往原目标。
- **司机缺失**：请求被创建后无人接单，或司机死亡/转为其他行为后请求悬挂，没有重派逻辑。

## 设计目标
- 对**步行/游泳**路径中断进行自动重算，避免单位停滞。
- 对**门户/乘船**链路的异常（门户失效、司机丢失、错送/漂移）自动检测并修复，确保乘客继续向原目标推进。
- 控制回退与重试次数，避免在地形剧烈变化时反复震荡。

## 总体方案
1. **路径会话与恢复上下文（PathSession）**：为每个 Actor 记录目标、最近成功位置、失败次数、上次重算时间、是否处于门户段。`updatePathMovement` 的 Abort 或检测到位移偏差时，按指数退避重新发起 `RequestPath`，并在连续失败后降级为“无门户”纯陆/水路搜索。
2. **门户请求状态机扩展**：在 `PortalRequest` 内增加 `Target`（最终目标）、`CurrentIndex`、`Version`（连通性版本戳）、`DriverAssignAt`、`LastProgressAt`，并明确状态迁移（WaitingDriver→Loading→Driving→Completed/Failed）。`PortalManager.OnUpdateGroup` 轮询请求健康度并触发纠错。
3. **司机派发与重派**：实现 `AssignNewRequestForDriver`，从 `_requests` 中挑选最近、同势力/同港口水域且未有司机的请求给空闲船；若当前司机死亡/长时间无进展（位置未接近下一个门户且计时超过阈值），自动解绑并重派。
4. **门户链自愈**：
   - **拓扑版本检查**：`WaterConnectivityUpdater` 每次重建后递增全局版本；请求若版本过旧，在 `PortalManager` 中调用 `FindPortalPath` 重新求链路（跳过已毁门户），同步调整 ToLoad/ToUnload（第一节点失效则将该节点的 ToUnload 并入下一节点；末端失效则整体降级为直达寻路）。
   - **入口不可达**：司机无法靠近 `PortalTile` 时，尝试使用同水体最近门户替换当前节点并重算后续链；若失败，将乘客标记为需重算普通路径并结束请求。
5. **上下客校验与错送修复**：
   - **Loading 阶段**：将 `ToLoad` 中的乘客设为 is_inside_boat，并在 Portal 步骤消费一次 PathStream，确保乘客不会继续等待旧 PortalStep。
   - **Unload 阶段**：在 `BehBoatTransportUnloadUnits` 前缀后，比较实际卸载点与期望 `PortalTile` 的距离；超出容差则视为错送：对每位乘客清理 Portal 请求、重置 `is_inside_boat/transportID`，并立即调用 `PathFinder.RequestPath` 以当前格到原始 `tile_target` 重算。
6. **路径落空自动补发**：
   - `TryMove` Abort 时，若 Actor 仍有 `tile_target`，设置短 CD（例如 0.3s）后自动重新 `RequestPath`；累积失败超过阈值后切换到纯地表/纯水路模式或直接标记任务失败。
   - 监控“长时间未前进”场景：记录上一次成功移动的 tile id，若 N 秒内未变化且 PathStream 仍有步，则主动丢弃当前流并重算。
7. **安全退出与清理**：当请求被取消/失败时，清理乘客标记、移除 Portal 队列、解除船只占用，避免残留状态影响下一次调度。

## 关键流程示意
- **步行路径中断**：`TryMove -> Abort` → 取消旧任务 → 检查失败计数 → 触发 `RequestPath(current_tile, tile_target)`，首个失败后等待 0.3s，后续按 0.3×2^n 退避，超过上限后降级为不带 Portal 的路径。
- **码头被拆**：`PortalManager` 发现链路节点缺失/版本过旧 → `FindPortalPath` 重新生成；若新链为空，则向所有乘客发起直达路径重算，标记请求 Completed。
- **司机丢失或卡死**：`PortalManager` 检测司机死亡/超时 → 解绑司机并重派最近空闲船；若无可用船则暂存为 WaitingDriver，并定期重试。
- **错送**：卸载点距期望 > 容差 → 直接对乘客重发 `RequestPath` → 释放当前 `PortalRequest`（或更新链路起点为实际卸载点所在门户）。

## 数据与日志
- 记录纠错触发类型、耗时与最终结果（成功重算/降级/失败）到调试日志，便于后续调参。
- 统计每类异常的次数（码头失效、司机重派、错送、步行重算）以评估稳定性。

## 风险与兼容性
- 频繁重算可能带来额外线程负载，需要为 PathStream 取消与对象池回收做防护。
- 司机重派需避免与原版 Taxi 请求冲突；可在挑选司机时排除已有 `taxi_request` 的船并为 Portal 请求单独占用标记。
- 错送后即时重算可能导致乘客在敌对区域触发战斗；必要时可为重算路径添加“安全优先”标签或临时禁战标记。

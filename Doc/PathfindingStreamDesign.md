# 多线程流式寻路设计（含传送门/区域抽象）

## 目标与约束
- **目标**：在不追求全局最优的前提下，快速给出一条可行且“够好”的路线，并能边算边输出 `(WorldTile, MovementMethod)` 供主线程消费。
- **要素**：普通地块、MapRegion（区域级节点）、传送门节点（船坞/车站等，非全连通）、不同移动方式（步行、游泳、坐船等）。
- **性能**：多线程计算，避免持锁访问游戏对象；流式输出保证角色在等待全路径期间仍可先行移动。

## 抽象与数据
- **节点**
  - `TileNode`：单个 `WorldTile`。
  - `RegionNode`：MapRegion（区域级抽象，用于粗粒度搜索）。
  - `PortalNode`：传送门入口/出口，包含 `wait_time`、`cooldown`、`links`（仅与特定 PortalNode 相连，非全连通）。
- **边类型**
  - `WalkEdge`：陆地/桥梁行走，代价 = 距离 / 速度。
  - `SwimEdge`：水面游泳，代价 = 距离 / 游泳速度 + 可能的疲劳惩罚。
  - `SailEdge`：乘船/坐车，代价 = 等待时间 + 航行时间。
  - `PortalEdge`：传送门跳跃，代价 = 等待时间 + 固定传输时间。
- **代价模型（统一用“时间”）**
  - `time = travel_distance / move_speed + wait_time + extra_penalty`
  - 水面宽度用于判断游泳是否可行；若宽度 > 阈值且存在船坞，则优先考虑船/传送。

## 分层图与搜索流程
1. **快照**：在工作线程读取起点/终点位置、所在 MapRegion、可用传送门信息，尽量只读结构化数据（避免 Unity 线程不安全对象）。
2. **区域层粗搜（Region Graph）**
   - 节点：起点所在 Region、终点所在 Region、所有 Portal 所在 Region。
   - 边：Region 邻接（陆地/水域）、PortalEdge（入口 Region → 出口 Region）。
   - 算法：A* / Dijkstra（权重为时间），但仅保留前 `K` 条候选路线（K 小，例如 3~5）以控制组合爆炸。
3. **细节层局部路径**
   - 对粗搜得到的每个“段”（如 起点Region → 入口Portal → 出口Portal → 终点Region），逐段求局部路径：
     - 近距离（曼哈顿 <= `short_range_tiles`）：直接步行/游泳单段 A*。
     - 水面跨越：测量水宽，若宽度 <= `max_swim_width` 采用游泳，否则寻找最近船坞并尝试 Sail。
   - 每完成一段就**流式推送**对应 `(WorldTile, MovementMethod)`。
4. **早期产出**：一旦确定“首段”路线（起点 → 第一关键点），立即输出步骤；后续段落继续在后台补全。
5. **失败回退**：若某段求解失败，降级策略：
   - 去掉传送门，尝试纯步行/游泳；
   - 缩小搜索半径，选择距离更近的 Portal；
   - 最终返回空路径并标记失败。

## 启发式与决策
- **是否用传送门**：比较 `walk_time_direct` 与 `walk_to_portal + wait + transfer + exit_to_goal`，若后者明显更小（设阈值系数，例如 0.8），才采用传送路线。
- **过河策略**：
  - 宽度 <= `max_swim_width` → 游泳。
  - 否则，若有船坞/桥梁在 `search_radius` 内 → 走路到船坞/桥梁再过河。
  - 否则强制游泳或回退为步行绕行（取决于是否容许涉水受伤）。
- **多候选 Portal 组合**：对“入口候选 × 出口候选”按 `walk_in + wait + transfer + walk_out` 排序，取前 `TopN` 进入粗搜，避免全排列。

## 多线程与流式接口
- **生产者**：路径线程（实现 `IPathGenerator`），持续调用 `stream.AddStep(tile, method)`。
- **消费者**：主线程 `updatePathMovement`，每帧 `TryPeek + Consume` 步骤。
- **取消条件**：角色被打断/死亡/目标改变时调用 `PathFinder.Cancel`，线程检查 `CancellationToken` 退出。
- **并发安全**：工作线程仅读不可变数据；写操作（如标记 tile 目标）仍在主线程。

### 伪代码
```csharp
Task GenerateAsync(req, stream, token):
    snapshot = TakeSnapshot(req, token)
    candidates = BuildRegionGraphAndRoutes(snapshot, token)   // 返回若干粗粒度段落
    if candidates.Empty: { stream.Complete(); return; }

    best = PickBestCandidate(candidates)
    foreach segment in best.Segments:
        localPath = SolveLocal(segment, snapshot, token)
        if localPath.Failed: continue fallback...
        foreach step in localPath.Steps:
            stream.AddStep(step.Tile, step.Method)
            if stream.IsCancelled: return
    stream.Complete()
```

## MapRegion 的使用
- 将 Region 视作“超节点”，用以快速跨洲/跨岛决策。
- Region 邻接关系可视为低分辨率网格，减少全地图搜索。
- Portal 连接的 Region 直接添加定向边，权重包含等待时间。

## 参数建议（可调）
- `K`（区域层候选数）：3~5
- `TopN`（Portal 配对候选）：5~8
- `max_swim_width`：依角色体质与环境设定（示例：8~12 tiles）
- `short_range_tiles`：16~24（近距离直接 A*）
- `search_radius`（寻找船坞/桥梁）：20~40 tiles

## 与现有接口的衔接
- 继续使用 `(WorldTile, MovementMethod)` 作为流式输出单元。
- `MovementMethod.Sail` 在主线程的 `updatePathMovement` 中留空，可后续接入乘船上/下船、等船逻辑。
- 生成器可替换：通过 `PathFinder.Instance.UseGenerator(new PortalAwareGenerator(...))` 注入上述策略。

## 迭代方向
- 引入动态等待时间（船班/车次表）与拥堵成本。
- 为 Portal 建立“可靠性”权重（被毁坏/不可用时自动避开）。
- 加入风险评估（火焰、敌对势力）作为额外惩罚项，形成“安全优先”路线。 

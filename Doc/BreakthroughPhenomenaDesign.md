# 修士突破异象系统设计（练气→筑基→金丹→元婴）

**版本**: v0.1  
**状态**: 设计草案（待落地）  
**最后更新**: 2025-XX-XX  

---

## 目标与约束
- 目标：为练气→筑基、筑基→金丹、金丹→元婴三个关键跃迁提供沉浸式异象演出，强化境界感与反馈。
- 约束：人物是像素小人，不渲染具体五官/肢体；优先使用粒子系统（参考 `Source/Core/Systems/Render/RenderStatusParticleSystem.cs`、`Source/Content/Systems/Render/RealmElementParticleRenderSystem.cs`）。序列帧仅在必要时补充。
- 性能：沿用全局共享粒子发射器的思路，控制粒子数量与生命周期，保持 60fps 目标。
- 可配置：通过 JSON/配置开关控制开关、粒子数量、持续时间，便于低配模式降级。

---

## 现有可复用模块
- 视觉：`RealmVisual` 组件与 Aura/Indicator/Element 粒子渲染系统；`RealmVisual.visual_state` 可加状态位（突破态）。
- 粒子：全局共享 `ParticleSystem`（状态粒子/元素粒子），可复用创建逻辑与渐隐曲线。
- 资源：`GameResources/cultiway/special_effects/aura/*` 现有光晕贴图；`GameResources/cultiway/effect/simple_tornado` 可做灵气漩涡雏形；闪电/云层可参考 vanilla `fx_lightning_*`。
- 触发：`Cultisyses.Xian.TryPerformUpgrade`、`BehXianLevelup` 行为链；`WorldLogs.LogCultisysLevelup` 用于日志播报。

---

## 异象分境界设计（粒子化落地）

### 1) 练气 → 筑基：灵气化液，筑就道基
- 场景感：室内闭关，灵气漩涡吸入。
- 粒子表现：
  - 吸附漩涡：基于圆形粒子环，缩放 0.6→1.0，旋转加速；颜色淡蓝/白。
  - 灵液滴落：中心向下掉落 1~3 粒子（大颗粒，透明度衰减）。
  - 排浊：短时灰色粒子喷出（半径极小），0.5s 内衰减。
  - 体表灵光：光晕透明度拉高 20%，3s 内回落。
  - 声效占位：江河声（若无资源则留 TODO）。
- 属性反馈：夜视/寿命提示用日志或浮字。

### 2) 筑基 → 金丹：液凝为丹，大道初成
- 场景感：百里灵气漏斗云、天灵灌注。
- 粒子表现：
  - 漏斗灵云：在头顶生成小型云团粒子（使用云贴图或白色粒子），沿 Y 轴缓慢旋转、收拢。
  - 灵流下灌：自顶向下的流线粒子束（蓝→金渐变），射向角色中心。
  - 虚丹成形：腹部位置生成小型旋转粒子团（金色核心，外圈微光），持续 2~3s。
  - 成丹爆闪：0.2s 金色亮闪，随后淡金色宝光环绕 3s。
  - 声效：大道清音（占位 TODO）。

### 3) 金丹 → 元婴：丹破婴生，我命由我
- 场景感：裂丹—爆破—元婴显化，伴随祥云或心魔劫雷。
- 粒子表现：
  - 裂丹预兆：腹部金色粒子球闪烁，间歇性裂纹闪（用噪声 alpha 掩码或闪烁）。
  - 爆破冲击：瞬时环形冲击波（使用爆闪贴图或扩张粒子），强光 0.15s。
  - 元婴虚影：头顶/腹部小型紫色渐隐粒子团（替代“寸高婴儿”形象），带缓慢旋转的多彩道韵粒子。
  - 神识暴涨：向外扩散的半透明粒子丝（淡紫/彩色），范围 1.5~2.5 tile。
  - 天象随机：30% 祥云（白/金云粒子上升），30% 心魔劫雷（自研闪电粒子），其余无天象。
  - 声效：爆裂低吼 + 远雷（占位 TODO）。

---

## 技术实现方案

### 组件与状态
- `RealmVisual.visual_state` 增加突破态标识，渲染系统可根据状态叠加特效。
- 新组件 `XianBreakthroughState`（已建空壳）：记录最近突破层级、演出计时，避免连播。
- 可选：`BreakthroughEvent` 结构体（临时对象）传递给渲染系统，包含 level_from/level_to、元素、强度。

### 触发流程（逻辑链）
1. `Cultisyses.Xian.TryPerformUpgrade` 内，检测升级成功后：
   - 写入/更新 `XianBreakthroughState`（存 level_to、visual_timer=duration、visual_level）。
   - 将 `RealmVisual.visual_state` 切为突破态（3），并记录目标境界。
   - 发送世界日志（可复用 `WorldLogs.LogCultisysLevelup`），增加额外文本/提示。
2. 行为层 `BehXianLevelup` 成功时附加调用 `BreakthroughVisualTrigger.Trigger(ae, from, to)`（新静态工具）。
3. 植物/水修分支可共用（参数化）。

### 渲染与特效
- 新系统 `BreakthroughVisualSystem`（渲染域，早于 Aura）：
  - 查询 `ActorBinder + RealmVisual + XianBreakthroughState`，判定 `visual_timer>0`。
  - 根据 `visual_level` 选择对应效果模板，调用统一的 `BreakthroughParticleEmitter`。
  - 支持多层粒子：吸附/冲击波/云/雷等；每层限定发射数与生命周期。
  - 使用共享 `ParticleSystem`（仿 `RealmElementParticleRenderSystem.GetEmitter()`）：World 空间，Billboard，colorOverLifetime 渐隐。
  - 层级：`EffectsTop_5`，sortingOrder 介于 Aura(-5) 与 Indicator(5) 之间，例如 -2~2。
  - 粒子尺寸、速度、半径与角色 `stats[S.scale]`、`RealmVisual.ScaleMultiplier` 挂钩。
- 光晕联动：
  - Aura 渲染读到 `visual_state==Breakthrough` 时临时放大 1.2~1.4，alpha 提升 15%。
  - Indicator 渲染：突破后 2s 内提升 alpha 上限，元婴突破时强制展示元婴图标（如有）。
- 天象：
  - 祥云：小型云粒子向上漂移（可用云贴图），淡出 2~3s。
  - 劫雷：调用 `EffectsLibrary.spawnAt("fx_lightning_small")` 或自制闪电粒子；需处理特效限流。

### 资源规划
- 贴图：若缺则新增占位：
  - `cultiway/special_effects/aura/break_qi_to_found.png`（小漩涡）
  - `cultiway/special_effects/aura/break_found_to_jin.png`（漏斗云/灵流）
  - `cultiway/special_effects/aura/break_jin_to_yuan.png`（裂丹闪、冲击波）
  - 如未准备序列帧，全部退化为彩色粒子 + 简单光圈。
- 配置：新增 `Content/RealmVisual/breakthrough_config.json`（或扩展现有 `realm_visual_config.json`）：
  - 每阶段的粒子数量、半径、速度、颜色、持续时间、是否播放天象。
  - 开关：总开关、低配开关、雷/云开关。

---

## TODO 清单（可直接交给 AI 生成代码）
1. **组件与数据**
   - [ ] 完善 `Source/Content/Components/XianBreakthroughState` 字段：`last_level`、`visual_level`、`visual_timer`、`rng_seed`。
   - [ ] 在 `RealmVisual` 添加常量：`VisualStateBreakthrough`（已定义常量需检查引用）。
   - [ ] 定义 DTO/配置读取 `breakthrough_config.json`（粒子参数表）。

2. **触发逻辑**
   - [ ] 在 `Cultisyses.Xian.TryPerformUpgrade` 成功分支写入 `XianBreakthroughState`，记录 from/to。
   - [ ] 增加工具类 `BreakthroughVisualTrigger`，封装写组件、重置计时的逻辑。
   - [ ] `BehXianLevelup`、`BehPlantXianLevelup`、`BehWaterCultivateLevelup` 成功后调用触发工具。
   - [ ] 日志：突破成功时附加“异象触发”文本（可用 `special2` 或新 key）。

3. **渲染系统**
   - [ ] 新建 `Source/Content/Systems/Render/BreakthroughVisualSystem.cs`：
     - Query `ActorBinder + RealmVisual + XianBreakthroughState`.
     - 按 `visual_level` 调用对应粒子模板。
     - 复用全局 `ParticleSystem`（仿 `RealmElementParticleRenderSystem`），支持多层 Emit。
     - 计时递减 `visual_timer`，为 0 时重置 `RealmVisual.visual_state=Default`。
   - [ ] 在 `Content/Manager.Init` 将系统插入 `GeneralRenderSystems`，位于 Aura/Element 粒子之前。
   - [ ] Aura/Indicator 系统中识别 `VisualStateBreakthrough`，临时加强 alpha/scale。

4. **粒子模板实现**
   - [ ] Qi→Foundation：吸附漩涡、灵液滴落、排浊粒子、体表亮度提升。
   - [ ] Foundation→Jindan：漏斗云、灵流下灌、虚丹旋转粒子、金光爆闪。
   - [ ] Jindan→Yuanying：裂丹闪烁、环形冲击波、元婴虚影粒子、神识外放丝；30% 祥云、30% 劫雷。
   - [ ] 统一颜色表：淡蓝(#CFEFFF)、金(#FFD700)、紫(#9370DB) 等；低配模式仅保留核心两层粒子。

5. **资源与配置**
   - [ ] 若无贴图，新增占位 PNG 至 `GameResources/cultiway/special_effects/aura/`，更新 `sprites.json`。
   - [ ] 配置文件示例：粒子数、半径、生命周期、旋转速度、闪电概率。

6. **验证与调试**
   - [ ] 在 Debug 模式添加命令：强制触发指定突破视觉（跳过逻辑），便于迭代。
   - [ ] 性能检查：单屏 20 人突破时粒子数上限、FPS 监控。
   - [ ] 低配开关：禁用天象/冲击波，粒子数量减半。

---

## 降级与兼容
- 无配置/无贴图：回退为基础 Aura 放大与单色粒子；不阻塞突破流程。
- MiniMap 渲染：与现有渲染系统一致，在小地图时不发射粒子。
- 植物/水栖单位：同模板，但半径/高度偏移需调整（配置项支持）。

---

## 备注
- 所有新增注释保持中文，符合项目规范。
- 如需额外序列帧，优先保持 64×64/128×128，保证像素风一致。


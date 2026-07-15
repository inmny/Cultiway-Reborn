# Cultiway UI 开发规范

本文规定 Cultiway 的 UI 分层、视觉语义、组件用法和验证要求。涉及窗口、弹层、列表、详情页、HUD、底栏按钮、Tooltip、滚动容器或原版 UI prefab 时，修改前必须阅读本文。

## 一、核心原则

- 业务 UI 只负责数据绑定、交互编排和业务命令，基础控件、布局和公共视觉统一复用。
- 优先保留 WorldBox 原版窗口、滚动条、Tooltip、按钮动画、导航和历史返回行为。
- 公共能力按项目级、功能族级和窗口级分层复用，不建立覆盖所有职责的巨型工厂。
- 公共 UI 层不得依赖 `Source/Content` 的具体玩法类型；Content 只能在上层组合和扩展公共控件。
- 修改范围应与当前功能一致，不为目录整齐进行跨功能批量移动或无关重做。

## 二、类型与所有权

开始实现前先确定 UI 类型：

| 类型 | 典型对象 | 实现方式 |
| --- | --- | --- |
| 代码构建窗口 | 万法阁、百宝阁、魔网、配置窗口 | Foundation + Controls + 功能族组件 |
| 原版 prefab 适配 | 宗门窗口、地理区域窗口、Mod 信息窗口 | 专用 Adapter 一次解析原版层级 |
| 重复动态条目 | 列表 Row、图标格、预览实体 | `APrefabPreview<T>` / `MonoObjPool<T>` |
| 生物信息页 | CreatureInfoPages | 页面负责领域数据，复用公共视觉与滚动结构 |
| HUD/世界覆盖层 | 附体界面、世界标记、选中信息 | 独立 Overlay，共用主题和资源，不套窗口结构 |
| 底栏与力量按钮 | `PowerButton`、Tab、能力入口 | 只注册并适配原版交互语义 |

按以下边界决定代码位置：

1. 两个不相关功能都需要，且视觉与行为语义相同：放入 `Foundation` 或 `Controls`。
2. 只在同一功能的多个窗口重复：做功能族组件。
3. 只在一个窗口出现：留在窗口内部，不为潜在复用提前抽象。
4. 依赖原版 prefab 固定节点：放入对应 Adapter 或该 prefab 的唯一拥有者。

注册职责必须保持单一：`Source/UI/Manager.cs` 初始化公共基础设施和窗口壳，`Source/Content/UI/Manager.cs` 注册具体玩法窗口、页面与力量按钮。

## 三、架构分层

```text
Source/UI/
  Foundation/  原子元素、布局、主题、资源、状态和 Tooltip
  Controls/    有完整结构与行为的复合控件
  Adapters/    原版 prefab 和原版控件的适配边界
  Prefab/      池化条目与可复用 prefab 组件
  ...          功能窗口与功能族组件
```

### 3.1 Foundation

- `UiTheme`：字体、尺寸阶梯和语义颜色的唯一入口。
- `UiResources` / `UiIcons`：原版公共 sprite、通用动作图标和 prefab 模板的唯一入口。
- `UiLayout`：`RectTransform`、固定尺寸、LayoutGroup 和清理子节点等纯布局操作。
- `UiElements`：文本、图片、按钮、输入框和 Toggle 等原子元素。
- `UiStateStyle`：统一应用 `Normal`、`Selected`、`Disabled`、`Destructive`、`Success`、`Warning`、`Error` 状态。
- `UiTooltip`：包装原版 `TipButton`，保留原版悬停和点击行为。

Foundation 不得引用 ECS 实体、法宝、宗门、法术或其他 Content 类型。

### 3.2 Controls

- `UiScrollPane`：统一滚动表面、Viewport 留白和原版滚动条槽位。
- `UiSearchField`：统一搜索输入的结构和状态。
- `UiSegmentedTabs`：统一互斥选择及中性灰色选中态。
- `UiOptionMenu`：统一遮罩、搜索、自适应列数、最大可见行数、滚动区和关闭区。
- `UiModal`：统一打开、关闭以及所属窗口 `CanvasGroup` 的恢复。
- `UiListRowChrome`：只管理 Row 表面、点击区域和语义状态。
- `UiEmptyState`：统一空内容提示。

复合控件应返回有类型对象并暴露稳定槽位，不应只返回一个需要调用方继续按名称查找的 `GameObject`。Options/Config 只描述尺寸、行为和语义 Variant，不携带任意共享颜色或任意公共 sprite 路径。

### 3.3 Adapters

- 固定节点和组件只在 Adapter 边界解析、验证一次，错误信息应包含 prefab 与节点路径。
- 先在 `.GameSource/Assets` 核对真实层级和组件类型，不能根据节点名称猜测 LayoutGroup。
- Adapter 对外提供有类型引用，业务代码不得继续散落 `transform.Find(...)`。
- 不在调用链各层重复添加相同的 `null` 兜底。
- 必须包装并保留 `ScrollWindow`、`TipButton`、`PowerButton` 的原版行为，不能另写近似实现。

代码构建窗口使用 `UiWindowContext.Bind()` 获取原版滚动条模板，并按实际尺寸校正 `windows/empty` 的返回按钮。基于 `windows/settings` 的页面实现 `IUiTabbedPage` 并交给 `UiTabbedWindowAdapter`。底栏配置按钮通过 `UiPowerButtonAdapter` 调整表面。

### 3.4 功能 UI

功能窗口只承担以下职责：

- 将领域数据转换为展示数据。
- 组合公共控件和本功能组件。
- 绑定事件并在统一的 `Refresh()` 中刷新状态。
- 将用户命令交给对应 Manager、Service 或 System。

同一功能多个窗口共享的详情头、目录 Row、工具栏等应做成功能族组件，但不得下沉为带有玩法类型的公共控件。

## 四、组件使用规则

- 原子控件使用 `UiElements`，布局使用 `UiLayout`，公共资源从 `UiTheme`、`UiResources` 和 `UiIcons` 获取。
- 滚动区、搜索、Tab、选项菜单、模态面板、空状态和 Row 状态优先使用对应 Controls。
- 离散筛选条件和排序方式使用 `UiOptionMenu` 直接选择，不允许按钮逐次循环候选项。
- 候选项可能增长时必须支持搜索；触发按钮始终显示当前选择。
- 二元设置使用 Toggle；互斥页面使用 `UiSegmentedTabs`；数值设置使用输入框、步进器或滑块。
- 图标按钮使用熟悉图标并配置 Tooltip；只有清晰命令才使用图标加文字。
- 模态框必须有四向内边距，正文和底部动作按钮不得贴住容器边缘。

代码构建窗口的典型用法：

```csharp
UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);
UiScrollPane catalog = UiScrollPane.CreateVertical(parent, "Catalog", 318f, 284f);
catalog.AttachOriginalScrollbar(context.ScrollbarTemplate);
catalog.SetSurface(UiSurface.WindowEmpty, UiTheme.Current.Metrics.SpacingMd);
```

## 五、视觉规范

### 5.1 主题与资源

- 共享字体、颜色、间距、尺寸和 sprite 路径只能从主题或资源入口读取。
- 领域颜色可以留在功能内，例如五行色、品质色和图表色；普通文本、选中、成功、警告和错误颜色必须统一。
- 通用动作图标只保留一份；器形、法术等领域图标由所属功能维护。
- 优先复用 `.GameSource/Assets` 中的原版资源；确实缺失时再添加 Mod 资源并登记用途。
- 间距使用 `UiTheme.Current.Metrics` 的 `2/4/6/8/12` 档位，控件高度使用 `22/24/28` 档位，不在业务窗口散落近似数值。

### 5.2 表面与状态

- `UiSurface.WindowInner`（`windowInnerSliced`）：菜单、筛选区、紧凑控制区和普通滚动容器。
- `UiSurface.WindowEmpty`（`windowEmptyFrame`）：大面积目录、工作区和空白预览区。
- 普通按钮使用统一按钮表面；选中状态使用中性灰色，不使用棕色强调。
- 禁用、警告、错误和破坏性操作必须使用对应语义状态，不能靠调用点临时改色。
- Row 左侧不得添加没有状态含义的装饰竖条。品质、选中或警告标记必须具有明确且一致的语义。

### 5.3 间距与滚动区

- 窗口内容、面板内容、菜单内容和动作区都必须保留四向内边距。
- 滚动框与外层背景之间必须保留外边距，不能横向撑满并紧贴边框。
- 滚动条位于独立右侧槽位，其左缘邻接滚动框右缘，不得覆盖内容或放进框内左侧。
- Viewport 必须预留滚动条宽度；滚动条显隐时正文左侧位置不得跳动。
- Row 图标与左边缘保持统一距离；图标、正文和右侧动作区使用稳定列宽。
- 空列表、单项、一页边界和超长列表必须保持相同留白。

### 5.4 信息层级

- 标题、摘要、正文、提示和状态使用固定字号与颜色层级；紧凑工具窗口不使用超大标题。
- 详情面板默认采用左上角正方形预览、右侧名称与摘要、下方详细内容的结构。
- 不嵌套卡片，不用大量无意义边框；优先通过留白、标题和单层表面划分区域。
- 动态文本不得挤动按钮、越过边界或遮挡其他内容。
- 所有可见文本和 Tooltip 必须本地化，并验证中文、英文和长文本。

## 六、生命周期与刷新

- 构造或 `Build()` 阶段只创建节点、绑定事件并缓存组件引用。
- `Bind()` 负责更换对象，`Refresh()` 负责一次性刷新文本、图标、可见性、交互态和语义状态。
- 不在多个回调中零散修改同一控件的颜色、文本和 `interactable`。
- 重复条目优先使用 `APrefabPreview<T>` 或 `MonoObjPool<T>`；只有少量、短生命周期菜单项可以销毁重建。
- 关闭窗口或销毁组件时解除外部事件订阅，避免重复打开后多次触发。
- 动态布局只在必要根节点重建，不在每个条目刷新时重建整个窗口。
- 世界工具的一次执行应使用打开或开始时冻结的数据快照，UI 不在结算过程中改变该次操作的语义。

## 七、扩展规则

- 新增公共能力前先确认现有 Foundation、Controls 和功能族组件无法表达该语义。
- 新的公共控件应至少有两个确实具有相同行为的调用方；否则先留在功能内部。
- 需要新视觉差异时先定义语义 Variant，再扩展主题或控件，不能在调用点临时形成隐含风格。
- 新增原版资源路径前检查 `.GameSource/Assets`，新增固定节点查找时放入 Adapter 或唯一拥有者。
- 扩展公共 API 后同步更新本文，并迁移实际调用方。
- 不恢复 `WanfaUiFactory`、`BaibaoUiFactory`、`UIUtils`，也不新增功能专属的通用 `FooUiFactory`。

## 八、检查清单

新增或修改 UI 时依次检查：

1. 明确 UI 类型、所有者和复用范围，并找到最相近的现有实现。
2. 核对 `.GameSource/Assets` 中的原版 prefab、组件、sprite 和资源路径。
3. 使用现有公共控件或功能族组件，没有建立重复工厂。
4. 共享颜色、尺寸、字体、图标和 sprite 路径没有写在业务窗口中。
5. 四周留白、滚动条槽、Row 图标左间距和右侧动作区稳定。
6. 普通、悬停、选中、禁用、空状态和模态关闭行为完整。
7. 所有文本和 Tooltip 已本地化，长文本不会覆盖控件。
8. 重复列表使用对象池，事件只绑定一次，关闭后无残留订阅。
9. 运行 `dotnet build Cultiway.csproj -c Debug`。
10. 在游戏内验证空列表、单项、一页边界、超长列表、所有 Tab、模态开关、重复开关窗口以及较小分辨率。

涉及 UI 的提交或 PR 应附截图或 GIF，并说明新增资源和手动验证路径。

## 九、禁止事项

- 禁止新增覆盖多个职责的巨型静态 UI 工厂。
- 禁止从业务窗口直接依赖另一个不相关功能的 UI 工具。
- 禁止在功能代码中重复硬编码公共 sprite 路径、颜色和控件尺寸。
- 禁止为了统一外观重写 `TipButton`、`PowerButton` 或 `ScrollWindow` 的原版行为。
- 禁止在每层调用链重复添加同一组 `null` 兜底。
- 禁止把玩法数据、ECS 查询或 Content 类型放入 Foundation/Controls。
- 禁止在公共类型尚未实现时，根据本文创建局部同名替代品。

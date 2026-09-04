# 架构总览

本文是整个模组的真实结构图与落点规则。写任何代码之前先读它，用来回答「这段代码该放哪、要接进哪些注册点」；写代码本身的风格约束读 [CODING.md](CODING.md)，做 UI 读 [UI.md](UI.md)，设计新系统读 [PLAN.md](PLAN.md)。

## 项目是什么

WorldBox 的修仙玩法模组「修仙[重制]」（mod id `CULTIWAY`）。技术底座：

- **NML（NeoModLoader）** 加载模组。NML 按约定反射找到 `Source/ModClass.cs` 的 `ModClass : BasicMod<ModClass>` 作为唯一入口。
- **双轨编译**：进游戏时 NML 自己把 `Source/` 编译成模组（产物在游戏目录 `StreamingAssets/mods/NML/CompiledMods/CULTIWAY.dll`）；`dotnet build Cultiway.csproj` 的产物只给 IDE 和静态检查用，游戏不加载它。所以改完代码直接运行 `../worldbox.exe` 测试，不需要先 dotnet build。
- **Harmony** 补丁修改原版行为；**Friflo ECS** 承载模组自己的运行时实体；原版游戏源码与资源在 `.GameSource/`（本地参考，查 API 和资源命名用）。

## 模组生命周期（顺序即依赖）

入口 `Source/ModClass.cs` 的 `OnModLoad()`，初始化顺序如下，新代码接进哪一层取决于它依赖什么：

1. 全局异常兜底补丁（`Debug/FinalizerPatch`）
2. 本地化键树（`LK.Root`）、日志（`CultiLog`）、临时 prefab 容器
3. `UI.Manager.Init()`——底栏「修仙」Tab 基础设施
4. `new WorldboxGame()`——构造时**反射发现** `Source/Worldbox*.cs` 全部嵌套库并按 `[Dependency]` 拓扑排序，随后 `Init()` 逐个初始化
5. `Core.Libraries.Manager`——模组自有资产库批量注册进游戏 `AssetManager`
6. `ModSaveManager`（独立 JSON 存档库）、Friflo `EntityStore`（唯一世界 `ModClass.I.W`）
7. `LoadLocales()`（扫描 `Locales/` 全部 csv/json）
8. 四个 `SystemRoot`：`GeneralLogicSystems / GeneralRenderSystems / TileLogicSystems / TileRenderSystems`，加事件处理组等
9. `SkillV3.Init()` → `Patch/Manager.Init()`（Harmony 补丁安装）→ `Content/Manager.Init()`（内容库注册 → 内容 ECS 系统 → 内容补丁）——**内容层最后初始化**
10. 小世界、万法阁/百宝阁服务、寻路等收尾

运行期：渲染系统每帧走 Unity `Update`；逻辑系统按游戏内时间驱动，外面套着帧调度器（`Core/Performance/`）。`Start()` 阶段做资产链接后的 `PostInit`（窗口实例化在这里）。`Reload()` 支持热重载（重扫 `GameResources`、重载本地化、逐库 `ICanReload.OnReload()`）。

## 目录地图

```
Source/
  ModClass.cs            唯一入口：生命周期、SystemRoot、每帧驱动、热重载
  WorldboxGame.cs        原版资产总库（反射发现下面的 Worldbox*.cs）
  Worldbox*.cs (21个)    每个文件扩展一种原版资产库（BaseStats/StatusEffects/PowerTabs...）
  Abstract/              自研框架基元：ExtendLibrary、ICanInit/ICanReload、
                         AssetId/GetOnly/CloneSource/Dependency 特性、ActorExtend 基类
  AbstractGame/          对游戏本体的薄壳（AGame）
  Core/                  模组通用框架（见下）
  Content/               玩法内容（修仙域，见下）；新玩法代码默认放这里
  Patch/                 全局 Harmony 补丁（PatchXxx 命名即自动安装）
  UI/                    公共 UI 基础设施与窗口壳（分层规范见 Prompts/UI.md）
  Utils/                 无状态工具与扩展方法（Extension/XxxTools.cs）
  Const/                 常量与枚举（含可调开关 GeneralSettings）
  Tables/                历史统计 SQLite 表定义
  LocaleKeys/            类型化本地化键树（遗留；主流做法是直接写 csv）
  Debug/                 Try.Start 包裹、断言、性能跑分

Source/Core/（通用框架，不含具体修仙玩法语义）
  Components/            ECS 组件（struct : IComponent）
  Systems/Logic|Render/  核心 ECS 系统
  EventSystem/           事件总线 EventSystemHub + 事件定义 + 处理系统
  Libraries/             模组自有资产库（XxxAsset + XxxLibrary 成对）+ Manager
  Persistence/           独立 JSON 存档（ModSaveManager + SaveDocumentDefinition + 迁移链）
  SkillLibV3/            技能子框架（组件/系统/施法/弹道/命中/视觉/万法阁）
  SubWorlds/ GeoLib/ Combat/ Performance/ Pathfinding/ Logging/ Localization/
  根级文件               ActorExtend（原版 Actor 的实体绑定）、Sect/GeoRegion 管理器等

Source/Content/（修仙玩法域）
  Manager.cs             内容入口：反射 Init 全部 ICanInit（含各 ExtendLibrary），
                         手工 Add 内容 ECS 系统，最后装内容补丁
  Actors/Buildings/StatusEffects/GodPowers/...cs   内容侧 ExtendLibrary 注册类
  Cultisyses.Xian.*.cs   按境界拆分的 partial（炼气/筑基/金丹/元婴/化神）
  SkillEntities.*.cs     按元素拆分的技能定义
  Components/            内容 ECS 组件；Systems/Logic|Render/  内容 ECS 系统
  Libraries/             内容自有资产库 + Manager
  Artifacts/             法宝全家（能力/原子/外观目录/编排/服务/百宝阁）
  SpiritVeins/ Sects/ KnightCombat/ Magic/   大系统各自成目录（服务+系统+数据）
  Behaviours/            AI 行为节点（Beh*.cs）与条件（Conditions/Cond*.cs）
  Combat/ Visuals/ MapModeVisuals/ UI/ Patch/ Events/ AIGC/ Const/ Extensions/

Content/                 【数据】代码显式加载的 JSON/CSV/OBJ/AssetBundle
GameResources/           【资源】NML 约定自动扫描的贴图/动画/皮肤（cultiway/ 是模组命名空间）
Locales/                 【文本】本地化 csv（key,cz 两列），自动加载
Assemblies/              随包分发的第三方 DLL（Friflo/MathNet/LibTessDotNet）
Scripts/                 Python/PowerShell 辅助脚本（生成器/数据转换/统计）
Doc/                     功能规划文档（见 PLAN.md 约定）
Prompts/                 AI 工作规约（本文件 + CODING/UI/PLAN）
.GameSource/             原版游戏源码与资源快照（gitignore，仅本地参考）
```

## 六个关键机制

### 1. ExtendLibrary——扩展原版资产库

给原版资产库（BaseStats、StatusEffects、PowerTabs、ActorTraits…）加内容，写一个 `Source/Worldbox<库名>.cs`（或内容侧 `Source/Content/Xxx.cs`）：

```csharp
public partial class WorldboxGame
{
    public class BaseStats : ExtendLibrary<BaseStatAsset, BaseStats>
    {
        [GetOnly("armor")] public static BaseStatAsset Armor { get; private set; }      // 引用原版
        [AssetId(nameof(IronArmor))] public static BaseStatAsset IronArmor { get; ... } // 新增（id 自动加前缀）
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit() { /* 配置属性 */ }
    }
}
```

注册是全自动的：`WorldboxGame` 反射发现全部嵌套库；`Content/Manager` 反射 Init 全部 `ICanInit`。初始化顺序有要求时标 `[Dependency(typeof(其他库))]`。`CloneSourceAttribute` 用于克隆原版资产改属性。

### 2. ECS——组件与系统

- 组件是 `struct : IComponent`，通用放 `Core/Components/`，玩法专属放 `Content/Components/`；
- 系统继承 `QuerySystem<T1,T2...>`（逐实体）或 `BaseSystem`（全局），低频任务继承 `Core/ThrottledSystem.cs`；
- **没有自动扫描**：Core 级系统在 `ModClass.OnModLoad` 手工 `Add`；**玩法级系统一律在 `Content/Manager.cs` 的 `Init()` 里 `ModClass.I.GeneralLogicSystems.Add(new XxxSystem())`**；
- 原版 Actor 与实体的绑定：组件里挂 `ActorBinder(actorId)`，访问经 `ActorExtendManager.Get(actor)`，全程锁 `EntityStoreLock.GlobalLock`；
- 事件走 `EventSystemHub.Publish`，处理系统继承 `GenericEventSystem<T>` 并加进 `LogicEventProcessSystemGroup`。

### 3. Harmony 补丁——命名即安装

新补丁只需：类名以 `Patch` 开头、放进 `Source/Patch/`（全局）或 `Source/Content/Patch/`（内容域）命名空间，两个 `Patch/Manager` 会反射自动 `CreateAndPatchAll`，无需任何注册。方法用 `[HarmonyPostfix, HarmonyPatch(typeof(T), nameof(T.M))]` 标注，Postfix 为主。安装后需要额外动作的类写 `public static void SpecialPatch()`。

### 4. 资源三轨

| 轨道 | 目录 | 加载方式 | 新增时要改代码吗 |
|---|---|---|---|
| 贴图/动画/皮肤 | `GameResources/` | NML 按约定自动扫描，代码用相对路径键引用（`SpriteTextureLoader.getSprite("cultiway/icons/xxx")`） | 纯补图不用；新路径键要改引用代码 |
| 数据/模型/着色器 | `Content/` | 代码显式加载，路径常量在对应 Setting/Loader 里（如 `ArtifactAppearanceCatalogLoader`） | 要（有加载器就加条目，没有就写） |
| 本地化 | `Locales/*.csv` | NML 自动加载（`key,cz` 两列） | 不用；代码里 `"key".Localize()` 或 `LocalizedText.setKeyAndUpdate` |

`GameResources/cultiway/` 是模组命名空间：`icons/`（分类图标）、`effect/<名>/<变体>/{appearance,runtime,dissipation}/`（特效序列帧 + `sprites.json`）、`special_effects/`（世界级贴图）。单位皮肤放 `actors/species/civs/<种族id>/<皮肤>/`（`male*/female*/warrior*` 前缀目录自动发现，`sprites.json` 用 `Specific` 逐帧 pivot；特效的 `sprites.json` 用 `Default 0.5` 中心）。新增资源前先在 `.GameSource/Assets` 核对原版同类资源的命名。

### 5. 持久化两轨

- **随原版存档走**：世界状态管理器（`SectManager` 等）加进 `WorldboxGame.AddMetaMainManager` 列表；实体数据天然存在 `ModClass.I.W`；
- **独立 JSON 存档**（跨存档的全局数据）：`Core/Persistence/ModSaveManager` + `SaveDocumentDefinition<T>` + 版本迁移链，原子写双备份，参考 `Content/Artifacts/Baibao/Persistence/`。

### 6. UI 分层

公共基础设施与窗口壳在 `Source/UI/`（`Foundation/Controls/Adapters/Prefab` 分层，窗口继承 `TabbedWindow` / `WindowMetaGeneric` 或克隆原版 prefab）；玩法窗口、神力按钮、生物信息页在 `Source/Content/UI/`。底栏按钮经 `UI.Manager.AddSection/AddButton` 注册。细节规范以 [UI.md](UI.md) 为准。

## 依赖方向

- `Content → Core` 是正规方向；`Source/UI` 的 Foundation 层**禁止**引用 Content（UI.md 红线）；
- `Core → Content` 存在约 13 处历史遗留（宗门等），**新代码禁止再加**；感到需要反向引用时，把共享概念下沉到 Core 或把功能挪进 Content；
- 公共 UI 控件进 `UI/Controls/` 的门槛：至少有两个调用方。

## 新代码落点速查

| 要做什么 | 代码放哪 | 注册点 |
|---|---|---|
| 给原版资产库加内容（新状态/新属性/新神力Tab...） | `Source/WorldboxXxx.cs` 或 `Source/Content/Xxx.cs` 的 ExtendLibrary | 无（自动发现） |
| 新玩法系统（灵脉/元神级别） | `Source/Content/<系统名>/` 目录：服务 + 数据；组件进 `Content/Components/`；系统进 `Content/Systems/Logic|Render/` | `Content/Manager.cs` Init：`GeneralLogicSystems.Add(...)` |
| 新状态效果 | `Content/StatusEffects.cs` 加属性并配置 | 无（自动注册）；逻辑挂 `Content/Combat/` 或独立系统 |
| 新种族/怪物 | `Content/Actors.<种族>.cs` + AI 节点 `Content/Behaviours/` + 任务 `Content/ActorTasks.cs` | 无；皮肤贴图按目录约定自动发现 |
| 新建筑 | `Content/Buildings.cs`（或其 partial） | 无；贴图按 `buildings/<id>/` 约定 |
| 新 ECS 组件/系统/事件（框架级） | `Core/Components|Systems|EventSystem/` | `ModClass.OnModLoad` 手工 Add |
| 新 Harmony 补丁 | `Source/Patch/PatchXxx.cs` 或 `Source/Content/Patch/PatchXxx.cs` | 无（类名即安装） |
| 新模组自有资产类型 | `Core/Libraries/XxxAsset.cs` + `XxxLibrary.cs`；数据进 `Content/Xxxs.cs` | `Core/Libraries/Manager.cs` 四处（字段/Init/LinkAssets/PostInit） |
| 新窗口/神力按钮 | 公共→`Source/UI/`；玩法→`Source/Content/UI/` | `UI.Manager.AddSection/AddButton`；先读 UI.md |
| 新本地化键 | `Locales/<域>.csv` 加一行 `Cultiway.<域>.<名>,中文` | 无 |
| 新独立存档文档 | `XxxSaveDefinition`（定义+迁移）放进使用它的服务目录 | 服务 Init 里 `ModClass.I.Persistence.Register(...)` |
| 新工具/扩展方法/常量 | `Source/Utils/`、`Source/Utils/Extension/`、`Source/Const/` | 无 |
| 新数据表/模型/着色器 | `Content/<域>/` | 对应 Setting/Loader 的路径常量 |
| 新贴图/特效/皮肤 | `GameResources/cultiway/...`（按第 4 节约定） | 见第 4 节表格 |
| 新辅助脚本 | `Scripts/`（动词开头命名；包用 `python -m Scripts.<名>`） | 无；产物不进 git |
| 新大子系统（小世界/技能库级别） | `Source/Core/<名>Lib/`（框架）+ `Source/Content/`（玩法数据） | 自带 Manager 挂 SystemGroup；`ModClass.OnModLoad` new 并 Init |

## 构建与发布

- 开发迭代：改 `Source/` → 运行 `../worldbox.exe`，NML 自动编译加载（支持热重载）；
- IDE/静态检查：`dotnet build Cultiway.csproj -c Debug`（产物不被游戏加载）；
- 发布：`pack.ps1` 打包 `Source + Assemblies + Content + GameResources + Locales + mod.json + icon.png + default_config.json` 到 `artifacts/Cultiway-<版本>.zip`（Doc/Scripts/README 不进包）。

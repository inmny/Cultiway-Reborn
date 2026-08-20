# 仓库指南

无论在什么地方，都要使用通俗语言，尽量少用专业术语和英文缩写

## 项目结构与模块组织

核心玩法逻辑位于 `Source/`，主要子系统按职责拆分到 `Core/`（核心系统架构）、`Patch/`（核心 Harmony 补丁）、`UI/`（核心UI）、`Utils/`（通用工具） 和 `LocaleKeys/`， `Source/Content/`下面有类似的架构但主要辅助于玩法内容。资源和数据表从 `Content/` 与 `GameResources/` 发布，本地化文本位于 `Locales/`。`Scripts/` 存放数据转换和平衡性辅助脚本，编译后的程序集输出到 `bin/<Configuration>/net48/`。

原版游戏源码位于 `.GameSource/`。需要核对原版资源时，优先参考 `.GameSource\Assets`，包括图标、贴图、图集、预制体、音效和资源路径命名；新增或替换 Mod 资源前，先确认原版资源结构，避免凭猜测复刻路径或命名。

## 构建、测试与开发命令

- `dotnet build Cultiway.csproj -c Debug`：还原对本地 WorldBox 安装目录的引用，并将调试 DLL 输出到 `bin/Debug/net48/`。
- 测试通过运行本地 WorldBox (../worldbox.exe) 进行，模组加载器会自动编译并加载mod。

## 编码规则

- 不要保留向后兼容。删除过时的代码路径，而非添加兼容层、回退方案或迁移逻辑。
- 选择完全满足当前需求的最简实现。避免臆测性抽象、多余配置和间接层。
- 分层演进系统。从能端到端跑通的最小版本起步，每个新能力都叠加在已经可用的产品之上。绝不拿一个能用的产品去换半成品的复杂设计。
- 保持组件模块化，职责边界清晰。
- 当成熟、维护良好的库能降低整体复杂度或提升可靠性时，优先采用。没有充分理由，不要重复实现通用功能。
- 在自己动手写实现或引入新包之前，先把项目已有的依赖和工具类用足。不查文档和类型定义，就别认定某个库缺少某项能力。
- 架构决策要着眼于长期。不要接受那种"先这样凑合、以后再换"的临时方案。

## 编码风格与命名约定

- 项目目标框架为 `net48`，启用 C# 12 语法和 unsafe 代码。使用 4 个空格缩进
- 公开类型和方法使用 `PascalCase`
- 局部变量和私有字段使用 `camelCase`，仅在确实提升可读性时给私有字段加 `_` 前缀。
- 相关扩展集中放在 `Source/Core` 下的 `<Concept>Extend.cs` 文件中，Harmony 补丁保持在 `Source/Patch` 下与功能对应。
- 不要编写重复的兜底代码。

项目未启用 Nullable 注解，因此要优先使用显式 guard 和 `Source/Utils` 中的工具方法。提交前使用仓库内 `.DotSettings` 配置运行 ReSharper 或 IDE 自动格式化。代码注释应使用中文（UTF-8）。

## UI 开发约束

涉及窗口、弹层、列表、详情页、HUD、底栏按钮、Tooltip、滚动容器、UI prefab 或 UI 资源的设计与实现时，开始修改前必须阅读 [`Prompts/UI.md`](Prompts/UI.md)。

## 功能规划设计

涉及一个复杂的系统、功能等的设计时，开始设计前必须阅读 [`Prompts/PLAN.md`](Prompts/PLAN.md)

## 注意事项

- 禁止反复、过度的审查

## 提交与 Pull Request 指南

遵循 `git log -5` 中现有的 Conventional Commit 风格，例如 `feat:`、`bugfix:`、`feat(scope): 描述`。摘要保持简短、现在时，并限定在单一变更范围内。提交信息应使用中文。

Pull Request 需要说明玩法影响，包含复现或验证步骤；涉及 UI 的变更应附截图或 GIF。关联相关路线图条目或 issue，标明 `GameResources/` 下新增的资源文件，并说明是否需要 Mod 用户执行手动迁移步骤。

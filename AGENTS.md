# 仓库指南

无论在什么地方，都要使用通俗语言，尽量少用专业术语和英文缩写

## 项目结构与模块组织

核心玩法逻辑位于 `Source/`，主要子系统按职责拆分到 `Core/`（核心系统架构）、`Patch/`（核心 Harmony 补丁）、`UI/`（核心UI）、`Utils/`（通用工具） 和 `LocaleKeys/`， `Source/Content/`下面有类似的架构但主要辅助于玩法内容。资源和数据表从 `Content/` 与 `GameResources/` 发布，本地化文本位于 `Locales/`。`Scripts/` 存放数据转换和平衡性辅助脚本，编译后的程序集输出到 `bin/<Configuration>/net48/`。

原版游戏源码位于 `.GameSource/`。需要核对原版资源时，优先参考 `.GameSource\Assets`，包括图标、贴图、图集、预制体、音效和资源路径命名；新增或替换 Mod 资源前，先确认原版资源结构，避免凭猜测复刻路径或命名。

## 构建、测试与开发命令

- `dotnet build Cultiway.csproj -c Debug`：还原对本地 WorldBox 安装目录的引用，并将调试 DLL 输出到 `bin/Debug/net48/`。
- 测试通过运行本地 WorldBox (../worldbox.exe) 进行，模组加载器会自动编译并加载mod。

## 编码约束

涉及代码实现时，开始实现前必须阅读 [`Prompts/CODING.md`](Prompts/CODING.md)。

## UI 开发约束

涉及窗口、弹层、列表、详情页、HUD、底栏按钮、Tooltip、滚动容器、UI prefab 或 UI 资源的设计与实现时，开始修改前必须阅读 [`Prompts/UI.md`](Prompts/UI.md)。

## 功能规划设计

涉及一个复杂的系统、功能等的设计时，开始设计前必须阅读 [`Prompts/PLAN.md`](Prompts/PLAN.md)

## 注意事项

- 禁止反复、过度的审查

## 提交与 Pull Request 指南

遵循 `git log -5` 中现有的 Conventional Commit 风格，例如 `feat:`、`bugfix:`、`feat(scope): 描述`。摘要保持简短、现在时，并限定在单一变更范围内。提交信息应使用中文。

Pull Request 需要说明玩法影响，包含复现或验证步骤；涉及 UI 的变更应附截图或 GIF。关联相关路线图条目或 issue，标明 `GameResources/` 下新增的资源文件，并说明是否需要 Mod 用户执行手动迁移步骤。

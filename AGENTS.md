# 仓库指南

## 项目结构与模块组织
核心玩法逻辑位于 `Source/`，主要子系统按职责拆分到 `Core/`（扩展与系统）、`Patch/`（Harmony 补丁）、`UI/`、`Utils/` 和 `LocaleKeys/`。资源和数据表从 `Content/` 与 `GameResources/` 发布，本地化文本位于 `Locales/`。`Scripts/` 存放数据转换和平衡性辅助脚本，编译后的程序集输出到 `bin/<Configuration>/net48/`。

原版游戏源码位于 `.GameSource/`。需要核对原版资源时，优先参考 `.GameSource\Assets`，包括图标、贴图、图集、预制体、音效和资源路径命名；新增或替换 Mod 资源前，先确认原版资源结构，避免凭猜测复刻路径或命名。

## 构建、测试与开发命令
- `dotnet build Cultiway.csproj -c Debug`：还原对本地 WorldBox 安装目录的引用，并将调试 DLL 输出到 `bin/Debug/net48/`。
- `python Scripts/csv2json.py Tables/<file>.csv`：将表格源文件转换为 `Content/` 下消费的 JSON 格式。
- `python Scripts/count_source_lines.py Source`：评审变更范围时，用于快速检查代码规模。

## 编码风格与命名约定
项目目标框架为 `net48`，启用 C# 12 语法和 unsafe 代码。使用 4 个空格缩进；公开类型和方法使用 `PascalCase`；局部变量和私有字段使用 `camelCase`，仅在确实提升可读性时给私有字段加 `_` 前缀。相关扩展集中放在 `Source/Core` 下的 `<Concept>Extend.cs` 文件中，Harmony 补丁保持在 `Source/Patch` 下与功能对应。

项目未启用 Nullable 注解，因此要优先使用显式 guard 和 `Source/Utils` 中的工具方法。提交前使用仓库内 `.DotSettings` 配置运行 ReSharper 或 IDE 自动格式化。代码注释应使用中文（UTF-8）。

## 测试指南
仓库没有自动化测试套件，主要通过 WorldBox 内手动验证。启动后自动通过 NeoModLoader 编译并加载 Mod，启用 `Source/Debug` 中的调试工具，并针对受影响系统（例如法术、宗门机制）创建游戏内场景验证。

数据变更需要重新运行对应脚本生成派生 JSON。

## 提交与 Pull Request 指南
遵循 `git log -5` 中现有的 Conventional Commit 风格，例如 `feat:`、`bugfix:`、`feat(scope): 描述`。摘要保持简短、现在时，并限定在单一变更范围内。提交信息应使用中文。

Pull Request 需要说明玩法影响，包含复现或验证步骤；涉及 UI 的变更应附截图或 GIF。关联相关路线图条目或 issue，标明 `GameResources/` 下新增的资源文件，并说明是否需要 Mod 用户执行手动迁移步骤。

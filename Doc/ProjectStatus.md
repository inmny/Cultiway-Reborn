# 项目进展总览 (Project Overview)

## 1. 当前状态 (Current Status)
*   **版本**: 0.0.24 (预览版)
*   **核心架构**: 基于 `Friflo.Engine.ECS` 的实体组件系统已搭建，代码结构清晰，模块化程度高。
*   **已实现核心系统**:
    *   **修仙体系 (Cultisys)**:
        *   境界 (Realms)、属性 (Stats)、资源 (丹药/元婴) 框架已存在。
        *   `Source/Content` 下包含大量数据定义（Elixirs, Jindans, Yuanyings）。
    *   **宗门系统 (Sect)**:
        *   包含宗门管理 (`SectManager`)、数据表 (`SectTable`) 及视觉表现 (`SectBanner`)。
    *   **技能系统 (Skill V3)**:
        *   第三版技能系统框架 (`SkillLibV3`) 已就绪。
        *   包含技能实体 (`SkillEntities`) 和词条系统 (`SkillModifiers`)。
    *   **环境系统 (Environment)**:
        *   灵气分布图 (`WakanMap`)。
        *   五行灵根 (`ElementRoots`)。
    *   **UI 系统**:
        *   `Source/UI` 和 `Source/LocaleKeys/UI` 下有大量界面代码，覆盖了大部分游戏交互。
    *   **数据驱动**:
        *   利用 CSV/JSON 管理大量游戏数据 (`Locales`, `Content`)。

## 2. 存在的问题与风险 (Issues & Risks)
*   **存档兼容性**: `mod.json` 明确指出“存档无效”。这意味着数据序列化/反序列化可能存在问题，或者存档结构尚未定型。
*   **性能优化**: 标记为“无优化”。ECS 虽然高效，但如果 System 逻辑复杂或查询频繁，仍可能导致卡顿。
*   **内容填充**: 虽然框架在，但具体的技能词条（Modifiers）还有很多未实现（参考 `Doc/ModifierEffects.md`）。

## 3. 下一步计划 (Next Steps)

### 短期目标 (Short-term)
1.  **完善技能词条 (Implement Skill Modifiers)**
    *   **目标**: 根据 `Doc/ModifierEffects.md`，实现尚未完成的词条。
    *   **建议**: 优先实现 `Explosion` (爆炸/AOE) 和 `LockOn` (锁定/命中)，这对战斗体验提升明显。
    *   **路径**: 在 `Source/Core/SkillLibV3` 或 `Source/Content/SkillModifiers.cs` 中添加逻辑。

2.  **验证核心玩法循环 (Verify Gameplay Loop)**
    *   **目标**: 确保 AI 能够自动“修炼 -> 突破 -> 加入宗门 -> 战斗”。
    *   **行动**: 使用 Debug 工具观察 AI 行为，确保没有逻辑死锁。

### 中期目标 (Mid-term)
3.  **修复存档系统 (Fix Save/Load)**
    *   **目标**: 让玩家能够保存进度。
    *   **行动**: 检查 `ICanCopy`, `ICanReload` 接口实现，确保自定义数据（如修仙等级、宗门归属）能正确写入 WorldBox 的存档结构中。

4.  **性能分析与优化 (Profiling & Optimization)**
    *   **目标**: 保证在大地图、多单位时的帧率。
    *   **行动**: 分析 `Source/Core/Systems` 下的 `Tick` 系统，优化高频调用的逻辑。

### 长期目标 (Long-term)
5.  **内容扩充 (Content Expansion)**
    *   增加更多的丹药、功法、事件。
    *   完善本地化文本。

## 4. 推荐的立即行动 (Action Items)
*   **Action 1**: 在 `Doc/` 下创建开发日志，记录每次修改。
*   **Action 2**: 选择 `Doc/ModifierEffects.md` 中的一个未实现词条（如 `Explosion`）进行开发，熟悉技能系统流程。


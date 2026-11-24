# 开发工具

| 用途 | 入口 |
|---|---|
| 遍历全部法术，录制词条与轨迹变化 | `python3 Scripts/SpellDemos/demo_all.py` |
| 指定一个法术或能力演示 | `python3 Scripts/SpellDemos/demo_spell.py 火球` |
| 演示工具完整参数、范围与输出说明 | [SpellDemos/README.md](SpellDemos/README.md) |
| 按技能改动选择编译、实机及存档复验 | `python3 Scripts/SpellDemos/check_skill_changes.py --plan` |
| 正式存档测试与测试世界 | [WorldSaveTests/README.md](WorldSaveTests/README.md) |
| 战斗模型制作与预览 | [CombatModelStudio/README.md](CombatModelStudio/README.md) |
| 着色器资源包构建 | `ShaderBuild/build.sh` |
| 法宝组合模型生成 | [artifact_compose/README.md](artifact_compose/README.md) |
| 妖兽器官资源生成 | [organ_gen/README.md](organ_gen/README.md) |
| 像素图逐像素编辑 | `pixel_text.py` |

旧的五套战斗探索与录像脚本已由 `SpellDemos` 替代，不再维护。历史录像和结果仍保留在 `artifacts/`；旧脚本归档与删除清单位于 `artifacts/spell_demo/`。

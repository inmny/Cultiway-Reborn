# 战斗主体制作与检查

从模组目录启动 `python3 -m http.server 9048 --bind 127.0.0.1`，在浏览器打开 `http://127.0.0.1:9048/Scripts/CombatModelStudio/`。预览所需的 Three.js 0.180.0 和旋转控制已放在 `vendor/`，无需外网。

模型源程序是 `generate.mjs`。在本目录运行 `npm ci` 和 `npm run generate`，会重新生成 `Content/CombatModels/` 中六个共同模型。游戏不需要安装这些制作工具。

导出坐标依次为横向、纵深、高度。模型包含完整位置、法向、三角形和基础颜色，表面材料可由招式组合覆盖。真身右臂在游戏内按真实掌击动作添加；制作预览中的身体因此只包含左臂。模型文件是发布资源，不属于长期世界存档。

预览用于检查形体与质感，实机视频用于确认最终战斗画面。预览材质与游戏材质分别运行，不能用预览截图代替实机验证。

新的共同形体使用 `forms.mjs`，水鲸的连续几何和游动动作为 `forms_whale.mjs`，导出通道集中在 `form_export.mjs`。运行 `node Scripts/CombatModelStudio/forms.mjs`，得到 `artifacts/skill_rebuild/form_sources/` 的制作交换资料；其中保留部件、材质分区、法向、表面坐标、三角形、每顶点四项权重、有序骨架及完整局部动作。刚性剑、枝、肢段和手掌也由同一入口生成。

`form_curve.mjs`提供封口厚曲面及目标曲线动作，`forms_wave.mjs`制作翻卷浪体。顶点和表面挂点都可混合最多四项控制；混合挂点只带动附着物的位置及朝向，保留刚性器物本身的尺寸。附加通道保留局部厚度与泡沫覆盖，水鲸的厚腹及薄鳍也由此区分。动作Markers保存命名阶段；构造的ActiveFromMarker及ActiveUntilMarker使用相同制作时点，并随Animate.Seconds调速。

发布时运行：

```bash
bash Scripts/ShaderBuild/build_forms.sh /home/inmny/Projects/CultiwayAssets /data/Unity/Hub/Editor/2022.3.60f1/Editor/Unity
```

脚本从源重新生成制作资料，将几何导入 Unity，再输出 `Content/Forms/` 的版本3说明与三平台 `cultiway_forms_*` 资源包。新说明保留明确的部位用途、连接、制作输入和实际变化声明，详见[版本化形体说明](../../Doc/技能系统全面重构/版本化形体说明与结构索引.md)。新增资源须在制作调用中提供`structure(...)`，不由导入器根据文件名猜用途。游戏只加载发布资源，动作、挂点及接触端点使用共同姿态。动作通过构造的 `Animate` 操作选择，`Clip` 为制作动作名称，`Seconds` 为调整后的完整时长，零使用原时长；同一动作随世界时间推进，暂停、接触驻留和预测回退也使用该时钟。详见[连续形体制作与运行](../../Doc/技能系统全面重构/连续形体制作与运行.md)。当前水鲸用于验证连续形变链路，最终水体材质、托举压落过程及城市交锋仍按完整 TODO 验收。

制作和构建在游戏退出后进行，完成后重新启动游戏再验证。资源包先写完整临时文件再原子替换，避免截断已打开的包；这不代表运行中的旧缓存能热切换到新版资源。

Three.js 与旋转控制按 MIT 许可使用，见 `Licenses/threejs.txt`。官方资料：[模型几何](https://threejs.org/docs/#api/en/core/BufferGeometry)、[旋转轮廓](https://threejs.org/docs/#api/en/geometries/LatheGeometry)、[曲线路径](https://threejs.org/docs/#api/en/geometries/TubeGeometry)。

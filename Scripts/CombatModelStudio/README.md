# 战斗主体制作与检查

从模组目录启动 `python3 -m http.server 9048 --bind 127.0.0.1`，在浏览器打开 `http://127.0.0.1:9048/Scripts/CombatModelStudio/`。预览所需的 Three.js 0.180.0 和旋转控制已放在 `vendor/`，无需外网。

模型源程序是 `generate.mjs`。在本目录运行 `npm ci` 和 `npm run generate`，会重新生成 `Content/CombatModels/` 中六个共同模型。游戏不需要安装这些制作工具。

导出坐标依次为横向、纵深、高度。模型包含完整位置、法向、三角形和基础颜色，表面材料可由招式组合覆盖。真身右臂在游戏内按真实掌击动作添加；制作预览中的身体因此只包含左臂。模型文件是发布资源，不属于长期世界存档。

预览用于检查形体与质感，实机视频用于确认最终战斗画面。预览材质与游戏材质分别运行，不能用预览截图代替实机验证。

新的共同形体使用 `forms.mjs`，水鲸的连续几何和游动动作为 `forms_whale.mjs`，导出通道集中在 `form_export.mjs`。运行 `node Scripts/CombatModelStudio/forms.mjs`，得到 `artifacts/skill_rebuild/form_sources/` 的制作交换资料；其中保留部件、材质分区、法向、表面坐标、三角形、每顶点四项权重、有序骨架及完整局部动作。刚性剑、枝、肢段和手掌也由同一入口生成。

`form_curve.mjs`提供封口厚曲面及目标曲线动作，`forms_wave.mjs`制作翻卷浪体。顶点和表面挂点都可混合最多四项控制；混合挂点只带动附着物的位置及朝向，保留刚性器物本身的尺寸。附加通道保留局部厚度与泡沫覆盖，水鲸的厚腹及薄鳍也由此区分。动作Markers保存命名阶段；构造的ActiveFromMarker及ActiveUntilMarker使用相同制作时点，并随Animate.Seconds调速。

`forms_rigid.mjs`制作敞口灵炉和铜镜，分别保留内外壁、器口、镜面、握柄、真实分件和明确连接。镜面表面与结构材料分开，不用颜色覆盖代替光学表面。运行`python3 Scripts/SpellDemos/verify_rigid_forms.py --record`可复现资源、构造与两种显示验证，见[刚性器物制作路径](../../Doc/技能系统全面重构/刚性器物制作路径.md)。

`forms_bodies.mjs`制作巨神和带指节的灵臂，`form_skeleton.mjs`复用Three.js已有表面提取工具，将连续体积按真实部位分区，同时保留共同法向、权重和控制。分区不会变成独立悬浮的球或肢段；面甲等刚性饰物跟随明确控制。生成仅在离线制作时执行，运行不重新生成顶点。动作、挂点和作用区仍走已有共同骨架。运行`python3 Scripts/SpellDemos/verify_skeletal_bodies.py --record`可验证并录制，见[骨架主体与连续肢体](../../Doc/技能系统全面重构/骨架主体与连续肢体.md)。

`forms_horn.mjs`保留雷角，并提供封闭渐细曲面的共用制作函数；`forms_organs.mjs`制作辨邪角、两种翼、单尾/三尾/六尾及背甲。各部件保留表面坐标、真实厚度与从自身顶点导出的作用形状。`Content/CreatureVisuals/organ_mounts_3d.json`按实际外观层登记模型、身体安装区域及需要替代的原部件，正常显示与身体动作共用这些资料，不改变Worldfall身体大小。完整逐翼、逐尾及非人支撑动作继续按4.29制作。运行`python3 Scripts/SpellDemos/verify_spatial_organs.py`可检查实际绑定、正式接触、双视角和正式读档；输出在`artifacts/skill_rebuild/spatial-organs/`。

`form_detail.mjs`使用固定版本meshoptimizer 1.3.0离线生成中远景网格，保留原顶点通道、接合边及控制信息；近景沿用完整模型。运行游戏无需该依赖。重新安装制作工具使用本目录的`npm ci`。显示级别及验证见[远近细节与批量绘制](../../Doc/技能系统全面重构/远近细节与批量绘制.md)。

发布时运行：

```bash
bash Scripts/ShaderBuild/build_forms.sh /home/inmny/Projects/CultiwayAssets /data/Unity/Hub/Editor/2022.3.60f1/Editor/Unity
```

脚本从源重新生成制作资料，将几何导入 Unity，再输出 `Content/Forms/` 的版本7说明与三平台 `cultiway_forms_*` 资源包。新说明保留明确的部位用途、连接、制作输入和实际变化声明，详见[版本化形体说明](../../Doc/技能系统全面重构/版本化形体说明与结构索引.md)。新增资源须在制作调用中提供`structure(...)`，不由导入器根据文件名猜用途。游戏只加载发布资源，动作、挂点及接触端点使用共同姿态。动作通过构造的 `Animate` 操作选择，`Clip` 为制作动作名称，`Seconds` 为调整后的完整时长，零使用原时长；同一动作随世界时间推进，暂停、接触驻留和预测回退也使用该时钟。详见[连续形体制作与运行](../../Doc/技能系统全面重构/连续形体制作与运行.md)。当前水鲸用于验证连续形变链路，最终水体材质、托举压落过程及城市交锋仍按完整 TODO 验收。

制作和构建在游戏退出后进行，完成后重新启动游戏再验证。资源包先写完整临时文件再原子替换，避免截断已打开的包；这不代表运行中的旧缓存能热切换到新版资源。

Three.js 与旋转控制按 MIT 许可使用，见 `Licenses/threejs.txt`。官方资料：[模型几何](https://threejs.org/docs/#api/en/core/BufferGeometry)、[旋转轮廓](https://threejs.org/docs/#api/en/geometries/LatheGeometry)、[曲线路径](https://threejs.org/docs/#api/en/geometries/TubeGeometry)。


远近网格与实例绘制均已接入。运行`python3 Scripts/SpellDemos/verify_form_batch.py --record --check-exit`可在私有游戏验证实际合批、独立姿态和颜色、逐像素对照、重载、切世界及退出释放，并生成两种显示的技术录像。该参数只退出脚本拥有的测试游戏。运行`verify_form_detail.py --record`仍可单独核对远近几何与实际选择。


第6版支持`write(...)`最后一个参数提供握持数组：稳定编号、已有挂点、绕轴方向、实际柄粗及允许的模型缩放/体高范围。剑、镜和炉把手已经声明；生成源、Unity导入和运行使用同一资料。`python3 Scripts/SpellDemos/verify_form_grips.py --record`核对并录制握持对齐，完整基础动作、支撑和双方接触继续按2.17推进。


`forms_ray.mjs`制作`energy_ray`：封闭胶囊体及`origin`、`extent`控制，两端保持实际厚度，中段按顶点权重伸展。结构明确声明`span`与制作长度、半径，根端及末端挂点、作用端点与网格同控。经现有`forms.mjs`和三平台构建入口发布，实机入口为`verify_ray.py`；最终流派造型和材质继续对应美术任务。

`forms_earth.mjs`制作独立`earth_spikes`：中央岩锋与六片侧岩锋使用封闭错落截面，各块整石分别沿共用动作升起、停留和收回；显示与作用端点读取同一控制，不把旧圆形范围继续当作隐藏伤害。`erupt`的命名阶段可在普通构造中继续调整；资源明确声明`HideBelowGround`，原版隐藏地下表面，三维使用真实地形深度。运行`python3 Scripts/SpellDemos/verify_ground_spike.py`验证原已学地刺的实际接管、伤害、一次支付、失败恢复及自然结束，证据位于`artifacts/skill_rebuild/trajectories/ground-spike/`；各流派最终美术与完整连续录像仍按内容交付阶段完成。


`forms_rift.mjs`制作独立`rift_opening`：连续暗腔与两侧厚裂唇共同张开、维持和闭合；左右权重控制网格，静止轴明确声明自身范围并承载内部接触，只有完全张开至开始合拢之间可生效。来源定义同时使用普通生长和命名收缩步骤，让初现及闭合末端的整体体量收尽，不留下完整高度的黑线。资源保留`rift_core`与`rift_lip`两种表面，混沌伤害独立登记，不按表面颜色猜元素。运行`python3 Scripts/SpellDemos/verify_rift.py`验证原已学裂隙、实际内部伤害、混沌抗性、费用及失败恢复，证据位于`artifacts/skill_rebuild/trajectories/rift/`。该来源当前只造成原有混沌伤害；空间解构、卷入去向及最终归墟美术继续对应后项。


`forms_fields.mjs`制作三种独立持续领域：`poison_pool`为低矮积液，共用九点地表承托；`poison_mist`保留内外密度和低频起伏；`entropy_field`使用不规则暗腔和紫色体积层次。每个实际作用面与对应网格共用控制。构造的`ConcurrentLimit`在成功受理后替换同来源最老实例，原容器编号只用于本世界计数，不写入长期资料。跟随连接的`ContactAtDestination`明确只在新位置接触，不扫过输入转移空隙；独立离手部位继续保留自己的路径。运行`python3 Scripts/SpellDemos/verify_fields.py`验证本批实际来源、间隔伤害、边界、数量上限、失败恢复及自然结束，证据位于`artifacts/skill_rebuild/trajectories/fields/`。治疗净化接入见下节；空间扭曲、最终流派美术与集中连续录像仍对应后项。


`forms_support.mjs`分别制作`healing_seed`、`rejuvenation_bloom`和`purification_wave`。叶芽追踪单个选中友军；恢复叶面与实际接触共用控制；净化的宽厚波面向外扩张，实际净化只随波面接触发生。表面使用独立玉色与珠白材料，不复用剑刃或烟雾。`ConstructionImpact.Restoration`独立保存恢复量和净化数量；`SelectedTargetOnly`保持单人目标限制；`ConstructionContactMode.Pulse`从第一个共同间隔开始采样当时真实接触，晚入场不会重排定时。运行`python3 Scripts/SpellDemos/verify_support.py`验证真实已学来源及支持行为。加`--section purification`只复验净化与失败恢复，加`--section failure`只复验共享支付与失败恢复，加`--section visuals`只重新拍摄治疗和恢复叶面；结果各写入独立子目录，保留先前证据。三份模型在制作输出时转换正反面顺序并保留朝外法线。骨架动作只使用位置与旋转；净化波由来源的普通生长步骤从中心展开，网格和接触一起缩放，不能靠未支持的动作缩放或另加隐藏圆区补漏。完整自主创作、编辑接入、正式冷启动和集中录像继续5.05的C，最终美术另行验收。


`forms_support.mjs`还制作三种独立祝福载体：赤铜厚结`battle_knot`、六角厚印`guard_seal`、宽厚圆端翼片`haste_vanes`。守护印仅投送原护甲属性，不登记实体格挡面；三者接触只取实际主体或核心，不借旧圆区扩大作用。骨架只保存位置和旋转，原轨迹继续通过普通追踪和速度步骤表达。运行`python3 Scripts/SpellDemos/verify_support.py --section blessings`单独验证本批；不带分段参数时顺序运行恢复净化与祝福两部分。正式保存冷启动、完整编辑接入和连续录像继续5.05的C。


`forms_ray.mjs`的`makeSourceRays()`提供`spirit_ray`与`solar_ray`：前者是独立玉白厚光柱，后者由三道相接的金色厚光脊组成，均有圆端。两者沿用`origin/extent`末端控制与明确`span`能力，网格和实际作用胶囊共同伸展；多个相接胶囊仍归同一接触组，不多收伤害。原已学来源验证使用`python3 Scripts/SpellDemos/verify_beams.py`。仅交付基础独立形体，最终流派美术继续后项。


`forms_ray.mjs`的`makeThunderChannel()`提供独立`thunder_channel`雷体：封闭扭转截面采用亮色与蓝紫色外表面分区，顶点和真实作用共用末端伸展控制，亮区不增加第二次伤害。原连锁由有限四个普通部位构成，后续部位通过`ConstructionActivation.Target`在真实接触后选择附近未访问目标，并使用上一段实际余力乘声明倍率。原已学入口及失败恢复验证使用`python3 Scripts/SpellDemos/verify_chain.py`，复用现成世界和人物；原自动连发照常逐发验证和收费。最终流派美术、完整编辑和自主创作、正式冷启动及集中连续录像仍按总计划后续交付。

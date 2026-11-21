using System;
using Newtonsoft.Json;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Core.Skills;

/// <summary>可保存和改造的能力构造；不包含网格、人物对象和本次施放进度。</summary>
public sealed class AbilityDefinition
{
    /// <summary>资料格式版本，与模组版本分别维护。</summary>
    public int Version = 1;

    /// <summary>跨存档保持的能力编号。</summary>
    public string Id;

    /// <summary>改造产生的新修订号。</summary>
    public int Revision = 1;

    /// <summary>显示名称。</summary>
    public string Name;

    /// <summary>创造者的长期人物编号，预制资料使用零。</summary>
    public long Creator;

    /// <summary>作者编号属于分享来源，不能在当前世界按号码寻找人物。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ExternalCreator;

    /// <summary>创造时使用的种子，载入不得重新生成结构。</summary>
    public int Seed;

    /// <summary>独立创作或修改所依据的原版本；空值表示没有单一原术。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public AbilityParent Parent;

    /// <summary>本次施放需要借用的实际库存材料，旧构造没有额外要求。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionInventoryInput[] Inventory;

    /// <summary>一次施放的最大世界秒数。</summary>
    public float Lifetime = 3f;

    /// <summary>由资源所属玩法实时报价的施法需求。</summary>
    public float Cost = 1f;

    /// <summary>实际资源通道的长期编号或首轮短名称；旧构造默认沿用真元。</summary>
    public string Resource = "wakan";

    /// <summary>额外支付通道，与主通道共用施法需求；各通道自行报价。</summary>
    public string[] AdditionalResources = Array.Empty<string>();

    /// <summary>主通道及额外通道采用单项、任选或全部支付。</summary>
    public SkillCastResourceRequirementMode ResourceMode;

    /// <summary>独立施放之间的最短世界秒数。</summary>
    public float Cooldown = 2f;

    /// <summary>同一人物同一能力最多并存几次；零表示不额外限制，成功受理后替换最老施放。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int ConcurrentLimit;

    /// <summary>自动计算尚未取整的基准，避免连续整数资源改造反复向上取整；旧资料没有此项。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionCalculatedValues Calculation;

    /// <summary>一次实际接触的基础作用量。</summary>
    public float Power = 1f;

    /// <summary>明确的目标选择距离；缺省沿用原有放置或实际身体触达，不能替代真实作用形状。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? TargetRange;

    /// <summary>整招默认的目标筛选及真实接触作用；空值保留敌方、正常伤害和无推拉。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionImpact Impact;

    /// <summary>施放的使用场景与目标类别；空值保持已有资料的战斗人物选取。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionUse Use;

    /// <summary>本次施放由来源为一名实际友方承担的伤害份额与总额。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionSoulPact SoulPact;

    /// <summary>构造的根部。</summary>
    public ConstructionNode Root;

    /// <summary>借用实际身体的动作；可独立存在，也可与生成形体并存。</summary>
    public BodyActionDefinition Body;

    /// <summary>生成主体的实际连接；空值保留首次格式在目标位置建立的行为。</summary>
    public ConstructionBinding Binding;

    /// <summary>命名的操作输入输出；旧构造缺省为空，继续使用原部位操作。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionStep[] Steps = Array.Empty<ConstructionStep>();
}

/// <summary>只记录直接来源，不递归复制整个传承链；实际构造自足，不依赖原术仍在目录。</summary>
public sealed class AbilityParent
{
    /// <summary>当时使用的原术稳定编号。</summary>
    public string Id;

    /// <summary>当时使用的原术修订。</summary>
    public int Revision;

    /// <summary>原术的实际创造者。</summary>
    public long Creator;

    /// <summary>这次改变的实际结构或过程。</summary>
    public string Change;
}

/// <summary>一个可嵌套形体；子形体可以在父体挂点集合上分布。</summary>
public sealed class ConstructionNode
{
    /// <summary>本层不重复的部位编号。</summary>
    public string Id;

    /// <summary>共用形体资源编号。</summary>
    public string Form;

    /// <summary>在实际部位上承载一条有厚度的真实景物带。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionLandscapeSlice LandscapeSlice;

    /// <summary>显示表面覆盖，空值保留各部件的默认表面。</summary>
    public string Surface;

    /// <summary>实际材料编号，与仅用于显示的表面分别保存。</summary>
    public string Matter = "physical";

    /// <summary>明确物态，空值使用实际材料的默认规则。</summary>
    public string State;

    /// <summary>改造后，本体伤害属性和表面使用当前实际材料，旧资料缺省保留固定设置。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool FollowMatter;

    /// <summary>实际部位作为接触介质时提供的通道，空值不参与介质分送。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string MediumChannel;

    /// <summary>仅用指定形状触发地块接触事件；不改变人物或环境作用的形状。缺省允许全部。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string WorldContactShape;

    /// <summary>圆形范围在结算时按人物的地图位置选人，不计身体宽度和高度。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ContactActorCenter;

    /// <summary>仅作用于显示的三个染色倍率，不改变实际材料和物态。</summary>
    public float[] Tint;

    /// <summary>父体挂点名；末尾星号表示选择具有此前缀的全部挂点。</summary>
    public string Anchor;

    /// <summary>用共同规则选择多个父空间位置并放置完整子结构，与单个Anchor互斥。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionDistribution Distribution;

    /// <summary>相对父挂点的位置。</summary>
    public float[] Offset = new float[3];

    /// <summary>局部三个方向的角度。</summary>
    public float[] Rotation = new float[3];

    /// <summary>实际尺寸倍率。</summary>
    public float Scale = 1f;

    /// <summary>未缩放材料体积；空值采用一个立方地图格，与攻击力量分别维护。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? Volume;

    /// <summary>显示及实际作用开始存在的世界时点；缺省从零开始，预备姿态仍可在窗外更新。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? AppearAt;

    /// <summary>显示及实际作用结束存在的世界时点；缺省随整次结束，最后一段真实路径仍检查接触。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? DisappearAt;

    /// <summary>等待真实事件后才开始此分支；空值沿用父分支的时钟。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionActivation Activation;

    /// <summary>允许明确制作的部位动作继续播放，材料仍保持形状并限制流动。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool AnimateWhileHeld;

    /// <summary>可选的形变姿态维持，不暂停飞行、旋转、生长或材料变化。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionHold Hold;

    /// <summary>实际作用开始时点，按整次施放计算。</summary>
    public float ActiveFrom;

    /// <summary>实际作用结束时点，负值表示持续到施放结束。</summary>
    public float ActiveUntil = -1f;

    /// <summary>使用制作动作中的开始阶段，明确指定时覆盖数字开始时点。</summary>
    public string ActiveFromMarker;

    /// <summary>使用制作动作中的结束阶段，时点随动作速度一起调整。</summary>
    public string ActiveUntilMarker;

    /// <summary>共用接触组，同组对同一人物只确认一次；空值为此展开部位，body与身体动作共用。</summary>
    public string ContactGroup;

    /// <summary>作用窗口、分次作用及结果收势跟随实际身体；未声明的独立主体继续自身过程。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool BodyWindow;

    /// <summary>同组共用的再次接触规则；空值继承同组明确规则，均未指定时每人一次。</summary>
    public ConstructionContactPolicy Contact;

    /// <summary>本部位明确覆盖整招的接触作用；空值使用整招规则。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionImpact Impact;

    /// <summary>依靠实际父体的地面支撑，以本部位接触抓持和推挤人物。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionSupportGrip SupportGrip;

    /// <summary>围绕本部位真实中心的持续水平范围运动，不改变接触面、伤害或屏障容量。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionRadialMotion RadialMotion;

    /// <summary>从本部位牵引已解构的独立材料，结束后保留其真实结果。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionMaterialVortex MaterialVortex;

    /// <summary>在本部位的实际开口暂留并抛出一件无主兵器。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionWeaponChamber WeaponChamber;

    /// <summary>本部位对应的原人物及明确允许的作用；空值沿原交锋规则。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionCorrespondence Correspondence;

    /// <summary>实际接触后怎样继续，默认保留穿过并对每个目标首次作用的行为。</summary>
    public ConstructionContactResponse OnContact;

    /// <summary>实际交锋用途；装饰不提供作用形状，屏障只拦截敌对形体。</summary>
    public ConstructionContactRole ContactRole;

    /// <summary>实体屏障是否阻止敌方地面人物穿过；友方及飞行人物仍可通行。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool BlocksGroundActors;

    /// <summary>本部位的状态只在固态屏障形成真实闭环时施加，每人每部位只尝试一次。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool EnclosesActors;

    /// <summary>相对于本次基础作用量的初始强度；抵消后余量继续参与交锋及伤害。</summary>
    public float Strength = 1f;

    /// <summary>屏障拦截时的规则；默认按实际余量相互消耗。</summary>
    public ConstructionInterception Interception;

    /// <summary>本部位最多反射次数；零表示不接受反射，最多32次。</summary>
    public int ReflectionLimit = 8;

    /// <summary>当前控制者失效后的部位规则；旧资料缺省结束，仍依赖身体或来源连接时不能独立继续。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionSourceLoss SourceLoss;

    /// <summary>已显现的部件受中断时保留为无作用落物的秒数，零表示直接结束。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float InterruptFall;

    /// <summary>本部位只在实际父出口向下时开始，使用现有挂点的位置和朝向。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionOutlet Outlet;

    /// <summary>本形体采用的共用操作。</summary>
    public ConstructionOperation[] Operations = Array.Empty<ConstructionOperation>();

    /// <summary>附着在本体上的子结构。</summary>
    public ConstructionNode[] Children = Array.Empty<ConstructionNode>();
}

/// <summary>屏障与攻击形体真正接触后的交互。</summary>
public enum ConstructionInterception
{
    /// <summary>按双方作用量相互消耗。</summary>
    Absorb = 0,

    /// <summary>容量足够时反射并交接归属，否则按正常吸收处理。</summary>
    Reflect = 1
}

/// <summary>由构造明确指定的作用用途，不根据表面颜色推测。</summary>
public enum ConstructionContactRole
{
    /// <summary>攻击人物，也能与敌对攻击或屏障相互消耗。</summary>
    Attack = 0,

    /// <summary>只拦截敌对攻击，不因接触人物造成伤害。</summary>
    Barrier = 1,

    /// <summary>仅提供连接及显示，不参与任何接触。</summary>
    Decoration = 2
}

/// <summary>实际接触确认后改变本形体，显示不自行播放这些结果。</summary>
public enum ConstructionContactResponse
{
    /// <summary>继续当前过程，可再接触其他目标。</summary>
    Continue = 0,

    /// <summary>在首次接触姿态定型驻留，之后仍可接触进入的其他目标。</summary>
    Stop = 1,

    /// <summary>本体及尚未脱离的子结构结束，已脱离的结构继续。</summary>
    End = 2
}

/// <summary>首批已经提供执行实现的操作；不是完整招式枚举。</summary>
public enum ConstructionOperationKind
{
    /// <summary>按世界时间连续长成。</summary>
    Grow = 0,

    /// <summary>在局部坐标中连续旋转。</summary>
    Spin = 1,

    /// <summary>解除父子连接并继承脱落点速度。</summary>
    Detach = 2,

    /// <summary>独立运动，转向速度大于零时追踪本次目标。</summary>
    Fly = 3,

    /// <summary>播放形体实际提供的骨架动作，挂点、作用区和显示共用。</summary>
    Animate = 4,

    /// <summary>在明确时点或真实接触后改变本部位材料及物态。</summary>
    ConvertMatter = 5,

    /// <summary>由明确事件停止当前部位，保留实际姿态。</summary>
    Stop = 6,

    /// <summary>完整部件从当前布局连续汇入父结构，不改变材料和身份。</summary>
    Gather = 7,

    /// <summary>按当前真实分件碎解，继承姿态、余力和控制归属。</summary>
    Fracture = 8,

    /// <summary>按实际地形、水深和动量流散，保留材料、身份及接触组。</summary>
    Flow = 9,

    /// <summary>收束本次生成主体，按未耗支付份额回能。</summary>
    Recover = 10,

    /// <summary>把生成材料交给世界独立维护。</summary>
    Leave = 11,

    /// <summary>从本部位实际触水位置进入有界导电。</summary>
    Conduct = 12,

    /// <summary>转折同一主体当前运动，保留速度大小、曲线路径和实际归属。</summary>
    Deflect = 13,

    /// <summary>从真实入口沿连通地面以有限速度传开。</summary>
    Propagate = 14,

    /// <summary>以实际立体厚度连接来源与有限目标方向。</summary>
    Ray = 15,
    /// <summary>改变独立运动的中心速度，保留目标、曲线、材料及命中记录。</summary>
    ChangeSpeed = 16,
    /// <summary>等待实际部位、前序、时点和条件就绪后确认一次，不改变主体。</summary>
    Ready = 17,
    /// <summary>通过已有控制结构收拢、展开或弯折主体，保持真实部位身份。</summary>
    Deform = 18,

    /// <summary>从本次借用的真实库存中消耗明确的材料。</summary>
    ConsumeInventory = 19,

    /// <summary>按当前材料耐受连续侵蚀真实生成实体。</summary>
    Erode = 20,

    /// <summary>把同一生成形体从入口的局部位置转到出口。</summary>
    MapSpace = 21,

    /// <summary>实际接触后把原人物送到成对出口，由世界确认。</summary>
    MapActor = 22,

    /// <summary>命名步骤把本次借用的原法宝送到成对出口，由世界确认。</summary>
    MapArtifact = 23,

    /// <summary>替换选中世界材料正在进行的侵蚀节奏。</summary>
    RetimeErosion = 24,

    /// <summary>向本次实际选中的人物送达讯号，不通过接触。</summary>
    SendSignal = 25,

    /// <summary>结束一个仍有余量的生成部位，按实际触发点把余量交给等待交接的部位。</summary>
    PassRemainder = 26
}

/// <summary>可保存的操作参数，编译时检查用途和时间。</summary>
public sealed class ConstructionOperation
{
    /// <summary>部位内可选的输出名称；未命名操作不增加输出跟踪。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string Id;

    /// <summary>骨架动作名称，其他操作不使用。</summary>
    public string Clip;

    /// <summary>对实际控制结构的间距、偏移和旋转目标。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionDeformation Deformation;

    /// <summary>本次真实库存消耗的输入与件数。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionInventoryUse InventoryUse;

    /// <summary>仅指定传讯步骤使用的内容和有效时间。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionSignal Signal;

    /// <summary>实际侵蚀份额，未配置时使用普通份额。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionErosion Erosion;

    /// <summary>双门入口的半径，未指定时为一个世界单位。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? MappingRadius;

    /// <summary>人物双门允许的成功通行次数；未指定时保留单次尝试。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? MappingCapacity;

    /// <summary>入口接触后允许暂存的世界秒数；未指定时只尝试立即抵达。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? MappingWaitSeconds;

    /// <summary>操作种类。</summary>
    public ConstructionOperationKind Kind;

    /// <summary>开始的世界秒数。</summary>
    public float At;

    /// <summary>生长、聚合或动作目标时长；制作动作填零使用原时长。</summary>
    public float Seconds;

    /// <summary>旋转角速度、飞行局部速度或聚合的目标位置。</summary>
    public float[] Vector = new float[3];

    /// <summary>飞行追踪时每秒允许转过的角度，零表示不追踪。</summary>
    public float TurnRate;

    /// <summary>条件生长的目标尺寸倍率，空值长至原始尺寸。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? TargetScale;

    /// <summary>条件动作从当前姿态过渡的秒数，空值采用0.15秒。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? BlendSeconds;

    /// <summary>本次动作实际开始后，在原动作时钟中维持；不会提前消耗等待事件的时间。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionHold Hold;

    /// <summary>聚合完成后的局部朝向；空值沿用节点原朝向。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float[] GatherRotation;

    /// <summary>按资源真实分件碎解的明确参数。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionFracture Fracture;

    /// <summary>有限浅层流散的实际参数。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionFlow Flow;

    /// <summary>收束时的实际回能效率，只适用于生成主体。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionRecovery Recovery;

    /// <summary>有界导电的距离、消耗及上限。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionConduction Conduction;

    /// <summary>地表传力的路程、速度、波面和实际消耗。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionPropagation Propagation;

    /// <summary>有限射线的长度、指向更新和角速度。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionRay Ray;

    /// <summary>材料交给世界后采用的存在规则。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionResidue Residue;

    /// <summary>可选的世界材料选择，未配置时只改变本生成主体。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionWorldMatter WorldMatter;

    /// <summary>从本次调整起算的时间安排；空数组恢复常速。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionProcessWindow[] TimeWindows;

    /// <summary>物态变化的明确输出材料。</summary>
    public string Matter;

    /// <summary>物态变化的明确输出物态，空值使用该材料默认。</summary>
    public string State;

    /// <summary>物态变化的实际触发，其他操作只允许默认的时间触发。</summary>
    public ConstructionChangeTrigger Trigger;

    /// <summary>正在运行的部位响应真实事件；空值沿用原有操作规则。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionCondition When;

    /// <summary>飞行加速度；空值表示不加速。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float[] Acceleration;

    /// <summary>初速度是否按地图方向解释；同样作为加速度方向的缺省规则。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool WorldVelocity;

    /// <summary>即使初速度随形体朝向，也按地图方向解释加速度。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool WorldAcceleration;

    /// <summary>实际作用形状接触地图零高度平面时的响应。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionGroundResponse Ground;

    /// <summary>飞行中心线以外的实际曲线路径，空值保留直行及现有追踪。</summary>
    public ConstructionPath Path;

    /// <summary>明确的飞行目标及到达处理，空值保留原有追踪。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionFlightGoal Goal;

    /// <summary>可选的间隔转移，空值保持连续飞行。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionFlightTransfer Transfer;

    /// <summary>调整速度操作的实际中心速度，零停止中心前进但不停止曲线偏移。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float Speed;

    /// <summary>开始飞行时是否保留脱落点的速度，旧资料默认保留。</summary>
    public bool InheritVelocity = true;
}

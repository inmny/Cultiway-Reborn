using System;
using System.IO;
using Cultiway.Core.Visuals.Forms;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>映像对应的原人物，受理后不因选敌或反射重新绑定。</summary>
public enum ConstructionCorrespondenceTarget
{
    /// <summary>本次选中的原人物。</summary>
    Target = 0,
    /// <summary>本次真实施放来源。</summary>
    Source = 1
}

/// <summary>映像可转交的附带作用，伤害另按传伤份额计算。</summary>
[Flags]
public enum ConstructionCorrespondenceEffects
{
    /// <summary>只按原有传伤份额处理伤害。</summary>
    None = 0,
    /// <summary>转交面向接触人物的恢复和净化。</summary>
    Restoration = 1,
    /// <summary>转交明确的状态和受击反应。</summary>
    Status = 2,
    /// <summary>转交指向原人物的直接推拉、击飞与击退。</summary>
    Force = 4
}

/// <summary>普通生成部位与原人物的明确对应，不另设一份人物生命。</summary>
public sealed class ConstructionCorrespondence
{
    /// <summary>所属部位内的稳定关系名称。</summary>
    public string Id;

    /// <summary>受理时确定的原人物。</summary>
    public ConstructionCorrespondenceTarget Target;

    /// <summary>实际交锋消耗按攻击原倍率传给原人物的份额，零表示不传伤。</summary>
    public float DamageFraction = 1f;

    /// <summary>允许转交的附带作用，缺省保留原映像行为。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionCorrespondenceEffects Effects;

    /// <summary>映像部位出现多久后开始传递，使用世界秒数。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float ActiveFrom;

    /// <summary>从此时点起停止传递，省略时随映像部位结束。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? ActiveUntil;

    /// <summary>发起的主动接触只作用于关系有效时的原人物，且本次归属者必须存活。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool OriginalContactOnly;

    /// <summary>首次确认作用后，以原人物的实际部位显示短暂立影。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ShowBody;

    /// <summary>让原人物的影身先升起，再以两侧真实接触取得抱持。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionShadowHold ShadowHold;

    /// <summary>在具名镜口显示原人物当前的真实表面。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionMirrorImage MirrorImage;
}

/// <summary>可组合的影身抱持规则，时间单位为世界秒。</summary>
public sealed class ConstructionShadowHold
{
    public float RiseSeconds = .52f;
    public float CloseSeconds = .34f;
    public float HoldSeconds = .95f;
    public float EscapeDistance = .55f;
    public float Strength = 3f;
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float DragDistance;
}

/// <summary>运行时使用的独立参数，不引用原编辑资料。</summary>
internal sealed class CompiledShadowHold
{
    internal readonly float RiseSeconds, CloseSeconds, HoldSeconds, EscapeDistance, Strength, DragDistance;

    internal CompiledShadowHold(ConstructionShadowHold source, float window, string path)
    {
        if (!InRange(source.RiseSeconds, .12f, 3f) || !InRange(source.CloseSeconds, .12f, 3f) ||
            !InRange(source.HoldSeconds, .12f, 12f) || !InRange(source.EscapeDistance, .1f, 6f) ||
            !InRange(source.Strength, .01f, 100f) || !InRange(source.DragDistance, 0f, 8f) ||
            source.RiseSeconds + source.CloseSeconds + source.HoldSeconds > window)
            throw new InvalidDataException(path + "：抱持的各阶段需要落在对应时段内，挣脱距离和强度须有限且为正");

        RiseSeconds = source.RiseSeconds;
        CloseSeconds = source.CloseSeconds;
        HoldSeconds = source.HoldSeconds;
        EscapeDistance = source.EscapeDistance;
        Strength = source.Strength;
        DragDistance = source.DragDistance;
    }

    private static bool InRange(float value, float minimum, float maximum) => value >= minimum && value <= maximum;
}

/// <summary>边界检查后的独立对应资料，运行不读取编辑中的声明。</summary>
public sealed class CompiledCorrespondence
{
    /// <summary>所属部位内的明确关系名称。</summary>
    public string Id { get; }

    /// <summary>受理时绑定的原人物类别。</summary>
    public ConstructionCorrespondenceTarget Target { get; }

    /// <summary>实际消耗的传伤份额。</summary>
    public float DamageFraction { get; }

    /// <summary>原人物允许接收的附带作用。</summary>
    public ConstructionCorrespondenceEffects Effects { get; }

    /// <summary>开始传递的部位年龄。</summary>
    public float ActiveFrom { get; }

    /// <summary>停止传递的部位年龄，缺省随真实部位结束。</summary>
    public float ActiveUntil { get; }

    /// <summary>主动接触限定到关系有效的原人物。</summary>
    public bool OriginalContactOnly { get; }

    /// <summary>确认作用后使用原人物身体显示映像。</summary>
    public bool ShowBody { get; }

    internal CompiledShadowHold ShadowHold { get; }

    /// <summary>已核对的镜口和映照资料。</summary>
    internal CompiledMirrorImage MirrorImage { get; }

    /// <summary>只在完整构造边界核对名称、端点和有限份额。</summary>
    /// <param name="source">明确声明的对应。</param><param name="lifetime">本次施放总寿命。</param>
    /// <param name="path">所属部位资料路径。</param><param name="resource">实际映像载体的形体。</param>
    internal CompiledCorrespondence(ConstructionCorrespondence source, float lifetime, string path,
        FormResource resource)
    {
        if (string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 128 ||
            Array.Exists(source.Id.ToCharArray(), character => !char.IsLetterOrDigit(character) && character != '_' && character != '-'))
            throw new InvalidDataException(path + "：对应名称须为1至128个字母、数字、下划线或短横线");
        if (!Enum.IsDefined(typeof(ConstructionCorrespondenceTarget), source.Target) ||
            float.IsNaN(source.DamageFraction) || float.IsInfinity(source.DamageFraction) ||
            source.DamageFraction < 0f || source.DamageFraction > 1f)
            throw new InvalidDataException(path + "：对应需要明确原人物，传伤份额须在零至一之间");
        const ConstructionCorrespondenceEffects supported = ConstructionCorrespondenceEffects.Restoration |
            ConstructionCorrespondenceEffects.Status | ConstructionCorrespondenceEffects.Force;
        if ((source.Effects & ~supported) != 0) throw new InvalidDataException(path + "/Effects：存在未知的映像作用许可");
        if (float.IsNaN(source.ActiveFrom) || source.ActiveFrom < 0f || source.ActiveFrom >= lifetime ||
            source.ActiveUntil is float until && (float.IsNaN(until) || until <= source.ActiveFrom || until > lifetime))
            throw new InvalidDataException(path + "：映像传递的起止时间需要在施放寿命内递增");
        Id = source.Id;
        Target = source.Target;
        OriginalContactOnly = source.OriginalContactOnly;
        ShowBody = source.ShowBody;
        DamageFraction = source.DamageFraction;
        Effects = source.Effects;
        ActiveFrom = source.ActiveFrom;
        ActiveUntil = source.ActiveUntil ?? float.PositiveInfinity;
        if (source.MirrorImage != null)
        {
            if (ShowBody || source.ShadowHold != null)
                throw new InvalidDataException(path + "/MirrorImage：镜内映像与脚下立影不能由同一部位发起");
            MirrorImage = new CompiledMirrorImage(source.MirrorImage, resource, path + "/MirrorImage");
        }
        if (source.ShadowHold != null)
        {
            if (Target != ConstructionCorrespondenceTarget.Target || !OriginalContactOnly || ShowBody ||
                DamageFraction != 0f || Effects != ConstructionCorrespondenceEffects.None)
                throw new InvalidDataException(path + "/ShadowHold：需要只与所选原身接触，不能同时转交伤害或附带作用");
            ShadowHold = new CompiledShadowHold(source.ShadowHold, (source.ActiveUntil ?? lifetime) - ActiveFrom,
                path + "/ShadowHold");
        }
    }
}

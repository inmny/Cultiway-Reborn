using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>按已有真实分件拆解，装饰和继续作用由资料明确指定。</summary>
public sealed class ConstructionFracture
{
    /// <summary>碎片最多继续存在的世界秒数，仍受整次上限限制。</summary>
    public float Seconds = 2f;
    /// <summary>在继承实际运动后，沿主体中心向外增加的分散速度。</summary>
    public float Speed = 2f;
    /// <summary>碎片围绕自身中心转动的每秒角度。</summary>
    public float Spin = 90f;
    /// <summary>沿地图高度向下的加速度。</summary>
    public float Gravity = 4f;
    /// <summary>明确继续原作用的真实部件名；其余分件均为装饰。</summary>
    public string[] ActiveParts = Array.Empty<string>();
    /// <summary>已经存在且仍附着的普通子结构随破坏脱落，否则随主体结束。</summary>
    public bool ReleaseChildren = true;
}

/// <summary>编译边界固定的分件、余力分配及后续运动，不持有可编辑资料。</summary>
internal sealed class CompiledFracture
{
    /// <summary>对应真实分件的展开部位索引。</summary>
    internal int[] Pieces;
    /// <summary>每个实际分件是否继续原作用。</summary>
    internal readonly bool[] Active;
    /// <summary>真正接受作用量的分件数。</summary>
    internal readonly int ActiveCount;
    /// <summary>存在窗口及分散和转动速度。</summary>
    internal readonly float Seconds, Speed, Spin;
    /// <summary>附属子结构的明确结束合同。</summary>
    internal readonly bool ReleaseChildren;
    /// <summary>原有飞行执行读取的后续下落与着地规则。</summary>
    internal readonly CompiledFlight Flight;

    /// <summary>一次核对真实分件及有界运动，缺少作用形状的分件不能被指定为攻击。</summary>
    /// <param name="source">明确碎解参数。</param>
    /// <param name="resource">实际形体。</param>
    /// <param name="role">原主体的作用用途。</param>
    /// <param name="lifetime">整次存在上限。</param>
    /// <param name="path">具体部位。</param>
    internal CompiledFracture(ConstructionFracture source, FormResource resource, ConstructionContactRole role, float lifetime, string path)
    {
        if (!resource.Structure.Supports("separate"))
            throw new InvalidDataException(path + "：形体" + resource.Description.Id + "没有可分离的真实结构，不能执行碎解；需要明确分件及可分离连接资料");
        if (source == null || resource.Meshes.Length < 2 || resource.Meshes.Length > 32 || source.ActiveParts == null ||
            !Finite(source.Seconds) || source.Seconds <= 0 || source.Seconds > lifetime ||
            !Finite(source.Speed) || source.Speed < 0 || source.Speed > 1000 ||
            !Finite(source.Spin) || Math.Abs(source.Spin) > 7200 || !Finite(source.Gravity) || source.Gravity < 0 || source.Gravity > 1000)
            throw new InvalidDataException(path + "：碎解需要2至32个真实分件、明确作用部件、有效存在窗口及有界分散、转动和下落");
        if (source.ActiveParts.Distinct().Count() != source.ActiveParts.Length ||
            source.ActiveParts.Any(name => !resource.Description.Parts.Any(part => part.Id == name)) ||
            role == ConstructionContactRole.Decoration && source.ActiveParts.Length > 0)
            throw new InvalidDataException(path + "：继续作用的分件须存在且不重复，装饰主体不能凭空产生攻击碎片");
        Active = resource.Description.Parts.Select(part => source.ActiveParts.Contains(part.Id)).ToArray();
        for (int part = 0; part < Active.Length; part++)
            if (Active[part] && !resource.Description.Contacts.Any(contact => contact.Part == part))
                throw new InvalidDataException(path + "：继续作用的分件缺少实际作用形状：" + resource.Description.Parts[part].Id);
        ActiveCount = Active.Count(value => value); Seconds = source.Seconds; Speed = source.Speed; Spin = source.Spin;
        ReleaseChildren = source.ReleaseChildren;
        Flight = new CompiledFlight(new ConstructionOperation
        {
            WorldVelocity = true,
            Acceleration = new[] { 0f, 0f, -source.Gravity },
            Ground = ConstructionGroundResponse.Stop
        }, Vector3.zero, path + "/碎片运动");
    }

    /// <summary>拒绝不能进入数值计算的外部参数。</summary>
    /// <param name="value">外部数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

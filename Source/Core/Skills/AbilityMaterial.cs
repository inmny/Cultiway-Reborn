using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>独立材料保存所需的当前数值，不保存Unity对象、地表和施放容器。</summary>
public sealed class AbilityMaterialData
{
    /// <summary>完整形体或以冒号分隔的真实分件编号。</summary>
    public string Form;

    /// <summary>实际材料及物态。</summary>
    public string Matter, State;

    /// <summary>明确的显示覆盖，不改变实际材料。</summary>
    public string Surface;

    /// <summary>根矩阵及必要的内部骨架姿态。</summary>
    public float[] Transform, Skin;

    /// <summary>保存时的有序控制名，无法对应的新骨架按单条报告。</summary>
    public string[] Controls;

    /// <summary>可选显示染色。</summary>
    public float[] Dye;

    /// <summary>当前体积及尚存的原支付份额。</summary>
    public double Volume, Resource;

    /// <summary>动态水层的实际数值，静态物件为空。</summary>
    public AbilityFlowData Flow;
}

/// <summary>独立水层的保存值，重新加载时重建地表和工作数组。</summary>
public sealed class AbilityFlowData
{
    /// <summary>原有数值设置。</summary>
    public ConstructionFlow Settings;

    /// <summary>实际格宽及地图起点。</summary>
    public float Cell, X, Y;

    /// <summary>每格水深与两个方向动量，依次保存。</summary>
    public float[] Water;

    /// <summary>已经外流、蒸散及转化的体积。</summary>
    public double Escaped, Evaporated, Recovered, Transferred;

    /// <summary>可选的逐格原支付余量，随共同水流输运。</summary>
    public double[] Budget;

    /// <summary>冻结形态的实际基底，流动地表仍由当前环境重新取得。</summary>
    public float[] Bed;
}

/// <summary>已经转交的生成材料；推进、选择、保存和显示共同读取这份状态。</summary>
public sealed class AbilityMaterial
{
    /// <summary>仍持有所属版本的形体资源，不在计算中读取网格。</summary>
    internal readonly FormResource Form;

    /// <summary>当前真实材料。</summary>
    internal AbilityMatter Matter;

    /// <summary>完整生成姿态与可选骨架。</summary>
    internal Matrix4x4 Transform;

    /// <summary>固定的内部姿态。</summary>
    internal readonly Matrix4x4[] Skin;

    /// <summary>同一内部姿态的模型边界。</summary>
    internal readonly Bounds? PoseBounds;

    /// <summary>原来世界空间的实际形状；水层由真实格子提供。</summary>
    internal readonly AbilityCapsule[] Shapes;

    /// <summary>明确显示表面与染色。</summary>
    internal readonly string Surface;

    /// <summary>可选显示染色。</summary>
    internal readonly Color? Dye;

    /// <summary>独立拥有的数值水层，空值为刚性材料。</summary>
    internal readonly AbilityFlow Flow;

    /// <summary>当前实际材料及支付份额。</summary>
    public double Volume { get; internal set; }

    /// <summary>尚存原支付份额。</summary>
    public double Resource { get; internal set; }

    /// <summary>动态曲面实际格数，不读取显示资源。</summary>
    public int Cells => Flow?.Settings.Cells ?? 0;

    /// <summary>从真实部位建立独立材料，传入的数组不再与施放共享修改。</summary>
    /// <param name="form">实际形体。</param><param name="matter">实际材料。</param>
    /// <param name="transform">实际根姿态。</param><param name="skin">已复制内部姿态。</param>
    /// <param name="bounds">模型边界。</param><param name="shapes">实际形状。</param>
    /// <param name="surface">显示覆盖。</param><param name="dye">显示染色。</param>
    /// <param name="volume">实际体积。</param><param name="resource">实际支付份额。</param>
    /// <param name="flow">独立数值水层。</param>
    internal AbilityMaterial(FormResource form, AbilityMatter matter, Matrix4x4 transform, Matrix4x4[] skin,
        Bounds? bounds,
        AbilityCapsule[] shapes, string surface, Color? dye, double volume, double resource, AbilityFlow flow)
    {
        Form = form;
        Matter = matter;
        Transform = transform;
        Skin = skin;
        PoseBounds = bounds;
        Shapes = shapes;
        Surface = surface;
        Dye = dye;
        Volume = volume;
        Resource = resource;
        Flow = flow;
    }

    /// <summary>恢复或地表变化时重建流动层基底，保持态仍用原有形状。</summary>
    /// <param name="terrain">当前共同地表。</param>
    internal void RefreshGround(AbilityHeightField terrain)
    {
        if (Flow != null && !Matter.HoldsShape) Flow.RefreshGround(terrain);
    }

    /// <summary>按实际传播时间推进，世界维护者决定每个有限时间片。</summary>
    /// <param name="seconds">有效世界秒数。</param><param name="terrain">本轮共同地表。</param>
    internal void Advance(float seconds, AbilityHeightField terrain) => AdvanceBounded(seconds, terrain, false);

    /// <summary>还不能进入共同格网时保留有限材料，不从边缘删除。</summary>
    /// <param name="seconds">实际时间片。</param><param name="terrain">当前共同地表。</param><param name="closeBoundary">是否反射边界。</param>
    internal void AdvanceBounded(float seconds, AbilityHeightField terrain, bool closeBoundary)
    {
        if (Flow == null || Matter.HoldsShape) return;
        double before = Volume;
        Flow.RefreshGround(terrain);
        Flow.AdvanceBounded(seconds, closeBoundary);
        Volume = Flow.Volume;
        if (before > 0) Resource *= Math.Min(1, Volume / before);
    }

    /// <summary>数值量转化为原资源，显示与真实体积一起减小。</summary>
    /// <param name="ratio">本步后保留比例。</param>
    internal void Retain(float ratio)
    {
        if (Flow != null)
        {
            Flow.Recover(ratio);
            Volume = Flow.Volume;
        }
        else
        {
            float scale = Mathf.Pow(ratio, 1f / 3f);
            Vector3 centre = Transform.GetColumn(3);
            Transform *= Matrix4x4.Scale(Vector3.one * scale);
            Volume *= ratio;
            for (int i = 0; i < Shapes.Length; i++)
            {
                AbilityCapsule old = Shapes[i];
                Shapes[i] = new AbilityCapsule(0,
                    centre + (old.A - centre) * scale, centre + (old.B - centre) * scale, old.Radius * scale, Matter.Id,
                    material: Matter, halfExtents: old.HalfExtents * scale);
            }
        }

        Resource *= ratio;
    }

    /// <summary>从同一世界事实建立可发布显示，不推进材料。</summary>
    /// <param name="terrain">当前共同地表。</param>
    internal FormVisualInstance Visual(AbilityHeightField terrain, FormLayerFrame layer = null) => new(Form, Transform, surface: Surface, skin: Skin,
        poseBounds: PoseBounds, matterSurface: Matter.Surface, dye: Dye, matter: Matter, terrain: terrain,
        layer: layer ?? (Flow == null ? null : new FormLayerFrame(Flow)));

    /// <summary>取得真实材料形状，选择与画面共用水深和固定姿态。</summary>
    /// <param name="output">复用选择缓冲。</param>
    internal void Contacts(List<AbilityCapsule> output)
    {
        if (Flow == null)
        {
            output.AddRange(Shapes);
            return;
        }

        for (int i = 0; i < Flow.Water.Length; i++)
            if (Flow.Water[i].x >= Flow.Cell * AbilityFlow.DryFraction)
                output.Add(CellShape(i));
    }

    /// <summary>实际水柱，不以平面宽度扩大高度。</summary>
    /// <param name="index">格子编号。</param>
    private AbilityCapsule CellShape(int index, float? height = null)
    {
        float depth = height ?? Flow.Water[index].x;
        var centre = new Vector3(Flow.Origin.x + (index % Cells + .5f) * Flow.Cell,
            Flow.Origin.y + (index / Cells + .5f) * Flow.Cell, Flow.Ground[index] + depth * .5f);
        return new AbilityCapsule(0, centre, centre, 0, Matter.Id, index, material: Matter,
            halfExtents: new Vector3(Flow.Cell * .5f, Flow.Cell * .5f, depth * .5f));
    }

    /// <summary>核对一组真实形状是否相交，复用共同胶囊及薄箱体算法。</summary>
    /// <param name="shape">材料形状。</param><param name="selection">本次实际选择形状。</param>
    private static bool Selected(in AbilityCapsule shape, IReadOnlyList<AbilityCapsule> selection)
    {
        foreach (AbilityCapsule other in selection)
            if (AbilityPairSweep.Find(shape, shape, other, other, out _, out _, out _))
                return true;
        return false;
    }

    /// <summary>按实际湿格选取；返回零、部分或全部，不复制原水量。</summary>
    /// <param name="selection">另一施放的实际形状。</param>
    /// <param name="picked">可复用的已选格缓冲。</param>
    internal bool Select(IReadOnlyList<AbilityCapsule> selection, List<int> picked)
    {
        picked.Clear();
        if (Flow == null) return Shapes.Any(shape => Selected(shape, selection));
        for (int i = 0; i < Flow.Water.Length; i++)
            if (Flow.Water[i].x >= Flow.Cell * AbilityFlow.DryFraction && Selected(CellShape(i), selection))
                picked.Add(i);
        return picked.Count > 0;
    }

    /// <summary>混流水柱内的来源按共同高度选择，只提取自己的局部实际水量。</summary>
    /// <param name="selection">真实选择形状。</param><param name="picked">接收选中格。</param><param name="heights">共同水柱高度。</param>
    internal bool SelectShared(IReadOnlyList<AbilityCapsule> selection, List<int> picked, float[] heights)
    {
        picked.Clear();
        for (int i = 0; i < Flow.Water.Length; i++)
            if (Flow.Water[i].x > 0 && heights[i] > 0 &&
                Selected(CellShape(i, heights[i]), selection))
                picked.Add(i);
        return picked.Count > 0;
    }

    /// <summary>空间筛选也按共同水柱，不将某一来源压在整池底部。</summary>
    /// <param name="output">实际空间缓冲。</param><param name="heights">共同水深。</param>
    internal void SharedContacts(List<AbilityCapsule> output, float[] heights)
    {
        for (int i = 0; i < Flow.Water.Length; i++)
            if (Flow.Water[i].x > 0 && heights[i] > 0)
                output.Add(CellShape(i, heights[i]));
    }

    /// <summary>已接受的部分水格转交新记录，按真实体积分配支付余量。</summary>
    /// <param name="picked">已选且尚未改动的格子。</param>
    internal AbilityMaterial Take(List<int> picked)
    {
        var taken = new AbilityFlow(Flow.Settings);
        Flow.CopyTo(taken);
        Array.Clear(taken.Water, 0, taken.Water.Length);
        if (taken.Budget != null) Array.Clear(taken.Budget, 0, taken.Budget.Length);
        foreach (int index in picked)
        {
            if (Flow.Budget != null)
            {
                taken.Budget[index] = Flow.Budget[index];
                Flow.Budget[index] = 0;
            }

            taken.Water[index] = Flow.Water[index];
            Flow.Water[index] = Vector3.zero;
        }

        double before = Volume;
        Flow.RefreshTotals();
        taken.RefreshTotals();
        Flow.Transferred += taken.Volume;
        double resource = taken.Budget != null ? taken.Resource :
            before > 0 ? Resource * Math.Min(1, taken.Volume / before) : 0;
        Resource -= resource;
        Volume = Flow.Volume;
        taken.Escaped = taken.Evaporated = taken.Recovered = taken.Transferred = 0;
        return new AbilityMaterial(Form, Matter, Transform, Skin, PoseBounds, Array.Empty<AbilityCapsule>(), Surface,
            Dye, taken.Volume, resource, taken);
    }

    /// <summary>格网转换后仍沿用本材料的外观与真实归属，水量由新工作区维护。</summary>
    /// <param name="flow">独立的实际格网层。</param>
    internal AbilityMaterial WithFlow(AbilityFlow flow) => new(Form, Matter, Transform, Skin, PoseBounds,
        Array.Empty<AbilityCapsule>(), Surface, Dye, flow.Volume, flow.Resource, flow);

    /// <summary>保存只包含稳定资源名与数值，不把显示和工作区对象序列化。</summary>
    internal AbilityMaterialData Capture()
    {
        return new AbilityMaterialData
        {
            Form = Form.Description.Id,
            Matter = Matter.Id,
            State = Matter.State,
            Surface = Surface,
            Transform = Pack(new[] { Transform }),
            Skin = Skin == null ? null : Pack(Skin),
            Volume = Volume,
            Resource = Resource,
            Controls = Form.Description.Controls.Select(control => control.Id).ToArray(),
            Dye = Dye.HasValue ? new[] { Dye.Value.r, Dye.Value.g, Dye.Value.b } : null,
            Flow = Flow == null ? null : Flow.Capture(Matter.HoldsShape)
        };
    }

    /// <summary>矩阵以固定数值保存，避免Unity属性递归序列化。</summary>
    /// <param name="matrices">已确定的矩阵。</param>
    internal static float[] Pack(Matrix4x4[] matrices) =>
        matrices.SelectMany(m => Enumerable.Range(0, 16).Select(i => m[i])).ToArray();

    /// <summary>在保存边界检查并重建材料，未知资源由世界玩法逐条报告。</summary>
    /// <param name="data">一条正式保存资料。</param>
    internal static AbilityMaterial Restore(AbilityMaterialData data)
    {
        bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        bool Values(float[] values, int count) =>
            values != null && values.Length == count && values.All(v => Finite(v));

        if (data == null || string.IsNullOrEmpty(data.Form) || !Values(data.Transform, 16) || !Finite(data.Volume) ||
            data.Volume < 0 ||
            !Finite(data.Resource) || data.Resource < 0 || data.Skin != null && !Values(data.Skin, 512) ||
            data.Dye != null && !Values(data.Dye, 3) || data.Controls == null || data.Controls.Length > 32)
            throw new InvalidDataException("世界材料姿态、体积或支付份额无效");
        string[] id = data.Form.Split(':');
        FormResource form = FormResources.Load(id[0]);
        if (id.Length > 2) throw new InvalidDataException("世界材料分件编号无效");
        if (id.Length == 2)
        {
            int index = Array.FindIndex(form.Description.Parts, part => part.Id == id[1]);
            if (index < 0) throw new InvalidDataException("世界材料分件已经不存在：" + data.Form);
            form = form.Part(index);
        }

        AbilityMatter matter = AbilityMatters.Resolve(data.Matter, data.State, "世界材料");
        if (data.Surface != null) form.Surface(data.Surface, true);

        Matrix4x4[] Unpack(float[] values)
        {
            var result = new Matrix4x4[values.Length / 16];
            for (int i = 0; i < values.Length; i++) result[i / 16][i % 16] = values[i];
            return result;
        }

        Matrix4x4 matrix = Unpack(data.Transform)[0];
        if (!data.Controls.SequenceEqual(form.Description.Controls.Select(control => control.Id)))
            throw new InvalidDataException("世界材料骨架已改变，无法对应旧姿态");
        FormPose pose = form.Rig.CreatePose();
        Matrix4x4[] skin = data.Skin == null ? null : Unpack(data.Skin);
        if (pose != null && skin == null) throw new InvalidDataException("世界材料缺少内部姿态");
        if (pose != null)
        {
            Array.Copy(skin, pose.Skin, skin.Length);
            form.Rig.RebuildBounds(pose);
        }

        AbilityCapsule[] shapes = BuildShapes(form, pose, matrix);
        AbilityFlow flow = data.Flow == null ? null : AbilityFlow.Restore(data.Flow);
        if (flow != null && matter.HoldsShape && data.Flow.Bed == null) throw new InvalidDataException("冻结材料缺少实际形态基底");
        return new AbilityMaterial(form, matter, matrix, skin, pose?.Bounds,
            shapes, data.Surface, data.Dye == null ? null : new Color(data.Dye[0], data.Dye[1], data.Dye[2]),
            flow?.Volume ?? data.Volume, data.Resource, flow);
    }

    /// <summary>按当前资源与同一骨架重建选择形状，更新模型后不保留一套过时判定。</summary>
    /// <param name="form">实际资源。</param><param name="pose">实际内部姿态。</param>
    /// <param name="matrix">根姿态。</param>
    internal static AbilityCapsule[] BuildShapes(FormResource form, FormPose pose, Matrix4x4 matrix)
    {
        float scale = Mathf.Max(matrix.lossyScale.x, matrix.lossyScale.y, matrix.lossyScale.z);
        if (form.Contacts.Length > 0)
            return form.Contacts.Select(c => new AbilityCapsule(0,
                matrix.MultiplyPoint3x4(c.ABlend.Point(c.A, pose)), matrix.MultiplyPoint3x4(c.BBlend.Point(c.B, pose)),
                c.Radius * scale, null)).ToArray();
        Bounds local = form.Description.Id.Contains(":")
            ? form.PosedBounds(pose)
            : pose?.Bounds ?? form.Meshes[0].bounds;
        Bounds world = default;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            if (i == 0) world = new Bounds(p, Vector3.zero);
            else world.Encapsulate(p);
        }

        return new[] { new AbilityCapsule(0, world.center, world.center, 0, null, halfExtents: world.extents) };
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>绑定当前身体真实拥有、通过外观取舍且有制作根点的器官。</summary>
internal sealed class BodyOrganBinding
{
    /// <summary>可选三维身体的真实模型接入，未安装时为空。</summary>
    internal static Func<Actor, CreatureVisualLayerAsset, BodyOrganModel> ModelProvider;

    /// <summary>本次真正借用的三维器官，原版部件为空。</summary>
    internal BodyOrganModel Model { get; private set; }

    /// <summary>同一制作图片和根点共享数值形状，候选选择不反复读图。</summary>
    private static readonly Dictionary<(Sprite sprite, Vector2 mount), BodyOrganShape> shapes = new();

    /// <summary>实际身体，只在主线程采集安装资料。</summary>
    private readonly Actor owner;

    /// <summary>原身体签名，器官替换或升阶后不继续借用旧形体。</summary>
    private readonly string signature;

    /// <summary>本次实际器官的稳定编号。</summary>
    private readonly string organId;

    /// <summary>实际外观层及原版帧集合。</summary>
    internal readonly CreatureVisualLayerAsset Layer;

    /// <summary>实际借用的图片，施放期间不换成另一张形状。</summary>
    internal readonly Sprite Sprite;

    /// <summary>制作图中的根点，相对图片左下角。</summary>
    internal readonly Vector2 Mount;

    /// <summary>由实际不透明像素整理的接触形状。</summary>
    internal readonly BodyOrganShape Shape;

    /// <summary>当前真实身体帧的安装点。</summary>
    private Vector2 bodyMount;

    /// <summary>当前真实身体帧的枢轴及像素单位。</summary>
    private Sprite body;

    /// <summary>锁定实际器官图片和几何，后续只更新根点。</summary>
    /// <param name="actor">实际身体。</param><param name="id">所选器官。</param>
    /// <param name="signature">真实身体签名。</param><param name="layer">实际显示层。</param>
    /// <param name="frame">当前制作帧。</param><param name="body">原版身体帧。</param>
    private BodyOrganBinding(Actor actor, string id, string signature, CreatureVisualLayerAsset layer,
        CreatureLayerFrame frame, Sprite body)
    {
        owner = actor;
        organId = id;
        this.signature = signature;
        Layer = layer;
        this.body = body;
        bodyMount = new Vector2(frame.Points["mount"].X, frame.Points["mount"].Y);
        Mount = bodyMount - new Vector2(frame.X, frame.Y);
        Sprite = SpriteTextureLoader.getSprite(frame.SpritePath);
        if (shapes.TryGetValue((Sprite, Mount), out Shape)) return;
        Color32[] pixels = CreatureAppearanceService.ReadPixels(Sprite);
        int width = Mathf.RoundToInt(Sprite.rect.width), height = Mathf.RoundToInt(Sprite.rect.height);
        var starts = new List<Vector2>();
        var ends = new List<Vector2>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (pixels[y * width + x].a == 0) continue;
            int first = x;
            while (x + 1 < width && pixels[y * width + x + 1].a != 0) x++;
            starts.Add(new Vector2(first + .5f, y + .5f) - Mount);
            ends.Add(new Vector2(x + .5f, y + .5f) - Mount);
        }

        if (starts.Count > 0)
        {
            Shape = new BodyOrganShape(starts.ToArray(), ends.ToArray());
            shapes.Add((Sprite, Mount), Shape);
        }
    }

    /// <summary>候选选择只查实际拥有及显示状态，不反复读取图片。</summary>
    /// <param name="actor">真实身体。</param><param name="id">实际器官编号。</param>
    /// <param name="signature">当前身体签名。</param><param name="layer">当前实际显示层。</param>
    /// <param name="frame">真实帧的制作资料。</param><param name="body">原版身体图片。</param>
    private static bool Resolve(Actor actor, string id, out string signature, out CreatureVisualLayerAsset layer,
        out CreatureLayerFrame frame, out Sprite body)
    {
        signature = null;
        layer = null;
        frame = null;
        body = actor.calculateMainSprite();
        if (body == null || !actor.TryGetExtend(out var owner) ||
            !owner.E.TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid ||
            !CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out var compiled))
            return false;
        var visible = CreatureAppearanceService.GetVisibleLayerIds(compiled);
        var ids = compiled.VisualLayers.Where(source => source.OrganId == id)
            .SelectMany(source => source.LayerIds).Where(visible.Contains).Distinct().ToArray();
        // 多个独立图层需要明确选择，不能任意取一个冒充整个器官。
        if (ids.Length != 1) return false;
        layer = Content.Libraries.Manager.CreatureVisualLayerLibrary.get(ids[0]);
        if (layer.ClipToBody || !layer.FramesByRig[compiled.Morph.VisualRigId].TryGetValue(body.name, out frame) ||
            frame.BodyWidth != body.rect.width || frame.BodyHeight != body.rect.height ||
            !frame.Points.ContainsKey("mount")) return false;
        signature = compiled.Signature;
        return true;
    }

    /// <summary>检查当前是否存在真实可控制器官。</summary>
    /// <param name="actor">真实身体。</param><param name="id">稳定器官编号。</param>
    internal static bool CanBind(Actor actor, string id) => Resolve(actor, id, out _, out var layer, out _, out _) &&
                                                            (ModelProvider == null ||
                                                             ModelProvider(actor, layer) != null);

    /// <summary>受理前捕获真实图片及作用段。</summary>
    /// <param name="actor">实际身体。</param><param name="id">稳定器官编号。</param>
    internal static BodyOrganBinding TryCreate(Actor actor, string id)
    {
        if (!Resolve(actor, id, out string signature, out var layer, out var frame, out var body)) return null;
        BodyOrganModel model = ModelProvider?.Invoke(actor, layer);
        if (ModelProvider != null && model == null) return null;
        var result = new BodyOrganBinding(actor, id, signature, layer, frame, body) { Model = model };
        return result.Shape == null ? null : result;
    }

    /// <summary>选择距离读取当前实际几何，不采用人类臂长。</summary>
    /// <param name="actor">真实身体。</param><param name="id">器官编号。</param>
    internal static float Reach(Actor actor, string id)
    {
        BodyOrganBinding binding = TryCreate(actor, id);
        if (binding == null) return 0f;
        if (binding.Model == null) return binding.Shape.Reach(binding.Frame());
        return binding.Model.Mount.magnitude * binding.Model.BodyScale + binding.Model.Resource.Contacts
            .Max(c => Mathf.Max(c.A.magnitude, c.B.magnitude) + c.Radius) * binding.Model.Scale;
    }

    /// <summary>正常三维显示沿用实际拥有及外观取舍，不从器官名称猜另一组外观。</summary>
    /// <param name="actor">真实身体。</param>
    internal static IEnumerable<CreatureVisualLayerAsset> VisibleLayers(Actor actor)
    {
        if (!actor.TryGetExtend(out var owner) ||
            !owner.E.TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid ||
            !CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out var compiled))
            yield break;
        foreach (string id in CreatureAppearanceService.GetVisibleLayerIds(compiled))
            yield return Content.Libraries.Manager.CreatureVisualLayerLibrary.get(id);
    }

    /// <summary>释放旧世界的图片引用和数值形状。</summary>
    internal static void ClearWorldState() => shapes.Clear();

    /// <summary>沿用当前身体动画的制作根点，器官消失后立即失效。</summary>
    internal bool Refresh()
    {
        if (!Resolve(owner, organId, out string current, out var layer, out var frame, out var native) ||
            current != signature || layer != Layer) return false;
        body = native;
        bodyMount = new Vector2(frame.Points["mount"].X, frame.Points["mount"].Y);
        return true;
    }

    /// <summary>在主线程把原版真实比例、翻转和倾斜转换为共同空间。</summary>
    /// <param name="direction">本次身体控制已经锁定的方向，选择距离时为空。</param>
    internal BodyOrganFrame Frame(Vector3? direction = null)
    {
        bool mirrored = owner.asset.can_flip && direction.HasValue ? direction.Value.x > 0f : owner.flip;
        var scale = new Vector2(owner.current_scale.x * (mirrored ? -1f : 1f),
            Mathf.Abs(owner.current_scale.y)) / body.pixelsPerUnit;
        Vector2 offset = Vector2.Scale(bodyMount - body.pivot, scale);
        Vector3 tilted = Quaternion.Euler(0f, 0f, owner.current_rotation.z) * offset;
        return new BodyOrganFrame(new Vector3(tilted.x, 0f, tilted.y), scale, owner.current_rotation.z,
            Model?.Frame(direction ?? (owner.flip ? Vector3.right : Vector3.left)), offset.y);
    }
}
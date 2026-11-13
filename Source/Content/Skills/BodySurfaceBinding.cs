using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Utils.Extension;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>目标表面的采集入口；普通原版图片与三维模型各自提供实际几何。</summary>
internal abstract partial class BodySurfaceBinding
{
    /// <summary>安装三维身体时提供实际模型，关闭三维视角不撤销此接入。</summary>
    internal static Func<Actor, BodySurfaceBinding> Provider;

    /// <summary>实际人物及其稳定身份。</summary>
    internal readonly Actor Actor;

    /// <summary>同一个人物，不沿用对象池的后来身份。</summary>
    internal readonly long Id;

    /// <summary>主线程发布时标记真实目标关系，普通查询不冻结图片。</summary>
    internal bool IsPinned;

    /// <summary>整体抓持才暂停自然姿态；只有握持臂受控时身体仍能自行活动。</summary>
    internal bool PinsWholeBody;

    /// <summary>原版读取同一图片池入口；模型的实际形状和配色直接来自共同表面。</summary>
    /// <param name="pose">已经发布的实际表面。</param>
    internal virtual Sprite PinnedSprite(BodySurfacePose pose) => nativeModelPicture;

    /// <summary>在原版完成并行准备后提交同一固定表面的实际显示。</summary>
    /// <param name="pose">维持中的表面。</param><param name="data">原版本轮绘制资料。</param><param name="index">人物顺序。</param>
    internal virtual bool PresentPinned(BodySurfacePose pose, ActorRenderData data, int index) =>
        PresentNativeModel(pose, data, index);

    /// <summary>采集时的身体资源与装备，避免对象池或换装沿用旧表面。</summary>
    private readonly ActorAsset bodyAsset;

    /// <summary>实际身体动画资料。</summary>
    private readonly AnimationContainerUnit appearance;

    /// <summary>创建时的真实器官及外观签名。</summary>
    private readonly string phenotype;

    /// <summary>实际四个装备槽中的物品。</summary>
    protected readonly Item[] Equipment;

    /// <summary>物品对象池重用后也能分辨原身份。</summary>
    private readonly long[] equipmentIds;

    /// <summary>原物品实际使用的装备资源。</summary>
    private readonly EquipmentAsset[] equipmentAssets;

    /// <summary>同一件物品换材后，旧装备表面不能继续维持。</summary>
    private readonly string[] equipmentMaterials;

    /// <summary>接触时的损坏状态；已经穿戴的旧破甲不使整个身体反复重建。</summary>
    private readonly bool[] equipmentBroken;

    /// <summary>当前已确认或实际采集的表面前后态。</summary>
    internal BodySurfacePose From, To;

    /// <summary>对应的游戏时间，原地暂停不重复推进走路。</summary>
    internal double FromTime, ToTime;

    /// <summary>当前资源与动作的合并布局。</summary>
    private BodySurfaceComposition composition;

    /// <summary>当前布局不变时只合并本轮姿态。</summary>
    /// <param name="poses">身体与真实动作表面。</param><param name="arms">本轮接管的手臂。</param>
    /// <param name="held">本轮是否接管持物。</param>
    internal BodySurfacePose Compose(IReadOnlyList<BodySurfacePose> poses, BodyOccupancy arms, bool held)
    {
        if (composition == null || !composition.Matches(poses, arms, held))
            composition = new BodySurfaceComposition(poses, arms, held, this is Native);
        return composition.Sample(poses);
    }

    /// <summary>取得原人物。</summary>
    /// <param name="actor">实际身体。</param>
    protected BodySurfaceBinding(Actor actor)
    {
        Actor = actor;
        Id = actor.id;
        bodyAsset = actor.asset;
        appearance = actor.animation_container;
        phenotype = BodyOrganBinding.Signature(actor);
        Equipment = new[]
        {
            actor.equipment?.helmet?.getItem(), actor.equipment?.armor?.getItem(),
            actor.equipment?.boots?.getItem(), actor.getWeapon()
        };
        equipmentIds = Array.ConvertAll(Equipment, item => item?.data?.id ?? 0);
        equipmentAssets = Array.ConvertAll(Equipment, item => item?.getAsset());
        equipmentMaterials = Array.ConvertAll(Equipment, item => item?.data?.material);
        equipmentBroken = Array.ConvertAll(Equipment, item => item?.isBroken() == true);
    }

    /// <summary>真正的身体或装备已改变时给出解除原因；行走切图片不算换身体。</summary>
    internal virtual string ChangedReason
    {
        get
        {
            if (Actor.data?.id != Id || Actor.asset != bodyAsset || Actor.animation_container != appearance)
                return "BodyChanged";
            if (BodyOrganBinding.Signature(Actor) != phenotype) return "OrgansChanged";
            for (int i = 0; i < Equipment.Length; i++)
            {
                Item current = i switch
                {
                    0 => Actor.equipment?.helmet?.getItem(),
                    1 => Actor.equipment?.armor?.getItem(), 2 => Actor.equipment?.boots?.getItem(),
                    _ => Actor.getWeapon()
                };
                if (!ReferenceEquals(current, Equipment[i]) || current != null &&
                    (current.data?.id != equipmentIds[i] || current.getAsset() != equipmentAssets[i] ||
                     current.data?.material != equipmentMaterials[i] ||
                     !current.isAlive() || current.isBroken() != equipmentBroken[i])) return "EquipmentChanged";
            }

            return null;
        }
    }

    /// <summary>每轮只采集一次，各个候选查询只读同一数值。</summary>
    /// <param name="foot">共同空间脚下。</param><param name="height">实际共同高度。</param><param name="time">实际采集时间。</param>
    internal void Capture(Vector3 foot, float height, double time)
    {
        if (To != null && time <= ToTime) return;
        From = To;
        FromTime = ToTime;
        To = Sample(foot, height, time);
        ToTime = time;
        if (From == null)
        {
            From = To;
            FromTime = time;
        }
        else if (!ReferenceEquals(From.Parts, To.Parts))
        {
            // 图片切帧没有对应顶点，采用当前真实轮廓；仍沿原根点移动，不能把整帧放在终点。
            Vector3 priorFoot = From.Foot;
            Matrix4x4 shift = Matrix4x4.Translate(priorFoot - To.Foot);
            From = new BodySurfacePose(To.Parts, Array.ConvertAll(To.Matrices, matrix => shift * matrix),
                priorFoot, To.Height, To.Heading);
            if (To.HasDeformation)
            {
                // 搭箭改变可见布局时，前态仍承接本轮真实弓形，不能退回初始直弓。
                var vertices = new Vector3[To.Parts.Length][];
                for (int part = 0; part < vertices.Length; part++) vertices[part] = To.Vertices(part);
                From = From.Deformed(vertices);
            }
            if (To.Opaque != null)
                From = From.WithOpaque(new BodySurfacePose(To.Opaque.Parts,
                    Array.ConvertAll(To.Opaque.Matrices, matrix => shift * matrix),
                    To.Opaque.Foot + priorFoot - To.Foot, To.Opaque.Height, To.Opaque.Heading));
        }

        if (From.Opaque != null && To.Opaque != null && !ReferenceEquals(From.Opaque.Parts, To.Opaque.Parts))
        {
            Vector3 movement = From.Foot - To.Foot;
            Matrix4x4 shift = Matrix4x4.Translate(movement);
            From = From.WithOpaque(new BodySurfacePose(To.Opaque.Parts,
                Array.ConvertAll(To.Opaque.Matrices, matrix => shift * matrix),
                To.Opaque.Foot + movement, To.Opaque.Height, To.Opaque.Heading));
        }
    }

    /// <summary>真实控制交接在共同步内改变位置时，从该确认时点重新采集，不沿用帧末旧表面。</summary>
    /// <param name="foot">实际交接后的根点。</param><param name="height">真实体高。</param><param name="time">共同确认时间。</param>
    internal void RestartCapture(Vector3 foot, float height, double time)
    {
        IsPinned = false;
        PinsWholeBody = false;
        From = To = null;
        FromTime = ToTime = time;
        From = To = Sample(foot, height, time);
    }

    /// <summary>取得共同时间中的同一表面。</summary>
    /// <param name="time">共同时间。</param>
    internal BodySurfacePose At(double time) => ToTime <= FromTime
        ? To
        : BodySurfacePose.Lerp(From, To, Mathf.Clamp01((float)((time - FromTime) / (ToTime - FromTime))));

    /// <summary>在同一身体表面上应用已经计算的整身、头部与两侧手臂，不推进普通步态。</summary>
    /// <param name="basis">原身体本步的表面。</param><param name="stance">共同整身支撑。</param>
    /// <param name="head">共同头部动作。</param><param name="right">右臂动作。</param><param name="left">左臂动作。</param>
    /// <param name="organ">实际受控的独立器官，其他动作为空。</param>
    internal virtual BodySurfacePose Acting(BodySurfacePose basis, BodyPose? stance, BodyPose? head,
        BodyPose? right, BodyPose? left, BodyOrganBinding organ) => basis;

    /// <summary>从接手时的实际身体建立恢复肢链，其他身体沿用原整体运动。</summary>
    /// <param name="basis">实际表面。</param><param name="stance">当前整身动作。</param>
    /// <param name="right">当前右臂。</param><param name="left">当前左臂。</param>
    internal virtual BodyRecoveryRig RecoveryRig(BodySurfacePose basis, BodyPose? stance = null,
        BodyPose? right = null, BodyPose? left = null) => null;

    /// <summary>读取普通持物时的真实握持臂，不借用另一次施法的姿态。</summary>
    /// <param name="surface">当前实际身体及持物表面。</param>
    internal virtual BodyPose? HeldArm(BodySurfacePose surface) => null;

    /// <summary>普通持物受外力时只重定位右臂，身体其余动作继续来自本轮自然采集。</summary>
    /// <param name="surface">当前自然表面。</param><param name="before">本轮原握持臂。</param>
    /// <param name="after">受约束后的臂链。</param>
    internal virtual BodySurfacePose ExternalArm(BodySurfacePose surface, in BodyPose before, in BodyPose after) =>
        Acting(surface, null, null, after, null, null);

    /// <summary>受控手腕带动接触时的实际持物表面，不重新生成或替换物品。</summary>
    /// <param name="surface">本轮身体及臂链。</param><param name="original">接触时的原物品表面。</param>
    /// <param name="delta">原握点发生的实际平移。</param>
    internal virtual BodySurfacePose ExternalHeld(BodySurfacePose surface, BodySurfacePose original, Vector3 delta)
    {
        var matrices = (Matrix4x4[])surface.Matrices.Clone();
        var vertices = new Vector3[surface.Parts.Length][];
        int from = 0;
        Matrix4x4 move = Matrix4x4.Translate(delta);
        for (int i = 0; i < surface.Parts.Length; i++)
        {
            BodySurfacePart part = surface.Parts[i];
            vertices[i] = surface.Vertices(i);
            if (part.Attachment != "item") continue;
            while (original.Parts[from].Attachment != "item") from++;
            BodySurfacePart old = original.Parts[from];
            matrices[part.TransformIndex < 0 ? i : part.TransformIndex] =
                move * original.Matrices[old.TransformIndex < 0 ? from : old.TransformIndex];
            vertices[i] = original.Vertices(from++);
        }
        return surface.WithPartTransforms(matrices, vertices);
    }

    /// <summary>原版加入实际出招臂链的不透明表面，三维继续由真实场景处理遮挡。</summary>
    /// <param name="body">实际身体。</param><param name="actions">本轮独立动作表面。</param>
    internal virtual BodySurfacePose IncludeActionOcclusion(BodySurfacePose body,
        IReadOnlyList<BodySurfacePose> actions) => body;

    /// <summary>由实际资源和身体运动生成姿态，不读取镜头是否绘制过人物。</summary>
    /// <param name="foot">共同脚下。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
    protected abstract BodySurfacePose Sample(Vector3 foot, float height, double time);

    /// <summary>在人物实际呈现接入边界创建，不使用共享人物胶囊。</summary>
    /// <param name="actor">实际身体。</param>
    internal static BodySurfaceBinding Create(Actor actor) => Provider != null ? Provider(actor) :
        actor.getSpriteToRender() == null ? null : new Native(actor);

    /// <summary>已经有实际部件映射的目标选择；手和脚不借用整段前臂或小腿冒充。</summary>
    /// <param name="part">构造中的真实部位。</param>
    internal static bool SupportsPart(string part) => part is "surface" or "torso" or "head" or
                                                          "arm.right.upper" or "arm.right.lower" or "arm.left.upper"
                                                          or "arm.left.lower" or
                                                          "leg.right.upper" or "leg.right.lower" or "leg.left.upper"
                                                          or "leg.left.lower" or
                                                          "armor.helmet" or "armor.chest" or "armor.boots" or "item" or
                                                          "hand.left" or "hand.right" or "foot.left" or "foot.right"
                                                          or "tail" or "wing.left" or "wing.right" ||
                                                      part?.StartsWith("organ:", StringComparison.Ordinal) == true &&
                                                      part.Length > 6 ||
                                                      part?.StartsWith("part:", StringComparison.Ordinal) == true &&
                                                      part.Length > 5;

    /// <summary>身体和独立器官共用同一种原版透明轮廓与有限深度，不另造更大的目标包络。</summary>
    /// <param name="image">真正显示的图片。</param><param name="selection">该图片的真实选择名。</param>
    /// <param name="height">图片单位的身体高度。</param>
    /// <param name="halfDepth">已提供的真实半厚度；空值按身体部位取得。</param>
    internal static BodySurfacePart[] NativePixels(Sprite image, string selection, float height, float? halfDepth = null)
        => Native.Pixels(image, new[] { new[] { selection } }, Vector2.zero, height, halfDepth: halfDepth);

    /// <summary>原版可见像素的有限身体，补出的深度保持原版投影轮廓不变。</summary>
    private sealed partial class Native : BodySurfaceBinding
    {
        /// <summary>进入或离开飞行、液体和悬浮状态时，旧像素固定点必须解除。</summary>
        private readonly bool flying, liquid, hovering;

        /// <summary>真实龙的原生动作切换也会改变身体，普通逐帧动画不作为形体改变。</summary>
        private readonly Dragon dragon;

        /// <summary>接入像素身体时的原生龙动作。</summary>
        private readonly DragonState dragonState;
        /// <summary>上次实际图片及其几何。</summary>
        private Sprite sprite;

        /// <summary>同一次像素采集对应的原动画帧。</summary>
        private Sprite nativeFrame;

        /// <summary>本次像素几何采用的真实资源版本。</summary>
        private readonly int resourceVersion = CreatureAppearanceService.ResourceVersion;

        /// <summary>资源换版会解除固定点，不能只更新图片而保留旧几何。</summary>
        internal override string ChangedReason => base.ChangedReason ?? HeldChangedReason ??
            (Actor.isFlying() != flying || Actor.isAffectedByLiquid() != liquid || Actor.isHovering() != hovering ||
             dragon != null && dragon.state != dragonState
                ? "BodyChanged" : null) ??
            (resourceVersion != CreatureAppearanceService.ResourceVersion
                ? "AppearanceResourceChanged"
                : null);

        /// <summary>实际独立头图。</summary>
        private Sprite head;

        /// <summary>头图在当前完整帧中的实际像素起点。</summary>
        private Vector2 headPosition;

        /// <summary>从实际可见像素生成的表面三角形。</summary>
        private BodySurfacePart[] parts;

        /// <summary>同一实际主图和头图的透明度，身体与遮挡只读一次资源。</summary>
        private byte[] bodyAlpha, headAlpha;

        /// <summary>同一帧完全不透明区域的前表面，只供实际背景器官遮挡。</summary>
        private BodySurfacePart[] opaqueParts;

        /// <summary>同一真实像素归属也用于头部等局部动作的遮挡表面。</summary>
        private string[][] regionNames;

        /// <summary>与真实身体图片同尺寸的区域索引。</summary>
        private int[] regionOwners;

        /// <summary>保留实际人物。</summary>
        /// <param name="actor">原版身体。</param>
        internal Native(Actor actor) : base(actor)
        {
            flying = actor.isFlying(); liquid = actor.isAffectedByLiquid(); hovering = actor.isHovering();
            dragon = actor.getActorComponent<Dragon>();
            if (dragon != null) dragonState = dragon.state;
            initialHeld = HeldAsset(actor);
            initiallyHeldVisible = initialHeld != null && actor.checkHasRenderedItem();
        }

        /// <summary>使用实际当前图片、翻转和原版尺寸，不以整个人物包络提前接触。</summary>
        /// <param name="foot">共同脚下。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
        protected override BodySurfacePose Sample(Vector3 foot, float height, double time)
        {
            Sprite native = Actor.calculateMainSprite();
            nativeFrame = native;
            // 与原版普通绘制一致；非着色身体可能使用不可读图集，不能送进着色像素袋。
            Sprite current = Actor.asset.need_colored_sprite ? Actor.calculateColoredSprite(native) : native;
            var excluded = ConstructedAbilityService.BodyOrganLayersAt(Actor.id, time);
            CreatureAppearanceFrame composed = CreatureAppearanceService.ComposeActorFrame(Actor, current, native, excluded);
            if (composed != null) current = composed.Sprite;

            bool hasHeld = HeldAsset(Actor) != null && Actor.checkHasRenderedItem();
            bool needsOpaque = hasHeld || BodyOrganBinding.VisibleLayers(Actor)
                .Any(layer => layer.Depth == CreatureLayerDepth.BehindBody);
            AnimationFrameData frame = Actor.getAnimationFrameData();
            Sprite currentHead = Actor.asset.need_colored_sprite && frame?.show_head == true
                ? Actor.cached_sprite_head
                : null;
            Vector2 currentHeadPosition = currentHead == null
                ? Vector2.zero
                : new Vector2(
                    (int)frame.pos_head_new.x - (int)currentHead.pivot.x +
                    Mathf.RoundToInt(current.pivot.x - native.pivot.x),
                    (int)frame.pos_head_new.y - (int)currentHead.pivot.y +
                    Mathf.RoundToInt(current.pivot.y - native.pivot.y));
            if (current != sprite || currentHead != head || currentHeadPosition != headPosition || parts == null)
            {
                sprite = current;
                head = currentHead;
                headPosition = currentHeadPosition;
                AssetManager.dynamic_sprites_library.checkDirty();
                bodyAlpha = composed?.Alpha ?? Array.ConvertAll(CreatureAppearanceService.ReadPixels(sprite), p => p.a);
                headAlpha = head == null
                    ? null
                    : Array.ConvertAll(CreatureAppearanceService.ReadPixels(head), p => p.a);
                opaqueParts = null;
                armShadows.Clear();
                string[][] names = new[] { new[] { "torso" } };
                if (composed != null)
                {
                    var owned = BodyOrganBinding.VisibleOrgans(Actor).ToLookup(v => v.Layer.id, v => "organ:" + v.Id,
                        StringComparer.Ordinal);
                    var headLayers = new HashSet<string>(BodyOrganBinding.VisibleLayers(Actor)
                        .Where(layer => layer.Channel is "horn" or "eye").Select(layer => layer.id), StringComparer.Ordinal);
                    names = composed.Layers.Select(group => group.Reverse()
                        .SelectMany(layer => layer == null ? new[] { "torso" } :
                            layer == "armor.helmet" ? new[] { layer, "head" } :
                            layer == "armor.chest" ? new[] { layer, "torso" } :
                            layer == "armor.boots" ? new[] { layer } :
                            layer.IndexOf('/') is int split && split >= 0
                                ? owned[layer.Substring(0, split)].SelectMany(id => new[] { id + layer.Substring(split), id })
                                : owned[layer])
                        .Concat(group.Any(layer => layer != null && headLayers.Contains(layer.Split('/')[0])) ? new[] { "head" } : Array.Empty<string>())
                        .Distinct(StringComparer.Ordinal).ToArray()).ToArray();
                }

                int[] owners = composed?.Owners;
                if (head != null)
                {
                    // 最终着色图已经画过头部，只改可见像素的归属，不再加第二份独立头图。
                    owners = owners == null ? new int[bodyAlpha.Length] : (int[])owners.Clone();
                    var classified = new List<string[]>(names);
                    var heads = new Dictionary<int, int>();
                    int headWidth = Mathf.RoundToInt(head.rect.width),
                        headHeight = Mathf.RoundToInt(head.rect.height),
                        width = Mathf.RoundToInt(sprite.rect.width),
                        rows = Mathf.RoundToInt(sprite.rect.height);
                    for (int y = 0; y < headHeight; y++)
                    for (int x = 0; x < headWidth; x++)
                    {
                        int px = x + (int)headPosition.x, py = y + (int)headPosition.y;
                        if (headAlpha[y * headWidth + x] == 0 || px < 0 || px >= width || py < 0 || py >= rows)
                            continue;
                        int pixel = py * width + px, group = owners[pixel];
                        if (Array.IndexOf(names[group], "torso") < 0) continue;
                        if (!heads.TryGetValue(group, out int selected))
                        {
                            selected = classified.Count;
                            heads.Add(group, selected);
                            classified.Add(Array.ConvertAll(names[group], name => name == "torso" ? "head" : name));
                        }

                        owners[pixel] = selected;
                    }

                    names = classified.ToArray();
                }

                ClassifyRegions(native, ref names, ref owners);
                regionNames = names; regionOwners = owners;
                parts = Pixels(sprite, names, Vector2.zero, height / Actor.actor_scale, bodyAlpha, owners);
            }

            if (needsOpaque && opaqueParts == null)
            {
                opaqueParts = OpaquePixels(sprite, Vector2.zero, height / Actor.actor_scale, bodyAlpha, regionNames, regionOwners);
            }

            Quaternion rotation = Quaternion.Euler(Actor.updateRotation());
            Matrix4x4 matrix = ImageMatrix(foot + new Vector3(Actor.move_jump_offset.x, 0f, Actor.move_jump_offset.y),
                rotation, Actor.current_scale, Actor.flip);
            var result = new BodySurfacePose(parts, Array.ConvertAll(parts, _ => matrix), foot, height,
                Actor.flip ? Mathf.PI : 0f);
            if (needsOpaque)
            {
                // 原版着色的半透明身体不能把后面的器官完全盖住。
                BodySurfacePart[] covered = Actor.color.a < 1f ? Array.Empty<BodySurfacePart>() : opaqueParts;
                result = result.WithOpaque(new BodySurfacePose(covered,
                    Array.ConvertAll(covered, _ => matrix), foot, height, result.Heading));
            }

            if (hasHeld) result = AppendHeld(result, rotation);
            nativeFrames[result.Parts] = (sprite, hasHeld ? heldSprite : null);
            return result;
        }

        /// <summary>原版图片和接触表面使用同一段躯干、头部及髋膝足变形。</summary>
        /// <param name="basis">原可见像素表面。</param><param name="stance">共同支撑。</param>
        /// <param name="head">共同头部。</param><param name="right">右臂。</param><param name="left">左臂。</param>
        /// <param name="organ">实际独立器官。</param>
        internal override BodySurfacePose Acting(BodySurfacePose basis, BodyPose? stance, BodyPose? head,
            BodyPose? right, BodyPose? left, BodyOrganBinding organ)
        {
            BodyOccupancy arms = (right.HasValue ? BodyOccupancy.RightArm : BodyOccupancy.None) |
                                 (left.HasValue ? BodyOccupancy.LeftArm : BodyOccupancy.None);
            BodyPose? body = head ?? stance;
            if (!body.HasValue) return OccludeHeld(basis.WithOpaque(ArmShadow(basis.Opaque, arms)));
            BodyPose active = body.Value;
            float scale = active.Height * Actor.actor_scale / SpatialActorPose.Height(Actor);
            Vector3 root = stance?.Foot ?? basis.Foot;
            var matrices = Array.ConvertAll(basis.Matrices, m => Matrix4x4.Translate(root - basis.Foot) * m);
            if (active.Organ is BodyOrganPose nativeOrgan && organ?.Model == null)
                for (int part = 0; part < basis.Parts.Length; part++)
                    if (basis.Parts[part].Attachment != "item")
                        matrices[basis.Parts[part].TransformIndex < 0 ? part : basis.Parts[part].TransformIndex] =
                            nativeOrgan.NativeBodyMatrix(root, sprite.pixelsPerUnit);
            var vertices = new Vector3[basis.Parts.Length][];
            for (int part = 0; part < vertices.Length; part++)
            {
                Matrix4x4 matrix =
                    basis.Matrices[basis.Parts[part].TransformIndex < 0 ? part : basis.Parts[part].TransformIndex];
                Matrix4x4 inverse = matrix.inverse;
                Vector3[] original = basis.Vertices(part);
                if (basis.Parts[part].Attachment == "item")
                {
                    vertices[part] = original;
                    continue;
                }

                vertices[part] = new Vector3[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(original[i]) - basis.Foot;
                    float height = point.y + point.z - sprite.bounds.min.y * scale;
                    Vector3 move = BodySpriteWarp.Displacement(point.x, height, sprite.bounds.center.x * scale,
                        sprite.bounds.size.x * scale, active.Height * .62f, stance?.Torso ?? Vector3.zero, body, Actor.flip);
                    vertices[part][i] = original[i] + inverse.MultiplyVector(move);
                }
            }

            var result =
                new BodySurfacePose(basis.Parts, matrices, root, basis.Height, basis.Heading).Deformed(vertices);
            if (basis.Opaque != null)
                result = result.WithOpaque(Acting(ArmShadow(basis.Opaque, arms), stance, head, right, left, organ));
            return OccludeHeld(result);
        }

        /// <summary>仅取同一真实图片的完全不透明像素和原行前表面，不使用包围盒遮住尾巴。</summary>
        /// <param name="image">实际显示图。</param><param name="offset">真实安装偏移。</param>
        /// <param name="height">图片单位的身体高度。</param><param name="alpha">同次资源读取的透明度。</param>
        /// <param name="names">真实身体部位，未分组的静态表面为空。</param><param name="owners">同一像素的部位归属。</param>
        private static BodySurfacePart[] OpaquePixels(Sprite image, Vector2 offset, float height, byte[] alpha,
            string[][] names = null, int[] owners = null)
        {
            byte[] opaque = Array.ConvertAll(alpha, a => a == 255 ? (byte)255 : (byte)0);
            BodySurfacePart[] regions = Pixels(image, names ?? new[] { new[] { "opaque" } }, offset, height, opaque, owners);
            for (int part = 0; part < regions.Length; part++)
            {
                Vector3[] vertices = regions[part].Vertices;
                var triangles = new List<int>(vertices.Length / 8 * 6);
                // 同一个Pixels构造器每行段依次给出前后八角；遮挡只需一次真实前表面投影。
                for (int start = 0; start < vertices.Length; start += 8)
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                regions[part] = new BodySurfacePart(regions[part].Id, vertices, triangles.ToArray()).WithSelections(regions[part].Selections);
            }

            return regions;
        }

        /// <summary>按同一透明轮廓分行，再按可见归属切段；切段不改变原整行的有限深度。</summary>
        /// <param name="image">真正显示的最终帧。</param><param name="names">各归属组的实际选择名。</param>
        /// <param name="offset">图片在身体中的位置。</param><param name="height">身体高度换算到图片单位。</param>
        /// <param name="alpha">合成时留下的透明度；普通帧只读取一次。</param>
        /// <param name="owners">合成时留下的归属；普通帧全部属于一个区域。</param>
        /// <param name="halfDepth">独立器官实际像素半厚度，普通身体保持各部位厚度。</param>
        internal static BodySurfacePart[] Pixels(Sprite image, string[][] names, Vector2 offset, float height,
            byte[] alpha = null, int[] owners = null, float? halfDepth = null)
        {
            int width = Mathf.RoundToInt(image.rect.width), rows = Mathf.RoundToInt(image.rect.height);
            alpha ??= CreatureAppearanceService.ReadPixels(image).Select(c => c.a).ToArray();
            var vertices = new List<Vector3>[names.Length];
            var triangles = new List<int>[names.Length];
            var coordinates = new List<Vector2>[names.Length];
            for (int group = 0; group < names.Length; group++)
            {
                vertices[group] = new List<Vector3>();
                triangles[group] = new List<int>();
                coordinates[group] = new List<Vector2>();
            }

            var depths = Array.ConvertAll(names, roles => halfDepth ?? height * (roles.Any(name => name.StartsWith("hand.", StringComparison.Ordinal) || name.StartsWith("foot.", StringComparison.Ordinal))
                ? .035f : roles.Any(name => name.StartsWith("arm.", StringComparison.Ordinal) || name.StartsWith("leg.", StringComparison.Ordinal)) ? .055f : roles.Contains("torso") ? .09f : .18f));
            float unit = 1f / image.pixelsPerUnit;
            float imageTop = (rows - image.pivot.y) * unit + offset.y;
            for (int row = 0; row < rows; row++)
            for (int x = 0; x < width;)
            {
                if (alpha[row * width + x] == 0)
                {
                    x++;
                    continue;
                }

                int first = x++;
                while (x < width && alpha[row * width + x] != 0) x++;
                int until = x;
                for (int column = first; column < until;)
                {
                    int startColumn = column, group = owners?[row * width + column] ?? 0;
                    // 关节使用自身肢段厚度，不能把躯干体积套到手腕、脚踝；投回原版的轮廓保持不变。
                    float half = depths[group];
                    while (++column < until && (owners?[row * width + column] ?? 0) == group)
                    {
                    }

                    float left = (startColumn - image.pivot.x) * unit + offset.x,
                        right = (column - image.pivot.x) * unit + offset.x,
                        bottom = (row - image.pivot.y) * unit + offset.y,
                        top = bottom + unit;
                    var points = vertices[group];
                    var faces = triangles[group];
                    int start = points.Count;
                    Corner(left, bottom, false);
                    Corner(right, bottom, false);
                    Corner(right, top, false);
                    Corner(left, top, false);
                    Corner(left, bottom, true);
                    Corner(right, bottom, true);
                    Corner(right, top, true);
                    Corner(left, top, true);
                    Face(0, 1, 2, 3);
                    Face(7, 6, 5, 4);
                    Face(0, 4, 5, 1);
                    // 仅补真实厚度台阶，保持相邻部位连通，不在同厚度归属边界增加内部碰撞面。
                    if (column == until) Face(1, 5, 6, 2);
                    else if (half > depths[owners?[row * width + column] ?? 0]) Boundary(right, depths[owners?[row * width + column] ?? 0]);
                    Face(2, 6, 7, 3);
                    if (startColumn == first) Face(3, 7, 4, 0);
                    else if (half > depths[owners?[row * width + startColumn - 1] ?? 0]) Boundary(left, depths[owners?[row * width + startColumn - 1] ?? 0]);

                    /// <summary>粗细部位交界只封外露台阶，两侧仍为同一可见体积。</summary>
                    /// <param name="px">实际交界。</param><param name="inner">邻侧半厚度。</param>
                    void Boundary(float px, float inner)
                    {
                        for (int side = 0; side < 2; side++)
                        {
                            int origin = points.Count;
                            for (int corner = 0; corner < 4; corner++)
                            {
                                float z = corner < 2 ? bottom : top, thickness = corner is 0 or 3 ? half : inner;
                                float depth = side == 0 ? Mathf.Min(thickness, Mathf.Max(0f, imageTop - z)) : -Mathf.Min(thickness, Mathf.Max(0f, z));
                                points.Add(new Vector3(px, -depth, z + depth));
                                coordinates[group].Add(new Vector2(px + (px == left ? unit : -unit) * .001f, z + (z == bottom ? unit : -unit) * .001f));
                            }
                            faces.AddRange(new[] { origin, origin + 1, origin + 2, origin, origin + 2, origin + 3 });
                        }
                    }

                    /// <summary>沿原版投影不变的方向补有限深度，归属切段不把身体削薄。</summary>
                    /// <param name="px">原图横向位置。</param><param name="z">原图竖向位置。</param>
                    /// <param name="front">是否为前面。</param>
                    void Corner(float px, float z, bool front)
                    {
                        float depth = front
                            ? Mathf.Min(half, Mathf.Max(0f, imageTop - z))
                            : -Mathf.Min(half, Mathf.Max(0f, z));
                        points.Add(new Vector3(px, -depth, z + depth));
                        // 几何仍落在真实边缘，采样略向本行段内部收，侧面不读到相邻透明像素。
                        float inset = unit * .001f;
                        coordinates[group].Add(new Vector2(px == left ? px + inset : px - inset,
                            z == bottom ? z + inset : z - inset));
                    }

                    /// <summary>按原轮廓连接同一物理面的两个三角形。</summary>
                    /// <param name="a">第一角。</param><param name="b">第二角。</param>
                    /// <param name="c">第三角。</param><param name="d">第四角。</param>
                    void Face(int a, int b, int c, int d)
                    {
                        faces.Add(start + a);
                        faces.Add(start + b);
                        faces.Add(start + c);
                        faces.Add(start + a);
                        faces.Add(start + c);
                        faces.Add(start + d);
                    }
                }
            }

            return Enumerable.Range(0, names.Length).Where(i => vertices[i].Count != 0)
                .Select(i => new BodySurfacePart(names[i][0], vertices[i].ToArray(), triangles[i].ToArray(),
                        names[i].FirstOrDefault(name => name.StartsWith("armor.", StringComparison.Ordinal)),
                        imageCoordinates: coordinates[i].ToArray())
                    .WithSelections(names[i].Skip(1).ToArray())).ToArray();
        }
    }
}

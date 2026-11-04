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
internal abstract class BodySurfaceBinding
{
    /// <summary>安装三维身体时提供实际模型，关闭三维视角不撤销此接入。</summary>
    internal static Func<Actor, BodySurfaceBinding> Provider;

    /// <summary>实际人物及其稳定身份。</summary>
    internal readonly Actor Actor;

    /// <summary>同一个人物，不沿用对象池的后来身份。</summary>
    internal readonly long Id;

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
            composition = new BodySurfaceComposition(poses, arms, held);
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
    internal static BodySurfacePart[] NativePixels(Sprite image, string selection, float height)
        => Native.Pixels(image, new[] { new[] { selection } }, Vector2.zero, height);

    /// <summary>原版可见像素的有限身体，补出的深度保持原版投影轮廓不变。</summary>
    private sealed class Native : BodySurfaceBinding
    {
        /// <summary>上次实际图片及其几何。</summary>
        private Sprite sprite;

        /// <summary>本次像素几何采用的真实资源版本。</summary>
        private readonly int resourceVersion = CreatureAppearanceService.ResourceVersion;

        /// <summary>资源换版会解除固定点，不能只更新图片而保留旧几何。</summary>
        internal override string ChangedReason => base.ChangedReason ??
                                                  (resourceVersion != CreatureAppearanceService.ResourceVersion
                                                      ? "AppearanceResourceChanged"
                                                      : null);

        /// <summary>实际独立头图。</summary>
        private Sprite head;

        /// <summary>头图在当前身体帧中的实际安装点。</summary>
        private Vector2 headPosition;

        /// <summary>从实际可见像素生成的表面三角形。</summary>
        private BodySurfacePart[] parts;

        /// <summary>同一实际主图和头图的透明度，身体与遮挡只读一次资源。</summary>
        private byte[] bodyAlpha, headAlpha;

        /// <summary>同一帧完全不透明区域的前表面，只供实际背景器官遮挡。</summary>
        private BodySurfacePart[] opaqueParts;

        /// <summary>保留实际人物。</summary>
        /// <param name="actor">原版身体。</param>
        internal Native(Actor actor) : base(actor)
        {
        }

        /// <summary>使用实际当前图片、翻转和原版尺寸，不以整个人物包络提前接触。</summary>
        /// <param name="foot">共同脚下。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
        protected override BodySurfacePose Sample(Vector3 foot, float height, double time)
        {
            Sprite native = Actor.calculateMainSprite();
            Sprite colored = Actor.calculateColoredSprite(native);
            Sprite current = Actor.asset.need_colored_sprite ? colored : native;
            CreatureAppearanceFrame composed = null;
            if (Actor.TryGetExtend(out var owner) && owner.E.TryGetComponent(out CreaturePhenotype phenotype) &&
                phenotype.IsValid &&
                CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature,
                    out var compiled))
            {
                string excluded = ConstructedAbilityService.BodyOrganAt(Actor.id, time)?.Layer.id;
                composed = CreatureAppearanceService.ComposeFrame(compiled, current, native, excluded);
                if (composed != null) current = composed.Sprite;
            }

            bool needsOpaque = BodyOrganBinding.VisibleLayers(Actor)
                .Any(layer => layer.Depth == CreatureLayerDepth.BehindBody);
            AnimationFrameData frame = Actor.getAnimationFrameData();
            Sprite currentHead = frame?.show_head == true ? Actor.cached_sprite_head : null;
            Vector2 currentHeadPosition =
                currentHead == null ? Vector2.zero : frame.pos_head_new + current.pivot - native.pivot;
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
                string[][] names = new[] { new[] { "torso" } };
                if (composed != null)
                {
                    var owned = BodyOrganBinding.VisibleOrgans(Actor).ToLookup(v => v.Layer.id, v => "organ:" + v.Id,
                        StringComparer.Ordinal);
                    names = composed.Layers.Select(group => group.Reverse()
                        .SelectMany(layer => layer == null ? new[] { "torso" } : owned[layer])
                        .Distinct(StringComparer.Ordinal).ToArray()).ToArray();
                }

                var visible = new List<BodySurfacePart>(Pixels(sprite, names, Vector2.zero,
                    height / Actor.actor_scale, bodyAlpha, composed?.Owners));
                if (head != null)
                    visible.AddRange(Pixels(head, new[] { new[] { "head" } },
                        (headPosition - sprite.pivot) / sprite.pixelsPerUnit, height / Actor.actor_scale, headAlpha));
                parts = visible.ToArray();
            }

            if (needsOpaque && opaqueParts == null)
            {
                var opaque = new List<BodySurfacePart>(OpaquePixels(sprite, Vector2.zero,
                    height / Actor.actor_scale, bodyAlpha));
                if (head != null)
                    opaque.AddRange(OpaquePixels(head, (headPosition - sprite.pivot) / sprite.pixelsPerUnit,
                        height / Actor.actor_scale, headAlpha));
                opaqueParts = opaque.ToArray();
            }

            float scale = Actor.actor_scale;
            Matrix4x4 matrix = Matrix4x4.TRS(foot, Quaternion.identity,
                new Vector3(Actor.flip ? -scale : scale, scale, scale));
            var result = new BodySurfacePose(parts, Array.ConvertAll(parts, _ => matrix), foot, height,
                Actor.flip ? Mathf.PI : 0f);
            if (!needsOpaque) return result;
            // 原版着色的半透明身体不能把后面的器官完全盖住。
            BodySurfacePart[] covered = Actor.color.a < 1f ? Array.Empty<BodySurfacePart>() : opaqueParts;
            return result.WithOpaque(new BodySurfacePose(covered,
                Array.ConvertAll(covered, _ => matrix), foot, height, result.Heading));
        }

        /// <summary>原版图片和接触表面使用同一段躯干、头部及髋膝足变形。</summary>
        /// <param name="basis">原可见像素表面。</param><param name="stance">共同支撑。</param>
        /// <param name="head">共同头部。</param><param name="right">右臂。</param><param name="left">左臂。</param>
        /// <param name="organ">实际独立器官。</param>
        internal override BodySurfacePose Acting(BodySurfacePose basis, BodyPose? stance, BodyPose? head,
            BodyPose? right, BodyPose? left, BodyOrganBinding organ)
        {
            BodyPose? body = head ?? stance;
            if (!body.HasValue) return basis;
            BodyPose active = body.Value;
            float scale = active.Height * Actor.actor_scale / SpatialActorPose.Height(Actor);
            Vector3 root = stance?.Foot ?? basis.Foot;
            var matrices = active.Organ is BodyOrganPose nativeOrgan && organ?.Model == null
                ? Array.ConvertAll(basis.Matrices, _ => nativeOrgan.NativeBodyMatrix(root, sprite.pixelsPerUnit))
                : Array.ConvertAll(basis.Matrices, m => Matrix4x4.Translate(root - basis.Foot) * m);
            var vertices = new Vector3[basis.Parts.Length][];
            for (int part = 0; part < vertices.Length; part++)
            {
                Matrix4x4 matrix =
                    basis.Matrices[basis.Parts[part].TransformIndex < 0 ? part : basis.Parts[part].TransformIndex];
                Matrix4x4 inverse = matrix.inverse;
                Vector3[] original = basis.Vertices(part);
                vertices[part] = new Vector3[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(original[i]) - basis.Foot;
                    float height = point.y + point.z - sprite.bounds.min.y * scale;
                    Vector3 move = BodySpriteWarp.Displacement(point.x, height, sprite.bounds.center.x * scale,
                        sprite.bounds.size.x * scale, active.Height * .62f, stance?.Torso ?? Vector3.zero, body);
                    vertices[part][i] = original[i] + inverse.MultiplyVector(move);
                }
            }

            var result =
                new BodySurfacePose(basis.Parts, matrices, root, basis.Height, basis.Heading).Deformed(vertices);
            return basis.Opaque == null
                ? result
                : result.WithOpaque(Acting(basis.Opaque, stance, head, right, left, organ));
        }

        /// <summary>仅取同一真实图片的完全不透明像素和原行前表面，不使用包围盒遮住尾巴。</summary>
        /// <param name="image">实际显示图。</param><param name="offset">真实安装偏移。</param>
        /// <param name="height">图片单位的身体高度。</param><param name="alpha">同次资源读取的透明度。</param>
        private static BodySurfacePart[] OpaquePixels(Sprite image, Vector2 offset, float height, byte[] alpha)
        {
            byte[] opaque = Array.ConvertAll(alpha, a => a == 255 ? (byte)255 : (byte)0);
            BodySurfacePart[] regions = Pixels(image, new[] { new[] { "opaque" } }, offset, height, opaque);
            for (int part = 0; part < regions.Length; part++)
            {
                Vector3[] vertices = regions[part].Vertices;
                var triangles = new List<int>(vertices.Length / 8 * 6);
                // 同一个Pixels构造器每行段依次给出前后八角；遮挡只需一次真实前表面投影。
                for (int start = 0; start < vertices.Length; start += 8)
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                regions[part] = new BodySurfacePart("opaque", vertices, triangles.ToArray());
            }

            return regions;
        }

        /// <summary>按同一透明轮廓分行，再按可见归属切段；切段不改变原整行的有限深度。</summary>
        /// <param name="image">真正显示的最终帧。</param><param name="names">各归属组的实际选择名。</param>
        /// <param name="offset">图片在身体中的位置。</param><param name="height">身体高度换算到图片单位。</param>
        /// <param name="alpha">合成时留下的透明度；普通帧只读取一次。</param>
        /// <param name="owners">合成时留下的归属；普通帧全部属于一个区域。</param>
        internal static BodySurfacePart[] Pixels(Sprite image, string[][] names, Vector2 offset, float height,
            byte[] alpha = null, int[] owners = null)
        {
            int width = Mathf.RoundToInt(image.rect.width), rows = Mathf.RoundToInt(image.rect.height);
            alpha ??= CreatureAppearanceService.ReadPixels(image).Select(c => c.a).ToArray();
            var vertices = new List<Vector3>[names.Length];
            var triangles = new List<int>[names.Length];
            for (int group = 0; group < names.Length; group++)
            {
                vertices[group] = new List<Vector3>();
                triangles[group] = new List<int>();
            }

            float unit = 1f / image.pixelsPerUnit;
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
                float half = (until - first) * unit * .5f;
                for (int column = first; column < until;)
                {
                    int startColumn = column, group = owners?[row * width + column] ?? 0;
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
                    // 同一连续行内只切分选择名，不在归属边界补出不可见的内墙。
                    if (column == until) Face(1, 5, 6, 2);
                    Face(2, 6, 7, 3);
                    if (startColumn == first) Face(3, 7, 4, 0);

                    /// <summary>沿原版投影不变的方向补有限深度，归属切段不把身体削薄。</summary>
                    /// <param name="px">原图横向位置。</param><param name="z">原图竖向位置。</param>
                    /// <param name="front">是否为前面。</param>
                    void Corner(float px, float z, bool front)
                    {
                        float depth = front
                            ? Mathf.Min(half, Mathf.Max(0f, height - z))
                            : -Mathf.Min(half, Mathf.Max(0f, z));
                        points.Add(new Vector3(px, -depth, z + depth));
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
                .Select(i => new BodySurfacePart(names[i][0], vertices[i].ToArray(), triangles[i].ToArray())
                    .WithSelections(names[i].Skip(1).ToArray())).ToArray();
        }
    }
}
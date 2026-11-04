using System;
using System.Collections.Generic;
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

    /// <summary>取得原人物。</summary>
    /// <param name="actor">实际身体。</param>
    protected BodySurfaceBinding(Actor actor)
    {
        Actor = actor;
        Id = actor.id;
        bodyAsset = actor.asset;
        appearance = actor.animation_container;
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
        if (From == null || !ReferenceEquals(From.Parts, To.Parts))
        {
            From = To;
            FromTime = time;
        }
    }

    /// <summary>取得共同时间中的同一表面。</summary>
    /// <param name="time">共同时间。</param>
    internal BodySurfacePose At(double time) => ToTime <= FromTime
        ? To
        : BodySurfacePose.Lerp(From, To, Mathf.Clamp01((float)((time - FromTime) / (ToTime - FromTime))));

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
                                                      part?.StartsWith("part:", StringComparison.Ordinal) == true &&
                                                      part.Length > 5;

    /// <summary>原版可见像素的有限身体，补出的深度保持原版投影轮廓不变。</summary>
    private sealed class Native : BodySurfaceBinding
    {
        /// <summary>上次实际图片及其几何。</summary>
        private Sprite sprite;

        /// <summary>实际独立头图。</summary>
        private Sprite head;

        /// <summary>头图在当前身体帧中的实际安装点。</summary>
        private Vector2 headPosition;

        /// <summary>从实际可见像素生成的表面三角形。</summary>
        private BodySurfacePart[] parts;

        /// <summary>保留实际人物。</summary>
        /// <param name="actor">原版身体。</param>
        internal Native(Actor actor) : base(actor)
        {
        }

        /// <summary>使用实际当前图片、翻转和原版尺寸，不以整个人物包络提前接触。</summary>
        /// <param name="foot">共同脚下。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
        protected override BodySurfacePose Sample(Vector3 foot, float height, double time)
        {
            Sprite current = Actor.calculateMainSprite();
            Actor.calculateColoredSprite(current);
            AnimationFrameData frame = Actor.getAnimationFrameData();
            Sprite currentHead = frame?.show_head == true ? Actor.cached_sprite_head : null;
            Vector2 currentHeadPosition = currentHead == null ? Vector2.zero : frame.pos_head_new;
            if (current != sprite || currentHead != head || currentHeadPosition != headPosition || parts == null)
            {
                sprite = current;
                head = currentHead;
                headPosition = currentHeadPosition;
                BodySurfacePart body = Pixels(sprite, "torso", Vector2.zero, height / Actor.actor_scale);
                parts = head == null
                    ? new[] { body }
                    : new[]
                    {
                        body,
                        Pixels(head, "head", (headPosition - sprite.pivot) / sprite.pixelsPerUnit,
                            height / Actor.actor_scale)
                    };
            }

            float scale = Actor.actor_scale;
            Matrix4x4 matrix = Matrix4x4.TRS(foot, Quaternion.identity,
                new Vector3(Actor.flip ? -scale : scale, scale, scale));
            return new BodySurfacePose(parts, Array.ConvertAll(parts, _ => matrix), foot, height,
                Actor.flip ? Mathf.PI : 0f);
        }

        /// <summary>逐行读取真正可见的像素，深度取该行可见宽度的一半，并限制在实际身体高度内。</summary>
        /// <param name="image">实际图片。</param><param name="id">实际部位。</param>
        /// <param name="offset">图片在身体中的位置。</param><param name="height">身体高度换算到图片单位。</param>
        private static BodySurfacePart Pixels(Sprite image, string id, Vector2 offset, float height)
        {
            Rect rect = image.rect;
            int width = (int)rect.width, rows = (int)rect.height;
            Color[] pixels = image.texture.GetPixels((int)rect.x, (int)rect.y, width, rows);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float unit = 1f / image.pixelsPerUnit;
            for (int row = 0; row < rows; row++)
            for (int x = 0; x < width;)
            {
                if (pixels[row * width + x].a == 0f)
                {
                    x++;
                    continue;
                }

                int first = x++;
                while (x < width && pixels[row * width + x].a != 0f) x++;
                float left = (first - image.pivot.x) * unit + offset.x,
                    right = (x - image.pivot.x) * unit + offset.x,
                    bottom = (row - image.pivot.y) * unit + offset.y,
                    top = bottom + unit,
                    half = (right - left) * .5f;
                int start = vertices.Count;
                // 原版投影为(x,y+z)。补出的前后深度沿投影不变的方向，不能让接触面跑出可见像素。
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
                Face(1, 5, 6, 2);
                Face(2, 6, 7, 3);
                Face(3, 7, 4, 0);

                /// <summary>沿投影不变的方向补出有限前后顶点。</summary>
                /// <param name="px">原图横向位置。</param><param name="z">原图竖向位置。</param><param name="front">是否为前面。</param>
                void Corner(float px, float z, bool front)
                {
                    float depth = front
                        ? Mathf.Min(half, Mathf.Max(0f, height - z))
                        : -Mathf.Min(half, Mathf.Max(0f, z));
                    vertices.Add(new Vector3(px, -depth, z + depth));
                }

                /// <summary>把一个有限侧面接成两个真实三角形。</summary>
                /// <param name="a">第一角。</param><param name="b">第二角。</param><param name="c">第三角。</param><param name="d">第四角。</param>
                void Face(int a, int b, int c, int d)
                {
                    triangles.Add(start + a);
                    triangles.Add(start + b);
                    triangles.Add(start + c);
                    triangles.Add(start + a);
                    triangles.Add(start + c);
                    triangles.Add(start + d);
                }
            }

            return new BodySurfacePart(id, vertices.ToArray(), triangles.ToArray());
        }
    }
}
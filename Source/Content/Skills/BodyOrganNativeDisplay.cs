using System.Collections.Generic;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

/// <summary>原版图片池完成后绘制已接管的真实器官，复用图片和绘制器。</summary>
internal static class BodyOrganNativeDisplay
{
    /// <summary>随可见数量增长的器官图片池，不在每帧创建资源。</summary>
    private static readonly List<SpriteRenderer> pictures = new();

    /// <summary>本帧实际使用数量。</summary>
    private static int count;

    /// <summary>开始一轮实际可见人物绘制。</summary>
    internal static void BeginFrame() => count = 0;

    /// <summary>从同轮数值姿态绘制真实器官，图片枢轴与制作根点保持对应。</summary>
    /// <param name="source">原版身体绘制器。</param><param name="actor">实际身体。</param>
    /// <param name="binding">本次真实器官。</param><param name="pose">共同完成姿态。</param>
    /// <param name="foot">原版离地脚下。</param>
    internal static void Publish(SpriteRenderer source, Actor actor, BodyOrganBinding binding, in BodyPose pose,
        Vector3 foot)
    {
        if (binding.Model != null) return;
        if (count == pictures.Count)
        {
            var obj = new GameObject("Cultiway.BodyOrgan");
            var created = obj.AddComponent<SpriteRenderer>();
            created.enabled = false;
            pictures.Add(created);
            FormNativeScenePass.Register(created);
        }

        SpriteRenderer renderer = pictures[count++];
        BodyOrganPose organ = pose.Organ.Value;
        Vector3 point = organ.Point(binding.Sprite.pivot - binding.Mount);
        point.z -= pose.Foot.z - foot.z;
        Vector2 offset = actor.move_jump_offset + actor.shake_offset;
        renderer.transform.SetPositionAndRotation(
            new Vector3(point.x + offset.x, point.y + point.z + offset.y, point.z),
            Quaternion.Euler(0f, 0f, organ.Angle));
        renderer.transform.localScale = new Vector3(organ.PixelScale.x * binding.Sprite.pixelsPerUnit,
            organ.PixelScale.y * binding.Sprite.pixelsPerUnit, 1f);
        renderer.gameObject.layer = source.gameObject.layer;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + (binding.Layer.Depth == CreatureLayerDepth.BehindBody ? -1 : 1);
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.color = source.color;
        renderer.sprite = binding.Sprite;
        renderer.enabled = source.enabled;
    }

    /// <summary>关闭本轮未使用的池对象，动作结束后不遗留第二个器官。</summary>
    internal static void EndFrame()
    {
        for (int i = count; i < pictures.Count; i++) pictures[i].enabled = false;
    }

    /// <summary>切换世界时释放本服务建立的对象，原器官图片仍由资源加载器拥有。</summary>
    internal static void ClearWorldState()
    {
        foreach (SpriteRenderer renderer in pictures)
            if (renderer != null)
                Object.Destroy(renderer.gameObject);
        pictures.Clear();
        count = 0;
    }
}
using System.Collections.Generic;
using Cultiway.Const;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>将原版物件和共用形体放进同一次深度绘制，主界面仍由原相机处理。</summary>
public sealed class FormNativeScenePass : MonoBehaviour
{
    /// <summary>实际原版精灵池中的对象，不逐帧搜索整个场景。</summary>
    private static readonly HashSet<Renderer> registered = new();
    /// <summary>已经接入的相机，世界切换时一起清除命令。</summary>
    private static readonly HashSet<FormNativeScenePass> passes = new();
    /// <summary>原版物件层。</summary>
    private static readonly int ObjectsLayer = SortingLayer.NameToID(RenderSortingLayerNames.Objects_4);
    /// <summary>原版上层效果。</summary>
    private static readonly int OverlayLayer = SortingLayer.NameToID(RenderSortingLayerNames.EffectsTop_5);
    /// <summary>保留原版覆盖层之间的顺序。</summary>
    private static readonly int OverlayValue = SortingLayer.GetLayerValueFromID(OverlayLayer);
    /// <summary>显示系统已经发布的当前完整状态。</summary>
    private FormNativeRenderSystem source;
    /// <summary>真正执行本次绘制的相机。</summary>
    private Camera camera;
    /// <summary>复用的绘制命令。</summary>
    private CommandBuffer commands;
    /// <summary>本相机暂时隐藏的对象，绘制后立即恢复。</summary>
    private readonly List<Renderer> hidden = new();
    /// <summary>本帧原版物件的固定绘制资料。</summary>
    private readonly List<Entry> objects = new();
    /// <summary>共同排序的半透明精灵与形体。</summary>
    private readonly List<Entry> transparent = new();
    /// <summary>原版界面性上层效果，最后绘制。</summary>
    private readonly List<Entry> overlays = new();
    /// <summary>复用相机裁剪平面。</summary>
    private readonly Plane[] planes = new Plane[6];
    /// <summary>保留专用原版着色器的批量画面。</summary>
    private readonly FormNativeSpriteAtlas atlas = new();
    /// <summary>保持本次表面版本存活。</summary>
    private FormResource resource;
    /// <summary>是否已补登记接入前创建的原版精灵。</summary>
    private static bool seeded;

    /// <summary>记录精灵池新建立的真实显示对象。</summary>
    /// <param name="renderer">原版精灵绘制器。</param>
    public static void Register(Renderer renderer)
    {
        if (renderer == null) return;
        registered.Add(renderer);
        if (renderer.gameObject.scene.IsValid() && renderer.GetComponent<FormNativeRegistration>() == null)
            renderer.gameObject.AddComponent<FormNativeRegistration>();
    }

    /// <summary>在指定相机上复用共同绘制入口，支持检查相机和实际主相机。</summary>
    /// <param name="target">真正绘制的相机。</param>
    /// <param name="owner">共用形体显示系统。</param>
    public static FormNativeScenePass Attach(Camera target, FormNativeRenderSystem owner)
    {
        FormNativeScenePass pass = target.GetComponent<FormNativeScenePass>();
        if (pass == null)
        {
            pass = target.gameObject.AddComponent<FormNativeScenePass>();
            pass.camera = target;
            pass.commands = new CommandBuffer { name = "Cultiway.NativeScene" };
            target.AddCommandBuffer(CameraEvent.AfterForwardAlpha, pass.commands);
            passes.Add(pass);
        }
        pass.source = owner;
        return pass;
    }

    /// <summary>在相机剔除前隐藏原版自动副本，并建立本帧完整绘制命令。</summary>
    private void OnPreCull()
    {
        Restore(); commands.Clear(); objects.Clear(); transparent.Clear(); overlays.Clear(); resource = source.FrameResource; atlas.Begin();
        if (resource == null) return;
        bool hasVisibleForm = false;
        for (int i = 0; i < source.ViewCount; i++) hasVisibleForm |= Visible(source.ViewAt(i));
        if (!hasVisibleForm) { resource = null; return; }
        if (!seeded)
        {
            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>()) Register(renderer);
            seeded = true;
        }
        registered.RemoveWhere(renderer => renderer == null);
        GeometryUtility.CalculateFrustumPlanes(camera, planes);
        float minimum = source.MinimumDepth, maximum = source.MaximumDepth;
        foreach (Renderer renderer in registered)
        {
            if (!renderer.gameObject.scene.IsValid() || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff || renderer.name == "Cultiway.FormPart" ||
                (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 || !Visible(renderer.bounds)) continue;
            int layer = renderer.sortingLayerID;
            Material original = renderer.sharedMaterial;
            if (original == null) continue;
            if (SortingLayer.GetLayerValueFromID(layer) >= OverlayValue || original.renderQueue >= (int)RenderQueue.Overlay)
            {
                renderer.forceRenderingOff = true; hidden.Add(renderer);
                overlays.Add(new Entry
                {
                    Renderer = renderer,
                    Material = original,
                    Depth = renderer.bounds.center.y - renderer.bounds.center.z,
                    Order = overlays.Count,
                    SortingOrder = renderer.sortingOrder,
                    Layer = SortingLayer.GetLayerValueFromID(layer),
                    Queue = original.renderQueue
                });
                continue;
            }
            if (layer != ObjectsLayer || renderer is not SpriteRenderer sprite || sprite.sprite == null) continue;
            bool stamp = original.shader.name.StartsWith("WorldBox/Building", System.StringComparison.Ordinal);
            if (!stamp && original.shader.name != "Sprites/Default") continue;
            renderer.forceRenderingOff = true; hidden.Add(renderer);
            Vector3 position = renderer.transform.position;
            var entry = new Entry
            {
                Renderer = renderer,
                Material = stamp ? resource.Generation.NativeStamp() : resource.Generation.NativeSprite(original),
                Stamp = stamp ? atlas.Add(sprite, camera) : (int?)null,
                Depth = 2 * (position.y - position.z) - renderer.bounds.center.y,
                Order = objects.Count,
                SortingOrder = renderer.sortingOrder
            };
            float ground = position.y - position.z;
            minimum = Mathf.Min(minimum, 2 * ground - renderer.bounds.max.y);
            maximum = Mathf.Max(maximum, 2 * ground - renderer.bounds.min.y);
            objects.Add(entry); transparent.Add(entry);
        }

        source.MapDepth(camera, minimum, maximum);
        objects.Sort(CompareOverlay);
        for (int i = 0; i < source.ViewCount; i++)
        {
            FormNativeRenderSystem.PartView part = source.ViewAt(i);
            if (part.Transparent && Visible(part)) transparent.Add(new Entry
            {
                Renderer = part.Renderer,
                Material = part.Renderer.sharedMaterial,
                Depth = part.Depth,
                Order = objects.Count + i
            });
        }
        atlas.Prepare(camera, transparent);
        atlas.Capture(commands, camera);
        commands.ClearRenderTarget(true, false, Color.clear);
        commands.SetGlobalFloat("_CultiwayNativeDepthScale", source.DepthScale);
        commands.SetGlobalFloat("_CultiwayNativeDepthBias", source.DepthBias);
        // 精灵只在不透明像素写深度；镂空、淡出和半透明边缘不会形成硬壳。
        foreach (Entry entry in objects) if (!entry.Stamp.HasValue) commands.DrawRenderer(entry.Renderer, entry.Material, 0, 0);
        atlas.Draw(commands, resource.Generation.NativeStamp(), 0, 0);
        for (int i = 0; i < source.ViewCount; i++)
        {
            FormNativeRenderSystem.PartView part = source.ViewAt(i);
            if (!part.Transparent && Visible(part)) commands.DrawRenderer(part.Renderer, part.Renderer.sharedMaterial, 0, 0);
        }
        foreach (Entry entry in objects) if (!entry.Stamp.HasValue) commands.DrawRenderer(entry.Renderer, entry.Material, 0, 1);
        atlas.Draw(commands, resource.Generation.NativeStamp(), 0, 1);
        FormTransparency.Begin(commands, camera.pixelWidth, camera.pixelHeight, BuiltinRenderTextureType.CameraTarget);
        for (int i = 0; i < transparent.Count;)
        {
            Entry entry = transparent[i];
            if (entry.Stamp.HasValue)
            {
                FormNativeSpriteAtlas.Run run = atlas.Runs[i];
                atlas.Draw(commands, entry.Material, run.Submesh, 3); i = run.End;
            }
            else { commands.DrawRenderer(entry.Renderer, entry.Material, 0, 3); i++; }
        }
        FormTransparency.Resolve(commands, resource.Generation.Transparency(), BuiltinRenderTextureType.CameraTarget,
            BuiltinRenderTextureType.CameraTarget, camera.pixelRect);
        overlays.Sort(CompareOverlay);
        if (overlays.Count > 0) commands.ClearRenderTarget(true, false, Color.clear);
        foreach (Entry entry in overlays) commands.DrawRenderer(entry.Renderer, entry.Material, 0, 0);
    }

    /// <summary>显式绘制同样遵守相机图层，三维端隐藏原版时不能继续提交二维副本。</summary>
    /// <param name="part">本帧已准备的形体部件。</param>
    private bool Visible(FormNativeRenderSystem.PartView part) => part.Renderer.enabled &&
        part.Renderer.gameObject.activeInHierarchy && (camera.cullingMask & (1 << part.Renderer.gameObject.layer)) != 0;

    /// <summary>只按画面四边检查，前后范围随后统一映射，不沿用精灵旧屏幕深度。</summary>
    /// <param name="bounds">精灵的原版边界。</param>
    private bool Visible(Bounds bounds)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 normal = planes[i].normal;
            float extent = Vector3.Dot(new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)), bounds.extents);
            if (planes[i].GetDistanceToPoint(bounds.center) + extent < 0) return false;
        }
        return true;
    }

    /// <summary>按实际前后位置排序半透明对象，同深度保留本帧顺序。</summary>
    /// <param name="left">一个显示对象。</param>
    /// <param name="right">另一个显示对象。</param>
    private static int Compare(Entry left, Entry right)
    {
        int depth = right.Depth.CompareTo(left.Depth);
        if (depth != 0) return depth;
        int explicitOrder = left.SortingOrder.CompareTo(right.SortingOrder);
        return explicitOrder != 0 ? explicitOrder : left.Order.CompareTo(right.Order);
    }

    /// <summary>上层提示保留原版明确的绘制次序。</summary>
    /// <param name="left">一个上层效果。</param>
    /// <param name="right">另一个上层效果。</param>
    private static int CompareOverlay(Entry left, Entry right)
    {
        int queue = left.Queue.CompareTo(right.Queue);
        if (queue != 0) return queue;
        int layer = left.Layer.CompareTo(right.Layer);
        if (layer != 0) return layer;
        int order = left.SortingOrder.CompareTo(right.SortingOrder);
        return order != 0 ? order : Compare(left, right);
    }

    /// <summary>本相机提交结束后还原精灵，不影响下一台相机或三维收集。</summary>
    private void OnPostRender() => Restore();

    /// <summary>只还原本次主动接管的原版绘制状态。</summary>
    private void Restore()
    {
        foreach (Renderer renderer in hidden) if (renderer != null) renderer.forceRenderingOff = false;
        hidden.Clear();
    }

    /// <summary>换世界时释放旧命令和所有本帧引用。</summary>
    /// <param name="owner">正在清理的显示系统。</param>
    internal static void Clear(FormNativeRenderSystem owner)
    {
        foreach (FormNativeScenePass pass in passes)
            if (pass.source == owner) pass.ClearFrame();
        registered.RemoveWhere(renderer => renderer == null);
    }

    /// <summary>清空当前相机的临时资料。</summary>
    private void ClearFrame()
    {
        Restore(); commands.Clear(); objects.Clear(); transparent.Clear(); overlays.Clear(); resource = null; atlas.Clear();
    }

    /// <summary>停用相机组件时不遗留隐藏状态。</summary>
    private void OnDisable() { if (commands != null) ClearFrame(); }

    /// <summary>实际相机销毁时卸下命令入口。</summary>
    private void OnDestroy()
    {
        if (commands == null) return;
        ClearFrame(); camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, commands); commands.Release(); passes.Remove(this);
    }

    /// <summary>一次已经冻结材质、前后位置和通道的实际绘制。</summary>
    internal struct Entry
    {
        /// <summary>原版或形体绘制器。</summary>
        internal Renderer Renderer;
        /// <summary>所属资源版本的共享表面。</summary>
        internal Material Material;
        /// <summary>专用原画面在本帧图集中的位置。</summary>
        internal int? Stamp;
        /// <summary>共同前后位置。</summary>
        internal float Depth;
        /// <summary>本帧稳定次序。</summary>
        internal int Order;
        /// <summary>上层效果的原版明确次序。</summary>
        internal int SortingOrder;
        /// <summary>原版覆盖层优先级。</summary>
        internal int Layer;
        /// <summary>覆盖层原有绘制队列，完整视图过渡仍在世界内容之上。</summary>
        internal int Queue;
    }
}

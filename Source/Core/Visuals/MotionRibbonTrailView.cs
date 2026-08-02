using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>把运动点和来源锚点渲染为实体芯与柔光边两层程序化运动视觉。</summary>
internal sealed class MotionRibbonTrailView : MonoBehaviour
{
    private const int TextureWidth = 32;
    private const int TextureHeight = 16;
    private static Texture2D coreTexture;
    private static Texture2D glowTexture;

    private List<Vector3> points;
    private List<Vector3> origins;
    private List<float> times;
    private RibbonPathMeshBuffer pathBuffer;
    private SweepSheetMeshBuffer sweepSheetBuffer;
    private ThrustSheetMeshBuffer thrustSheetBuffer;
    private Mesh coreMesh;
    private Mesh glowMesh;
    private MeshFilter coreFilter;
    private MeshFilter glowFilter;
    private MeshRenderer coreRenderer;
    private MeshRenderer glowRenderer;
    private MaterialPropertyBlock coreBlock;
    private MaterialPropertyBlock glowBlock;
    private MotionRibbonTrail settings;
    private bool detached;
    private int sourceEntityId;

    /// <summary>本渲染帧最后一次由来源实体刷新的标记。</summary>
    internal int LastTouchedFrame { get; private set; }

    /// <summary>创建供对象池克隆的完整双层网格视图。</summary>
    internal static MotionRibbonTrailView CreatePrefab()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(MotionRibbonTrailView));
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        MotionRibbonTrailView view = obj.AddComponent<MotionRibbonTrailView>();
        view.coreRenderer = AddLayer(view.transform, "core", -1, out view.coreFilter);
        view.glowRenderer = AddLayer(view.transform, "glow", -2, out view.glowFilter);
        view.CreateMeshInstances();
        view.ResetView();
        return view;
    }

    /// <summary>为池中新克隆的视图建立独占网格，避免不同轨迹互相覆盖。</summary>
    internal void CreateMeshInstances()
    {
        ResolveLayers();
        coreMesh = NewMesh("MotionRibbonCore");
        glowMesh = NewMesh("MotionRibbonGlow");
        coreFilter.sharedMesh = coreMesh;
        glowFilter.sharedMesh = glowMesh;
        EnsureState();
    }

    /// <summary>在 Unity 克隆后重新取得不会自动序列化的私有子节点引用。</summary>
    private void ResolveLayers()
    {
        if (coreFilter == null)
        {
            Transform core = transform.Find("core");
            coreFilter = core.GetComponent<MeshFilter>();
            coreRenderer = core.GetComponent<MeshRenderer>();
        }
        if (glowFilter == null)
        {
            Transform glow = transform.Find("glow");
            glowFilter = glow.GetComponent<MeshFilter>();
            glowRenderer = glow.GetComponent<MeshRenderer>();
        }
    }

    /// <summary>把池化视图绑定到一个新的 ECS 来源实体。</summary>
    internal void Bind(int entityId)
    {
        ResetView();
        sourceEntityId = entityId;
        detached = false;
    }

    /// <summary>判断当前视图是否仍属于指定来源实体。</summary>
    internal bool IsBoundTo(int entityId)
    {
        return sourceEntityId == entityId;
    }

    /// <summary>记录来源实体本帧的真实位置，并更新可变轨迹头部。</summary>
    internal void Touch(Vector3 position, Vector3 origin, in MotionRibbonTrail trail, float now, int frame)
    {
        EnsureState();
        settings = trail;
        detached = false;
        LastTouchedFrame = frame;
        Sample(position, origin, now);
        TrimToCapacity();
    }

    /// <summary>停止跟随来源实体，保留既有路径直到历史时间耗尽。</summary>
    internal void Detach()
    {
        detached = true;
    }

    /// <summary>刷新双层路径网格；返回 false 表示离体轨迹已经完全消散。</summary>
    internal bool Render(float now)
    {
        EnsureState();
        PruneExpired(now);
        int minimumPoints = settings.Shape == MotionRibbonShape.AxialThrust ? 1 : 2;
        if (detached && (points.Count < minimumPoints || now - times[^1] >= ResolveHistory()))
        {
            HideLayers();
            return false;
        }

        if (points.Count < minimumPoints)
        {
            HideLayers();
            return true;
        }

        float fade = detached
            ? 1f - Mathf.Clamp01((now - times[^1]) / ResolveHistory())
            : 1f;
        if (settings.Shape == MotionRibbonShape.AxialThrust)
        {
            RenderThrustSheet(fade);
            return true;
        }
        if (settings.Shape == MotionRibbonShape.RadialSweep)
        {
            RenderSweepSheet(fade);
            return true;
        }

        RenderPath(now, fade);
        return true;
    }

    /// <summary>使用通用透明材质绘制固定宽度路径带。</summary>
    private void RenderPath(float now, float fade)
    {
        SetMaterials(
            WorldVisualResources.TransparentSpriteMaterial,
            WorldVisualResources.TransparentSpriteMaterial);
        float tileLength = Mathf.Max(0.03f, settings.TileLength);
        var coreStyle = new RibbonPathStyle(tileLength, settings.FlowSpeed, 0.04f, 0.82f, 1f, true);
        var glowStyle = new RibbonPathStyle(tileLength, settings.FlowSpeed * 0.72f, 0.02f, 0.72f, 1f, true);
        RibbonPathMesh.Build(
            coreMesh,
            pathBuffer,
            points,
            Mathf.Max(0.01f, settings.CoreWidth),
            coreStyle,
            now,
            settings.CoreColor,
            Mathf.Clamp01(settings.CoreAlpha) * fade,
            true);
        RibbonPathMesh.Build(
            glowMesh,
            pathBuffer,
            points,
            Mathf.Max(settings.CoreWidth, settings.GlowWidth),
            glowStyle,
            now,
            settings.GlowColor,
            Mathf.Clamp01(settings.GlowAlpha) * fade,
            true);
        ApplyTexture(coreRenderer, CoreTexture, ref coreBlock);
        ApplyTexture(glowRenderer, GlowTexture, ref glowBlock);
        coreRenderer.enabled = coreMesh.vertexCount > 0;
        glowRenderer.enabled = glowMesh.vertexCount > 0;
    }

    /// <summary>使用专用 Shader 绘制来源中心到武器端点之间的双层轴向枪芒。</summary>
    private void RenderThrustSheet(float fade)
    {
        SetMaterials(
            WorldVisualResources.WeaponThrustCoreMaterial,
            WorldVisualResources.WeaponThrustGlowMaterial);
        Vector3 point = points[^1];
        Vector3 origin = origins[^1];
        ThrustSheetMesh.Build(
            coreMesh,
            thrustSheetBuffer,
            origin,
            point,
            settings.ThrustStartOffset,
            settings.ThrustTipExtension,
            Mathf.Max(0.01f, settings.CoreWidth),
            settings.CoreColor,
            Mathf.Clamp01(settings.CoreAlpha) * fade);
        ThrustSheetMesh.Build(
            glowMesh,
            thrustSheetBuffer,
            origin,
            point,
            settings.ThrustStartOffset * 0.82f,
            settings.ThrustTipExtension * 1.18f,
            Mathf.Max(settings.CoreWidth, settings.GlowWidth),
            settings.GlowColor,
            Mathf.Clamp01(settings.GlowAlpha) * fade);
        ApplyProceduralProperties(coreRenderer, ref coreBlock);
        ApplyProceduralProperties(glowRenderer, ref glowBlock);
        coreRenderer.enabled = coreMesh.vertexCount > 0;
        glowRenderer.enabled = glowMesh.vertexCount > 0;
    }

    /// <summary>使用专用 Shader 绘制来源中心到武器轨迹之间的成片扫掠扇面。</summary>
    private void RenderSweepSheet(float fade)
    {
        SetMaterials(
            WorldVisualResources.WeaponSweepCoreMaterial,
            WorldVisualResources.WeaponSweepGlowMaterial);
        float innerRatio = Mathf.Clamp(settings.SweepInnerRadiusRatio, 0.05f, 0.92f);
        SweepSheetMesh.Build(
            coreMesh,
            sweepSheetBuffer,
            points,
            origins,
            innerRatio,
            settings.SweepOuterExtension,
            settings.CoreColor,
            Mathf.Clamp01(settings.CoreAlpha) * fade);
        SweepSheetMesh.Build(
            glowMesh,
            sweepSheetBuffer,
            points,
            origins,
            Mathf.Max(0.05f, innerRatio - 0.075f),
            settings.SweepOuterExtension + Mathf.Max(0f, settings.SweepGlowExpansion),
            settings.GlowColor,
            Mathf.Clamp01(settings.GlowAlpha) * fade);
        ApplyProceduralProperties(coreRenderer, ref coreBlock);
        ApplyProceduralProperties(glowRenderer, ref glowBlock);
        coreRenderer.enabled = coreMesh.vertexCount > 0;
        glowRenderer.enabled = glowMesh.vertexCount > 0;
    }

    /// <summary>清空池化视图上一任来源留下的采样和网格状态。</summary>
    internal void ResetView()
    {
        EnsureState();
        points.Clear();
        origins.Clear();
        times.Clear();
        coreMesh?.Clear();
        glowMesh?.Clear();
        sourceEntityId = 0;
        LastTouchedFrame = 0;
        detached = false;
        settings = default;
        HideLayers();
    }

    /// <summary>建立网格渲染子节点并使用统一透明材质。</summary>
    private static MeshRenderer AddLayer(
        Transform parent,
        string name,
        int sortingOrder,
        out MeshFilter filter)
    {
        GameObject layer = new(name, typeof(MeshFilter), typeof(MeshRenderer));
        layer.transform.SetParent(parent, false);
        filter = layer.GetComponent<MeshFilter>();
        MeshRenderer renderer = layer.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = WorldVisualResources.TransparentSpriteMaterial;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    /// <summary>创建一个不会写入场景或资源文件的动态网格。</summary>
    private static Mesh NewMesh(string name)
    {
        return new Mesh
        {
            name = name,
            hideFlags = HideFlags.DontSave,
        };
    }

    /// <summary>保证池化克隆持有独立的托管采样缓冲。</summary>
    private void EnsureState()
    {
        points ??= new List<Vector3>(32);
        origins ??= new List<Vector3>(32);
        times ??= new List<float>(32);
        pathBuffer ??= new RibbonPathMeshBuffer();
        sweepSheetBuffer ??= new SweepSheetMeshBuffer();
        thrustSheetBuffer ??= new ThrustSheetMeshBuffer();
    }

    /// <summary>在固定采样点之间维护一个随实体逐帧移动的轨迹头部。</summary>
    private void Sample(Vector3 position, Vector3 origin, float now)
    {
        position.z = 0f;
        origin.z = 0f;
        if (points.Count == 0)
        {
            points.Add(position);
            origins.Add(origin);
            times.Add(now);
            return;
        }

        if (points.Count == 1)
        {
            if ((position - points[0]).sqrMagnitude <= 0.000001f) return;
            points.Add(position);
            origins.Add(origin);
            times.Add(now);
            return;
        }

        float minDistance = Mathf.Max(0.005f, settings.MinSampleDistance);
        if ((points[^1] - points[^2]).sqrMagnitude >= minDistance * minDistance)
        {
            points.Add(points[^1]);
            origins.Add(origins[^1]);
            times.Add(times[^1]);
        }
        points[^1] = position;
        origins[^1] = origin;
        times[^1] = now;
    }

    /// <summary>按配置上限移除最老的固定采样点。</summary>
    private void TrimToCapacity()
    {
        int maximum = Mathf.Clamp(settings.MaxPoints, 4, 64);
        while (points.Count > maximum)
        {
            points.RemoveAt(0);
            origins.RemoveAt(0);
            times.RemoveAt(0);
        }
    }

    /// <summary>移除已经超出历史窗口且不再参与路径形状的旧采样。</summary>
    private void PruneExpired(float now)
    {
        float history = ResolveHistory();
        while (points.Count > 2 && now - times[0] > history)
        {
            points.RemoveAt(0);
            origins.RemoveAt(0);
            times.RemoveAt(0);
        }
    }

    /// <summary>返回经过下限约束的历史保留时间。</summary>
    private float ResolveHistory()
    {
        return Mathf.Max(0.05f, settings.HistorySeconds);
    }

    /// <summary>隐藏两层渲染器而不改变对象池激活状态。</summary>
    private void HideLayers()
    {
        if (coreRenderer != null) coreRenderer.enabled = false;
        if (glowRenderer != null) glowRenderer.enabled = false;
    }

    /// <summary>切换两层共享材质，不为单条轨迹创建材质实例。</summary>
    private void SetMaterials(Material coreMaterial, Material glowMaterial)
    {
        if (coreRenderer.sharedMaterial != coreMaterial) coreRenderer.sharedMaterial = coreMaterial;
        if (glowRenderer.sharedMaterial != glowMaterial) glowRenderer.sharedMaterial = glowMaterial;
    }

    /// <summary>清理路径纹理留下的属性并向程序化 Shader 提交统一实例参数。</summary>
    private static void ApplyProceduralProperties(MeshRenderer renderer, ref MaterialPropertyBlock block)
    {
        block ??= new MaterialPropertyBlock();
        block.Clear();
        block.SetColor("_Color", Color.white);
        block.SetFloat("_Opacity", 1f);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>通过属性块设置遮罩纹理，避免为每条轨迹复制材质。</summary>
    private static void ApplyTexture(
        MeshRenderer renderer,
        Texture texture,
        ref MaterialPropertyBlock block)
    {
        block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetTexture("_MainTex", texture);
        block.SetColor("_Color", Color.white);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>返回中心带使用的锐利横向透明遮罩。</summary>
    private static Texture2D CoreTexture => coreTexture ??= BuildTexture("MotionRibbonCoreMask", false);

    /// <summary>返回柔光带使用的平滑横向透明遮罩。</summary>
    private static Texture2D GlowTexture => glowTexture ??= BuildTexture("MotionRibbonGlowMask", true);

    /// <summary>生成可沿路径重复的白色透明遮罩，避免依赖外部贴图。</summary>
    private static Texture2D BuildTexture(string name, bool glow)
    {
        var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
        {
            name = name,
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
        };
        var pixels = new Color32[TextureWidth * TextureHeight];
        for (var y = 0; y < TextureHeight; y++)
        {
            float vertical = Mathf.Abs(y / (TextureHeight - 1f) * 2f - 1f);
            float crossSection = glow
                ? Mathf.Pow(Mathf.Clamp01(1f - vertical), 2.25f)
                : Mathf.Pow(Mathf.Clamp01(1f - vertical), 0.42f);
            for (var x = 0; x < TextureWidth; x++)
            {
                float flow = 0.9f + Mathf.Sin((x + 0.5f) / TextureWidth * Mathf.PI * 2f) * 0.1f;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(crossSection * flow) * 255f);
                pixels[y * TextureWidth + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}

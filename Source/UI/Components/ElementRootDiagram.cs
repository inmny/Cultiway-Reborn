using System;
using System.Globalization;
using System.Text;
using Cultiway.Const;
using Cultiway.Core.Components;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>灵根总览图的细节等级；小尺寸会主动省略无法辨认的装饰。</summary>
public enum ElementRootDiagramDetail
{
    /// <summary>用于列表和提示图标，仅保留主要纹理与数据边界。</summary>
    Compact,
    /// <summary>用于角色详情等中等尺寸位置，显示完整静态构成。</summary>
    Medium,
    /// <summary>用于配置窗口，显示完整纹理、鱼眼和低频动态。</summary>
    Large
}

/// <summary>集中定义阴阳混沌圆形材质、五行长条材质与八种元素辨识主色。</summary>
internal static class ElementRootDiagramStyles
{
    private const string PatternRoot = "cultiway/ui/element_root_diagram/";
    private const string AuraPath = PatternRoot + "chaos_aura";

    private static readonly string[] PatternPaths =
    {
        PatternRoot + "neg",
        PatternRoot + "pos",
        PatternRoot + "entropy"
    };

    private static readonly string[] RingPatternPaths =
    {
        PatternRoot + "iron_strip",
        PatternRoot + "wood_strip",
        PatternRoot + "water_strip",
        PatternRoot + "fire_strip",
        PatternRoot + "earth_strip"
    };

    private static readonly Color[] Colors =
    {
        new Color32(190, 205, 216, 255),
        new Color32(96, 134, 55, 255),
        new Color32(51, 158, 224, 255),
        new Color32(238, 91, 24, 255),
        new Color32(145, 101, 60, 255),
        new Color32(37, 42, 75, 255),
        new Color32(244, 231, 199, 255),
        new Color32(126, 128, 140, 255)
    };

    private static readonly Texture[] Textures = new Texture[3];
    private static readonly Texture[] RingTextures = new Texture[5];
    private static Texture _auraTexture;

    /// <summary>取得元素的辨识主色，供滑块等非纹理控件复用。</summary>
    public static Color GetColor(int elementIndex)
    {
        ValidateIndex(elementIndex);
        return Colors[elementIndex];
    }

    /// <summary>取得阴、阳或混沌的圆形材质，并按完整徽记图案进行平滑采样。</summary>
    public static Texture GetTexture(int elementIndex)
    {
        var textureIndex = elementIndex switch
        {
            ElementIndex.Neg => 0,
            ElementIndex.Pos => 1,
            ElementIndex.Entropy => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(elementIndex), elementIndex, null)
        };
        if (Textures[textureIndex] != null) return Textures[textureIndex];

        var sprite = SpriteTextureLoader.getSprite(PatternPaths[textureIndex]);
        var texture = sprite == null ? Texture2D.whiteTexture : sprite.texture;
        if (texture != Texture2D.whiteTexture)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
        }
        Textures[textureIndex] = texture;
        return texture;
    }

    /// <summary>取得横向恰好表示完整一圈的五行透明长条纹理。</summary>
    public static Texture GetRingTexture(int elementIndex)
    {
        if (elementIndex < 0 || elementIndex >= RingTextures.Length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex), elementIndex, null);
        if (RingTextures[elementIndex] != null) return RingTextures[elementIndex];

        var sprite = SpriteTextureLoader.getSprite(RingPatternPaths[elementIndex]);
        var texture = sprite == null ? Texture2D.whiteTexture : sprite.texture;
        if (texture != Texture2D.whiteTexture)
        {
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
        }
        RingTextures[elementIndex] = texture;
        return texture;
    }

    /// <summary>取得独立于混沌占比底色的透明烟雾外缘。</summary>
    public static Texture GetAuraTexture()
    {
        if (_auraTexture != null) return _auraTexture;
        var sprite = SpriteTextureLoader.getSprite(AuraPath);
        _auraTexture = sprite == null ? Texture2D.whiteTexture : sprite.texture;
        if (_auraTexture != Texture2D.whiteTexture)
        {
            _auraTexture.wrapMode = TextureWrapMode.Clamp;
            _auraTexture.filterMode = FilterMode.Bilinear;
        }
        return _auraTexture;
    }

    private static void ValidateIndex(int elementIndex)
    {
        if (elementIndex < 0 || elementIndex >= 8)
            throw new ArgumentOutOfRangeException(nameof(elementIndex), elementIndex, null);
    }
}

/// <summary>
/// 可在配置窗口、角色详情和提示中复用的灵根总览图。
/// 混沌作为整圆背景，五行透明长条构成外环，阴阳在内圈形成与五行分离的太极。
/// </summary>
public sealed class ElementRootDiagram : MonoBehaviour
{
    private const float FiveElementsEmptyCenterRadius = 0.28f;
    private const float FiveElementsMinOuterRadius = 0.59f;
    private const float FiveElementsMaxOuterRadius = 0.91f;
    private const float FiveElementsYinYangGap = 0.012f;
    private const float FiveElementsRotationSpeed = 3f;
    private const float InternalAnimationInterval = 1f / 30f;
    private const float ChaosBackgroundRotationSpeed = 1.8f;
    private const float YinYangMinRadius = 0.25f;
    private const float YinYangMaxRadius = 0.38f;
    private const float YinYangRotationSpeed = 6f;
    private const float YinYangTextureFlowSpeed = 2.5f;
    private const float TransitionMaxHalfAngle = 10f;
    private const float TransitionSectorFraction = 0.22f;

    private static readonly int[] FiveElementOrder =
    {
        ElementIndex.Wood,
        ElementIndex.Fire,
        ElementIndex.Earth,
        ElementIndex.Iron,
        ElementIndex.Water
    };

    private static readonly float[] FiveElementTextureFlowSpeeds =
    {
        0.018f,
        0.016f,
        0.021f,
        0.024f,
        0.014f
    };

    private static readonly float[] ChaosCloudRadii = { 1f, 0.72f, 0.45f, 0.24f };
    private static readonly float[] ChaosCloudMaxAlpha = { 0.64f, 0.52f, 0.44f, 0.36f };
    private static readonly float[] ChaosCloudRotationSpeeds = { 0.45f, -0.7f, 1.05f, -1.4f };

    private readonly float[] _ratios = new float[8];
    private readonly ElementRootDiscGraphic[] _chaosClouds = new ElementRootDiscGraphic[4];
    private readonly ElementRootRingSegmentGraphic[] _fiveElements = new ElementRootRingSegmentGraphic[5];
    private readonly ElementRootRingSegmentGraphic[] _transitionBottom = new ElementRootRingSegmentGraphic[5];
    private readonly ElementRootRingSegmentGraphic[] _transitionTop = new ElementRootRingSegmentGraphic[5];
    private readonly int[] _activeElements = new int[5];
    private readonly int[] _transitionFromElements = new int[5];
    private readonly int[] _transitionToElements = new int[5];
    private readonly float[] _sectorStarts = new float[5];
    private readonly float[] _sectorAngles = new float[5];
    private readonly float[] _transitionHalfAngles = new float[5];
    private readonly float[] _fiveElementTexturePhases = new float[5];
    private RectTransform _chaosGroup;
    private ElementRootDiscGraphic _chaosAura;
    private RectTransform _fiveElementsGroup;
    private RectTransform _yinYangGroup;
    private ElementRootYinYangRegionGraphic _yin;
    private ElementRootYinYangRegionGraphic _yang;
    private ElementRootDiscGraphic _yinEye;
    private ElementRootDiscGraphic _yangEye;
    private ElementRootOrbHighlightGraphic _orbHighlight;
    private ElementRootDiagramOverlayGraphic _overlay;
    private ElementRootDiagramDetail _detail;
    private TipButton _tip;
    private bool _initialized;
    private bool _valid;
    private float _fiveRatio;
    private float _yinYangRatio;
    private float _chaosRatio;
    private float _nextInternalAnimationTime;
    private int _activeTransitionCount;
    private string _tooltipTitle;
    private string _tooltipSummary;

    /// <summary>创建指定尺寸与细节等级的灵根总览图。</summary>
    public static ElementRootDiagram Create(Transform parent, string name, float size,
        ElementRootDiagramDetail detail)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(LayoutElement), typeof(ElementRootDiagram));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, size, size);
        var hitArea = obj.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

        var diagram = obj.GetComponent<ElementRootDiagram>();
        diagram.Initialize(detail);
        return diagram;
    }

    /// <summary>使用实际灵根组件刷新图形，并设置 tooltip 中的类型摘要。</summary>
    public void SetElementRoot(ElementRoot root, string title = null, string summary = null)
    {
        var values = new float[8];
        for (var i = 0; i < values.Length; i++) values[i] = root[i];
        SetValues(values, title, summary);
    }

    /// <summary>使用任意八项非负权重刷新图形；传入值会在组件内部统一归一化。</summary>
    public void SetValues(float[] values, string title = null, string summary = null)
    {
        if (values == null || values.Length < 8)
            throw new ArgumentException("灵根总览图需要八项元素值", nameof(values));
        if (!_initialized) Initialize(ElementRootDiagramDetail.Medium);

        double sum = 0d;
        for (var i = 0; i < _ratios.Length; i++)
        {
            var value = values[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) value = 0f;
            _ratios[i] = value;
            sum += value;
        }

        _valid = sum > 0d;
        if (_valid)
        {
            for (var i = 0; i < _ratios.Length; i++) _ratios[i] = (float)(_ratios[i] / sum);
        }
        else
        {
            Array.Clear(_ratios, 0, _ratios.Length);
        }

        _tooltipTitle = string.IsNullOrEmpty(title)
            ? "Cultiway.ElementRootDiagram.Tooltip.Title".Localize()
            : title;
        _tooltipSummary = summary;
        RefreshGeometry();
        RefreshTooltip();
    }

    private void Initialize(ElementRootDiagramDetail detail)
    {
        if (_initialized)
        {
            _detail = detail;
            return;
        }

        _initialized = true;
        _detail = detail;
        _chaosGroup = CreateLayer("Chaos Group");
        for (var i = 0; i < _chaosClouds.Length; i++)
        {
            _chaosClouds[i] = CreateTexturedGraphic<ElementRootDiscGraphic>(
                $"Chaos Cloud {i}", ElementIndex.Entropy, 1f, _chaosGroup);
            _chaosClouds[i].transform.localEulerAngles = new Vector3(0f, 0f, i * 73f);
        }
        _chaosAura = CreateTexturedGraphic<ElementRootDiscGraphic>("Chaos Aura",
            ElementRootDiagramStyles.GetAuraTexture(), Color.clear, 1f, _chaosGroup);
        _fiveElementsGroup = CreateLayer("Five Elements Group");
        for (var i = 0; i < _fiveElements.Length; i++)
            _fiveElements[i] = CreateRingGraphic($"Five Element {i}", i, _fiveElementsGroup);
        for (var i = 0; i < _transitionBottom.Length; i++)
        {
            _transitionBottom[i] = CreateRingGraphic(
                $"Transition {i} Bottom", ElementIndex.Iron, _fiveElementsGroup);
            _transitionTop[i] = CreateRingGraphic(
                $"Transition {i} Top", ElementIndex.Iron, _fiveElementsGroup);
        }
        _yinYangGroup = CreateLayer("Yin Yang Group");
        _yin = CreateTexturedGraphic<ElementRootYinYangRegionGraphic>(
            "Yin", ElementIndex.Neg, 1f, _yinYangGroup);
        _yang = CreateTexturedGraphic<ElementRootYinYangRegionGraphic>(
            "Yang", ElementIndex.Pos, 1f, _yinYangGroup);
        _yinEye = CreateSolidGraphic<ElementRootDiscGraphic>("Yin Eye",
            new Color32(239, 231, 207, 255), _yinYangGroup);
        _yangEye = CreateSolidGraphic<ElementRootDiscGraphic>("Yang Eye",
            new Color32(17, 19, 22, 255), _yinYangGroup);
        _orbHighlight = CreateGraphic<ElementRootOrbHighlightGraphic>("Orb Highlight", _yinYangGroup);
        _orbHighlight.raycastTarget = false;
        _overlay = CreateGraphic<ElementRootDiagramOverlayGraphic>("Boundaries");
        _overlay.raycastTarget = false;

        UiTooltip.Set(gameObject, string.Empty, string.Empty);
        _tip = GetComponent<TipButton>();
    }

    private T CreateTexturedGraphic<T>(string name, int elementIndex, float alpha,
        Transform parent = null)
        where T : ElementRootTexturedGraphic
    {
        var texture = ElementRootDiagramStyles.GetTexture(elementIndex);
        return CreateTexturedGraphic<T>(name, texture,
            ElementRootDiagramStyles.GetColor(elementIndex), alpha, parent);
    }

    /// <summary>创建使用可循环五行长条的圆环片段。</summary>
    private ElementRootRingSegmentGraphic CreateRingGraphic(string name, int elementIndex,
        Transform parent)
    {
        var graphic = CreateGraphic<ElementRootRingSegmentGraphic>(name, parent);
        SetRingTexture(graphic, elementIndex);
        graphic.raycastTarget = false;
        return graphic;
    }

    /// <summary>给圆环片段切换元素长条；资源缺失时使用元素主色作为可见兜底。</summary>
    private static void SetRingTexture(ElementRootRingSegmentGraphic graphic, int elementIndex)
    {
        var texture = ElementRootDiagramStyles.GetRingTexture(elementIndex);
        graphic.SetTexture(texture, true);
        graphic.color = texture == Texture2D.whiteTexture
            ? ElementRootDiagramStyles.GetColor(elementIndex)
            : Color.white;
    }

    private T CreateTexturedGraphic<T>(string name, Texture texture, Color fallbackColor, float alpha,
        Transform parent = null)
        where T : ElementRootTexturedGraphic
    {
        var graphic = CreateGraphic<T>(name, parent);
        graphic.SetTexture(texture);
        var color = texture == Texture2D.whiteTexture
            ? fallbackColor
            : Color.white;
        color.a *= alpha;
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    private T CreateSolidGraphic<T>(string name, Color color, Transform parent = null)
        where T : ElementRootTexturedGraphic
    {
        var graphic = CreateGraphic<T>(name, parent);
        graphic.SetTexture(Texture2D.whiteTexture);
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    /// <summary>创建覆盖整个徽记区域的普通容器层。</summary>
    private RectTransform CreateLayer(string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(transform, false);
        var rect = obj.GetComponent<RectTransform>();
        UiLayout.Stretch(rect);
        return rect;
    }

    private T CreateGraphic<T>(string name, Transform parent = null) where T : Graphic
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        obj.transform.SetParent(parent == null ? transform : parent, false);
        UiLayout.Stretch(obj.GetComponent<RectTransform>());
        return obj.GetComponent<T>();
    }

    private void RefreshGeometry()
    {
        if (!_valid)
        {
            SetAllDataGraphicsActive(false);
            _overlay.gameObject.SetActive(true);
            _overlay.Configure(false);
            return;
        }

        _fiveRatio = _ratios[ElementIndex.Iron] + _ratios[ElementIndex.Wood] +
                     _ratios[ElementIndex.Water] + _ratios[ElementIndex.Fire] +
                     _ratios[ElementIndex.Earth];
        _yinYangRatio = _ratios[ElementIndex.Neg] + _ratios[ElementIndex.Pos];
        _chaosRatio = _ratios[ElementIndex.Entropy];

        RefreshChaos();
        RefreshFiveElements();
        RefreshYinYang();
        _overlay.gameObject.SetActive(true);
        _overlay.Configure(true);
    }

    /// <summary>用多层缩放云团填满整个底图；混沌占比只控制云雾浓度。</summary>
    private void RefreshChaos()
    {
        if (_detail != ElementRootDiagramDetail.Large)
        {
            _chaosGroup.localRotation = Quaternion.identity;
            for (var i = 0; i < _chaosClouds.Length; i++)
                _chaosClouds[i].transform.localEulerAngles = new Vector3(0f, 0f, i * 73f);
            _chaosAura.transform.localRotation = Quaternion.identity;
        }

        var visible = _chaosRatio > 0.000001f;
        for (var i = 0; i < _chaosClouds.Length; i++)
            _chaosClouds[i].gameObject.SetActive(visible);
        _chaosAura.gameObject.SetActive(visible);
        if (!visible) return;

        var intensity = Mathf.Sqrt(_chaosRatio);
        for (var i = 0; i < _chaosClouds.Length; i++)
        {
            _chaosClouds[i].Configure(Vector2.zero, ChaosCloudRadii[i]);
            _chaosClouds[i].color = new Color(0.62f, 0.64f, 0.67f,
                ChaosCloudMaxAlpha[i] * intensity);
        }
        _chaosAura.Configure(Vector2.zero, 1f, 1.08f);
        _chaosAura.color = new Color(0.68f, 0.7f, 0.73f, 0.36f * intensity);
    }

    /// <summary>
    /// 刷新五行圆环。每个元素使用纯色段，边界另用下一元素底层渐入和当前元素顶层渐出完成定向混合。
    /// </summary>
    private void RefreshFiveElements()
    {
        if (_detail != ElementRootDiagramDetail.Large)
            _fiveElementsGroup.localRotation = Quaternion.identity;
        _activeTransitionCount = 0;
        for (var i = 0; i < _fiveElements.Length; i++)
        {
            _fiveElements[i].gameObject.SetActive(false);
            _transitionBottom[i].gameObject.SetActive(false);
            _transitionTop[i].gameObject.SetActive(false);
        }
        if (_fiveRatio <= 0.000001f) return;

        var activeCount = 0;
        for (var i = 0; i < FiveElementOrder.Length; i++)
        {
            var elementIndex = FiveElementOrder[i];
            if (_ratios[elementIndex] > 0.000001f)
                _activeElements[activeCount++] = elementIndex;
        }
        if (activeCount == 0) return;

        var innerRadius = _yinYangRatio > 0.000001f
            ? ResolveYinYangRadius() + FiveElementsYinYangGap
            : FiveElementsEmptyCenterRadius;
        var outerRadius = Mathf.Lerp(FiveElementsMinOuterRadius, FiveElementsMaxOuterRadius,
            Mathf.SmoothStep(0f, 1f, _fiveRatio));
        var currentAngle = 90f;
        for (var i = 0; i < activeCount; i++)
        {
            var elementIndex = _activeElements[i];
            _sectorStarts[i] = currentAngle;
            _sectorAngles[i] = 360f * _ratios[elementIndex] / _fiveRatio;
            currentAngle -= _sectorAngles[i];
        }

        if (activeCount == 1)
        {
            var elementIndex = _activeElements[0];
            var graphic = _fiveElements[elementIndex];
            SetRingTexture(graphic, elementIndex);
            graphic.Configure(innerRadius, outerRadius, 90f, -360f);
            graphic.SetPhase(_detail == ElementRootDiagramDetail.Large
                ? _fiveElementTexturePhases[elementIndex]
                : 0f);
            graphic.gameObject.SetActive(true);
            return;
        }

        for (var i = 0; i < activeCount; i++)
        {
            var next = (i + 1) % activeCount;
            _transitionHalfAngles[i] = Mathf.Min(TransitionMaxHalfAngle,
                Mathf.Min(_sectorAngles[i], _sectorAngles[next]) * TransitionSectorFraction);
        }

        for (var i = 0; i < activeCount; i++)
        {
            var elementIndex = _activeElements[i];
            var entryHalfAngle = _transitionHalfAngles[(i - 1 + activeCount) % activeCount];
            var exitHalfAngle = _transitionHalfAngles[i];
            var pureAngle = Mathf.Max(0.001f,
                _sectorAngles[i] - entryHalfAngle - exitHalfAngle);
            var graphic = _fiveElements[elementIndex];
            SetRingTexture(graphic, elementIndex);
            graphic.Configure(innerRadius, outerRadius,
                _sectorStarts[i] - entryHalfAngle, -pureAngle);
            graphic.SetPhase(_detail == ElementRootDiagramDetail.Large
                ? _fiveElementTexturePhases[elementIndex]
                : 0f);
            graphic.gameObject.SetActive(true);

            var nextElementIndex = _activeElements[(i + 1) % activeCount];
            var boundaryAngle = _sectorStarts[i] - _sectorAngles[i];
            var transitionStart = boundaryAngle + exitHalfAngle;
            var transitionSweep = -exitHalfAngle * 2f;
            var bottom = _transitionBottom[i];
            var top = _transitionTop[i];

            // 顺时针进入下一元素：下一元素在底层由浅变深，当前元素在顶层由深变浅。
            SetRingTexture(bottom, nextElementIndex);
            bottom.Configure(innerRadius, outerRadius,
                transitionStart, transitionSweep, 0f, 1f);
            bottom.SetPhase(_detail == ElementRootDiagramDetail.Large
                ? _fiveElementTexturePhases[nextElementIndex]
                : 0f);
            bottom.gameObject.SetActive(true);

            SetRingTexture(top, elementIndex);
            top.Configure(innerRadius, outerRadius,
                transitionStart, transitionSweep, 1f, 0f);
            top.SetPhase(_detail == ElementRootDiagramDetail.Large
                ? _fiveElementTexturePhases[elementIndex]
                : 0f);
            top.gameObject.SetActive(true);

            _transitionFromElements[i] = elementIndex;
            _transitionToElements[i] = nextElementIndex;
        }
        _activeTransitionCount = activeCount;
    }

    /// <summary>刷新与五行圆环保持固定透明间隔的实色太极。</summary>
    private void RefreshYinYang()
    {
        var visible = _yinYangRatio > 0.000001f;
        _yinYangGroup.gameObject.SetActive(visible);
        _yin.gameObject.SetActive(visible);
        _yang.gameObject.SetActive(visible);
        _orbHighlight.gameObject.SetActive(visible && _detail != ElementRootDiagramDetail.Compact);
        if (!visible)
        {
            _yinEye.gameObject.SetActive(false);
            _yangEye.gameObject.SetActive(false);
            return;
        }

        var yinRatio = _ratios[ElementIndex.Neg] / _yinYangRatio;
        var offset = YinYangDiagramGeometry.SolveOffset(yinRatio);
        var radius = ResolveYinYangRadius();
        _yin.Configure(radius, offset, true);
        _yang.Configure(radius, offset, false);
        RefreshFishEyes(radius, offset, yinRatio);
        if (_orbHighlight.gameObject.activeSelf)
            _orbHighlight.Configure(radius);
        if (_detail != ElementRootDiagramDetail.Large)
        {
            _yinYangGroup.localEulerAngles = Vector3.zero;
            _yin.SetTextureRotation(0f);
            _yang.SetTextureRotation(0f);
        }
    }

    /// <summary>根据阴阳总占比解析太极半径，供太极本体和五行内缘共享同一几何基准。</summary>
    private float ResolveYinYangRadius()
    {
        return Mathf.Lerp(YinYangMinRadius, YinYangMaxRadius,
            Mathf.SmoothStep(0f, 1f, _yinYangRatio));
    }

    private void RefreshFishEyes(float radius, float offset, float yinRatio)
    {
        if (_detail == ElementRootDiagramDetail.Compact || radius <= 0.04f)
        {
            _yinEye.gameObject.SetActive(false);
            _yangEye.gameObject.SetActive(false);
            return;
        }

        const float eyeY = 0.5f;
        var halfWidth = Mathf.Sqrt(1f - eyeY * eyeY);
        var topBoundary = YinYangDiagramGeometry.GetBoundary(eyeY, offset);
        var bottomBoundary = YinYangDiagramGeometry.GetBoundary(-eyeY, offset);
        var yinWidth = topBoundary + halfWidth;
        var yangWidth = halfWidth - bottomBoundary;
        var balance = Mathf.Sqrt(Mathf.Clamp01(4f * yinRatio * (1f - yinRatio)));
        var localEyeRadius = Mathf.Min(0.1f, Mathf.Min(yinWidth, yangWidth) * 0.18f) * balance;
        var eyeRadius = localEyeRadius * radius;
        var visible = eyeRadius > 0.004f;
        _yinEye.gameObject.SetActive(visible);
        _yangEye.gameObject.SetActive(visible);
        if (!visible) return;

        var yinCenter = new Vector2((-halfWidth + topBoundary) * 0.5f, eyeY) * radius;
        var yangCenter = new Vector2((bottomBoundary + halfWidth) * 0.5f, -eyeY) * radius;
        _yinEye.Configure(yinCenter, eyeRadius);
        _yangEye.Configure(yangCenter, eyeRadius);
    }

    private void SetAllDataGraphicsActive(bool value)
    {
        for (var i = 0; i < _chaosClouds.Length; i++)
            _chaosClouds[i].gameObject.SetActive(value);
        _chaosAura.gameObject.SetActive(value);
        for (var i = 0; i < _fiveElements.Length; i++)
        {
            _fiveElements[i].gameObject.SetActive(value);
            _transitionBottom[i].gameObject.SetActive(value);
            _transitionTop[i].gameObject.SetActive(value);
        }
        _yinYangGroup.gameObject.SetActive(value);
        _yin.gameObject.SetActive(value);
        _yang.gameObject.SetActive(value);
        _yinEye.gameObject.SetActive(value);
        _yangEye.gameObject.SetActive(value);
        _orbHighlight.gameObject.SetActive(value);
    }

    private void RefreshTooltip()
    {
        if (_tip == null) return;
        _tip.textOnClick = _tooltipTitle;
        if (!_valid)
        {
            _tip.textOnClickDescription = "Cultiway.ElementRootRain.UI.InvalidRatios".Localize();
            return;
        }

        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(_tooltipSummary)) builder.AppendLine(_tooltipSummary);
        builder.AppendLine(string.Format("Cultiway.ElementRootDiagram.Tooltip.Groups".Localize(),
            FormatPercent(_fiveRatio), FormatPercent(_yinYangRatio), FormatPercent(_chaosRatio)));
        for (var i = 0; i < _ratios.Length; i++)
        {
            builder.Append(ElementIndex.ElementNames[i].Localize())
                .Append(": ")
                .Append(FormatPercent(_ratios[i]));
            if (i < _ratios.Length - 1) builder.AppendLine();
        }
        _tip.textOnClickDescription = builder.ToString();
    }

    private static string FormatPercent(float ratio)
    {
        return (ratio * 100f).ToString("0.#", CultureInfo.CurrentCulture) + "%";
    }

    private void Update()
    {
        if (_detail != ElementRootDiagramDetail.Large || !_valid) return;
        var time = Time.unscaledTime;

        if (_fiveRatio > 0.000001f)
            _fiveElementsGroup.localRotation = Quaternion.Euler(
                0f, 0f, time * FiveElementsRotationSpeed);

        if (_yinYangRatio > 0.000001f)
            _yinYangGroup.localRotation = Quaternion.Euler(
                0f, 0f, -time * YinYangRotationSpeed);

        if (time >= _nextInternalAnimationTime)
        {
            _nextInternalAnimationTime = time + InternalAnimationInterval;
            UpdateInternalAnimation(time);
        }

        if (_chaosRatio <= 0.000001f) return;
        _chaosGroup.localRotation = Quaternion.Euler(
            0f, 0f, -time * ChaosBackgroundRotationSpeed);
        for (var i = 0; i < _chaosClouds.Length; i++)
            _chaosClouds[i].transform.localEulerAngles = new Vector3(0f, 0f,
                i * 73f + time * ChaosCloudRotationSpeeds[i]);
        _chaosAura.transform.localEulerAngles = new Vector3(0f, 0f, -time * 0.35f);
    }

    /// <summary>刷新五行环内纹理流动和太极内部纹理旋转，不改变两者的外部几何姿态。</summary>
    private void UpdateInternalAnimation(float time)
    {
        if (_fiveRatio > 0.000001f)
        {
            for (var i = 0; i < _fiveElementTexturePhases.Length; i++)
                _fiveElementTexturePhases[i] = Mathf.Repeat(
                    time * FiveElementTextureFlowSpeeds[i], 1f);
            ApplyFiveElementTexturePhases();
        }

        if (_yinYangRatio <= 0.000001f) return;
        var textureRotation = -time * YinYangTextureFlowSpeed;
        _yin.SetTextureRotation(textureRotation);
        _yang.SetTextureRotation(textureRotation);
    }

    /// <summary>把元素自身的纹理相位同步到纯色片段及其参与的两层边界渐变。</summary>
    private void ApplyFiveElementTexturePhases()
    {
        for (var i = 0; i < _fiveElements.Length; i++)
        {
            if (_fiveElements[i].gameObject.activeSelf)
                _fiveElements[i].SetPhase(_fiveElementTexturePhases[i]);
        }

        for (var i = 0; i < _activeTransitionCount; i++)
        {
            _transitionBottom[i].SetPhase(
                _fiveElementTexturePhases[_transitionToElements[i]]);
            _transitionTop[i].SetPhase(
                _fiveElementTexturePhases[_transitionFromElements[i]]);
        }
    }
}

using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Core;

internal sealed class ControlledSkillTargetSelection : MonoBehaviour
{
    private const string HostName = "CultiwayControlledSkillTargetSelection";
    private const float HoldToSelectSeconds = 0.22f;
    private const float DefaultRadius = 5f;
    private const float MinRadius = 1.5f;
    private const float MaxRadius = 18f;
    private const float WheelRadiusStep = 1f;
    private const float CircleWidth = 0.14f;
    private const int CircleSegments = 96;
    private const string SelectionEffectPath = "effects/PrefabUnitSelectionEffect";
    private const string SelectionEffectFallbackPath = "effects/unit_selected_effect";
    private static readonly Color CastRangeColor = new(0.2f, 0.62f, 1f, 0.28f);

    private static ControlledSkillTargetSelection _instance;
    private static HotkeyAsset _castHotkey;
    private static bool _cameraZoomGateInstalled;
    private static HotkeyAction _vanillaZoomAction;

    private readonly Vector3[] _circlePoints = new Vector3[CircleSegments];
    private LineRenderer _circle;
    private MeshRenderer _castRangeFill;
    private Transform _castRangeFillTransform;
    private Material _lineMaterial;
    private Material _castRangeMaterial;
    private Mesh _castRangeMesh;
    private Transform _markerRoot;
    private MonoObjPool<SelectionMarker> _selectionMarkerPool;
    private bool _pressActive;
    private bool _selecting;
    private float _pressStartTime;
    private float _radius = DefaultRadius;
    private float _displayRadius = DefaultRadius;
    private Vector3 _center;

    internal static void Configure(HotkeyAsset castHotkey)
    {
        _castHotkey = castHotkey;
        Ensure();
    }

    internal static void InstallCameraZoomGate()
    {
        if (_cameraZoomGateInstalled || HotkeyLibrary.zoom == null) return;

        _vanillaZoomAction = HotkeyLibrary.zoom.holding_action;
        HotkeyLibrary.zoom.holding_action = asset =>
        {
            if (ShouldConsumeCameraZoom()) return;
            _vanillaZoomAction?.Invoke(asset);
        };
        _cameraZoomGateInstalled = true;
    }

    internal static void Begin(HotkeyAsset castHotkey)
    {
        _castHotkey = castHotkey ?? _castHotkey;
        Ensure();
        _instance.BeginInternal();
    }

    private static void Ensure()
    {
        if (_instance != null) return;

        var obj = new GameObject(HostName, typeof(ControlledSkillTargetSelection));
        Object.DontDestroyOnLoad(obj);
        _instance = obj.GetComponent<ControlledSkillTargetSelection>();
    }

    private void Awake()
    {
        _instance = this;
        CreateCircle();
        CreateSelectionMarkerPool();
        HideCircle();
    }

    private void Update()
    {
        if (!_pressActive) return;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        if (!IsCastKeyHeld())
        {
            Release();
            return;
        }

        var scroll = Input.mouseScrollDelta.y;
        if (!_selecting && (Time.unscaledTime - _pressStartTime >= HoldToSelectSeconds || Mathf.Abs(scroll) > 0.01f))
        {
            _selecting = true;
        }

        if (!_selecting) return;

        float fixedEffectRadius = ControlledCultivatorSkillControls.TryGetControlledActor(out Actor actor)
            ? ControlledCultivatorSkillControls.ResolveSelectedAbilityEffectRadius(actor.GetExtend())
            : 0f;
        if (fixedEffectRadius <= 0f && Mathf.Abs(scroll) > 0.01f)
        {
            _radius = Mathf.Clamp(_radius + scroll * WheelRadiusStep, MinRadius, MaxRadius);
        }

        UpdateCircle();
    }

    private static bool ShouldConsumeCameraZoom()
    {
        if (_instance == null || !_instance._pressActive) return false;
        if (!ControlledCultivatorSkillControls.TryGetControlledActor(out _)) return false;
        return _instance.IsCastKeyHeld();
    }

    private void BeginInternal()
    {
        if (_pressActive) return;
        if (!ControlledCultivatorSkillControls.TryGetControlledActor(out _)) return;

        _pressActive = true;
        _selecting = false;
        _pressStartTime = Time.unscaledTime;
        _radius = Mathf.Clamp(_radius, MinRadius, MaxRadius);
        _displayRadius = _radius;
        HideCircle();
    }

    private void Release()
    {
        var area = _selecting
            ? new SkillTargetSelectionArea(true, _center, _displayRadius)
            : SkillTargetSelectionArea.Inactive;
        Cancel();
        ControlledCultivatorSkillControls.CastSelectedSkill(area);
    }

    private void Cancel()
    {
        _pressActive = false;
        _selecting = false;
        HideCircle();
    }

    private bool IsCastKeyHeld()
    {
        return IsKeyHeld(_castHotkey?.default_key_1)
               || IsKeyHeld(_castHotkey?.default_key_2)
               || IsKeyHeld(_castHotkey?.default_key_3)
               || Input.GetKey(KeyCode.R);
    }

    private static bool IsKeyHeld(KeyCode? key)
    {
        return key.HasValue && key.Value != KeyCode.None && Input.GetKey(key.Value);
    }

    private void CreateCircle()
    {
        _lineMaterial = CreateRenderMaterial(Color.white);
        _castRangeMaterial = CreateRenderMaterial(CastRangeColor);
        _castRangeFill = CreateCircleFill("CastRangeFill", 49);
        _circle = CreateCircleRenderer("TargetSelectionCircle", CircleWidth, 50);
        _circle.enabled = false;
    }

    private MeshRenderer CreateCircleFill(string name, int sortingOrder)
    {
        var circleObj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        circleObj.transform.SetParent(transform, false);
        circleObj.transform.localPosition = Vector3.zero;
        circleObj.transform.localScale = Vector3.one;
        _castRangeFillTransform = circleObj.transform;

        _castRangeMesh = CreateUnitCircleMesh();
        circleObj.GetComponent<MeshFilter>().sharedMesh = _castRangeMesh;

        var renderer = circleObj.GetComponent<MeshRenderer>();
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        renderer.sortingOrder = sortingOrder;

        if (_castRangeMaterial != null)
        {
            renderer.sharedMaterial = _castRangeMaterial;
        }
        else if (LibraryMaterials.instance != null)
        {
            renderer.sharedMaterial = LibraryMaterials.instance.mat_world_object;
        }

        renderer.enabled = false;
        return renderer;
    }

    private LineRenderer CreateCircleRenderer(string name, float width, int sortingOrder)
    {
        var circleObj = new GameObject(name, typeof(LineRenderer));
        circleObj.transform.SetParent(transform, false);
        circleObj.transform.localPosition = Vector3.zero;
        circleObj.transform.localScale = Vector3.one;

        var renderer = circleObj.GetComponent<LineRenderer>();
        renderer.positionCount = CircleSegments;
        renderer.loop = true;
        renderer.useWorldSpace = true;
        renderer.widthMultiplier = width;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        renderer.sortingOrder = sortingOrder;

        if (_lineMaterial != null)
        {
            renderer.material = _lineMaterial;
        }
        else if (LibraryMaterials.instance != null)
        {
            renderer.sharedMaterial = LibraryMaterials.instance.mat_world_object;
        }

        renderer.enabled = false;
        return renderer;
    }

    private static Material CreateRenderMaterial(Color color)
    {
        var shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Transparent")
                     ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                     ?? Shader.Find("Unlit/Color");
        if (shader == null) return null;

        var material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };
        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        ConfigureTransparentMaterial(material);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }
    }

    private static Mesh CreateUnitCircleMesh()
    {
        var vertices = new Vector3[CircleSegments + 1];
        var triangles = new int[CircleSegments * 3];
        vertices[0] = Vector3.zero;

        for (var i = 0; i < CircleSegments; i++)
        {
            var angle = i / (float)CircleSegments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == CircleSegments - 1 ? 1 : i + 2;
        }

        var mesh = new Mesh
        {
            name = "CultiwayCastRangeFillMesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateSelectionMarkerPool()
    {
        var root = new GameObject("CultiwayControlledSkillTargetMarkers");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;
        _markerRoot = root.transform;

        var prefabObj = CreateSelectionMarkerPrefab();
        prefabObj.transform.SetParent(_markerRoot, false);
        prefabObj.SetActive(false);

        var prefab = prefabObj.GetComponent<SelectionMarker>();
        _selectionMarkerPool = new MonoObjPool<SelectionMarker>(
            prefab,
            _markerRoot,
            marker => marker.EnsureInitialized(),
            marker => marker.EnsureInitialized(),
            marker => marker.Hide());
    }

    private static GameObject CreateSelectionMarkerPrefab()
    {
        var original = Resources.Load<GameObject>(SelectionEffectPath);
        var obj = original != null
            ? Object.Instantiate(original)
            : new GameObject("CultiwaySkillTargetSelectionMarkerPrefab", typeof(SpriteRenderer), typeof(SpriteAnimation));

        obj.name = "CultiwaySkillTargetSelectionMarkerPrefab";
        if (obj.TryGetComponent<UnitSelectionEffect>(out var vanillaSelectionEffect))
        {
            vanillaSelectionEffect.enabled = false;
        }

        if (!obj.TryGetComponent<SpriteRenderer>(out _))
        {
            obj.AddComponent<SpriteRenderer>();
        }

        if (!obj.TryGetComponent<SpriteAnimation>(out var animation))
        {
            animation = obj.AddComponent<SpriteAnimation>();
        }

        if (animation.frames == null || animation.frames.Length == 0)
        {
            animation.frames = LoadSelectionFrames();
        }

        var marker = obj.GetComponent<SelectionMarker>() ?? obj.AddComponent<SelectionMarker>();
        marker.EnsureInitialized();
        return obj;
    }

    private static Sprite[] LoadSelectionFrames()
    {
        return SpriteTextureLoader.getSpriteList(SelectionEffectFallbackPath, true) ?? new Sprite[0];
    }

    private void UpdateCircle()
    {
        if (_circle == null) return;
        if (!ControlledCultivatorSkillControls.TryGetControlledActor(out var actor))
        {
            HideCircle();
            return;
        }

        var mousePos = (Vector3)World.world.getMousePos();
        mousePos.z = 0f;
        var caster = actor.GetExtend();
        _center = ControlledCultivatorSkillControls.ClampSkillTargetPos(caster, mousePos);
        float effectRadius = ControlledCultivatorSkillControls.ResolveSelectedAbilityEffectRadius(caster);
        _displayRadius = effectRadius > 0f ? effectRadius : _radius;
        var area = new SkillTargetSelectionArea(true, _center, _displayRadius);
        var targets = ControlledCultivatorSkillControls.CollectManualTargets(actor, area,
            World.world.kingdoms_wild.get("possessed"));
        var color = effectRadius > 0f || targets.Count > 0
            ? new Color(0.15f, 0.9f, 1f, 0.85f)
            : new Color(1f, 0.35f, 0.2f, 0.65f);

        _circle.startColor = color;
        _circle.endColor = color;
        _circle.enabled = true;

        for (var i = 0; i < CircleSegments; i++)
        {
            var angle = i / (float)CircleSegments * Mathf.PI * 2f;
            _circlePoints[i] = new Vector3(
                _center.x + Mathf.Cos(angle) * _displayRadius,
                _center.y + Mathf.Sin(angle) * _displayRadius,
                _center.z + 0.25f);
        }

        _circle.SetPositions(_circlePoints);
        UpdateCastRangeFill(actor, caster);
        UpdateSelectionMarkers(actor, targets);
    }

    private void UpdateCastRangeFill(Actor actor, ActorExtend caster)
    {
        if (_castRangeFill == null || _castRangeFillTransform == null || caster == null || caster.Base.isRekt()) return;

        var center = actor.current_position;
        var range = Mathf.Max(0.1f, ControlledCultivatorSkillControls.ResolveSelectedAbilityRange(caster));
        if (_castRangeMaterial != null && _castRangeMaterial.HasProperty("_Color"))
        {
            _castRangeMaterial.color = CastRangeColor;
        }

        _castRangeFillTransform.position = new Vector3(center.x, center.y, 0.2f);
        _castRangeFillTransform.localScale = new Vector3(range, range, 1f);
        _castRangeFill.enabled = true;
    }

    private void UpdateSelectionMarkers(Actor actor, IReadOnlyList<BaseSimObject> targets)
    {
        if (_selectionMarkerPool == null) return;

        _selectionMarkerPool.ResetToStart();
        var color = World.world.getArchitectColor();
        color.a = 0.8f;

        if (targets.Count == 0)
        {
            ShowSelectionMarker(_center, actor.current_scale, color);
        }
        else
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || target.isRekt()) continue;
                ShowSelectionMarker(target.current_position, GetSelectionScale(target), color);
            }
        }

        _selectionMarkerPool.ClearUnsed();
    }

    private void ShowSelectionMarker(Vector3 position, Vector3 scale, Color color)
    {
        var marker = _selectionMarkerPool.GetNext();
        marker.Show(position, scale, color, Time.deltaTime);
    }

    private static Vector3 GetSelectionScale(BaseSimObject target)
    {
        var scale = target.current_scale;
        if (scale.sqrMagnitude > 0.0001f) return scale;

        var size = Mathf.Max(target.stats[S.size], 0.1f);
        var uniform = Mathf.Clamp(size * 0.1f, 0.08f, 0.6f);
        return new Vector3(uniform, uniform, 1f);
    }

    private void HideCircle()
    {
        if (_circle != null)
        {
            _circle.enabled = false;
        }

        if (_castRangeFill != null)
        {
            _castRangeFill.enabled = false;
        }

        _selectionMarkerPool?.Clear();
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
        }

        if (_castRangeMaterial != null)
        {
            Destroy(_castRangeMaterial);
        }

        if (_castRangeMesh != null)
        {
            Destroy(_castRangeMesh);
        }
    }

    private sealed class SelectionMarker : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private SpriteAnimation _animation;
        private bool _initialized;

        public void EnsureInitialized()
        {
            if (_initialized) return;

            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null)
            {
                _renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
                _renderer.sortingOrder = 55;
                if (LibraryMaterials.instance != null)
                {
                    _renderer.sharedMaterial = LibraryMaterials.instance.mat_world_object;
                }
            }

            _animation = GetComponent<SpriteAnimation>();
            if (_animation != null)
            {
                if (_animation.frames == null || _animation.frames.Length == 0)
                {
                    _animation.frames = LoadSelectionFrames();
                }

                _animation.create();
                _animation.resetAnim();
            }

            _initialized = true;
        }

        public void Show(Vector3 position, Vector3 scale, Color color, float elapsed)
        {
            EnsureInitialized();

            transform.position = position;
            transform.localScale = scale.sqrMagnitude > 0.0001f ? scale : Vector3.one * 0.1f;
            if (_renderer != null)
            {
                _renderer.color = color;
            }

            _animation?.update(elapsed);
        }

        public void Hide()
        {
            transform.position = Globals.POINT_IN_VOID;
        }
    }
}

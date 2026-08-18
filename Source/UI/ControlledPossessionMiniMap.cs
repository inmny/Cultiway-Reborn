using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

/// <summary>附体操控期间显示在左上角的角色中心局部小地图。</summary>
internal sealed class ControlledPossessionMiniMap : MonoBehaviour
{
    private const string RootName = "CultiwayControlledPossessionMiniMap";
    private const int TextureSize = 64;
    private const int HalfExtent = TextureSize / 2;
    private const float HudSize = 144f;
    private const float HudMargin = 12f;
    private const float MapInset = 5f;
    private const float RefreshInterval = 0.25f;

    private static readonly Color32 OutsideColor = new(10, 12, 13, 235);
    private static readonly Color32 PlayerOutlineColor = new(20, 20, 20, 255);
    private static readonly Color32 PlayerColor = new(255, 220, 72, 255);
    private static readonly Color32 EnemyColor = new(239, 74, 76, 255);
    private static readonly Color32 AllyColor = new(83, 220, 130, 255);
    private static readonly Color32 NeutralColor = new(102, 196, 255, 255);

    private static ControlledPossessionMiniMap instance;

    private readonly Color32[] pixels = new Color32[TextureSize * TextureSize];
    private readonly WorldTile[] sampledTiles = new WorldTile[TextureSize * TextureSize];
    private RectTransform rootRect;
    private CanvasGroup canvasGroup;
    private RawImage mapImage;
    private Texture2D mapTexture;
    private PossessionUI boundUi;
    private Actor lastActor;
    private Actor markerOwner;
    private Func<Actor, bool> inspectActor;
    private MarkerKind pendingMarker;
    private int lastCenterX = int.MinValue;
    private int lastCenterY = int.MinValue;
    private Vector2Int lastHeading = Vector2Int.right;
    private float nextRefreshTime;
    private bool visible;

    internal static void Ensure()
    {
        if (instance != null) return;

        GameObject root = new(RootName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup),
            typeof(ControlledPossessionMiniMap));
        Transform parent = GetHudParent();
        if (parent != null) root.transform.SetParent(parent, false);
    }

    internal static void ClearWorldState()
    {
        instance?.ResetState(true);
    }

    private void Awake()
    {
        instance = this;
        rootRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        inspectActor = InspectActor;
        BuildVisuals();
        ConfigureRootRect();
        ResetState(true);
    }

    private void Update()
    {
        if (!EnsureBound() || !TryGetControlledActor(out Actor actor) || World.world == null ||
            actor.current_tile == null || MapBox.width <= 0 || MapBox.height <= 0)
        {
            Hide();
            return;
        }

        SetVisible(true);
        WorldTile center = actor.current_tile;
        bool actorChanged = actor != lastActor;
        Vector2Int heading = ResolveHeading(actor, actorChanged);
        bool visualChanged = actorChanged || center.x != lastCenterX || center.y != lastCenterY ||
                             heading != lastHeading;
        if (!visualChanged && Time.unscaledTime < nextRefreshTime) return;

        lastHeading = heading;
        Render(actor, center);
        lastActor = actor;
        lastCenterX = center.x;
        lastCenterY = center.y;
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
    }

    private void BuildVisuals()
    {
        Image frame = GetComponent<Image>();
        UiResources.ApplySurface(frame, UiSurface.WindowInner);
        frame.raycastTarget = false;

        GameObject mapObject = new("Map", typeof(RectTransform), typeof(RawImage));
        mapObject.transform.SetParent(transform, false);
        mapImage = mapObject.GetComponent<RawImage>();
        mapImage.raycastTarget = false;
        mapImage.color = Color.white;
        UiLayout.Stretch(mapImage.rectTransform, MapInset, MapInset, MapInset, MapInset);

        mapTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false)
        {
            name = "Cultiway_ControlledPossessionMiniMap",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        mapImage.texture = mapTexture;
    }

    private bool EnsureBound()
    {
        Transform parent = GetHudParent();
        if (PossessionUI.instance == null || parent == null) return false;
        if (boundUi == PossessionUI.instance && transform.parent == parent) return true;

        boundUi = PossessionUI.instance;
        transform.SetParent(parent, false);
        ConfigureRootRect();
        transform.SetAsLastSibling();
        return true;
    }

    private static Transform GetHudParent()
    {
        return CanvasMain.instance?.canvas_ui?.transform.Find("CanvasParent");
    }

    private void ConfigureRootRect()
    {
        if (rootRect == null) return;

        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(HudMargin, -HudMargin);
        rootRect.sizeDelta = new Vector2(HudSize, HudSize);
        rootRect.localScale = Vector3.one;
    }

    private Vector2Int ResolveHeading(Actor actor, bool actorChanged)
    {
        Vector2 movement = ControllableUnit.getMovementVector();
        if (movement.sqrMagnitude < 0.09f)
        {
            if (!actorChanged) return lastHeading;
            return actor.is_looking_left ? Vector2Int.left : Vector2Int.right;
        }

        int x = 0;
        int y = 0;
        if (Mathf.Abs(movement.x) >= 0.2f) x = movement.x > 0f ? 1 : -1;
        if (Mathf.Abs(movement.y) >= 0.2f) y = movement.y > 0f ? 1 : -1;
        return new Vector2Int(x, y);
    }

    private void Render(Actor actor, WorldTile center)
    {
        int minX = center.x - HalfExtent;
        int minY = center.y - HalfExtent;
        for (int localY = 0; localY < TextureSize; localY++)
        {
            int worldY = minY + localY;
            for (int localX = 0; localX < TextureSize; localX++)
            {
                int index = localY * TextureSize + localX;
                int worldX = minX + localX;
                if (worldX < 0 || worldY < 0 || worldX >= MapBox.width || worldY >= MapBox.height)
                {
                    sampledTiles[index] = null;
                    pixels[index] = OutsideColor;
                    continue;
                }

                WorldTile tile = World.world.GetTile(worldX, worldY);
                sampledTiles[index] = tile;
                pixels[index] = GetMapColor(tile);
            }
        }

        markerOwner = actor;
        for (int index = 0; index < sampledTiles.Length; index++)
        {
            WorldTile tile = sampledTiles[index];
            if (tile == null || !tile.hasUnits()) continue;

            pendingMarker = MarkerKind.None;
            tile.doUnits(inspectActor);
            if (pendingMarker == MarkerKind.None) continue;
            pixels[index] = GetMarkerColor(pendingMarker);
        }
        markerOwner = null;

        DrawPlayerMarker();
        mapTexture.SetPixels32(pixels);
        mapTexture.Apply(false, false);
    }

    private static Color32 GetMapColor(WorldTile tile)
    {
        if (tile == null) return OutsideColor;
        Color32 color = tile.getColor();
        if (!tile.hasBuilding()) return color;

        Color32 buildingColor = tile.building.getColorForMinimap(tile);
        return buildingColor.a == 0 ? color : buildingColor;
    }

    private bool InspectActor(Actor candidate)
    {
        if (candidate == null || candidate == markerOwner) return true;

        MarkerKind marker;
        Kingdom ownerKingdom = markerOwner?.kingdom;
        if (ownerKingdom != null && candidate.kingdom != null && ownerKingdom.isEnemy(candidate.kingdom))
        {
            marker = MarkerKind.Enemy;
        }
        else if (ownerKingdom != null && candidate.kingdom == ownerKingdom)
        {
            marker = MarkerKind.Ally;
        }
        else
        {
            marker = MarkerKind.Neutral;
        }

        if (marker > pendingMarker) pendingMarker = marker;
        return pendingMarker != MarkerKind.Enemy;
    }

    private static Color32 GetMarkerColor(MarkerKind marker)
    {
        return marker switch
        {
            MarkerKind.Enemy => EnemyColor,
            MarkerKind.Ally => AllyColor,
            _ => NeutralColor
        };
    }

    private void DrawPlayerMarker()
    {
        int directionX = lastHeading.x;
        int directionY = lastHeading.y;
        int perpendicularX = -directionY;
        int perpendicularY = directionX;
        int tipX = HalfExtent + directionX * 3;
        int tipY = HalfExtent + directionY * 3;
        int arrowBaseX = tipX - directionX;
        int arrowBaseY = tipY - directionY;
        int wingAX = arrowBaseX + perpendicularX;
        int wingAY = arrowBaseY + perpendicularY;
        int wingBX = arrowBaseX - perpendicularX;
        int wingBY = arrowBaseY - perpendicularY;

        DrawCross(HalfExtent, HalfExtent, 2, PlayerOutlineColor);
        for (int step = 1; step <= 3; step++)
        {
            DrawCross(HalfExtent + directionX * step, HalfExtent + directionY * step, 1,
                PlayerOutlineColor);
        }
        DrawCross(wingAX, wingAY, 1, PlayerOutlineColor);
        DrawCross(wingBX, wingBY, 1, PlayerOutlineColor);

        DrawCross(HalfExtent, HalfExtent, 1, PlayerColor);
        for (int step = 1; step <= 3; step++)
        {
            SetPixel(HalfExtent + directionX * step, HalfExtent + directionY * step, PlayerColor);
        }
        SetPixel(wingAX, wingAY, PlayerColor);
        SetPixel(wingBX, wingBY, PlayerColor);
    }

    private void DrawCross(int centerX, int centerY, int radius, Color32 color)
    {
        for (int offset = -radius; offset <= radius; offset++)
        {
            SetPixel(centerX + offset, centerY, color);
            SetPixel(centerX, centerY + offset, color);
        }
    }

    private void SetPixel(int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= TextureSize || y >= TextureSize) return;
        pixels[y * TextureSize + x] = color;
    }

    private void Hide()
    {
        if (!visible && lastActor == null)
        {
            SetVisible(false);
            return;
        }
        lastActor = null;
        markerOwner = null;
        lastCenterX = int.MinValue;
        lastCenterY = int.MinValue;
        lastHeading = Vector2Int.right;
        nextRefreshTime = 0f;
        SetVisible(false);
    }

    private void ResetState(bool clearTexture)
    {
        boundUi = null;
        Hide();
        if (!clearTexture || mapTexture == null) return;

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = OutsideColor;
            sampledTiles[i] = null;
        }
        mapTexture.SetPixels32(pixels);
        mapTexture.Apply(false, false);
    }

    private void SetVisible(bool state)
    {
        visible = state;
        if (canvasGroup == null) return;
        canvasGroup.alpha = state ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private static bool TryGetControlledActor(out Actor actor)
    {
        actor = null;
        if (!ControllableUnit.isControllingUnit()) return false;

        actor = ControllableUnit.getControllableUnit();
        return actor != null && !actor.isRekt();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(instance, this)) instance = null;
        if (mapTexture != null) Object.Destroy(mapTexture);
        mapTexture = null;
        mapImage = null;
    }

    private enum MarkerKind : byte
    {
        None,
        Neutral,
        Ally,
        Enemy
    }
}

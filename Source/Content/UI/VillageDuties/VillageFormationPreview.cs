using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.VillageDuties;

/// <summary>以固定方格点显示指挥者和成员，不创建假人物或改动实际站位。</summary>
internal sealed class VillageFormationPreview : MonoBehaviour
{
    /// <summary>复用图像宽度。</summary>
    private const int Width = 480;
    /// <summary>复用图像高度。</summary>
    private const int Height = 128;
    /// <summary>复用像素。</summary>
    private readonly Color32[] pixels = new Color32[Width * Height];
    /// <summary>唯一运行期纹理。</summary>
    private Texture2D texture;
    /// <summary>标题和图例。</summary>
    private Text heading;

    /// <summary>在所属页面建立一次预览。</summary>
    /// <param name="parent">编制页面容器。</param>
    internal static VillageFormationPreview Create(Transform parent)
    {
        var obj = UiLayout.Create(parent, "FormationPreview", false, 500, 158, UiTheme.Current.Metrics.SpacingSm);
        var preview = obj.AddComponent<VillageFormationPreview>();
        preview.heading = UiElements.CreateText(obj.transform, "Legend", "", Width, 24, 7, TextAnchor.MiddleLeft);
        var diagram = new GameObject("Points", typeof(RectTransform), typeof(RawImage));
        diagram.transform.SetParent(obj.transform, false); UiLayout.SetSize(diagram.transform, Width, Height);
        var image = diagram.GetComponent<RawImage>(); image.raycastTarget = false;
        preview.texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, name = "Cultiway_VillageFormation" };
        image.texture = preview.texture;
        return preview;
    }

    /// <summary>无选中单位时预览草稿小队；选择营或整军时显示实际成员的分层阵列。</summary>
    /// <param name="city">唯一军队所属村庄。</param>
    /// <param name="data">实际成员与正式指挥者。</param>
    /// <param name="draft">尚未提交的编制草稿。</param>
    /// <param name="unitId">当前选择的营或小队编号。</param>
    /// <param name="level">小队、营、整军的预览层级。</param>
    internal void Bind(City city, VillageMilitaryData data, VillageDutyPolicy draft, int unitId, int level)
    {
        var points = new List<(Vector2 position, int kind)>();
        if (level == 0)
        {
            int count = Math.Min(draft.SquadSize, 1024);
            var squad = data.Squads.FirstOrDefault(s => s.Id == unitId);
            int leaderSlot = squad != null && squad.Members.TryGetValue(squad.Commander, out int position) ? position : 0;
            var occupied = squad?.Members.ToDictionary(p => p.Value, p => p.Key);
            for (int slot = 0; slot < count; slot++)
            {
                long actorId = occupied != null && occupied.TryGetValue(slot, out long id) ? id : 0;
                int kind = squad == null ? (slot == 0 ? 2 : 1) : actorId == 0 ? 0 :
                    actorId == squad.Commander ? 2 : actorId == squad.DutyLeader ? 3 : 1;
                points.Add((slot == leaderSlot ? Vector2Int.zero : VillageRectangularFormationPlanner.MemberOffset(draft, slot, leaderSlot), kind));
            }
        }
        else
        {
            long root = city.army?.getCaptain()?.id ?? 0;
            var squads = level == 1 ? data.Squads.Where(s => s.BattalionId == unitId) : data.Squads;
            Vector2 origin = Vector2.zero;
            foreach (var squad in squads)
            foreach (long id in squad.Members.Keys)
            {
                var actor = World.world.units.get(id);
                if (actor == null || !actor.isAlive()) continue;
                var point = VillageRectangularFormationPlanner.ArmyOffset(data, squad, actor, root);
                points.Add((point, id == squad.Commander || id == root ? 2 : id == squad.DutyLeader ? 3 : 1));
            }
            if (points.Count > 0 && level == 1) origin = points.OrderByDescending(p => p.position.y).First().position;
            if (level == 1) for (int i = 0; i < points.Count; i++) points[i] = (points[i].position - origin, points[i].kind);
        }
        float width = points.Count == 0 ? 0 : points.Max(p => p.position.x) - points.Min(p => p.position.x) + 1;
        float depth = points.Count == 0 ? 0 : points.Max(p => p.position.y) - points.Min(p => p.position.y) + 1;
        heading.text = WindowVillageDuties.T("FormationLegend") + "\n" + WindowVillageDuties.F("FormationSize", width, depth);
        Draw(points);
    }

    /// <summary>按实际范围等比例缩放，保留规则方格和位于前方的指挥点。</summary>
    /// <param name="points">要显示的点位及其身份颜色。</param>
    private void Draw(List<(Vector2 position, int kind)> points)
    {
        Array.Clear(pixels, 0, pixels.Length);
        if (points.Count > 0)
        {
            float minX = points.Min(p => p.position.x), maxX = points.Max(p => p.position.x);
            float minY = points.Min(p => p.position.y), maxY = points.Max(p => p.position.y);
            float scale = Math.Min((Width - 20f) / Math.Max(4, maxX - minX), (Height - 20f) / Math.Max(4, maxY - minY));
            foreach (var point in points)
            {
                int x = (int)((Width - (maxX - minX) * scale) * .5f + (point.position.x - minX) * scale);
                int y = (int)((Height - (maxY - minY) * scale) * .5f + (point.position.y - minY) * scale);
                Color32 color = point.kind switch { 0 => UiTheme.Current.Palette.MutedText,
                    2 => UiTheme.Current.Palette.Warning, 3 => UiTheme.Current.Palette.Success, _ => UiTheme.Current.Palette.PrimaryText };
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                    if ((uint)(x + dx) < Width && (uint)(y + dy) < Height) pixels[(y + dy) * Width + x + dx] = color;
            }
        }
        texture.SetPixels32(pixels); texture.Apply(false, false);
    }

    /// <summary>窗口销毁时释放运行期图像。</summary>
    private void OnDestroy() { if (texture != null) Destroy(texture); }
}

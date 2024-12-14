using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>
///     UI多边形
/// </summary>
public class PolygonComponent : MaskableGraphic
{
    [SerializeField] private Texture texture;

    public bool fill = true;

    [Range(3, 360)] public int sides = 3;

    [Range(0, 360)] public float rotation;

    [Range(0, 1)] public float[] verticesDistances = new float[3];

    private float Size => Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);

    public override Texture mainTexture => texture == null ? s_WhiteTexture : texture;

    public Texture Texture
    {
        get => texture;
        set
        {
            if (texture == value) return;
            texture = value;
            SetVerticesDirty();
            SetMaterialDirty();
        }
    }

    public void DrawPolygon(int count)
    {
        sides = count;
        verticesDistances = new float[count + 1];
        for (var i = 0; i < count; i++) verticesDistances[i] = 1;
    }

    public void DrawPolygon(List<float> datas)
    {
        var final_datas = new List<float>(datas);
        sides = final_datas.Count;
        final_datas.Add(final_datas[0]);
        verticesDistances = final_datas.ToArray();
        SetVerticesDirty();
    }

    private UIVertex[] SetVertexs(Vector2[] vertices, Vector2[] uvs)
    {
        var vertexs = new UIVertex[4];
        for (var i = 0; i < vertices.Length; i++)
        {
            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;
            vert.position = vertices[i];
            vert.uv0 = uvs[i];
            vertexs[i] = vert;
        }

        return vertexs;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 prev_x = Vector2.zero;
        Vector2 prev_y = Vector2.zero;
        Vector2 uv0;
        Vector2 uv1;
        Vector2 uv2;
        Vector2 uv3;
        Vector2 pos0;
        Vector2 pos1;
        Vector2 pos2;
        Vector2 pos3;
        var degrees = 360f / sides;
        var vertices = sides + 1;
        if (verticesDistances.Length != vertices)
        {
            verticesDistances = new float[vertices];
            for (var i = 0; i < vertices - 1; i++) verticesDistances[i] = 1;
        }

        verticesDistances[vertices - 1] = verticesDistances[0];
        for (var i = 0; i < vertices; i++)
        {
            var outer = -rectTransform.pivot.x * Size * verticesDistances[i];
            var inner = -rectTransform.pivot.x * Size * (verticesDistances[i] + 0.02f);
            var rad = Mathf.Deg2Rad            * (i * degrees                 + rotation);
            var c = Mathf.Cos(rad);
            var s = Mathf.Sin(rad);
            uv0 = new Vector2(0, 1);
            uv1 = new Vector2(1, 1);
            uv2 = new Vector2(1, 0);
            uv3 = new Vector2(0, 0);
            pos0 = prev_x;
            pos1 = new Vector2(outer * c, outer * s);
            if (fill)
            {
                pos2 = Vector2.zero;
                pos3 = Vector2.zero;
            }
            else
            {
                pos2 = new Vector2(inner * c, inner * s);
                pos3 = prev_y;
            }

            prev_x = pos1;
            prev_y = pos2;
            vh.AddUIVertexQuad(SetVertexs(new[] { pos0, pos1, pos2, pos3 }, new[] { uv0, uv1, uv2, uv3 }));
        }
    }
}
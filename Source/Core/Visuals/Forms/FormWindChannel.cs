using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal sealed partial class FormResourceGeneration
{
    private Mesh windChannelWalls, windChannelCrest;

    /// <summary>随资源版本共享的两排折面，实际尺寸由当次风槽给出。</summary>
    internal Mesh WindChannelWalls => windChannelWalls ??= BuildWindChannel(false);

    /// <summary>有宽度的流团沿两排折面向出口运动。</summary>
    internal Mesh WindChannelCrest => windChannelCrest ??= BuildWindChannel(true);

    private Mesh BuildWindChannel(bool crest)
    {
        const int segments = 36;
        Vector2[] profile = crest
            ? new[] { new Vector2(.32f, .10f), new Vector2(.48f, .61f),
                new Vector2(.71f, .76f), new Vector2(.90f, .34f), new Vector2(.80f, .18f) }
            : new[] { new Vector2(.29f, .03f), new Vector2(.58f, .16f), new Vector2(.65f, .65f),
                new Vector2(.76f, .89f), new Vector2(.93f, .55f), new Vector2(1f, .04f) };
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int side = -1; side <= 1; side += 2)
        {
            int start = vertices.Count;
            for (int segment = 0; segment <= segments; segment++)
            {
                float along = (float)segment / segments;
                float width = crest ? Mathf.Sin(along * Mathf.PI) : ConstructionWindChannel.WidthAt(along);
                float end = crest ? width : Mathf.Sin(along * Mathf.PI) * .66f + .34f;
                float fold = .86f + Mathf.Sin(along * 31f + side * 1.7f) * .10f +
                             Mathf.Sin(along * 76f - side) * .04f;
                for (int layer = 0; layer < profile.Length; layer++)
                {
                    Vector2 shape = profile[layer];
                    vertices.Add(new Vector3(along - .5f, side * width * shape.x,
                        .024f + shape.y * end * fold));
                    uv.Add(new Vector2(along * 4f, (float)layer / (profile.Length - 1)));
                    if (segment == 0 || layer == 0) continue;
                    int a = start + segment * profile.Length + layer;
                    int b = a - 1, c = a - profile.Length, d = c - 1;
                    if (side < 0)
                    {
                        triangles.Add(d); triangles.Add(b); triangles.Add(a);
                        triangles.Add(d); triangles.Add(a); triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(d); triangles.Add(a); triangles.Add(b);
                        triangles.Add(d); triangles.Add(c); triangles.Add(a);
                    }
                }
            }
        }
        var mesh = new Mesh { name = crest ? "Cultiway Wind Channel Crest" : "Cultiway Wind Channel Walls" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        Own(mesh);
        return mesh;
    }
}

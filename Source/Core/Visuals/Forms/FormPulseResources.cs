using System;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal sealed partial class FormResourceGeneration
{
    /// <summary>建立立体圆面，厚度、轮廓与接触等比，共用同一个资源版本。</summary>
    internal void CreatePulseForms()
    {
        CreatePulseForm("pressure_wave", "pressure_wave", 0.11f, 8,
            [new(.62f, .015f), new(.69f, .078f), new(.76f, .032f), new(.84f, .10f), new(.92f, .177f),
                new(.975f, .099f), new(1f, .033f), new(.985f, .008f), new(.62f, .008f)]);
        CreatePulseForm("purifying_curtain", "purifying_curtain", 0.24f, 12,
            [new(.53f, .01f), new(.66f, .028f), new(.78f, .082f), new(.85f, .12f),
                new(.95f, .10f), new(1f, .027f), new(.98f, .008f), new(.53f, .006f)]);
        CreatePulseForm("seal_crush", "seal_crush", 0.56f, 7,
            [new(.36f, .052f), new(.49f, .094f), new(.58f, .178f), new(.65f, .118f), new(.72f, .162f),
                new(.83f, .092f), new(.94f, .076f), new(1f, .041f), new(.97f, .029f), new(.36f, .025f)]);
        CreatePulseForm("edict_seal", "edict_seal", .46f, 8,
            [new(.77f, .029f), new(.80f, .13f), new(.85f, .105f), new(.89f, .06f), new(.94f, .07f),
                new(.97f, .103f), new(1f, .056f), new(.985f, .03f), new(.77f, .017f)]);
        CreatePulseForm("meridian_seal", "meridian_seal", .88f, 4,
            [new(.73f, .08f), new(.71f, .45f), new(.67f, .89f), new(.55f, 1.16f), new(.47f, 1.23f),
                new(.83f, .81f), new(.91f, .48f), new(1f, .17f), new(.93f, .08f)]);
        CreateRadialPanels("myriad_banner", 10, Vector3.up * .036f,
            [new(0f, 1f, .046f), new(-.17f, .94f, .16f), new(-.15f, .82f, .37f),
                new(0f, .76f, .49f), new(.15f, .82f, .37f), new(.17f, .94f, .16f)]);
        CreateRadialPanels("command_banner", 8, Vector3.forward * .029f,
            [new(0f, .28f, .056f), new(.16f, .51f, .085f), new(.16f, .91f, .133f),
                new(0f, 1f, .11f), new(-.16f, .91f, .133f), new(-.16f, .51f, .085f)]);
        CreatePulseForm("pearl_phase", "pearl_phase", .24f, 5,
            [new(.04f, .39f), new(.26f, .442f), new(.52f, .393f), new(.69f, .29f),
                new(.85f, .183f), new(.96f, .082f), new(1f, .033f), new(.97f, .011f), new(.04f, .008f)]);
        CreateRadialPanels("suppression_field", 8, Vector3.forward * .095f,
            [new(-.17f, .46f, .23f), new(-.23f, .77f, .39f), new(0f, 1f, .17f),
                new(.23f, .77f, .39f), new(.17f, .46f, .23f)]);
        CreatePulseForm("ding_fire_rim", "ding_fire_rim", .58f, 8,
            [new(.51f, .023f), new(.59f, .053f), new(.64f, .142f), new(.70f, .064f),
                new(.82f, .226f), new(.88f, .127f), new(.94f, .081f), new(1f, .035f),
                new(.97f, .012f), new(.51f, .008f)], twist: .48f);
        CreateDingFunnels();
        CreatePulseForm("tower_prison", "tower_prison", .42f, 8,
            [new(.65f, .027f), new(.69f, .106f), new(.71f, .238f), new(.74f, .253f),
                new(.79f, .171f), new(.85f, .162f), new(.88f, .089f), new(.98f, .093f),
                new(1f, .042f), new(.98f, .016f), new(.65f, .014f)]);
        CreatePulseForm("tower_shadow_field", "tower_shadow_field", .34f, 7,
            [new(.51f, .020f), new(.62f, .053f), new(.71f, .112f), new(.79f, .087f),
                new(.88f, .139f), new(.96f, .074f), new(1f, .026f), new(.96f, .009f), new(.51f, .008f)]);
        CreatePulseForm("tower_ward", "tower_ward", .19f, 9,
            [new(.43f, .055f), new(.48f, .143f), new(.57f, .266f), new(.61f, .272f),
                new(.70f, .138f), new(.79f, .128f), new(.85f, .149f), new(.93f, .093f),
                new(1f, .039f), new(.98f, .018f), new(.43f, .014f)]);
        CreatePulseForm("gourd_vortex", "gourd_vortex", .60f, 6,
            [new(.24f, .09f), new(.30f, .16f), new(.42f, .11f), new(.54f, .18f),
                new(.66f, .10f), new(.78f, .14f), new(.91f, .087f), new(1f, .034f),
                new(.94f, .010f), new(.24f, .018f)], twist: 1.6f);
        CreateRadialPanels("truth_mirror", 10, Vector3.forward * .025f,
            [new(.07f, .47f, .047f), new(.16f, .75f, .09f), new(.14f, .95f, .105f),
                new(0f, 1f, .112f), new(-.14f, .95f, .105f), new(-.16f, .75f, .09f), new(-.07f, .47f, .047f)],
            mirrorFace: "truth_mirror_face");
        CreateRadialPanels("soul_stasis_mirror", 2, Vector3.up * .11f,
            [new(-.39f, .63f, .14f), new(-.53f, .72f, .46f), new(-.49f, .79f, 1.12f),
                new(-.25f, .82f, 1.43f), new(.25f, .82f, 1.43f), new(.49f, .79f, 1.12f),
                new(.53f, .72f, .46f), new(.39f, .63f, .14f)],
            yaw: -55f, mirrorFace: "soul_stasis_mirror_face");
    }

    /// <summary>闭合的螺旋流槽从实际边缘垂落到鼎口，转动时保留侧壁和液体厚度。</summary>
    private void CreateDingFunnels()
    {
        const string id = "ding_devouring_funnels";
        const int streams = 5, length = 20, sides = 10;
        const float twist = 1.5f;
        var vertices = new Vector3[streams * (length + 1) * (sides + 1)];
        var uv = new Vector2[vertices.Length];
        var faces = new System.Collections.Generic.List<int>(streams * length * sides * 6);
        int stride = sides + 1;
        for (int stream = 0; stream < streams; stream++)
        {
            int start = stream * (length + 1) * stride;
            for (int along = 0; along <= length; along++)
            {
                float t = (float)along / length;
                float radius = Mathf.Lerp(.09f, 1f, t);
                float heading = 2f * Mathf.PI * stream / streams + (1f - t) * twist;
                float spread = .046f + .19f * Mathf.Sin(t * 2.6f);
                float height = .036f + .16f * t * t;
                for (int side = 0; side <= sides; side++)
                {
                    float v = (float)side / sides;
                    float cross = v * Mathf.PI * 2f;
                    float angle = heading + Mathf.Sin(cross) * spread;
                    float lip = Mathf.Cos(cross) * (.016f + .052f * t);
                    int a = start + along * stride + side;
                    vertices[a] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), height + lip);
                    uv[a] = new Vector2(t, v);
                    if (along == length || side == sides) continue;
                    int b = a + stride;
                    faces.Add(a); faces.Add(b); faces.Add(a + 1);
                    faces.Add(a + 1); faces.Add(b); faces.Add(b + 1);
                }
            }
            int end = start + length * stride;
            for (int side = 1; side < sides - 1; side++)
            {
                faces.Add(start); faces.Add(start + side); faces.Add(start + side + 1);
                faces.Add(end); faces.Add(end + side + 1); faces.Add(end + side);
            }
        }

        var mesh = new Mesh { name = "Cultiway_" + id, hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.SetTriangles(faces, 0);
        mesh.RecalculateNormals();
        PublishPulseForm(id, [id], [mesh], nameof(CreateDingFunnels),
            new FormParameterDescription { Id = "funnels", Values = [streams, twist] });
    }

    /// <summary>转动完整截面，保留实体斜面和起伏，最外缘始终等于实际单位半径。</summary>
    /// <param name="id">共用形体编号。</param><param name="surface">实际表面。</param>
    /// <param name="fold">截面的起伏程度。</param><param name="lobes">沿周向的起伏数量。</param>
    /// <param name="profile">按半径和高度排列的一圈封闭截面。</param>
    /// <param name="twist">从内向外绕转的幅度，零表示各层峰谷对齐。</param>
    private void CreatePulseForm(string id, string surface, float fold, int lobes, Vector2[] profile, float twist = 0f)
    {
        const int sides = 72;
        int stride = sides + 1;
        var vertices = new Vector3[(profile.Length + 1) * stride];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[profile.Length * sides * 6];
        int index = 0;
        for (int row = 0; row <= profile.Length; row++)
        {
            Vector2 section = profile[row % profile.Length];
            for (int side = 0; side <= sides; side++)
            {
                float u = (float)side / sides, angle = 2f * Mathf.PI * u;
                float swell = Mathf.Cos((angle + section.x * twist) * lobes);
                float radius = section.x - fold * (1f - section.x) * (1f - swell) * .32f;
                float height = section.y * (1f + fold * swell);
                int a = row * stride + side;
                vertices[a] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), height);
                uv[a] = new Vector2(u, (float)row / profile.Length);
                if (side == sides || row == profile.Length) continue;
                int b = a + stride;
                triangles[index++] = a;
                triangles[index++] = b;
                triangles[index++] = a + 1;
                triangles[index++] = a + 1;
                triangles[index++] = b;
                triangles[index++] = b + 1;
            }
        }
        var mesh = new Mesh { name = "Cultiway_" + id, hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        PublishPulseForm(id, [surface], [mesh], nameof(CreatePulseForm),
            new FormParameterDescription { Id = "folds", Values = twist == 0f ? [lobes, fold] : [lobes, fold, twist] });
    }

    /// <summary>按真实作用半径排开独立的面，保留实体厚度及各面之间的留白。</summary>
    private void CreateRadialPanels(string id, int count, Vector3 depth, Vector3[] outline, float yaw = 0f,
        string mirrorFace = null)
    {
        var builder = new SpatialVisualMeshBuilder();
        var inlays = mirrorFace != null ? new SpatialVisualMeshBuilder() : null;
        Vector3 panelCenter = Vector3.zero;
        foreach (Vector3 vertex in outline) panelCenter += vertex;
        panelCenter /= outline.Length;
        var front = new Vector3[outline.Length];
        var back = new Vector3[outline.Length];
        for (int i = 0; i < count; i++)
        {
            Quaternion turn = Quaternion.AngleAxis((i + .5f) * 360f / count + yaw, Vector3.forward);
            Vector3 thickness = turn * depth;
            for (int j = 0; j < outline.Length; j++)
            {
                front[j] = turn * outline[j];
                back[outline.Length - 1 - j] = front[j] - thickness;
            }
            if (inlays != null)
            {
                Vector3 center = turn * panelCenter;
                // 内嵌镜面两边都可见，边缘比镜面厚，旋转相机时仍能看见材质分层。
                var polished = Array.ConvertAll(front, point => Vector3.Lerp(center, point, .79f) + thickness * .14f);
                inlays.AddFace(polished, Color.white);
                Array.Reverse(polished);
                for (int j = 0; j < polished.Length; j++) polished[j] -= thickness * 1.28f;
                inlays.AddFace(polished, Color.white);
            }
            builder.AddFace(front, Color.white);
            builder.AddFace(back, Color.white);
            for (int j = 0; j < outline.Length; j++)
            {
                int next = (j + 1) % outline.Length;
                builder.AddFace([front[next], front[j], front[j] - thickness, front[next] - thickness], Color.white);
            }
        }
        Mesh frame = Bake(builder, id);
        string[] surfaces = inlays == null ? [id] : [id, mirrorFace];
        Mesh[] meshes = inlays == null ? [frame] : [frame, Bake(inlays, mirrorFace)];
        PublishPulseForm(id, surfaces, meshes, nameof(CreateRadialPanels),
            new FormParameterDescription { Id = "panels", Values = [count, depth.magnitude] });

        Mesh Bake(SpatialVisualMeshBuilder geometry, string name)
        {
            SpatialVisualMesh panels = geometry.Build(name);
            return new Mesh
            {
                name = "Cultiway_" + name, hideFlags = HideFlags.DontSave,
                vertices = panels.Vertices, normals = panels.Normals, triangles = panels.Triangles,
                uv = Array.ConvertAll(panels.Vertices, point => new Vector2(point.x, point.y))
            };
        }
    }

    /// <summary>圆面沿用同一个实际接触形状及收束时长，外轮廓由各招模型提供。</summary>
    private void PublishPulseForm(string id, string[] surfaces, Mesh[] meshes, string entry,
        FormParameterDescription geometry)
    {
        const float dissolve = .25f;
        foreach (Mesh mesh in meshes)
        {
            mesh.RecalculateBounds();
            Own(mesh);
        }
        var parts = new FormPartDescription[surfaces.Length];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = new FormPartDescription
            {
                Id = i == 0 ? "front" : "inlay", Roles = [i == 0 ? "wave.front" : "mirror.face"],
                Surface = surfaces[i], Pivot = new float[3]
            };
        var description = new FormDescription
        {
            Version = FormDescription.FormatVersion, Id = id, Forward = [0f, 1f, 0f], Up = [0f, 0f, 1f],
            Structure = new FormStructureDescription
            {
                Kind = "volume", Changes = ["transform"],
                Generation = new FormGenerationDescription
                {
                    Source = "Source/Core/Visuals/Forms/FormPulseResources.cs", Entry = entry, Parameters = [geometry]
                }
            },
            Parts = parts,
            Anchors = Array.Empty<FormAnchorDescription>(),
            Controls = [new FormControlDescription { Id = "wave", Position = new float[3], Rotation = [0f, 0f, 0f, 1f] }],
            Contacts =
            [
                new FormContactDescription
                {
                    Id = "reach", Part = 0, A = new float[3],
                    Sector = new FormContactSector { Radius = 1f, Angle = 360f, HalfThickness = .04f }
                }
            ],
            Motions =
            [
                new FormMotionDescription
                {
                    Id = "dissolve", Duration = dissolve,
                    Keys =
                    [
                        new FormMotionKeyDescription { Positions = new float[3], Rotations = [0f, 0f, 0f, 1f] },
                        new FormMotionKeyDescription { At = dissolve, Fade = 1f, Positions = new float[3], Rotations = [0f, 0f, 0f, 1f] }
                    ]
                }
            ]
        };
        var resource = new FormResource(description, meshes, this);
        forms.Add(id, resource);
        references.Add(new WeakReference<FormResource>(resource));
        foreach (string surface in surfaces)
        {
            Surface(surface, true);
            Surface(surface, false);
        }
    }
}

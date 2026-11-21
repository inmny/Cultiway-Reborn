using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>把共同姿态绘制到窗口纹理，既不发布到世界也不修改场景的材质或相机。</summary>
internal sealed class FormPreviewViewport : IDisposable
{
    internal readonly RenderTexture Image = new(640, 320, 24, RenderTextureFormat.ARGB32)
        { name = "Cultiway.ConstructionPreview", hideFlags = HideFlags.DontSave };
    private readonly CommandBuffer commands = new() { name = "Cultiway.能力形体预览" };
    private readonly MaterialPropertyBlock properties = new();
    private readonly List<Vector3> lines = new();
    private readonly List<int> lineIndices = new();
    private readonly Mesh outline = new() { name = "Cultiway.PreviewContacts", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
    private readonly Material wire;
    private Bounds bounds;

    internal FormPreviewViewport(Vector3 source, Vector3 target)
    {
        bounds = new Bounds(source, Vector3.one * 2f);
        bounds.Encapsulate(target);
        wire = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.DontSave };
        wire.SetColor("_Color", new Color(.3f, 1f, .8f));
        wire.SetInt("_ZTest", (int)CompareFunction.Always);
        wire.SetInt("_ZWrite", 0);
        outline.MarkDynamic();
    }

    internal void Draw(IReadOnlyList<FormVisualInstance> visuals, IReadOnlyList<AbilityCapsule> contacts, float time, bool showContacts)
    {
        lines.Clear();
        foreach (AbilityCapsule contact in contacts)
        {
            if (showContacts) AppendOutline(contact);
            if (contact.Prism != null) bounds.Encapsulate(contact.Prism.Pose.Bounds());
            else
            {
                Vector3 radius = contact.IsBox ? contact.HalfExtents : Vector3.one * contact.Radius;
                bounds.Encapsulate(contact.A - radius); bounds.Encapsulate(contact.A + radius);
                bounds.Encapsulate(contact.B - radius); bounds.Encapsulate(contact.B + radius);
            }
        }
        foreach (FormVisualInstance visual in visuals)
        for (int part = 0; part < visual.PartCount; part++)
        {
            Bounds model = visual.Layer?.Bounds ?? visual.PoseBounds ?? visual.MeshFor(part).bounds;
            Matrix4x4 pose = visual.PartTransform(part);
            Vector3 half = model.extents;
            for (int corner = 0; corner < 8; corner++)
                bounds.Encapsulate(pose.MultiplyPoint3x4(model.center + new Vector3((corner & 1) == 0 ? -half.x : half.x,
                    (corner & 2) == 0 ? -half.y : half.y, (corner & 4) == 0 ? -half.z : half.z)));
        }

        float size = Mathf.Max(1.2f, bounds.extents.magnitude * 1.15f);
        Vector3 eye = bounds.center + new Vector3(.28f, -.92f, .68f).normalized * (size * 3f);
        Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                         Matrix4x4.TRS(eye, Quaternion.LookRotation(bounds.center - eye, Vector3.forward), Vector3.one).inverse;
        float width = size * Image.width / Image.height;
        Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-width, width, -size, size, .01f, size * 6f), true);
        var area = new Rect(0, 0, Image.width, Image.height);
        commands.Clear();
        commands.SetRenderTarget(Image);
        commands.SetViewport(area);
        commands.ClearRenderTarget(true, true, new Color(.055f, .069f, .081f));
        commands.SetViewProjectionMatrices(view, projection);
        FormResource transparency = null;
        foreach (FormVisualInstance visual in visuals)
        for (int part = 0; part < visual.PartCount; part++)
        {
            var material = visual.Resource.Surface(visual.SurfaceFor(part), false);
            if (Opaque(visual, material)) DrawPart(visual, part, material, 2);
            else transparency ??= visual.Resource;
        }
        if (transparency != null)
        {
            FormTransparency.Begin(commands, Image.width, Image.height, Image);
            foreach (FormVisualInstance visual in visuals)
            for (int part = 0; part < visual.PartCount; part++)
            {
                var material = visual.Resource.Surface(visual.SurfaceFor(part), false);
                if (!Opaque(visual, material)) DrawPart(visual, part, material, 4);
            }
            FormTransparency.Resolve(commands, transparency.Generation.Transparency(), Image, Image, area);
        }
        if (lines.Count > 0)
        {
            outline.Clear();
            outline.SetVertices(lines);
            lineIndices.Clear();
            for (int i = 0; i < lines.Count; i++) lineIndices.Add(i);
            outline.SetIndices(lineIndices, MeshTopology.Lines, 0);
            commands.DrawMesh(outline, Matrix4x4.identity, wire);
        }
        RenderTexture previous = RenderTexture.active;
        Graphics.ExecuteCommandBuffer(commands);
        RenderTexture.active = previous;

        void DrawPart(FormVisualInstance visual, int part, Material material, int pass)
        {
            Mesh mesh = visual.MeshFor(part);
            Matrix4x4 pose = visual.PartTransform(part);
            bool skinned = !visual.DynamicGeometry && visual.Skin != null && visual.Resource.Description.Parts[part].Skin;
            properties.Clear();
            properties.SetMatrix("_FP_VP", projection * view);
            properties.SetVector("_FP_Eye", eye);
            properties.SetVector("_FP_LightDir", new Vector4(-.43f, -.46f, .77f, 0f));
            properties.SetColor("_FP_LightCol", new Color(.58f, .55f, .51f));
            properties.SetColor("_FP_AmbCol", new Color(.41f, .46f, .51f));
            properties.SetFloat("_WorldTime", time);
            properties.SetMatrix("_FormMatrix", pose);
            properties.SetMatrix("_FormNormalMatrix", pose.inverse.transpose);
            properties.SetFloat("_UseSkinning", skinned ? 1f : 0f);
            if (skinned) properties.SetMatrixArray("_FormBones", visual.Skin);
            properties.SetColor("_Color", visual.Tint ?? material.GetColor("_Color"));
            properties.SetColor("_Dye", visual.Dye ?? Color.white);
            properties.SetFloat("_FormClosedVolume", !visual.DynamicGeometry && visual.Resource.Description.Parts[part].HasSurfaceData ? 1f : 0f);
            properties.SetVector("_FormBuild", new Vector4(visual.BuildProgress, FormFormation.TipSpan,
                !visual.DynamicGeometry && visual.Resource.Description.Parts[part].HasBuildData ? 1f : 0f, visual.Feedback.Strength));
            properties.SetVector("_FormContact", visual.Feedback.Region(Vector3.zero));
            commands.DrawMesh(mesh, pose, material, 0, pass, properties);
        }
    }

    private static bool Opaque(in FormVisualInstance visual, Material material) =>
        material.GetFloat("_ZWrite") > .5f && !visual.IsTranslucent;

    /// <summary>按接触形状的原轮廓显示；胶囊只取截面线，半径沿用实际数值。</summary>
    private void AppendOutline(in AbilityCapsule contact)
    {
        if (contact.Prism != null || contact.IsBox)
        {
            var shape = contact.Prism?.Pose ?? AbilityPrism.Sample(contact, contact, default, 1f);
            int side = shape.VertexCount / 2;
            for (int i = 0; i < side; i++)
            {
                int next = (i + 1) % side;
                Edge(shape.Vertex(i), shape.Vertex(next));
                Edge(shape.Vertex(i + side), shape.Vertex(next + side));
                Edge(shape.Vertex(i), shape.Vertex(i + side));
            }
            return;
        }
        Vector3 axis = (contact.B - contact.A).normalized;
        if (axis == Vector3.zero) axis = Vector3.forward;
        Vector3 sideX = Vector3.Cross(axis, Mathf.Abs(axis.z) < .9f ? Vector3.forward : Vector3.up).normalized;
        Vector3 sideY = Vector3.Cross(axis, sideX);
        Ring(contact.A, sideX, sideY, contact.Radius);
        if (contact.A != contact.B) Ring(contact.B, sideX, sideY, contact.Radius);
        for (int i = 0; i < 4; i++)
        {
            Vector3 side = i == 0 ? sideX : i == 1 ? sideY : i == 2 ? -sideX : -sideY;
            Edge(contact.A + side * contact.Radius, contact.B + side * contact.Radius);
            Arc(contact.A, side, -axis, contact.Radius, 4, Mathf.PI * .5f);
            Arc(contact.B, side, axis, contact.Radius, 4, Mathf.PI * .5f);
        }
    }

    private void Ring(Vector3 center, Vector3 a, Vector3 b, float radius) => Arc(center, a, b, radius, 12, 2f * Mathf.PI);

    private void Arc(Vector3 center, Vector3 a, Vector3 b, float radius, int segments, float angle)
    {
        Vector3 previous = center + a * radius;
        for (int i = 1; i <= segments; i++)
        {
            float at = angle * i / segments;
            Vector3 point = center + (a * Mathf.Cos(at) + b * Mathf.Sin(at)) * radius;
            Edge(previous, point);
            previous = point;
        }
    }

    private void Edge(Vector3 a, Vector3 b) { lines.Add(a); lines.Add(b); }

    public void Dispose()
    {
        commands.Release();
        Image.Release();
        UnityEngine.Object.Destroy(Image);
        UnityEngine.Object.Destroy(outline);
        UnityEngine.Object.Destroy(wire);
    }
}

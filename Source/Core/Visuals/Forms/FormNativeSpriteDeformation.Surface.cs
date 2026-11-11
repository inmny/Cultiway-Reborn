using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>被抓持身体沿真实像素表面绘制，三维旋转也不退回近似平面摆放。</summary>
public static partial class FormNativeSpriteDeformation
{
    /// <summary>原版图片池完成后，只发布已经确认的整身表面。</summary>
    /// <param name="renderer">实际身体或持物图片。</param><param name="surface">共同表面。</param>
    /// <param name="item">是否只绘制实际持物。</param>
    internal static void SetSurface(SpriteRenderer renderer, BodySurfacePose surface, bool item)
    {
        if (!poses.TryGetValue(renderer, out Pose pose)) poses.Add(renderer, pose = new Pose());
        pose.Active = true;
        pose.SurfacePose = surface;
        pose.SurfaceItem = item;
    }

    /// <summary>图片姿态复用原纹理，只在抓住的新表面换版时建立可见几何。</summary>
    internal sealed partial class Pose
    {
        /// <summary>这一帧已经确认的实际表面。</summary>
        internal BodySurfacePose SurfacePose;

        /// <summary>当前图片是否属于持物。</summary>
        internal bool SurfaceItem;

        /// <summary>本次固定表面的原姿态，几何裁切与纹理坐标只制作一次。</summary>
        private BodySurfacePose rasterBasis;

        /// <summary>已经裁切的原世界顶点，资源和已有姿态不变。</summary>
        private Vector3[] surfacePoints, projectedPoints;

        /// <summary>缓存对应身体或持物图片。</summary>
        private bool rasterItem;

        /// <summary>返回普通动作时，让原图片变形重新准备自己的网格。</summary>
        internal void ResetSurface()
        {
            SurfacePose = null;
            rasterBasis = null;
            source = null;
        }

        /// <summary>先复用可见几何，再作同一刚性运动和原版投影，最后提交共享深度材质。</summary>
        /// <param name="renderer">原版当前实际图片。</param>
        private void PrepareSurface(SpriteRenderer renderer)
        {
            BodySurfacePose basis = SurfacePose.RigidBasis ?? SurfacePose;
            if (rasterBasis != basis || source != renderer.sprite || rasterItem != SurfaceItem)
                RebuildSurface(basis, renderer.sprite);
            Vector3 foot = SurfacePose.Foot;
            var root = new Vector3(foot.x, foot.y + foot.z, foot.z);
            Matrix = Matrix4x4.Translate(root);
            Matrix4x4 motion = SurfacePose.RigidBasis == null ? Matrix4x4.identity : SurfacePose.RigidTransform.Matrix;
            HasVertexGround = true;
            MinimumDepth = float.PositiveInfinity;
            MaximumDepth = float.NegativeInfinity;
            for (int i = 0; i < surfacePoints.Length; i++)
            {
                Vector3 point = motion.MultiplyPoint3x4(surfacePoints[i]);
                projectedPoints[i] = new Vector3(point.x - root.x, point.y + point.z - root.y, point.y - foot.y);
                float depth = point.y - point.z;
                MinimumDepth = Mathf.Min(MinimumDepth, depth);
                MaximumDepth = Mathf.Max(MaximumDepth, depth);
            }
            Mesh.vertices = projectedPoints;
            Mesh.RecalculateBounds();
            Bounds = new Bounds(Mesh.bounds.center + root, Mesh.bounds.size);
            Properties.Clear();
            renderer.GetPropertyBlock(Properties);
            Properties.SetFloat("_NativeVertexGround", 1f);
            Properties.SetTexture("_MainTex", source.texture);
            Properties.SetColor("_RendererColor", renderer.color);
            Properties.SetVector("_Flip", Vector4.one);
            Texture2D alpha = source.associatedAlphaSplitTexture;
            Properties.SetFloat("_EnableExternalAlpha", alpha == null ? 0 : 1);
            if (alpha != null) Properties.SetTexture("_AlphaTex", alpha);
        }

        /// <summary>保留真实可见三角面和原图坐标，裁切片段不换用变形后的纹理位置。</summary>
        /// <param name="basis">建立时的完整表面。</param><param name="sprite">原版实际合成图或持物图。</param>
        private void RebuildSurface(BodySurfacePose basis, Sprite sprite)
        {
            rasterBasis = basis;
            rasterItem = SurfaceItem;
            source = sprite;
            var points = new List<Vector3>();
            var coordinates = new List<Vector2>();
            var indices = new List<int>();
            Vector2[] spritePoints = sprite.vertices, spriteUv = sprite.uv;
            int third = 2;
            while (third < spritePoints.Length && Mathf.Abs(Cross(spritePoints[1] - spritePoints[0],
                       spritePoints[third] - spritePoints[0])) < 1e-12f) third++;
            Vector2 x = spritePoints[1] - spritePoints[0], y = spritePoints[third] - spritePoints[0];
            float area = Cross(x, y);
            for (int part = 0; part < basis.Parts.Length; part++)
            {
                BodySurfacePart piece = basis.Parts[part];
                if ((piece.Attachment == "item") != SurfaceItem) continue;
                int transform = piece.TransformIndex < 0 ? part : piece.TransformIndex;
                Matrix4x4 matrix = basis.Matrices[transform];
                Vector3[] actual = basis.Vertices(part);
                for (int face = 0; face < piece.Triangles.Length; face += 3)
                {
                    int a = piece.Triangles[face], b = piece.Triangles[face + 1], c = piece.Triangles[face + 2];
                    foreach (Vector3[] polygon in basis.Fragments(part, face))
                    {
                        int start = points.Count;
                        foreach (Vector3 point in polygon)
                        {
                            var anchor = new BodySurfaceAnchor(part, face, point, matrix.MultiplyPoint3x4(actual[a]),
                                matrix.MultiplyPoint3x4(actual[b]), matrix.MultiplyPoint3x4(actual[c]));
                            Vector3 local = piece.Vertices[a] * anchor.Weights.x + piece.Vertices[b] * anchor.Weights.y +
                                            piece.Vertices[c] * anchor.Weights.z;
                            Vector2 uvPoint = (piece.ImageCoordinates == null ? new Vector2(local.x, local.y + local.z) :
                                piece.ImageCoordinates[a] * anchor.Weights.x + piece.ImageCoordinates[b] * anchor.Weights.y +
                                piece.ImageCoordinates[c] * anchor.Weights.z) - spritePoints[0];
                            float u = Cross(uvPoint, y) / area, v = Cross(x, uvPoint) / area;
                            coordinates.Add(spriteUv[0] + u * (spriteUv[1] - spriteUv[0]) +
                                            v * (spriteUv[third] - spriteUv[0]));
                            points.Add(point);
                        }
                        for (int i = 1; i + 1 < polygon.Length; i++)
                        { indices.Add(start); indices.Add(start + i); indices.Add(start + i + 1); }
                    }
                }
            }
            surfacePoints = points.ToArray();
            projectedPoints = new Vector3[surfacePoints.Length];
            Mesh.Clear();
            Mesh.vertices = projectedPoints;
            Mesh.SetUVs(0, coordinates);
            Mesh.SetTriangles(indices, 0);
            var colors = new Color32[surfacePoints.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            Mesh.colors32 = colors;
        }

        /// <summary>图集中任意三个实际顶点确定原图坐标，允许旋转或紧密打包。</summary>
        /// <param name="a">第一平面向量。</param><param name="b">第二平面向量。</param>
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}

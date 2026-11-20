using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class CompiledBodyAction
{
    /// <summary>同一握持坐标下，沿完整杆段滑动时器物的位移。</summary>
    private readonly Vector3 gripSlideOffset;

    /// <summary>滑握只能在直径相同、同轴且由真实杆形覆盖的两个握点之间连续发生。</summary>
    /// <param name="resource">实际持物的资源。</param><param name="start">原握点。</param>
    /// <param name="destination">滑动终点。</param><param name="socket">手内的实际坐标。</param>
    /// <param name="original">原握点已经对齐的器物矩阵。</param>
    private Vector3 CompileGripSlide(FormResource resource, FormGrip start, string destination, Matrix4x4 socket,
        Matrix4x4 original)
    {
        FormGrip end = ConstructionReferences.Resolve("Body/SlideGrip", () => resource.Grip(destination),
            "Grip", resource.Description.Id + " / " + destination);
        RequireShaft(resource, start, end, "Body/SlideGrip");
        return end.Align(socket, WeaponSize).GetColumn(3) - original.GetColumn(3);
    }

    /// <summary>确认两处实际握点处于同一段有正确粗细、可闭合的直杆。</summary>
    /// <param name="resource">实际持物资源。</param><param name="start">主手握点。</param>
    /// <param name="end">另一实际握点。</param><param name="path">错误所对应的资料位置。</param>
    private void RequireShaft(FormResource resource, FormGrip start, FormGrip end, string path)
    {
        Vector3 from = start.Transform.GetColumn(3), to = end.Transform.GetColumn(3);
        int part = resource.Description.Anchors.Single(anchor => anchor.Id == start.Anchor).Part;
        if (part != resource.Description.Anchors.Single(anchor => anchor.Id == end.Anchor).Part ||
            Vector3.Distance(from, to) < .0001f ||
            Quaternion.Angle(start.Transform.rotation, end.Transform.rotation) > .05f ||
            Vector3.Cross((to - from).normalized, (Vector3)start.Transform.GetColumn(2)).sqrMagnitude > 1e-6f ||
            Mathf.Abs(start.Diameter - end.Diameter) > .0001f ||
            !end.Fits(WeaponSize, .035f * HandScale, .08f * HandScale) ||
            !resource.Contacts.Any(contact => contact.Part == part && !contact.IsPrism &&
                Mathf.Abs(contact.Radius * 2f - start.Diameter) < .0001f &&
                OnShaft(contact.A, contact.B, from) && OnShaft(contact.A, contact.B, to)))
            throw new InvalidDataException(path + "：两个握点须沿同一段可握持的实心直杆");
    }

    /// <summary>握点应落在实际直杆轴线上，不允许从一个握柄穿到另一个部件。</summary>
    /// <param name="a">真实杆段起点。</param><param name="b">真实杆段终点。</param><param name="point">握点。</param>
    private static bool OnShaft(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 axis = b - a;
        float projected = Vector3.Dot(point - a, axis);
        return axis.sqrMagnitude > 0f && projected >= 0f && projected <= axis.sqrMagnitude &&
               Vector3.Cross(axis, point - a).sqrMagnitude <= axis.sqrMagnitude * 1e-8f;
    }

    /// <summary>范围从可接触阶段的两个滑握极值取得，轴向移动中间不会扩大包络。</summary>
    /// <param name="matrix">原握点的器物矩阵。</param><param name="contacts">当前动作所用的实际表面。</param>
    /// <param name="slides">是否声明了真实滑握。</param>
    private float ContactExtent(Matrix4x4 matrix, FormContactBinding[] contacts, bool slides)
    {
        float min = 0f, max = 0f;
        if (slides)
        {
            float from = KeyAt(ActiveFrom).GripSlide, until = KeyAt(actionUntil).GripSlide;
            min = Mathf.Min(from, until);
            max = Mathf.Max(from, until);
            foreach (Key key in keys)
            {
                if (key.At <= ActiveFrom || key.At >= actionUntil) continue;
                min = Mathf.Min(min, key.GripSlide);
                max = Mathf.Max(max, key.GripSlide);
            }
        }

        Vector3 first = gripSlideOffset * min, last = gripSlideOffset * max;
        float scale = matrix.lossyScale.x, extent = 0f;
        foreach (FormContactBinding contact in contacts)
        {
            Vector3 a = matrix.MultiplyPoint3x4(contact.A), b = matrix.MultiplyPoint3x4(contact.B);
            float earliest = Mathf.Max((a + first).magnitude, (b + first).magnitude);
            float latest = Mathf.Max((a + last).magnitude, (b + last).magnitude);
            extent = Mathf.Max(extent, Mathf.Max(earliest, latest) + contact.Radius * scale);
        }
        return extent;
    }
}

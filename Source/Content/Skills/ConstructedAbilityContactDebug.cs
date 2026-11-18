using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>与正式模型分开发布，只显示本次明确选择的施放。</summary>
    private const string ContactDebugProvider = "ability.contact-debug";

    /// <summary>要检查的实际施放编号，零表示关闭。</summary>
    private static long contactDebugCast;

    /// <summary>临时隐藏这次施放的正式形体，以免实体遮挡有效区。</summary>
    private static bool contactDebugOnlyShapes;

    /// <summary>只在开启时重用的形状及线段缓冲。</summary>
    private static readonly List<AbilityCapsule> contactDebugShapes = new();
    private static readonly List<Vector3> contactDebugVertices = new();
    private static readonly List<int> contactDebugIndices = new();

    /// <summary>由主线程生成，原版与三维视角一同读取。</summary>
    private static Mesh contactDebugMesh;

    /// <summary>开启指定施放的有效区调试显示。立即移除上一次的轮廓，下一次确认后显示新轮廓。</summary>
    /// <param name="castId">从施放事件得到的实际编号。传零关闭。</param>
    /// <param name="onlyShapes">是否暂时隐藏所选施放的正式形体。不会隐藏人物或改变接触。</param>
    public static void SetContactDebug(long castId = 0, bool onlyShapes = false)
    {
        if (contactDebugCast == castId && contactDebugOnlyShapes == onlyShapes) return;
        ClearContactDebug();
        contactDebugCast = castId;
        contactDebugOnlyShapes = castId != 0 && onlyShapes;
    }

    /// <summary>只在完整一轮确认后读取与执行相同的形状入口，结束的施放不再绘制。</summary>
    private void PublishContactDebug()
    {
        if (contactDebugCast == 0) return;
        RunningCast selected = null;
        foreach (RunningCast running in casts)
            if (running.Id == contactDebugCast)
            {
                selected = running;
                break;
            }
        if (selected == null)
        {
            ClearContactDebug();
            return;
        }

        Collect(selected, contactDebugShapes, false);
        contactDebugVertices.Clear();
        foreach (AbilityCapsule shape in contactDebugShapes)
            AbilityContactOutline.Append(shape, contactDebugVertices);
        if (contactDebugVertices.Count == 0)
        {
            FormVisualScene.Publish(ContactDebugProvider, Array.Empty<FormVisualInstance>());
            return;
        }

        if (contactDebugMesh == null)
        {
            contactDebugMesh = new Mesh { name = "Cultiway Effective Contact Debug", hideFlags = HideFlags.DontSave,
                indexFormat = IndexFormat.UInt32 };
            contactDebugMesh.MarkDynamic();
        }
        contactDebugMesh.Clear();
        contactDebugMesh.SetVertices(contactDebugVertices);
        while (contactDebugIndices.Count < contactDebugVertices.Count) contactDebugIndices.Add(contactDebugIndices.Count);
        contactDebugMesh.SetIndices(contactDebugIndices, 0, contactDebugVertices.Count, MeshTopology.Lines, 0);
        FormVisualScene.Publish(ContactDebugProvider, new[]
        {
            new FormVisualInstance(FormResources.ContactVolume, contactDebugMesh, new Color(.14f, 1f, .73f, .92f), terrain)
        });
    }

    /// <summary>关掉调试或换世界时立即清掉显示和残留形状。</summary>
    private static void ClearContactDebug()
    {
        contactDebugCast = 0;
        contactDebugOnlyShapes = false;
        contactDebugShapes.Clear();
        if (contactDebugMesh == null) return;
        FormVisualScene.Publish(ContactDebugProvider, Array.Empty<FormVisualInstance>());
        Object.Destroy(contactDebugMesh);
        contactDebugMesh = null;
    }
}

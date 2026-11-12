using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>从真实装备资料重建地面兵器，复用原模组的器形和配色。</summary>
internal static partial class BodyWorldfallRenderer
{
    /// <summary>读档后的独立兵器无需创建虚假持有人，直接取原装备外观及原模型。</summary>
    /// <param name="item">已恢复的同一件原版物品。</param>
    private static BodySurfacePose GroundWeaponAppearance(Item item)
    {
        object sprites = externalSprites.GetValue(externalInstance.GetValue(null));
        Sprite image = ItemRendering.getItemMainSpriteFrame(item.getAsset());
        object look = heldLook.Invoke(null, new[] { item.getAsset().id, spriteImage.Invoke(sprites, new object[] { image }) });
        object model = heldModel.Invoke(null, new[] { look, (object)0 });
        object instance = Activator.CreateInstance(meshType);
        meshModel.SetValue(instance, model);
        ensurePose.Invoke(instance, null);
        var original = (Array)modelParts.GetValue(model);
        var parts = new List<BodySurfacePart>();
        var matrices = new List<Matrix4x4>();
        for (int i = 0; i < original.Length; i++)
        {
            object part = original.GetValue(i);
            var positions = (float[])partPositions.GetValue(part);
            var points = new Vector3[positions.Length / 3];
            for (int v = 0; v < points.Length; v++) points[v] = new Vector3(positions[v * 3], positions[v * 3 + 1], positions[v * 3 + 2]);
            parts.Add(new BodySurfacePart("item", points, (int[])partTriangles.GetValue(part), "item",
                faceColors: WorldfallSurfaceColors.Read(instance, part)));
            matrices.Add(WorldMatrix(instance, i));
        }
        return new BodySurfacePose(parts.ToArray(), matrices.ToArray(), Vector3.zero, 1f, 0f);
    }
}

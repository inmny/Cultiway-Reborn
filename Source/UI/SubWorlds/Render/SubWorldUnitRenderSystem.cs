using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Objects;
using Cultiway.Core.SubWorlds.Runtime;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>查询 Actor 类别与共享视觉状态，并使用原版 UnitRenderer prefab 提交 Sprite。</summary>
internal sealed class SubWorldUnitRenderSystem : QuerySystem<Position, SubWorldActor, SubWorldVisual>, IWorldStateClearable
{
    private const string UnitRendererPrefabPath = "prefabs/PrefabUnitRenderer";

    private readonly SubWorldRenderState state;
    private readonly Transform unitsRoot;
    private readonly MonoObjPool<GroupSpriteObject> unitPool;
    private readonly Dictionary<string, UnitVisualDefinition> definitions = new();

    internal SubWorldUnitRenderSystem(SubWorldRenderState state, Transform parent)
    {
        this.state = state;
        GameObject creatures = new("Creatures");
        creatures.transform.SetParent(parent, false);
        GameObject units = new("Units");
        units.transform.SetParent(creatures.transform, false);
        unitsRoot = units.transform;
        GroupSpriteObject prefab = Resources.Load<GroupSpriteObject>(UnitRendererPrefabPath) ??
                                   throw new InvalidOperationException(
                                       $"原版 UnitRenderer prefab 未找到: {UnitRendererPrefabPath}");
        unitPool = new MonoObjPool<GroupSpriteObject>(prefab, unitsRoot);
        units.SetActive(false);
    }

    void IWorldStateClearable.ClearWorldState()
    {
        unitPool.Clear();
        definitions.Clear();
    }

    protected override void OnUpdate()
    {
        if (unitsRoot.gameObject.activeSelf != state.GameplayVisible)
            unitsRoot.gameObject.SetActive(state.GameplayVisible);
        unitPool.ResetToStart();
        if (state.GameplayVisible)
        {
            Query.ForEachEntity((ref Position position, ref SubWorldActor actor, ref SubWorldVisual visual, Entity _) =>
            {
                if (visual.State != SubWorldVisualState.Default)
                    throw new InvalidOperationException($"Actor 不支持当前视觉阶段: {visual.State}");
                UnitVisualDefinition definition = GetDefinition(actor.ActorAssetId);
                GroupSpriteObject view = unitPool.GetNext();
                int frame = Mathf.FloorToInt(Tick.time * definition.AnimationSpeed) % definition.Frames.Length;
                view.setSprite(definition.Frames[frame]);
                Vector3 renderPosition = position.value;
                view.setPosOnly(ref renderPosition);
                Vector3 scale = new(definition.Scale, definition.Scale, 1f);
                view.setScale(ref scale);
            });
        }
        unitPool.ClearUnsed();
    }

    private UnitVisualDefinition GetDefinition(string actorAssetId)
    {
        if (definitions.TryGetValue(actorAssetId, out UnitVisualDefinition definition)) return definition;
        ActorAsset actorAsset = AssetManager.actor_library.get(actorAssetId) ??
                                throw new InvalidOperationException(
                                    $"SubWorld 单位 Actor Asset 未注册: {actorAssetId}");
        AnimationContainerUnit animation = DynamicActorSpriteCreatorUI.getContainerForUI(
            actorAsset, true, actorAsset.texture_asset);
        definition = new UnitVisualDefinition(
            animation.idle.frames,
            actorAsset.animation_idle_speed,
            actorAsset.base_stats["scale"]);
        definitions.Add(actorAssetId, definition);
        return definition;
    }

    private sealed class UnitVisualDefinition
    {
        internal UnitVisualDefinition(Sprite[] frames, float animationSpeed, float scale)
        {
            Frames = frames;
            AnimationSpeed = animationSpeed;
            Scale = scale;
        }

        internal Sprite[] Frames { get; }
        internal float AnimationSpeed { get; }
        internal float Scale { get; }
    }
}

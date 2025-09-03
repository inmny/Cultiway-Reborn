using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;
[Dependency(typeof(SkillTrajectories))]
public class SkillEntities : ExtendLibrary<SkillEntityAsset, SkillEntities>
{
    public static SkillEntityAsset Fireball { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        Fireball.Element = new ElementComposition(fire: 1f);
        Fireball.PrefabEntity = Fireball.World.CreateEntity(
            new SkillEntity()
            {
                SkillContainer = default,
                Asset = Fireball
            }, 
            new SkillContext(),
            new Position(),
            new Rotation(),
            new Scale(0.1f),
            new ColliderSphere()
            {
                Radius = 1f
            },
            new ColliderConfig()
            {
                  Enabled = true,
                  Enemy = true,
                  Actor = true
            },
            new AnimBindRenderer(),
            new AnimController()
            {
               meta  = new()
               {
                   frame_interval = 0.1f,
                   loop = true
               }
            },
            new AnimData()
            {
                frames = SpriteTextureLoader.getSpriteList("cultiway/effect/flying_fireball")
            }, Tags.Get<TagPrefab>());
        Fireball.PrefabEntity.Add(new Velocity()
        {
            Value = 10
        }, new Trajectory()
        {
            ID = SkillTrajectories.TowardsDirection.id
        });
        Fireball.OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            ModClass.I.SkillV3.SpawnAnim("cultiway/effect/explosion_fireball", target.GetSimPos(), Vector3.right);

            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            foreach (var obj in SkillUtils.IterEnemyInSphere(entity.GetComponent<Position>().v2, 2, attacker))
            {
                if (obj.isActor())
                {
                    obj.a.GetExtend().GetHit(context.Strength, ref Fireball.Element, attacker);
                }
                else
                {
                    obj.b.getHit(context.Strength, pAttacker: attacker);
                }
                on_effect_obj?.Invoke(entity, obj);
            }
            entity.AddTag<TagRecycle>();
            return false;
        };
    }
}
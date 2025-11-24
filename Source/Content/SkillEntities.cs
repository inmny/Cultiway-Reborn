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
    public static SkillEntityAsset GoldSword { get; private set; }
    public static SkillEntityAsset GoldBlade { get; private set; }
    public static SkillEntityAsset WoodThorn { get; private set; }
    public static SkillEntityAsset FallWood { get; private set; }
    public static SkillEntityAsset WaterArrow { get; private set; }
    public static SkillEntityAsset WaterBall { get; private set; }
    public static SkillEntityAsset WaterBlade { get; private set; }
    public static SkillEntityAsset Fireball { get; private set; }
    public static SkillEntityAsset FireBlade { get; private set; }
    public static SkillEntityAsset FallStone { get; private set; }
    public static SkillEntityAsset StoneThorn { get; private set; }
    public static SkillEntityAsset WindBlade { get; private set; }
    public static SkillEntityAsset WindPolo { get; private set; }
    public static SkillEntityAsset Tornado { get; private set; }
    public static SkillEntityAsset FallLightning { get; private set; }
    public static SkillEntityAsset LightningPolo { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        GoldSword.Element = new ElementComposition(iron: 1f);
        GoldBlade.Element = new ElementComposition(iron: 1f);
        WoodThorn.Element = new ElementComposition(wood: 1f);
        FallWood.Element = new ElementComposition(wood: 1f);
        WaterArrow.Element = new ElementComposition(water: 1f);
        WaterBall.Element = new ElementComposition(water: 1f);
        WaterBlade.Element = new ElementComposition(water: 1f);
        Fireball.Element = new ElementComposition(fire: 1f);
        FireBlade.Element = new ElementComposition(fire: 1f);
        FallStone.Element = new ElementComposition(earth: 1f);
        StoneThorn.Element = new ElementComposition(earth: 1f);
        WindBlade.Element = new ElementComposition(water:0.5f, wood:0.5f);
        WindPolo.Element = new ElementComposition(water:0.5f, wood:0.5f);
        Tornado.Element = new ElementComposition(water:0.5f, wood:0.5f);
        FallLightning.Element = new ElementComposition(water:0.5f, fire:0.5f);
        LightningPolo.Element = new ElementComposition(water:0.5f, fire:0.5f);
        GoldSword.SetupCommonPrefab("cultiway/effect/gold_sword")
            .SetupColliderSphere(1f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref GoldSword.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);
            entity.AddTag<TagRecycle>();
            return false;
        };
        GoldBlade.SetupCommonPrefab("cultiway/effect/gold_blade", anim_loop: false)
            .SetupColliderSphere(1.5f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref GoldBlade.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            return true;
        };
        WoodThorn.SetupCommonPrefab("cultiway/effect/wood_thorn", anim_loop: false)
            .SetupColliderSphere(1.2f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref WoodThorn.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);
            return true;
        };
        FallWood.SetupCommonPrefab("cultiway/effect/fall_wood")
            .SetupColliderSphere(1.2f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref FallWood.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            entity.AddTag<TagRecycle>();
            return true;
        };
        WaterArrow.SetupCommonPrefab("cultiway/effect/single_water_sword")
            .SetupColliderSphere(1f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref WaterArrow.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            entity.AddTag<TagRecycle>();
            return false;
        };
        WaterBall.SetupCommonPrefab("cultiway/effect/water_polo")
            .SetupColliderSphere(1f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref WaterBall.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            entity.AddTag<TagRecycle>();
            return false;
        };
        WaterBlade.SetupCommonPrefab("cultiway/effect/water_blade", anim_loop: false)
            .SetupColliderSphere(1.5f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref WaterBlade.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            return true;
        };
        Fireball.SetupCommonPrefab("cultiway/effect/fire_polo")
            .SetupColliderSphere(1f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
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
        FireBlade.SetupCommonPrefab("cultiway/effect/fire_blade", anim_loop: false)
            .SetupColliderSphere(1.5f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
                if (target.isActor())
                {
                    target.a.GetExtend().GetHit(context.Strength, ref FireBlade.Element, attacker);
                }
                else
                {
                    target.b.getHit(context.Strength, pAttacker: attacker);
                }

                on_effect_obj?.Invoke(entity, target);

            return true;
        };
        FallStone.SetupCommonPrefab("cultiway/effect/fall_rock")
            .SetupColliderSphere(1.2f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref FallStone.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            entity.AddTag<TagRecycle>();
            return true;
        };
        StoneThorn.SetupCommonPrefab("cultiway/effect/ground_thorn", anim_loop:false)
            .SetupColliderSphere(1.2f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {

            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref StoneThorn.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);
            return true;
        };
        WindBlade.SetupCommonPrefab("cultiway/effect/wind_blade", anim_loop: false)
            .SetupColliderSphere(1.5f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref WindBlade.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            return true;
        };
        WindPolo.SetupCommonPrefab("cultiway/effect/wind_polo")
            .SetupColliderSphere(1f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
                if (target.isActor())
                {
                    target.a.GetExtend().GetHit(context.Strength, ref WindPolo.Element, attacker);
                }
                else
                {
                    target.b.getHit(context.Strength, pAttacker: attacker);
                }

                on_effect_obj?.Invoke(entity, target);

            entity.AddTag<TagRecycle>();
            return false;
        };
        Tornado.SetupCommonPrefab("cultiway/effect/simple_tornado")
            .SetupColliderSphere(1.5f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirectionNoRot)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {

            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
                if (target.isActor())
                {
                    target.a.GetExtend().GetHit(context.Strength, ref Tornado.Element, attacker);
                }
                else
                {
                    target.b.getHit(context.Strength, pAttacker: attacker);
                }

                on_effect_obj?.Invoke(entity, target);

            return true;
        };
        FallLightning.SetupCommonPrefab("cultiway/effect/default_lightning", anim_loop: false)
            .SetupColliderSphere(0.5f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
            if (target.isActor())
            {
                target.a.GetExtend().GetHit(context.Strength, ref FallLightning.Element, attacker);
            }
            else
            {
                target.b.getHit(context.Strength, pAttacker: attacker);
            }

            on_effect_obj?.Invoke(entity, target);

            entity.AddTag<TagRecycle>();
            return true;
        };
        LightningPolo.SetupCommonPrefab("cultiway/effect/lightning_polo")
            .SetupColliderSphere(1f, new ColliderConfig()
            {
                Enabled = true,
                Enemy = true,
                Actor = true
            })
            .SetupDefaultTraj(SkillTrajectories.TowardsDirection)
            .OnObjCollision = (ref SkillContext context, Entity skill_container, Entity entity,
            BaseSimObject target) =>
        {
            var on_effect_obj = skill_container.GetComponent<SkillContainer>().OnEffectObj;
            var attacker = context.SourceObj;
                if (target.isActor())
                {
                    target.a.GetExtend().GetHit(context.Strength, ref LightningPolo.Element, attacker);
                }
                else
                {
                    target.b.getHit(context.Strength, pAttacker: attacker);
                }

                on_effect_obj?.Invoke(entity, target);

            entity.AddTag<TagRecycle>();
            return false;
        };
    }
}
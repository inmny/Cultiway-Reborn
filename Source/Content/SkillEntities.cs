using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

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
            ModClass.LogInfo($"{entity.Id} hit {target.getData().id}");
            entity.AddTag<TagRecycle>();
            return false;
        };
    }
}
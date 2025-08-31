using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

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
                SkillContainer = default
            }, 
            new SkillContext(),
            new Position(),
            new Scale(0.1f),
            new ColliderSphere()
            {
                Radius = 1f
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
    }
}
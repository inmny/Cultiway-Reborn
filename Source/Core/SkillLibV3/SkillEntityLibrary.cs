using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

public class SkillEntityLibrary : AssetLibrary<SkillEntityAsset>
{
    public static SkillEntityAsset RawAnim { get; private set; }
    public override void init()
    {
        base.init();
        RawAnim = add(new SkillEntityAsset()
        {
            id = "raw_anim"
        });
        RawAnim.PrefabEntity = RawAnim.World.CreateEntity(
            new Position(), new Rotation(), new Scale(0.1f),
            new AnimBindRenderer(), new AnimController()
            {
                meta = new()
                {
                    frame_interval = 0.1f,
                    loop = false
                }
            },
            new AliveTimer()
            {
                value = 0f
            },
            new AliveTimeLimit()
            {
                value = 5f
            },
            new AnimData()
            {
                frames = SpriteTextureLoader.getSpriteList("cultiway/effect/flying_fireball")
            }, Tags.Get<TagPrefab>()
        );
        
    }
}
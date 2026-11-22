using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Persistence;

/// <summary>登记实际支持的长期资料，各玩法只维护自己的版本和转换。</summary>
internal static class WorldSaveContent
{
    /// <summary>内容资产初始化后登记；基础先于修炼，修炼先于具体妖兽身体。</summary>
    internal static void Initialize()
    {
        WorldObjectSaveService.Initialize();
        ModClass.I.WorldSaves.WorldRestored += YaoNirvanaService.RestoreStillness;
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, WorldObjectSaveData>("objects", "物件与知识", 3,
                WorldObjectSaveService.Capture, WorldObjectSaveService.Prepare,
                migrations: new ISaveMigration[] { new WorldObjectsV1ToV2(), new WorldObjectsV2ToV3() })
            { BeforeActors = true });
        WorldEnvironmentSaveService.Initialize();
        var actors = ModClass.I.WorldSaves.Actors;
        actors.Register(new SaveSection<ActorExtend, CharacterSaveData>("character", "人物基础", 1,
            CharacterSaveService.CaptureCharacter, CharacterSaveService.RestoreCharacter,
            CharacterSaveService.ValidateCharacter));
        actors.Register(new SaveSection<ActorExtend, CultivationSaveData>("cultivation", "修炼成长", 1,
            CharacterSaveService.CaptureCultivation, CharacterSaveService.RestoreCultivation,
            CharacterSaveService.ValidateCultivation));
        actors.Register(new SaveSection<ActorExtend, JObject>("yao", "妖兽成长", 1,
            actor => YaoPersistence.Capture(actor) is string json ? JObject.Parse(json) : null,
            (actor, data) => YaoPersistence.Restore(actor, data.ToString(Formatting.None))));
        ActorPositionSaveService.Initialize();
    }
}

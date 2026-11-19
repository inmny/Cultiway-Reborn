using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core;

namespace Cultiway.Content.FamilyFarming;

/// <summary>注册原粮和家庭农活，把原版农民职业接到同一条完整生产链。</summary>
[Dependency(typeof(ActorTasks))]
internal sealed class FamilyFarmContent : ICanInit
{
    /// <summary>原粮复用麦类图标和背包外观，但没有食用属性或食物配方。</summary>
    internal static ResourceAsset RawGrain { get; private set; }

    /// <summary>注册内容和低频更新，解除风车选址绑定并让农田允许城市建设。</summary>
    public void Init()
    {
        RawGrain = AssetManager.resources.add(new ResourceAsset {
            id = FamilyFarmService.RawGrainId, path_icon = ResourceLibrary.wheat.path_icon,
            path_gameplay_sprite = ResourceLibrary.wheat.path_gameplay_sprite,
            full_sprite_path = "items/resources/" + ResourceLibrary.wheat.path_gameplay_sprite,
            gameplay_sprites = ResourceLibrary.wheat.gameplay_sprites,
            type = ResType.Ingredient, food = false, maximum = 999, storage_max = 200,
            order = AssetManager.resources.list.Count });
        if (RawGrain.gameplay_sprites == null)
            RawGrain.gameplay_sprites = SpriteTextureLoader.getSpriteList(RawGrain.full_sprite_path);
        var crop = AssetManager.buildings.get("wheat");
        crop.resources_given.Clear(); crop.addResource(FamilyFarmService.RawGrainId, 1);
        // 城市建设沿用原版占地检查，农田地表不再阻挡房屋和公共建筑。
        TopTileLibrary.field.can_build_on = true;
        var farmer = AssetManager.job_actor.get("farmer");
        var publicTasks = farmer.tasks.ToArray();
        farmer.tasks.Clear(); farmer.addTask(ActorTasks.FamilyFarmWork.id); farmer.addCondition(new CondFamilyFarmPolicy());
        farmer.addTask("end_job"); farmer.addCondition(new CondFamilyFarmPolicy(true));
        foreach (var task in publicTasks) { task.addCondition(new CondFamilyFarmPolicy(true), false); farmer.tasks.Add(task); }
        Cultiway.Patch.PatchMapBox.RegisterActionOnClearWorld(FamilyFarmService.Clear);
        ModClass.I.GeneralLogicSystems.Add(new FamilyFarmSystem());
    }

    /// <summary>轮流处理村庄，避免每帧遍历所有家庭和土地。</summary>
    private sealed class FamilyFarmSystem : ThrottledSystem
    {
        /// <summary>当前村庄轮转位置。</summary>
        private int cursor;
        /// <summary>以模拟时间计算的更新间隔。</summary>
        protected override float IntervalSeconds => 1;
        /// <summary>每轮最多处理两个村庄。</summary>
        protected override void OnThrottledUpdate()
        {
            var cities = World.world.cities.list;
            for (int n = 0; n < System.Math.Min(2, cities.Count); n++) FamilyFarmService.Update(cities[cursor++ % cities.Count]);
        }
        /// <summary>换世界后从第一个村庄重新轮转。</summary>
        protected override void OnThrottleWorldStateCleared() => cursor = 0;
    }
}

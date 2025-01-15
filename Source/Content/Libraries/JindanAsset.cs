using Cultiway.Content.Components;
using Cultiway.Core;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;
public delegate bool JindanCheck(ActorExtend ae, ref XianBase xian_base);
public delegate float JindanScore(ActorExtend ae, ref XianBase xian_base);
public class JindanAsset : Asset
{
    private JindanGroupAsset _group;
    public JindanCheck check;
    public JindanScore score;
    public string wrapped_skill_id;

    public BaseStats Stats = new()
    {
        [S.mod_health] = 0.2f,
        [S.mod_damage] = 0.2f,
        [nameof(WorldboxGame.BaseStats.IronArmor)] = 1,
        [nameof(WorldboxGame.BaseStats.WoodArmor)] = 1,
        [nameof(WorldboxGame.BaseStats.WaterArmor)] = 1,
        [nameof(WorldboxGame.BaseStats.FireArmor)] = 1,
        [nameof(WorldboxGame.BaseStats.EarthArmor)] = 1,
        [nameof(WorldboxGame.BaseStats.IronMaster)] = 1,
        [nameof(WorldboxGame.BaseStats.WoodMaster)] = 1,
        [nameof(WorldboxGame.BaseStats.WaterMaster)] = 1,
        [nameof(WorldboxGame.BaseStats.FireMaster)] = 1,
        [nameof(WorldboxGame.BaseStats.EarthMaster)] = 1,
    };
    public JindanGroupAsset Group
    {
        get => _group;
        set
        {
            _group?.jindans.Remove(this);
            _group = value;
            _group.jindans.Add(this);
        }
    }

    public string GetName()
    {
        return LM.Get(id);
    }

    public string GetDescription()
    {
        return LM.Get($"{id}.Info");
    }
}
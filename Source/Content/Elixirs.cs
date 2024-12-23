using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

[Dependency(typeof(Jindans))]
public class Elixirs : ExtendLibrary<ElixirAsset, Elixirs>
{
    private const string      prefix = "Cultiway.Elixir";
    public static ElixirAsset OpenElementRootElixir { get; private set; }
    public static ElixirAsset WakanRestoreElixir { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets(prefix);
        OpenElementRootElixir.name_key = $"{prefix}.OpenElementRootElixir";
        OpenElementRootElixir.craft_action = (ae, elixir_entity, ingrediants) =>
        {
            ref ElementRoot er = ref ingrediants[2].GetComponent<ElementRoot>();
            elixir_entity.AddComponent(er);
        };
        OpenElementRootElixir.effect_type = ElixirEffectType.DataChange;
        OpenElementRootElixir.effect_action = (ActorExtend ae, Entity elixir_entity, ref Elixir _) =>
        {
            ae.AddComponent(elixir_entity.GetComponent<ElementRoot>());
        };
        OpenElementRootElixir.consumable_check_action =
            (ActorExtend ae, Entity elixir_entity, ref Elixir _) => !ae.HasElementRoot();
        OpenElementRootElixir.ingrediants = new ElixirIngrediantCheck[]
        {
            new()
            {
                count = 1,
                element_root_id = ElementRoots.Earth.id,
                jindan_id = Jindans.Bentonite.id
            },
            new()
            {
                count = 1,
                element_root_id = ElementRoots.Wood.id,
                jindan_id = Jindans.Condensed.id
            },
            new()
            {
                count = 1,
                need_element_root = true
            }
        };
        WakanRestoreElixir.name_key = $"{prefix}.WakanRestoreElixir";
        WakanRestoreElixir.craft_action = (ae, elixir_entity, ingrediants) =>
        {
            ref ElementRoot er = ref ingrediants[0].GetComponent<ElementRoot>();
            elixir_entity.GetComponent<Elixir>().value = er.GetStrength();
        };
        WakanRestoreElixir.effect_type = ElixirEffectType.Restore;
        WakanRestoreElixir.effect_action = (ActorExtend ae, Entity elixir_entity, ref Elixir elixir) =>
        {
            ae.RestoreWakan(elixir.value);
        };
        WakanRestoreElixir.consumable_check_action = (ActorExtend ae, Entity elixir_entity, ref Elixir elixir) =>
            ae.HasCultisys<Xian>() && ae.GetCultisys<Xian>().wakan <
            ae.Base.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit;
        WakanRestoreElixir.ingrediants = new ElixirIngrediantCheck[]
        {
            new()
            {
                count = 1,
                element_root_id = ModClass.L.ElementRootLibrary.Common.id
            }
        };
    }
}
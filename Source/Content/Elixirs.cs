using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using strings;

namespace Cultiway.Content;

[Dependency(typeof(Jindans))]
public class Elixirs : ExtendLibrary<ElixirAsset, Elixirs>
{
    private const string      prefix = "Cultiway.Elixir";
    public static ElixirAsset OpenElementRootElixir { get; private set; }
    public static ElixirAsset WakanRestoreElixir { get; private set; }
    public static ElixirAsset EnlightenElixir { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets(prefix);
        OpenElementRootElixir.name_key = $"{prefix}.OpenElementRootElixir";
        OpenElementRootElixir.description_key = $"{prefix}.OpenElementRootElixir.Info";
        OpenElementRootElixir.craft_action = (ae, elixir_entity, ingredients) =>
        {
            ref ElementRoot er = ref ingredients[2].GetComponent<ElementRoot>();
            elixir_entity.AddComponent(er);
        };
        OpenElementRootElixir.SetupDataChange((ActorExtend ae, Entity elixir_entity, ref Elixir _) =>
        {
            ae.AddComponent(elixir_entity.GetComponent<ElementRoot>());
        });
        OpenElementRootElixir.consumable_check_action =
            (ActorExtend ae, Entity elixir_entity, ref Elixir _) => !ae.HasElementRoot();
        OpenElementRootElixir.ingredients = new ElixirIngredientCheck[]
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
        WakanRestoreElixir.description_key = $"{prefix}.WakanRestoreElixir.Info";
        WakanRestoreElixir.craft_action = (ae, elixir_entity, ingredients) =>
        {
            ref ElementRoot er = ref ingredients[0].GetComponent<ElementRoot>();
            elixir_entity.GetComponent<Elixir>().value = er.GetStrength();
        };
        WakanRestoreElixir.SetupRestore((ActorExtend ae, Entity elixir_entity, ref Elixir elixir) =>
        {
            ae.RestoreWakan(elixir.value);
        });
        WakanRestoreElixir.consumable_check_action = (ActorExtend ae, Entity elixir_entity, ref Elixir elixir) =>
            ae.HasCultisys<Xian>() && ae.GetCultisys<Xian>().wakan <
            ae.Base.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit;
        WakanRestoreElixir.ingredients = new ElixirIngredientCheck[]
        {
            new()
            {
                count = 1,
                element_root_id = ModClass.L.ElementRootLibrary.Common.id
            }
        };
        EnlightenElixir.name_key = $"{prefix}.EnlightenElixir";
        EnlightenElixir.description_key = $"{prefix}.EnlightenElixir.Info";
        EnlightenElixir.SetupStatusGain((ActorExtend ae, Entity elixir_entity, ref Elixir elixir) =>
        {
            var value = Randy.randomFloat(10, 60);// elixir.value;
            var status = StatusEffects.Enlighten.NewEntity();
            status.AddComponent(new StatusOverwriteStats(){stats = new BaseStats()
            {
                [S.intelligence] = value
            }});
            status.AddComponent(new AliveTimeLimit()
            {
                value = value
            });
            ae.AddSharedStatus(status);
        });
    }
}
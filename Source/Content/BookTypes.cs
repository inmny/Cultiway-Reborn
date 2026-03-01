using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

public class BookTypes : ExtendLibrary<BookTypeAsset, BookTypes>
{
    public static BookTypeAsset Cultibook { get; private set; }
    public static BookTypeAsset Elixirbook { get; private set; }
    public static BookTypeAsset Skillbook { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        Cultibook.requirement_check = (actor, _) =>
        {
            return actor.GetExtend().HasCultisys<Xian>();
        };
        Cultibook.name_template = WorldboxGame.NameGenerators.Cultibook.id;
        Cultibook.path_icons = "cultibook/";
        Cultibook.GetExtend<BookTypeAssetExtend>().custom_cover_name = "cultibook";
        Cultibook.GetExtend<BookTypeAssetExtend>().instance_read_action = LearnCultibook;
        
        
        Elixirbook.requirement_check = (actor, _) =>
        {
            return false;
        };
        Elixirbook.name_template = WorldboxGame.NameGenerators.Cultibook.id;
        Elixirbook.path_icons = "cultibook/";
        Elixirbook.GetExtend<BookTypeAssetExtend>().custom_cover_name = "cultibook";
        Elixirbook.GetExtend<BookTypeAssetExtend>().instance_read_action = LearnElixirbook;
        
        
        Skillbook.requirement_check = (actor, _) =>
        {
            return actor.GetExtend().HasCultisys<Xian>();
        };
        Skillbook.name_template = WorldboxGame.NameGenerators.Cultibook.id;
        Skillbook.path_icons = "cultibook/";
        Skillbook.GetExtend<BookTypeAssetExtend>().custom_cover_name = "cultibook";
        Skillbook.GetExtend<BookTypeAssetExtend>().instance_read_action = LearnSkillbook;
    }

    private static void LearnSkillbook(Actor actor, Book book, BookTypeAsset asset)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return;
        var be = book.GetExtend();
        if (!be.HasComponent<Skillbook>())
        {
            ModClass.LogError($"Book {book} ({be.E}) does not have Skillbook component!");
            return;
        }
        ae.LearnSkillV3(be.GetComponent<Skillbook>().SkillContainer, true);
    }
    private static void LearnElixirbook(Actor actor, Book book, BookTypeAsset asset)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return;
        var be = book.GetExtend();
        if (!be.HasComponent<Elixirbook>())
        {
            ModClass.LogError($"Book {book} ({be.E}) does not have Elixirbook component!");
            return;
        }
        var elixir_asset = be.GetComponent<Elixirbook>().Asset;
        var master = ae.GetMaster(elixir_asset);
        ae.Master(elixir_asset, master + 1);
    }
    private static void LearnCultibook(Actor actor, Book book, BookTypeAsset asset)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return;
        var be = book.GetExtend();
        if (!be.HasComponent<Cultibook>())
        {
            ModClass.LogError($"Book {book} ({be.E}) does not have Cultibook component!");
            return;
        }
        var cultibook_asset = be.GetComponent<Cultibook>().Asset;
        if (cultibook_asset == null) return;

        // 检查是否已有主修功法
        var mainCultibook = ae.GetMainCultibook();
        
        if (mainCultibook == null)
        {
            // 如果没有主修功法，设为新功法为主修
            ae.SetMainCultibook(cultibook_asset);
            // 初始掌握程度设为1%（表示刚开始学习）
            ae.AddMainCultibookMastery(1f);
            ae.Master(cultibook_asset, 1f);
        }
        else if (mainCultibook == cultibook_asset)
        {
            // 如果新功法就是主修功法，增加掌握程度
            ae.AddMainCultibookMastery(1f);
            ae.Master(cultibook_asset, ae.GetMainCultibookMastery() + 1);
        }
        else
        {
            // 如果已有主修功法，添加为了解
            var knownMastery = ae.GetMaster(cultibook_asset);
            // 如果之前没有了解过，初始化为1%了解程度
            // 如果已经了解过，增加了解程度（但上限较低，比如最多50%）
            var newMastery = knownMastery > 0 ? Mathf.Min(knownMastery + 1f, 50f) : 1f;
            ae.Master(cultibook_asset, newMastery);
        }
    }
}
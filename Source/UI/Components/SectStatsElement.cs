using System.Collections;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Components;

internal class SectStatsElement : WindowMetaElement<Sect, SectData>, IStatsElement, IRefreshElement
{
    internal const string IconCultibooks = "i_sect_cultibooks";
    internal const string IconElixirRecipes = "i_sect_elixir_recipes";
    internal const string IconSkillbooks = "i_sect_skillbooks";

    private StatsIconContainer _statsIcons;
    private StatsIcon[] _icons;

    GameObject IStatsElement.gameObject => gameObject;

    public override void Awake()
    {
        _statsIcons = gameObject.AddOrGetComponent<StatsIconContainer>();
        _icons = GetComponentsInChildren<StatsIcon>(true);
        base.Awake();
    }

    public override void clear()
    {
        ClearIcons();
        base.clear();
    }

    public void setIconValue(string name, float mainValue, float? max = null, string color = "", bool floatValue = false, string ending = "", char separator = '/')
    {
        _statsIcons.setIconValue(name, mainValue, max, color, floatValue, ending, separator);
    }

    public override IEnumerator showContent()
    {
        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) yield break;

        _statsIcons.showGeneralIcons<Sect, SectData>(sect);
        setIconValue("i_buildings", sect.CountBuildings());
        setIconValue(IconCultibooks, sect.data.CultibookCount);
        setIconValue(IconElixirRecipes, sect.data.ElixirRecipeCount);
        setIconValue(IconSkillbooks, sect.data.SkillbookCount);
    }

    private void ClearIcons()
    {
        if (_icons == null) return;

        for (int i = 0; i < _icons.Length; i++)
        {
            _icons[i].gameObject.SetActive(false);
        }
    }
}

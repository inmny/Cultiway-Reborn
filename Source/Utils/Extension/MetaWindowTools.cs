
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Utils.Extension;

public static class MetaWindowTools
{
    public static GameObject SetupTabTitleContainer<TWindow, TMeta, TMetaData>(this TWindow window, string tab_title_obj_name, string locale_key, string left_icon_path, string right_icon_path)
        where TWindow : WindowMetaGeneric<TMeta, TMetaData>
        where TMeta : CoreSystemObject<TMetaData>
        where TMetaData : BaseSystemData
    {
        var tab_title_obj = window.transform.Find("Background/Scroll View/Viewport/Content/" + tab_title_obj_name);
        if (tab_title_obj == null) return null;
        
        tab_title_obj.Find("title_tab").GetComponent<LocalizedText>().setKeyAndUpdate(locale_key);
        tab_title_obj.Find("icon_left").GetComponent<Image>().sprite = SpriteTextureLoader.getSprite(left_icon_path);
        tab_title_obj.Find("icon_right").GetComponent<Image>().sprite = SpriteTextureLoader.getSprite(right_icon_path);

        return tab_title_obj.gameObject;
    }
}
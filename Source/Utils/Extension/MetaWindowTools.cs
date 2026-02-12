
using System.Collections.Generic;
using System.Linq;
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
    public static void DeleteTab(this TabbedWindow window, string tab_name)
    {
        var all_tabs = window.GetComponentsInChildren<WindowMetaTab>(true);
        var tab = all_tabs.FirstOrDefault(t => t.name == tab_name);
        if (tab == null) return;

        var elements_to_delete = new List<GameObject>();
        foreach (var element in tab.tab_elements)
        {
            var used = false;
            foreach (var another_tab in all_tabs)
            {
                if (another_tab == tab) continue;

                if (another_tab.tab_elements.Contains(element))
                {
                    used = true;
                    break;
                }
            }
            if (used) continue;
            
            elements_to_delete.Add(element.gameObject);
        }
        tab.tab_elements.Clear();
        Object.DestroyImmediate(tab.gameObject);
        foreach (var element in elements_to_delete)
        {
            Object.DestroyImmediate(element);
        }
    }
}
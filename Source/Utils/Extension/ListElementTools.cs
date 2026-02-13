
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Utils.Extension;

public static class ListElementTools
{
    public static CountUpOnClick NewCount<TElement, TMeta, TData>(this TElement obj, string id, string icon_path)
        where TElement : WindowListElementBase<TMeta, TData>
        where TMeta : CoreSystemObject<TData>
        where TData : BaseSystemData
    {
        var prefab = obj.transform.Find("Icons/Age").gameObject;
        var new_count = Object.Instantiate(prefab, prefab.transform.parent);
        new_count.name = id;
        new_count.GetComponent<Image>().sprite = SpriteTextureLoader.getSprite(icon_path);
        return new_count.GetComponent<CountUpOnClick>();
    }
}
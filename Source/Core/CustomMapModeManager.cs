using System;
using System.Threading;
using Cultiway.Core.Libraries;
using UnityEngine;

namespace Cultiway.Core;

public class CustomMapModeManager
{
    public CustomMapLayer MapLayer { get; private set; }

    public CustomMapModeAsset CurrMapMode
    {
        get
        {
            var lib = ModClass.L.CustomMapModeLibrary;
            int len = lib.list.Count;
            for (int i = 0; i < len; i++)
            {
                if (PlayerConfig.optionBoolEnabled(lib.list[i].toggle_name))
                {
                    return lib.list[i];
                }
            }

            return null;
        }
    }

    public void Initialize()
    {
        GameObject custom_map_layer_obj =
            new("[layer]Energy Layer", typeof(CustomMapLayer), typeof(SpriteRenderer));
        custom_map_layer_obj.transform.SetParent(World.world.transform);
        custom_map_layer_obj.transform.localPosition = Vector3.zero;
        custom_map_layer_obj.transform.localScale = Vector3.one;
        custom_map_layer_obj.GetComponent<SpriteRenderer>().sortingOrder = 1;
        MapLayer = custom_map_layer_obj.GetComponent<CustomMapLayer>();
        World.world._map_layers.Add(MapLayer);

        StartUpdate();
    }

    private void StartUpdate()
    {
        new Thread(() =>
        {
            while (true)
                try
                {
                    Thread.Sleep(500);
                    MapLayer.PreparePixels();
                }
                catch (Exception e)
                {
                    //is_running = false;
                    ModClass.LogWarning("游戏时间倍率过高");
                    ModClass.LogWarning($"[{e.GetType()}]: {e.Message}\n{e.StackTrace}");
                    //LogService.LogErrorConcurrent(e.StackTrace);
                }
        }).Start();
    }

    public void SetAllDirty()
    {
        MapLayer.SetAllDirty();
    }
}
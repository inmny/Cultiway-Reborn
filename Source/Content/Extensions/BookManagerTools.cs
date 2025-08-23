using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class BookManagerTools
{
    private static CultibookLibrary _cultibookLibrary = Libraries.Manager.CultibookLibrary;
    public static Book CreateNewCultibook(this BookManager manager, Actor creator)
    {
        var ae = creator.GetExtend();
        var raw_cultibook = manager.GenerateNewBook(creator, BookTypes.Cultibook);
        if (raw_cultibook == null)
        {
            return null;
        }
        var be = raw_cultibook.GetExtend();
        var stats = new BaseStats();

        void SoftmaxStats(IList<string> stat_ids)
        {
            (string, float, float)[] armor_stats = new (string, float, float)[stat_ids.Count];
            var exp_sum = 0f;
            var max_val = 0f;
            for (int i = 0; i < armor_stats.Length; i++)
            {
                var stat_id = stat_ids[i];
                var stat_value = creator.stats[stat_id];
                max_val = Mathf.Max(max_val, stat_value);
                armor_stats[i] = (stat_id, stat_value, 0f);
            }

            for (int i = 0; i < armor_stats.Length; i++)
            {
                var stat_value = armor_stats[i].Item2;
                var exp_value = Mathf.Exp(stat_value - max_val);
                armor_stats[i].Item3 = exp_value;
                exp_sum += exp_value;
            }

            for (int i = 0; i < armor_stats.Length; i++)
            {
                armor_stats[i].Item3 /= exp_sum;
            }
            Array.Sort(armor_stats, (a, b) => b.Item3.CompareTo(a.Item3));
            var accum_prob = 0f;
            for (int i = 0; i < armor_stats.Length; i++)
            {
                var prob = armor_stats[i].Item3;
                accum_prob += prob;
                var stat_id = armor_stats[i].Item1;
                var stat_value = armor_stats[i].Item2;
                stats[stat_id] = stat_value;
                if (accum_prob > 0.9f)
                {
                    break;
                }
            }

        }
        SoftmaxStats(WorldboxGame.BaseStats.ArmorStats);
        SoftmaxStats(WorldboxGame.BaseStats.MasterStats);
        var cultibook = _cultibookLibrary.AddDynamic(new CultibookAsset()
        {
            id = Guid.NewGuid().ToString(),
            FinalStats = stats,
            Level = new ItemLevel(),
            Name = raw_cultibook.name
        });
        be.AddComponent(new Cultibook(cultibook.id));
        be.AddComponent(cultibook.Level);
        ae.Master(cultibook, 100);
        return raw_cultibook;
    }
}
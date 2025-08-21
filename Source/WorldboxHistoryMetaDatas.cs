using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Tables;
using Cultiway.Utils;
using db;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class HistoryMetaDatas : ExtendLibrary<HistoryMetaDataAsset, HistoryMetaDatas>
    {
        public static HistoryMetaDataAsset Sect { get; private set; }
        protected override void OnInit()
        {
            RegisterAssets();
            Sect.table_type = typeof(SectTable);
            Sect.table_types = new Dictionary<HistoryInterval, Type>()
            {
                { HistoryInterval.Yearly1, typeof(SectTableYearly1) },
                { HistoryInterval.Yearly5, typeof(SectTableYearly5) },
                { HistoryInterval.Yearly10, typeof(SectTableYearly10) },
                { HistoryInterval.Yearly50, typeof(SectTableYearly50) },
                { HistoryInterval.Yearly100, typeof(SectTableYearly100) },
                { HistoryInterval.Yearly500, typeof(SectTableYearly500) },
                { HistoryInterval.Yearly1000, typeof(SectTableYearly1000) },
                { HistoryInterval.Yearly5000, typeof(SectTableYearly5000) },
                { HistoryInterval.Yearly10000, typeof(SectTableYearly10000) },
            };
            Sect.collector = obj =>
            {
                var sect = (Sect)obj;
                return new SectTableYearly1()
                {
                    id = sect.getID(),
                    population = sect.countUnits(),
                    adults = sect.countAdults(),
                    children = sect.countChildren(),
                };
            };
        }

        protected override void PostInit(HistoryMetaDataAsset asset)
        {
            base.PostInit(asset);

            foreach (var prop in asset.table_type.GetTypeInfo().DeclaredProperties)
            {
                if (prop.CanRead && prop.CanWrite && prop.GetMethod != null && prop.SetMethod != null && prop.GetMethod.IsPublic && prop.SetMethod.IsPublic && !prop.GetMethod.IsStatic && !prop.SetMethod.IsStatic)
                {
                    var history_data_asset = AssetManager.history_data_library.get(prop.Name);
                    asset.categories.Add(history_data_asset);
                }
            }
            
            SmoothLoaderUtils.Insert(() =>
            {
                foreach (var type in asset.table_types.Values)
                {
                    DBTables.createOrMigrateTable(type);
                }
            }, $"Creating Stats ({asset.table_type.Name})", container => container.id.Contains("Creating Stats ("));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.GeoLib.Components
{
    /// <summary>
    /// 让一个实体记录指向当前世界中的某个地区，并可按地区编号取得对应对象。
    /// </summary>
    public struct GeoRegionBinder(long id) : IComponent
    {
        /// <summary>当前地区对象在地区管理器中的编号。</summary>
        public long ID = id;
        [Ignore]
        /// <summary>取得编号对应的地区；首次访问后保留结果，供本次游戏继续使用。</summary>
        public GeoRegion GeoRegion{
            get{
                if (_geo_region != null)
                    return _geo_region;
                _geo_region = WorldboxGame.I.GeoRegions.get(ID);
                return _geo_region;
            }
        }
        /// <summary>已经找到的地区对象，保留下来以免每次访问都重新查询。</summary>
        internal GeoRegion _geo_region;
    }
}
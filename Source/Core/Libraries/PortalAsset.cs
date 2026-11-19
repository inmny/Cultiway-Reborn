using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;

namespace Cultiway.Core.Libraries
{
    public class PortalAsset : Asset
    {
        public Action<Portal> RequestRebuildGraph;
        /// <summary>站点失效时取消整个行程，不把候车者移交到下一站。</summary>
        public bool CancelOnMissingBuilding;
        /// <summary>已经分配载具的行程不再追加或合并乘客。</summary>
        public bool FreezeAfterAssignment;
        /// <summary>内容层在请求登记时保存乘客所需的业务目标。</summary>
        public Action<Actor, Portal, Portal> PassengerRequested;
        /// <summary>提供确定的上下客点；没有专用规则时沿用原版施工点。</summary>
        public Func<Portal, WorldTile> PassengerTile;
        /// <summary>需要固定站序的运输类型提供与寻路估时一致的路线。</summary>
        public Func<Portal, Portal, Actor, IReadOnlyList<Portal>> ResolveRoute;
        /// <summary>内容层检查乘客当前是否允许申请运输。</summary>
        public Func<Actor, Portal, Portal, bool> CanRequest;
        /// <summary>只合并归属和运输批次一致的需求。</summary>
        public Func<Actor, string> PassengerGroupKey;
        /// <summary>运行中只允许加入符合既有站序且有空位的需求。</summary>
        public Func<Actor, PortalRequest, Portal, Portal, bool> CanJoinJourney;
        public HashSet<BuildingAsset> Buildings = new();
    }
}

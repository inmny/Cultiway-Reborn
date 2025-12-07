using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cultiway.Utils.Extension
{
    public static class BuildingTools
    {
        public static T GetBuildingComponent<T>(this Building building)
            where T : BaseBuildingComponent
        {
            if (building.components_list == null)
                return null;
            foreach (var component in building.components_list)
            {
                if (component is T t)
                {
                    return t;
                }
            }
            return null;
        }
    }
}

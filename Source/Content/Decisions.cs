using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Extensions;

namespace Cultiway.Content
{
    [Dependency(typeof(ActorTasks), typeof(SectJobs))]
    public class Decisions : ExtendLibrary<DecisionAsset, Decisions>
    {
        protected override bool AutoRegisterAssets()
        {
            return true;
        }
        public static DecisionAsset CivTravelToCity { get; private set; }
        public static DecisionAsset FindSectJob { get; private set; }
        protected override void OnInit()
        {
            InitCivs();
        }
        private void InitCivs()
        {
            CivTravelToCity.priority = NeuroLayer.Layer_1_Low;
            CivTravelToCity.path_icon = "ui/Icons/iconCity";
            CivTravelToCity.cooldown = 5;
            CivTravelToCity.unique = true;
            CivTravelToCity.action_check_launch = (Actor pActor) => pActor.city.hasZones();
            CivTravelToCity.weight = 0.5f;
            CivTravelToCity.list_civ = true;
            CivTravelToCity.task_id = ActorTasks.TravelToCity.id;

            FindSectJob.priority = NeuroLayer.Layer_3_High;
            FindSectJob.path_icon = "ui/Icons/iconShowTasks";
            FindSectJob.cooldown = SectConst.SectConstructionDecisionCooldown;
            FindSectJob.cooldown_on_launch_failure = true;
            FindSectJob.action_check_launch = SectJobRules.HasAssignableJob;
            FindSectJob.weight = 1.8f;
            FindSectJob.task_id = ActorTasks.FindSectJob.id;
        }
        protected override void GlobalPostInit()
        {
            base.GlobalPostInit();
            var list_only_civ = AssetManager.decisions_library.list_only_civ.ToList();
            var list_only_children = AssetManager.decisions_library.list_only_children.ToList();
            var list_only_city =AssetManager.decisions_library.list_only_city.ToList();
            var list_only_animal =AssetManager.decisions_library.list_only_animal.ToList();
            var list_others = AssetManager.decisions_library.list_others.ToList();
            var current_index = AssetManager.decisions_library.list.Count;
            foreach (var asset in assets_added)
            {
                asset.decision_index = current_index++;
                asset.priority_int_cached = (int)asset.priority;
                asset.has_weight_custom = asset.weight_calculate_custom != null;
                if (!asset.unique)
                {
                    if (asset.list_baby)
                    {
                        list_only_children.Add(asset);
                    }
                    else if (asset.list_animal)
                    {
                        list_only_animal.Add(asset);
                    }
                    else if (asset.list_civ)
                    {
                        list_only_civ.Add(asset);
                    }
                    else
                    {
                        list_others.Add(asset);
                    }
                }
            }
            AssetManager.decisions_library.list_only_civ = list_only_civ.ToArray();
            AssetManager.decisions_library.list_only_children = list_only_children.ToArray();
            AssetManager.decisions_library.list_only_city = list_only_city.ToArray();
            AssetManager.decisions_library.list_only_animal = list_only_animal.ToArray();
            AssetManager.decisions_library.list_others = list_others.ToArray();
        }
    }
}

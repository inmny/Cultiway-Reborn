using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>由藏宝阁库存关系派生供奉关系；所有结构变更均在查询结束后执行。</summary>
public sealed class ArtifactSectInstallationSystem : QuerySystem<SectInventoryBinder>
{
    private readonly List<RelationMutation> mutations = new();
    private readonly List<Sect> dirtySects = new();

    public ArtifactSectInstallationSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        mutations.Clear();
        dirtySects.Clear();
        Query.ForEachEntity((ref SectInventoryBinder binder, Entity inventory) =>
        {
            Sect sect = binder.Sect;
            var stored = inventory.GetRelations<InventoryRelation>();
            var installed = inventory.GetRelations<ArtifactSectInstallationRelation>();

            for (int i = 0; i < stored.Length; i++)
            {
                Entity artifact = stored[i].item;
                if (!artifact.IsAvailable() || !artifact.HasComponent<Artifact>() ||
                    !ArtifactSectService.HasSectUse(artifact) || Contains(installed, artifact)) continue;
                mutations.Add(new RelationMutation(inventory, artifact, sect, true));
            }

            for (int i = 0; i < installed.Length; i++)
            {
                Entity artifact = installed[i].artifact;
                if (artifact.IsAvailable() && Contains(stored, artifact) &&
                    artifact.HasComponent<Artifact>() && ArtifactSectService.HasSectUse(artifact)) continue;
                mutations.Add(new RelationMutation(inventory, artifact, sect, false));
            }
        });

        for (int i = 0; i < mutations.Count; i++)
        {
            RelationMutation mutation = mutations[i];
            if (mutation.add)
            {
                mutation.inventory.AddRelation(new ArtifactSectInstallationRelation
                {
                    artifact = mutation.artifact,
                });
            }
            else
            {
                mutation.inventory.RemoveRelation<ArtifactSectInstallationRelation>(mutation.artifact);
            }
            AddDirtySect(mutation.sect);
        }

        for (int i = 0; i < dirtySects.Count; i++)
        {
            if (dirtySects[i] != null && !dirtySects[i].isRekt())
            {
                ArtifactSectService.MarkMemberStatsDirty(dirtySects[i]);
            }
        }
    }

    private void AddDirtySect(Sect sect)
    {
        if (sect != null && !dirtySects.Contains(sect)) dirtySects.Add(sect);
    }

    private static bool Contains(Relations<ArtifactSectInstallationRelation> relations, Entity artifact)
    {
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].artifact == artifact) return true;
        }
        return false;
    }

    private static bool Contains(Relations<InventoryRelation> relations, Entity artifact)
    {
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == artifact) return true;
        }
        return false;
    }

    private readonly struct RelationMutation
    {
        public readonly Entity inventory;
        public readonly Entity artifact;
        public readonly Sect sect;
        public readonly bool add;

        public RelationMutation(Entity inventory, Entity artifact, Sect sect, bool add)
        {
            this.inventory = inventory;
            this.artifact = artifact;
            this.sect = sect;
            this.add = add;
        }
    }
}

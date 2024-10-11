using System;
using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct ElementRoot : IComponent
{
    public float            Iron  { get; private set; }
    public float            Wood  { get; private set; }
    public float            Water { get; private set; }
    public float            Fire  { get; private set; }
    public float            Earth { get; private set; }
    public ElementRootAsset Type  { get; private set; }
    public BaseStats        Stats { get; private set; }

    public ElementRoot(float[] composition)
    {
        for (int i = 0; i < 5; i++)
        {
            var value = composition[i];
            switch (i)
            {
                case ElementIndex.Iron:
                    Iron = value;
                    break;
                case ElementIndex.Wood:
                    Wood = value;
                    break;
                case ElementIndex.Water:
                    Water = value;
                    break;
                case ElementIndex.Fire:
                    Fire = value;
                    break;
                case ElementIndex.Earth:
                    Earth = value;
                    break;
            }
        }

        Stats = new();
        Update();
    }

    private void Update()
    {
        Type = ModClass.L.ElementRootLibrary.GetRootType([Iron, Wood, Water, Fire, Earth], out var sim);
        Stats.clear();
        Stats.MergeStats(Type.base_stats, sim);
    }

    public override string ToString()
    {
        return $"[{Type}]: {Iron}, {Wood}, {Water}, {Fire}, {Earth}";
    }

    public float this[int idx]
    {
        get => idx switch
        {
            ElementIndex.Iron  => Iron,
            ElementIndex.Wood  => Wood,
            ElementIndex.Water => Water,
            ElementIndex.Fire  => Fire,
            ElementIndex.Earth => Earth,
            _                  => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
        };
        private set
        {
            switch (idx)
            {
                case ElementIndex.Iron:
                    Iron = value;
                    break;
                case ElementIndex.Wood:
                    Wood = value;
                    break;
                case ElementIndex.Water:
                    Water = value;
                    break;
                case ElementIndex.Fire:
                    Fire = value;
                    break;
                case ElementIndex.Earth:
                    Earth = value;
                    break;
            }

            Update();
        }
    }
}
using System;
using Cultiway.Const;

namespace Cultiway.Core;

public struct ElementComposition
{
    public float iron;
    public float wood;
    public float water;
    public float fire;
    public float earth;

    public static class Static
    {
        internal static ElementComposition empty = new ElementComposition([0, 0, 0, 0, 0]);
    }

    public ElementComposition(float[] composition)
    {
        for (int i = 0; i < 5; i++)
        {
            this[i] = composition[i];
        }
    }

    public readonly float[] AsArray()
    {
        return
        [
            iron, wood, water, fire, earth
        ];
    }

    public float this[int idx]
    {
        get => idx switch
        {
            ElementIndex.Iron  => iron,
            ElementIndex.Wood  => wood,
            ElementIndex.Water => water,
            ElementIndex.Fire  => fire,
            ElementIndex.Earth => earth,
            _                  => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
        };
        set
        {
            switch (idx)
            {
                case ElementIndex.Iron:
                    iron = value;
                    break;
                case ElementIndex.Wood:
                    wood = value;
                    break;
                case ElementIndex.Water:
                    water = value;
                    break;
                case ElementIndex.Fire:
                    fire = value;
                    break;
                case ElementIndex.Earth:
                    earth = value;
                    break;
            }
        }
    }
}
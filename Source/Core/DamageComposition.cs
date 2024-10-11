using System;
using Cultiway.Const;

namespace Cultiway.Core;

public struct DamageComposition
{
    public ElementComposition element;
    public float              soul;

    public static class Static
    {
        internal static DamageComposition empty = new DamageComposition([0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0]);
    }

    public DamageComposition(float[] composition)
    {
        for (int i = 0; i < 6; i++)
        {
            this[i] = composition[i];
        }
    }

    public float[] AsArray()
    {
        return
        [
            element.iron, element.wood, element.water, element.fire, element.earth, soul
        ];
    }

    public float this[int idx]
    {
        get => idx switch
        {
            DamageIndex.Iron  => element.iron,
            DamageIndex.Wood  => element.wood,
            DamageIndex.Water => element.water,
            DamageIndex.Fire  => element.fire,
            DamageIndex.Earth => element.earth,
            DamageIndex.Soul  => soul,
            _                 => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
        };
        set
        {
            switch (idx)
            {
                case DamageIndex.Iron:
                case DamageIndex.Wood:
                case DamageIndex.Water:
                case DamageIndex.Fire:
                case DamageIndex.Earth:
                    element[idx] = value;
                    break;
                case DamageIndex.Soul:
                    soul = value;
                    break;
            }
        }
    }
}
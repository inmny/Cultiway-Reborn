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
    public float neg;
    public float pos;
    public float entropy;

    public static class Static
    {
        internal static ElementComposition empty = new([1, 1, 1, 1, 1, 1, 1, 0]);
    }

    public void Normalize()
    {
        float sum = 0;
        int i;
        for (i = 0; i < 8; i++) sum += this[i];

        if (sum == 0)
            for (i = 0; i < 8; i++)
                this[i] = 0.125f;
        else
            for (i = 0; i < 8; i++)
                this[i] /= sum;
    }

    public ElementComposition(float[] composition, bool normalize = false)
    {
        for (var i = 0; i < 8; i++)
        {
            this[i] = composition[i];
        }

        if (normalize)
            Normalize();
    }

    public override string ToString()
    {
        return $"{iron}, {wood}, {water}, {fire}, {earth}, ({neg}, {pos}), [{entropy}]";
    }

    public readonly float[] AsArray()
    {
        return
        [
            iron, wood, water, fire, earth, neg, pos, entropy
        ];
    }

    public float this[int idx]
    {
        get => idx switch
        {
            ElementIndex.Iron    => iron,
            ElementIndex.Wood    => wood,
            ElementIndex.Water   => water,
            ElementIndex.Fire    => fire,
            ElementIndex.Earth   => earth,
            ElementIndex.Neg     => neg,
            ElementIndex.Pos     => pos,
            ElementIndex.Entropy => entropy,
            _                    => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
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
                case ElementIndex.Neg:
                    neg = value;
                    break;
                case ElementIndex.Pos:
                    pos = value;
                    break;
                case ElementIndex.Entropy:
                    entropy = value;
                    break;
            }
        }
    }
}
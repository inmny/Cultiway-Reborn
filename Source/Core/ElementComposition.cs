using System;
using Cultiway.Const;
using Cultiway.Core.Semantics;
using UnityEngine;

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
        public static readonly ElementComposition Iron = new(iron: 1f, normalize: true);
        public static readonly ElementComposition Wood = new(wood: 1f, normalize: true);
        public static readonly ElementComposition Water = new(water: 1f, normalize: true);
        public static readonly ElementComposition Fire = new(fire: 1f, normalize: true);
        public static readonly ElementComposition Earth = new(earth: 1f, normalize: true);

        /// <summary>纯阴属性伤害构成，魂系攻击统一使用此值。</summary>
        public static readonly ElementComposition Neg = new(neg: 1f, normalize: true);

        public static readonly ElementComposition IronWood =
            new(iron: 0.5f, wood: 0.5f, normalize: true);
        public static readonly ElementComposition IronWater =
            new(iron: 0.5f, water: 0.5f, normalize: true);
        public static readonly ElementComposition IronFire =
            new(iron: 0.5f, fire: 0.5f, normalize: true);
        public static readonly ElementComposition IronEarth =
            new(iron: 0.5f, earth: 0.5f, normalize: true);
        public static readonly ElementComposition WoodWater =
            new(wood: 0.5f, water: 0.5f, normalize: true);
        public static readonly ElementComposition WoodFire =
            new(wood: 0.5f, fire: 0.5f, normalize: true);
        public static readonly ElementComposition WoodEarth =
            new(wood: 0.5f, earth: 0.5f, normalize: true);
        public static readonly ElementComposition WaterFire =
            new(water: 0.5f, fire: 0.5f, normalize: true);
        public static readonly ElementComposition WaterEarth =
            new(water: 0.5f, earth: 0.5f, normalize: true);
        public static readonly ElementComposition FireEarth =
            new(fire: 0.5f, earth: 0.5f, normalize: true);
        public static readonly ElementComposition Wind =
            new(wood: 0.425f, water: 0.425f, entropy: 0.15f, normalize: true);
        public static readonly ElementComposition Ice = new(water: 0.7f, neg: 0.3f, normalize: true);
        public static readonly ElementComposition Lightning =
            new(iron: 0.25f, pos: 0.5f, entropy: 0.25f, normalize: true);
        public static readonly ElementComposition Poison =
            new(wood: 0.4f, water: 0.25f, neg: 0.35f, normalize: true);
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

    public ElementComposition(float iron = 0, float wood = 0, float water = 0, float fire = 0, float earth = 0, float neg = 0, float pos = 0,
        float entropy = 0, bool normalize = false)
    {
        this.iron = iron;
        this.wood = wood;
        this.water = water;
        this.fire = fire;
        this.earth = earth;
        this.neg = neg;
        this.pos = pos;
        this.entropy = entropy;
        if (normalize)
            Normalize();
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

    public string HexColor()
    {
        var palette = SemanticColorResolver.Resolve(ElementSemanticProfileService.Build(this));
        return Toolbox.colorToHex(palette.GetColor(0, Color.white));
    }
}

using System;
using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using NeoModLoader.services;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct ElementRoot : IComponent
{
    public float            Iron    { get; private set; }
    public float            Wood    { get; private set; }
    public float            Water   { get; private set; }
    public float            Fire    { get; private set; }
    public float            Earth   { get; private set; }
    public float            Neg     { get; private set; }
    public float            Pos     { get; private set; }
    public float            Entropy { get; private set; }
    [Ignore]
    public ElementRootAsset Type    { get; private set; }
    [Ignore]
    public BaseStats        Stats   { get; }

    public ElementRoot(float[] composition)
    {
        for (var i = 0; i < 8; i++)
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
                case ElementIndex.Neg:
                    Neg = value;
                    break;
                case ElementIndex.Pos:
                    Pos = value;
                    break;
                case ElementIndex.Entropy:
                    Entropy = value;
                    break;
            }
        }

        Stats = new();
        Update();
    }

    public static ElementRoot Roll()
    {
        var composition = new float[8];
        for (var i = 0; i < 8; i++) composition[i] = Mathf.Abs(RdUtils.NextStdNormal());

        return new ElementRoot(composition);
    }

    public float GetStrength()
    {
        return Mathf.Exp(
            (
                (Iron  + Wood + Water + Fire + Earth) / 5
                + (Neg + Pos)                         / 2
                + Entropy
            ) / 3
        );
    }

    private void Update()
    {
        Type = ModClass.L.ElementRootLibrary.GetRootType([Iron, Wood, Water, Fire, Earth, Neg, Pos, Entropy],
            out var sim);
        Stats.clear();
        Stats.MergeStats(Type.base_stats, sim);
        Stats[nameof(WorldboxGame.BaseStats.IronArmor)] = Iron;
        Stats[nameof(WorldboxGame.BaseStats.WoodArmor)] = Wood;
        Stats[nameof(WorldboxGame.BaseStats.WaterArmor)] = Water;
        Stats[nameof(WorldboxGame.BaseStats.FireArmor)] = Fire;
        Stats[nameof(WorldboxGame.BaseStats.EarthArmor)] = Earth;
        Stats[nameof(WorldboxGame.BaseStats.NegArmor)] = Neg;
        Stats[nameof(WorldboxGame.BaseStats.PosArmor)] = Pos;
        Stats[nameof(WorldboxGame.BaseStats.EntropyArmor)] = Entropy;

        Stats[nameof(WorldboxGame.BaseStats.IronMaster)] = Iron;
        Stats[nameof(WorldboxGame.BaseStats.WoodMaster)] = Wood;
        Stats[nameof(WorldboxGame.BaseStats.WaterMaster)] = Water;
        Stats[nameof(WorldboxGame.BaseStats.FireMaster)] = Fire;
        Stats[nameof(WorldboxGame.BaseStats.EarthMaster)] = Earth;
        Stats[nameof(WorldboxGame.BaseStats.NegMaster)] = Neg;
        Stats[nameof(WorldboxGame.BaseStats.PosMaster)] = Pos;
        Stats[nameof(WorldboxGame.BaseStats.EntropyMaster)] = Entropy;


        Stats["Mod" + nameof(WorldboxGame.BaseStats.IronMaster)] = Mathf.Exp(Iron);
        Stats["Mod" + nameof(WorldboxGame.BaseStats.WoodMaster)] = Mathf.Exp(Wood);
        Stats["Mod" + nameof(WorldboxGame.BaseStats.WaterMaster)] = Mathf.Exp(Water);
        Stats["Mod" + nameof(WorldboxGame.BaseStats.FireMaster)] = Mathf.Exp(Fire);
        Stats["Mod" + nameof(WorldboxGame.BaseStats.EarthMaster)] = Mathf.Exp(Earth);
        Stats["Mod" + nameof(WorldboxGame.BaseStats.NegMaster)] = Mathf.Exp(Neg);
        Stats["Mod" + nameof(WorldboxGame.BaseStats.PosMaster)] = Mathf.Exp(Pos);
        Stats["Mod" + nameof(WorldboxGame.BaseStats.EntropyMaster)] = Mathf.Exp(Entropy);
    }

    public override string ToString()
    {
        return $"[{Type}]: {Iron}, {Wood}, {Water}, {Fire}, {Earth}, ({Neg}, {Pos}), [{Entropy}]";
    }

    public float this[int idx]
    {
        get => idx switch
        {
            ElementIndex.Iron    => Iron,
            ElementIndex.Wood    => Wood,
            ElementIndex.Water   => Water,
            ElementIndex.Fire    => Fire,
            ElementIndex.Earth   => Earth,
            ElementIndex.Neg     => Neg,
            ElementIndex.Pos     => Pos,
            ElementIndex.Entropy => Entropy,
            _                    => throw new ArgumentOutOfRangeException(nameof(idx), idx, null)
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
                case ElementIndex.Neg:
                    Neg = value;
                    break;
                case ElementIndex.Pos:
                    Pos = value;
                    break;
                case ElementIndex.Entropy:
                    Entropy = value;
                    break;
            }

            Update();
        }
    }
}
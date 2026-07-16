using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Pathfinding;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>从已装备法器中确定性选择当前御器载具，并维护唯一的载具关系。</summary>
public static class ArtifactVehicleService
{
    private const float BoardingRadius = 3f;

    public static bool TryResolve(Actor actor, out ArtifactVehicleCandidate result)
    {
        result = default;
        bool found = false;
        var relations = actor.GetExtend().E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            EquippedArtifactRelation relation = relations[i];
            Entity artifact = relation.artifact;
            if (!artifact.IsAvailable()) continue;

            ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
            for (int j = 0; j < abilitySet.abilities.Length; j++)
            {
                ArtifactAbilityInstance ability = abilitySet.abilities[j];
                ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
                ArtifactVehicleAbilityProfile profile = asset.vehicle_use;
                if (profile == null ||
                    !ArtifactAbilityLifecycle.MeetsState(relation.state, profile.minimum_state)) continue;

                ArtifactVehicleCandidate candidate = new(
                    artifact,
                    ability.instance_id,
                    relation.priority,
                    Mathf.Max(0.1f, profile.ResolveSpeedMultiplier?.Invoke(ability) ?? 1f),
                    Mathf.Max(1, profile.ResolvePassengerCapacity?.Invoke(ability) ?? 1));
                if (!found || IsBetter(candidate, result))
                {
                    result = candidate;
                    found = true;
                }
            }
        }
        return found;
    }

    public static float ResolveFlightSpeedMultiplier(Actor actor, float fallback)
    {
        return TryResolve(actor, out ArtifactVehicleCandidate candidate)
            ? Mathf.Max(fallback, candidate.speed_multiplier)
            : fallback;
    }

    public static void SyncFlightRelation(Actor actor)
    {
        Entity owner = actor.GetExtend().E;
        if (!TryResolve(actor, out ArtifactVehicleCandidate candidate))
        {
            ClearFlightRelation(owner);
            return;
        }

        var relations = owner.GetRelations<ArtifactVehicleRelation>();
        if (relations.Length == 1 &&
            relations[0].artifact == candidate.artifact &&
            relations[0].ability_instance_id == candidate.ability_instance_id)
        {
            TrimPassengers(actor, candidate.passenger_capacity);
            return;
        }

        ClearFlightRelation(owner, false);
        owner.AddRelation(new ArtifactVehicleRelation
        {
            artifact = candidate.artifact,
            ability_instance_id = candidate.ability_instance_id,
        });
        TrimPassengers(actor, candidate.passenger_capacity);
    }

    public static void ClearFlightRelation(Actor actor)
    {
        ClearFlightRelation(actor.GetExtend().E);
    }

    private static void ClearFlightRelation(Entity owner, bool disembarkPassengers = true)
    {
        if (disembarkPassengers) DisembarkAll(owner);
        var relations = owner.GetRelations<ArtifactVehicleRelation>();
        if (relations.Length == 0) return;
        Entity[] artifacts = new Entity[relations.Length];
        for (int i = 0; i < relations.Length; i++) artifacts[i] = relations[i].artifact;
        for (int i = 0; i < artifacts.Length; i++)
        {
            owner.RemoveRelation<ArtifactVehicleRelation>(artifacts[i]);
        }
    }

    /// <summary>按当前载具容量搭载近旁同伴，返回本次新增乘员数。</summary>
    public static int BoardNearbyPassengers(Actor driver)
    {
        if (!driver.data.hasFlag(ContentActorDataKeys.IsFlying_flag) ||
            !TryResolve(driver, out ArtifactVehicleCandidate vehicle)) return 0;

        using ListPool<Actor> candidates = new();
        if (driver.hasArmy() && driver.army.getCaptain() == driver)
        {
            for (int i = 0; i < driver.army.units.Count; i++)
            {
                Actor passenger = driver.army.units[i];
                if (CanBoard(driver, passenger)) candidates.Add(passenger);
            }
        }
        else if (driver.hasStatus(S_Status.possessed) && ControllableUnit.isControllingUnit(driver))
        {
            foreach (Actor passenger in Finder.getUnitsFromChunk(driver.current_tile, 1))
            {
                if (CanBoard(driver, passenger)) candidates.Add(passenger);
            }
        }

        candidates.Sort((left, right) =>
        {
            float leftDistance = Toolbox.SquaredDistVec2Float(driver.current_position, left.current_position);
            float rightDistance = Toolbox.SquaredDistVec2Float(driver.current_position, right.current_position);
            int comparison = leftDistance.CompareTo(rightDistance);
            return comparison != 0 ? comparison : left.data.id.CompareTo(right.data.id);
        });

        int boarded = 0;
        for (int i = 0; i < candidates.Count && CountPassengers(driver) < vehicle.passenger_capacity; i++)
        {
            if (TryBoard(driver, candidates[i])) boarded++;
        }
        return boarded;
    }

    /// <summary>将一个近旁友方实体加入当前载具，供玩家交互、AI 和剧情能力共用。</summary>
    public static bool TryBoard(Actor driver, Actor passenger)
    {
        if (!CanBoard(driver, passenger) ||
            !driver.data.hasFlag(ContentActorDataKeys.IsFlying_flag) ||
            !TryResolve(driver, out ArtifactVehicleCandidate vehicle)) return false;

        Entity passengerEntity = passenger.GetExtend().E;
        if (passengerEntity.TryGetComponent(out ArtifactVehiclePassenger existing))
        {
            return existing.driver == driver.GetExtend().E;
        }

        Entity driverEntity = driver.GetExtend().E;
        int seatIndex = ResolveFreeSeat(driverEntity, vehicle.passenger_capacity);
        if (seatIndex < 0) return false;

        driverEntity.AddRelation(new ArtifactVehiclePassengerRelation { passenger = passengerEntity });
        passengerEntity.AddComponent(new ArtifactVehiclePassenger
        {
            driver = driverEntity,
            seat_index = seatIndex,
            was_flying = passenger.isFlying(),
        });
        passenger.stopMovement();
        PathFinder.Instance.Cancel(passenger);
        UpdatePassengerPose(passenger, driver, seatIndex);
        return true;
    }

    /// <summary>解除一个乘员与载具的关系，并恢复其登乘前的飞行语义。</summary>
    public static void Disembark(Entity passengerEntity)
    {
        if (!passengerEntity.IsAvailable() ||
            !passengerEntity.TryGetComponent(out ArtifactVehiclePassenger state)) return;

        if (state.driver.IsAvailable())
        {
            state.driver.RemoveRelation<ArtifactVehiclePassengerRelation>(passengerEntity);
        }
        if (passengerEntity.TryGetComponent(out ActorBinder binder) && binder.Actor != null)
        {
            binder.Actor.setFlying(state.was_flying || binder.Actor.asset.flying);
            binder.Actor.velocity = Vector3.zero;
            binder.Actor.under_forces = false;
        }
        passengerEntity.RemoveComponent<ArtifactVehiclePassenger>();
    }

    internal static void UpdatePassengerPose(Actor passenger, Actor driver, int seatIndex)
    {
        float angle = (-90f + seatIndex * 137.5f) * Mathf.Deg2Rad;
        float radius = 0.38f + seatIndex / 4 * 0.16f;
        Vector2 offset = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        passenger.stopMovement();
        passenger.setCurrentTilePosition(driver.current_tile);
        passenger.current_position += offset;
        passenger.position_height = driver.position_height + 0.12f;
        passenger.velocity = Vector3.zero;
        passenger.under_forces = false;
        passenger.setFlying(true);
        passenger.skipBehaviour();
    }

    private static bool CanBoard(Actor driver, Actor passenger)
    {
        if (passenger == null || passenger == driver || !passenger.isAlive() ||
            passenger.kingdom != driver.kingdom || passenger.isInsideSomething() ||
            passenger.data.hasFlag(ContentActorDataKeys.IsFlying_flag)) return false;
        float distance = Toolbox.SquaredDistVec2Float(driver.current_position, passenger.current_position);
        return distance <= BoardingRadius * BoardingRadius;
    }

    private static int CountPassengers(Actor driver)
    {
        return driver.GetExtend().E.GetRelations<ArtifactVehiclePassengerRelation>().Length;
    }

    private static int ResolveFreeSeat(Entity driver, int capacity)
    {
        bool[] occupied = new bool[capacity];
        var relations = driver.GetRelations<ArtifactVehiclePassengerRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity passenger = relations[i].passenger;
            if (!passenger.IsAvailable() ||
                !passenger.TryGetComponent(out ArtifactVehiclePassenger state)) continue;
            if (state.seat_index >= 0 && state.seat_index < occupied.Length) occupied[state.seat_index] = true;
        }
        for (int i = 0; i < occupied.Length; i++)
        {
            if (!occupied[i]) return i;
        }
        return -1;
    }

    private static void TrimPassengers(Actor driver, int capacity)
    {
        Entity driverEntity = driver.GetExtend().E;
        var relations = driverEntity.GetRelations<ArtifactVehiclePassengerRelation>();
        var passengers = new List<Entity>(relations.Length);
        var stalePassengers = new List<Entity>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity passenger = relations[i].passenger;
            if (passenger.IsAvailable() && passenger.HasComponent<ArtifactVehiclePassenger>())
            {
                passengers.Add(passenger);
            }
            else
            {
                stalePassengers.Add(passenger);
            }
        }
        for (int i = 0; i < stalePassengers.Count; i++)
        {
            driverEntity.RemoveRelation<ArtifactVehiclePassengerRelation>(stalePassengers[i]);
        }
        passengers.Sort((left, right) =>
            left.GetComponent<ArtifactVehiclePassenger>().seat_index.CompareTo(
                right.GetComponent<ArtifactVehiclePassenger>().seat_index));
        for (int i = capacity; i < passengers.Count; i++) Disembark(passengers[i]);
    }

    private static void DisembarkAll(Entity driver)
    {
        var relations = driver.GetRelations<ArtifactVehiclePassengerRelation>();
        Entity[] passengers = new Entity[relations.Length];
        for (int i = 0; i < relations.Length; i++) passengers[i] = relations[i].passenger;
        for (int i = 0; i < passengers.Length; i++)
        {
            if (passengers[i].IsAvailable() && passengers[i].HasComponent<ArtifactVehiclePassenger>())
            {
                Disembark(passengers[i]);
            }
            else
            {
                driver.RemoveRelation<ArtifactVehiclePassengerRelation>(passengers[i]);
            }
        }
    }

    private static bool IsBetter(ArtifactVehicleCandidate candidate, ArtifactVehicleCandidate current)
    {
        int comparison = candidate.priority.CompareTo(current.priority);
        if (comparison != 0) return comparison > 0;
        comparison = candidate.speed_multiplier.CompareTo(current.speed_multiplier);
        if (comparison != 0) return comparison > 0;
        comparison = candidate.passenger_capacity.CompareTo(current.passenger_capacity);
        if (comparison != 0) return comparison > 0;
        comparison = candidate.artifact.Id.CompareTo(current.artifact.Id);
        if (comparison != 0) return comparison < 0;
        return string.CompareOrdinal(candidate.ability_instance_id, current.ability_instance_id) < 0;
    }
}

public readonly struct ArtifactVehicleCandidate
{
    public readonly Entity artifact;
    public readonly string ability_instance_id;
    public readonly int priority;
    public readonly float speed_multiplier;
    public readonly int passenger_capacity;

    public ArtifactVehicleCandidate(
        Entity artifact,
        string abilityInstanceId,
        int priority,
        float speedMultiplier,
        int passengerCapacity)
    {
        this.artifact = artifact;
        ability_instance_id = abilityInstanceId;
        this.priority = priority;
        speed_multiplier = speedMultiplier;
        passenger_capacity = passengerCapacity;
    }
}

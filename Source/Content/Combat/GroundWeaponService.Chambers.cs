using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Combat;

internal sealed partial class GroundWeaponService
{
    /// <summary>一个器口一次只接入一件原兵器。临时关系不写入档案。</summary>
    internal sealed class Chamber
    {
        internal Entry Held;
        internal double EnteredAt, NextPick;
        internal Vector3 Arrival;
        internal Quaternion ArrivalRotation;
        internal bool Loaded;

        /// <summary>停在最后实际位置，现有地面重力和拾取接手。</summary>
        internal void Release()
        {
            if (Held?.WeaponChamber == this)
            {
                SetBottom(Held);
                Held.WeaponChamber = null;
            }
            Held = null;
        }
    }

    /// <summary>确认真器口内最近的一件同一原物，继续保存它的真实随动姿态。</summary>
    internal static void DriveChamber(Chamber chamber, in WeaponChamberFrame frame, double clock)
    {
        if (!chamber.Loaded && frame.Accepting && clock >= chamber.NextPick)
        {
            chamber.NextPick = clock + .075;
            Entry closest = null;
            float distance = float.PositiveInfinity;
            foreach (Entry entry in entries.Values)
            {
                if (!AirflowEligible(entry) || entry.Data.Length > frame.Depth * .72f) continue;
                Vector3 position = Vector(entry.Data.Position);
                if (!frame.Contains(position)) continue;
                float candidate = (position - frame.Mouth).sqrMagnitude;
                if (candidate > distance || candidate == distance && entry.Data.ItemId >= closest.Data.ItemId) continue;
                distance = candidate;
                closest = entry;
            }
            if (closest != null)
            {
                chamber.Held = closest;
                chamber.Loaded = true;
                chamber.EnteredAt = clock;
                var toLocal = Quaternion.Inverse(frame.Rotation);
                chamber.Arrival = toLocal * (Vector(closest.Data.Position) - frame.Mouth);
                chamber.ArrivalRotation = toLocal * Rotation(closest.Data);
                closest.WeaponChamber = chamber;
                Carry(closest, Vector2.zero, 0f);
            }
        }

        Entry held = chamber.Held;
        if (held == null) return;
        if (!entries.TryGetValue(held.Data.ItemId, out Entry current) || current != held ||
            !held.Item.isAlive() || held.Item.shouldbe_removed || held.Item.hasActor() || held.Item.hasCity())
        {
            chamber.Release();
            return;
        }

        float draw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)(clock - chamber.EnteredAt) / .24f));
        float axis = Mathf.Lerp(-.52f, WeaponChamberFrame.LipDepth, frame.ExitProgress * frame.ExitProgress);
        Vector3 local = Vector3.Lerp(chamber.Arrival, Vector3.forward * (frame.Depth * axis), draw);
        Vector3 location = frame.Mouth + frame.Rotation * local;
        Quaternion rotation = frame.Rotation * Quaternion.Slerp(chamber.ArrivalRotation, Quaternion.identity, draw);
        held.Data.Position = Values(location);
        held.Data.Rotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w };
        held.Time = clock;
        if (!frame.Releasing) return;

        Carry(held, frame.Forward * frame.ReleaseSpeed, Mathf.Min(4.8f, frame.ReleaseSpeed * .22f));
        chamber.Release();
    }
}

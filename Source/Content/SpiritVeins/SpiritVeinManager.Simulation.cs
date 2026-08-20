using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

public sealed partial class SpiritVeinManager
{
    /// <summary>每月让灵气沿脉节下行，再由主次脉节共同供养脉域并结算局部污染。</summary>
    internal void UpdateMonth()
    {
        if (!IsReady || !WorldWakanService.IsInitialized) return;
        float tide = ResolveTideMultiplier();
        RecoverSections(tide);
        TransferBetweenSections(tide);
        SupplyField(tide);
        UpdateSectionPollution();
        PropagatePollutionDownstream();
        RefreshGroundsAndEyes();
        for (int i = 0; i < sections.Count; i++) sections[i].RefreshStatus();
        AdvancePendingTerrainChanges();
        displayRevision = NextRevision(displayRevision);
        WorldWakanService.PublishDisplayValues();
    }

    private void RecoverSections(float tide)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection section = sections[i];
            float recovery = section.MonthlyRecovery * tide * Mathf.Lerp(0.22f, 1f, section.Purity);
            if (section.Kind == VeinSectionKind.SourceDomain) recovery *= 1.18f;
            section.CurrentAmount = Mathf.Min(section.Capacity, section.CurrentAmount + recovery);
        }
    }

    private void TransferBetweenSections(float tide)
    {
        int maximumId = 0;
        for (int i = 0; i < sections.Count; i++) maximumId = Mathf.Max(maximumId, sections[i].Id);
        var incoming = new float[maximumId + 1];
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection source = sections[i];
            if (source.DownstreamSectionIds.Length == 0 || source.CurrentAmount <= 0f) continue;
            float available = Mathf.Min(
                source.CurrentAmount * 0.18f,
                source.MonthlyTransfer * tide * Mathf.Lerp(0.08f, 1f, source.Patency));
            if (available <= 0f) continue;
            float each = available / source.DownstreamSectionIds.Length;
            float transferred = 0f;
            for (int downstreamIndex = 0; downstreamIndex < source.DownstreamSectionIds.Length; downstreamIndex++)
            {
                SpiritVeinSection target = GetSection(source.DownstreamSectionIds[downstreamIndex]);
                if (target == null) continue;
                float room = Mathf.Max(0f, target.Capacity - target.CurrentAmount - incoming[target.Id]);
                float amount = Mathf.Min(each, room);
                if (amount <= 0f) continue;
                incoming[target.Id] += amount;
                transferred += amount;
            }
            source.CurrentAmount = Mathf.Max(0f, source.CurrentAmount - transferred);
        }

        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection section = sections[i];
            if ((uint)section.Id >= (uint)incoming.Length || incoming[section.Id] <= 0f) continue;
            section.CurrentAmount = Mathf.Min(section.Capacity, section.CurrentAmount + incoming[section.Id]);
        }
    }

    private void SupplyField(float tide)
    {
        if (fieldTileIds.Length == 0) return;
        int maximumId = 0;
        for (int i = 0; i < sections.Count; i++) maximumId = Mathf.Max(maximumId, sections[i].Id);
        var budgets = new float[maximumId + 1];
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection section = sections[i];
            budgets[section.Id] = Mathf.Min(section.CurrentAmount, section.EffectiveSupply * tide);
        }

        int start = supplyCursor % fieldTileIds.Length;
        for (int offset = 0; offset < fieldTileIds.Length; offset++)
        {
            int tileId = fieldTileIds[(start + offset) % fieldTileIds.Length];
            float target = ResolveDynamicTarget(tileId);
            float deficit = target - WorldWakanService.GetClean(tileId);
            if (deficit <= 0.001f) continue;
            SpiritVeinSection primary = GetSection(field.SectionByTile[tileId]);
            SpiritVeinSection secondary = GetSection(field.SecondarySectionByTile[tileId]);
            float primaryWeight = field.FieldStrength[tileId];
            float secondaryWeight = field.SecondaryStrength[tileId] * 0.38f;
            float totalWeight = Mathf.Max(0.001f, primaryWeight + secondaryWeight);
            float firstRequest = deficit * primaryWeight / totalWeight;
            float supplied = SupplyTile(primary, budgets, tileId, firstRequest);
            supplied += SupplyTile(secondary, budgets, tileId, deficit - supplied);
            if (supplied < deficit) SupplyTile(primary, budgets, tileId, deficit - supplied);
        }
        supplyCursor = (start + Mathf.Max(1, fieldTileIds.Length / 9)) % fieldTileIds.Length;
    }

    private static float SupplyTile(
        SpiritVeinSection section,
        float[] budgets,
        int tileId,
        float requested)
    {
        if (section == null || requested <= 0f || (uint)section.Id >= (uint)budgets.Length) return 0f;
        float spend = Mathf.Min(requested, budgets[section.Id], section.CurrentAmount);
        if (spend <= 0f) return 0f;
        float dirtyRatio = section.Purity < 0.4f ? Mathf.Clamp01((0.4f - section.Purity) * 0.35f) : 0f;
        float dirtyAdded = WorldWakanService.AddDirty(tileId, spend * dirtyRatio);
        float cleanAdded = WorldWakanService.AddClean(tileId, spend * (1f - dirtyRatio));
        float actual = dirtyAdded + cleanAdded;
        budgets[section.Id] = Mathf.Max(0f, budgets[section.Id] - actual);
        section.CurrentAmount = Mathf.Max(0f, section.CurrentAmount - actual);
        return actual;
    }

    private float ResolveDynamicTarget(int tileId)
    {
        float target = SpiritVeinSettings.BackgroundCleanWakan;
        SpiritVeinSection primary = GetSection(field.SectionByTile[tileId]);
        SpiritVein primaryVein = primary == null ? null : GetVeinByTopologyId(primary.VeinId);
        if (primary != null && primaryVein != null)
        {
            target += ResolveSectionContribution(
                primary,
                primaryVein,
                field.FieldStrength[tileId],
                field.Convergence[tileId],
                field.Leakage[tileId]);
        }

        SpiritVeinSection secondary = GetSection(field.SecondarySectionByTile[tileId]);
        SpiritVein secondaryVein = secondary == null ? null : GetVeinByTopologyId(secondary.VeinId);
        if (secondary != null && secondaryVein != null)
        {
            target += ResolveSectionContribution(
                secondary,
                secondaryVein,
                field.SecondaryStrength[tileId] * 0.38f,
                field.Convergence[tileId],
                field.Leakage[tileId]);
        }

        GatheringGround ground = GetGround(field.GroundByTile[tileId]);
        if (ground != null)
        {
            SpiritVeinEye eye = GetEye(ground.EyeId);
            float quality = 1.18f + (int)ground.Quality * 0.18f;
            float state = Mathf.Lerp(0.15f, 1f, ground.FillRatio) * Mathf.Lerp(0.2f, 1f, ground.Purity);
            target *= Mathf.Lerp(1f, quality, field.Convergence[tileId] * state);
            if (eye != null)
            {
                int distance = TileDistance(tileId, eye.TileId);
                float eyeFalloff = Mathf.Clamp01(1f - distance / Mathf.Max(2f, Mathf.Sqrt(ground.TileIds.Length)));
                target += eye.BaseConcentration * eyeFalloff * 0.28f * state;
            }
        }
        return Mathf.Clamp(target, SpiritVeinSettings.BackgroundCleanWakan, WorldWakanService.MaximumValue);
    }

    private static float ResolveSectionContribution(
        SpiritVeinSection section,
        SpiritVein vein,
        float strength,
        float convergence,
        float leakage)
    {
        float state = Mathf.Lerp(0.12f, 1f, section.FillRatio) *
                      Mathf.Lerp(0.2f, 1f, section.Purity) *
                      Mathf.Lerp(0.1f, 1f, section.Patency);
        float terrainShape = (0.62f + convergence * 0.58f) * (1f - leakage * 0.28f);
        return SpiritVeinSettings.ResolveBaseWakan(vein.Scale) * strength * terrainShape * state;
    }

    private void UpdateSectionPollution()
    {
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection section = sections[i];
            if (section.TileIds.Length == 0) continue;
            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt(section.TileIds.Length * SpiritVeinSettings.MonthlyPollutionSampleRatio),
                1,
                Mathf.Min(48, section.TileIds.Length));
            int stride = Mathf.Max(1, section.TileIds.Length / sampleCount);
            int offset = section.Id % stride;
            float clean = 0f;
            float dirty = 0f;
            int actualSamples = 0;
            for (int sample = 0; sample < sampleCount; sample++)
            {
                int index = Mathf.Min(section.TileIds.Length - 1, offset + sample * stride);
                int tileId = section.TileIds[index];
                clean += WorldWakanService.GetClean(tileId);
                dirty += WorldWakanService.GetDirty(tileId);
                actualSamples++;
            }
            if (actualSamples == 0) continue;
            clean /= actualSamples;
            dirty /= actualSamples;
            float pressure = dirty / Mathf.Max(SpiritVeinSettings.BackgroundCleanWakan, clean + dirty * 0.15f);
            float sensitivity = ResolvePollutionSensitivity(section.Id);
            if (pressure > 0.1f)
            {
                section.Purity = Mathf.Clamp01(
                    section.Purity - Mathf.Clamp((pressure - 0.1f) * 0.018f * sensitivity, 0.001f, 0.03f));
            }
            else
            {
                float recovery = section.Kind == VeinSectionKind.Outlet ? 0.006f : 0.0035f;
                section.Purity = Mathf.Clamp01(section.Purity + recovery / sensitivity);
                if (dirty > 0f && section.CurrentAmount > section.Capacity * 0.2f)
                {
                    float cleanBudget = Mathf.Min(section.MonthlyRecovery * 0.08f, section.CurrentAmount * 0.01f);
                    float each = cleanBudget / actualSamples;
                    float removed = 0f;
                    for (int sample = 0; sample < sampleCount; sample++)
                    {
                        int index = Mathf.Min(section.TileIds.Length - 1, offset + sample * stride);
                        removed += WorldWakanService.WithdrawDirty(section.TileIds[index], each);
                    }
                    section.CurrentAmount = Mathf.Max(0f, section.CurrentAmount - removed);
                }
            }
        }
    }

    private float ResolvePollutionSensitivity(int sectionId)
    {
        for (int i = 0; i < eyes.Count; i++)
        {
            if (eyes[i].SectionId != sectionId) continue;
            return eyes[i].Manifestation switch
            {
                SpiritEyeManifestation.SpiritSpring => 0.7f,
                SpiritEyeManifestation.YangPool => 0.82f,
                SpiritEyeManifestation.YinPool => 1.22f,
                SpiritEyeManifestation.FireCave => 1.12f,
                SpiritEyeManifestation.ChaosBreath => 1.35f,
                _ => 1f
            };
        }
        return 1f;
    }

    private void PropagatePollutionDownstream()
    {
        int maximumId = 0;
        for (int i = 0; i < sections.Count; i++) maximumId = Mathf.Max(maximumId, sections[i].Id);
        var losses = new float[maximumId + 1];
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection source = sections[i];
            if (source.Purity >= 0.92f || source.DownstreamSectionIds.Length == 0) continue;
            float loss = (1f - source.Purity) * 0.012f * Mathf.Lerp(0.2f, 1f, source.Patency);
            float each = loss / source.DownstreamSectionIds.Length;
            for (int j = 0; j < source.DownstreamSectionIds.Length; j++)
            {
                int targetId = source.DownstreamSectionIds[j];
                if ((uint)targetId < (uint)losses.Length) losses[targetId] += each;
            }
        }
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection section = sections[i];
            if ((uint)section.Id < (uint)losses.Length && losses[section.Id] > 0f)
                section.Purity = Mathf.Clamp01(section.Purity - losses[section.Id]);
        }
    }

    private void RefreshGroundsAndEyes()
    {
        for (int i = 0; i < grounds.Count; i++)
        {
            GatheringGround ground = grounds[i];
            SpiritVeinSection primary = GetSection(ground.SectionId);
            SpiritVeinSection guest = GetSection(ground.GuestSectionId);
            float fill = primary?.FillRatio ?? 0f;
            float purity = primary?.Purity ?? 0f;
            if (guest != null)
            {
                fill = Mathf.Lerp(fill, guest.FillRatio, 0.35f);
                purity = Mathf.Lerp(purity, guest.Purity, 0.35f);
            }
            ground.FillRatio = fill;
            ground.Purity = purity;
            SpiritVeinEye eye = GetEye(ground.EyeId);
            if (eye == null) continue;
            eye.FillRatio = fill;
            eye.Purity = purity;
        }
    }

    private void InitializeWakanFromField()
    {
        if (!WorldWakanService.IsInitialized || WorldWakanService.Width != width || WorldWakanService.Height != height)
            WorldWakanService.InitializeWorld(width, height, SpiritVeinSettings.BackgroundCleanWakan);
        for (int tileId = 0; tileId < checked(width * height); tileId++)
            WorldWakanService.SetClean(tileId, SpiritVeinSettings.BackgroundCleanWakan);
        for (int i = 0; i < fieldTileIds.Length; i++)
        {
            int tileId = fieldTileIds[i];
            WorldWakanService.SetClean(tileId, ResolveDynamicTarget(tileId));
        }
    }

    private static float ResolveTideMultiplier()
    {
        Entity worldRecord = ModClass.I?.WorldRecord?.E ?? default;
        if (worldRecord.IsNull || !worldRecord.HasComponent<WakanTideStatus>()) return 1f;
        return worldRecord.GetComponent<WakanTideStatus>().rise ? 1.25f : 0.75f;
    }
}

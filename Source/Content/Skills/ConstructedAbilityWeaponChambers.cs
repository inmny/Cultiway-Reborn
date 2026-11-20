using Cultiway.Content.Combat;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>在共同推进确认后，沿真实部位接入、暂留或释放器口里的兵器。</summary>
    private static void ConfirmWeaponChambers(RunningCast running, double clock)
    {
        int[] nodes = running.Cast.Plan.WeaponChamberNodes;
        if (nodes.Length == 0 || running.Practice) return;
        for (int i = 0; i < nodes.Length; i++)
        {
            int node = nodes[i];
            var chamber = running.WeaponChambers?[i];
            if (!running.Owner(node).IsLive || running.Carrier is { IsValid: false } ||
                !running.Cast.TryWeaponChamber(node, out var frame))
            {
                chamber?.Release();
                continue;
            }
            if (chamber == null)
            {
                if (!frame.Accepting) continue;
                running.WeaponChambers ??= new GroundWeaponService.Chamber[nodes.Length];
                chamber = running.WeaponChambers[i] = new GroundWeaponService.Chamber();
            }
            GroundWeaponService.DriveChamber(chamber, in frame, clock);
        }
    }

    /// <summary>施放取消、结束或切换世界时，归还剩余原物的控制。</summary>
    private static void ReleaseWeaponChambers(RunningCast running)
    {
        if (running.WeaponChambers == null) return;
        foreach (var chamber in running.WeaponChambers) chamber?.Release();
        running.WeaponChambers = null;
    }
}

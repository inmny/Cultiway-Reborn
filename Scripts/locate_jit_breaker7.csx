// 第七轮：在已加载的真实世界里，逐步二分 Collect 调用子树。
var logPath = "/tmp/tactical_step_log.txt";
System.IO.File.WriteAllText(logPath, "");
void Step(string name) { System.IO.File.AppendAllText(logPath, name + "\n"); }

var world = World.world;
var units = world.units.getSimpleList();
if (units.Count < 2)
{
    // 全图随机找陆地生成
    var ax = world.map_chunk_manager.amount_x * 16;
    var rng = new System.Random(23);
    WorldTile good = null;
    for (int i = 0; i < 3000 && good == null; i++)
    {
        var t = world.GetTile(rng.Next(4, ax - 4), rng.Next(4, ax - 4));
        if (t?.Type != null && !t.Type.liquid && !t.Type.lava && !t.Type.block) good = t;
    }
    if (good == null) return new { ok = false, stage = "无陆地" };
    while (units.Count < 2)
    {
        var s = world.units.spawnNewUnit("human", good, false, pSpawnHeight: 0f, pAdultAge: true);
        if (s == null) return new { ok = false, stage = "生成失败" };
        units = world.units.getSimpleList();
    }
}
var actorA = units[0];
var actorB = units[1];
Step("单位: " + actorA.id + "/" + actorB.id);

var providerType = typeof(Cultiway.Core.Combat.Tactical.CombatWorldService).Assembly
    .GetType("Cultiway.Core.Combat.Tactical.AdvancedCombatActionProvider");
var provider = System.Activator.CreateInstance(providerType);
var addPoolMethod = providerType.GetMethod("AddPool",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

Step("步骤1: AddPool(近战池) 开始");
var seen = new System.Collections.Generic.HashSet<CombatActionAsset>();
var output = new ListPool<Cultiway.Core.Combat.Tactical.CombatActionCandidate>();
addPoolMethod.Invoke(provider, new object[] {
    actorA, (BaseSimObject)actorB, CombatActionPool.BEFORE_ATTACK_MELEE, false, seen, output });
Step("步骤1: 完成，候选=" + output.Count);

Step("步骤2: AddPool(远程池) 开始");
addPoolMethod.Invoke(provider, new object[] {
    actorA, (BaseSimObject)actorB, CombatActionPool.BEFORE_ATTACK_RANGE, true, seen, output });
Step("步骤2: 完成，候选=" + output.Count);

Step("步骤3: 逐个触发 can_do_action 委托开始");
foreach (var poolId in new[] { CombatActionPool.BEFORE_ATTACK_MELEE, CombatActionPool.BEFORE_ATTACK_RANGE })
{
    var actions = actorA.getCombatActionPool(poolId);
    if (actions == null) { Step("步骤3: 池 " + poolId + " 为空"); continue; }
    foreach (var action in actions)
    {
        if (action?.can_do_action == null) continue;
        Step("步骤3: 调用 " + action.id + ".can_do_action");
        var r = action.can_do_action(actorA, actorB);
        Step("步骤3: " + action.id + " 返回 " + r);
    }
}
Step("步骤3: 委托全部完成");

Step("步骤4: 完整 CombatActionService.Collect 开始");
var asm = typeof(Cultiway.Core.Combat.Tactical.CombatWorldService).Assembly;
var svcType = asm.GetType("Cultiway.Core.Combat.Tactical.CombatActionService");
var collectMethod = svcType.GetMethod("Collect",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
object[] args = {
    Cultiway.Utils.Extension.ActorTools.GetExtend(actorA), (BaseSimObject)actorB, null, 1f,
    (System.Collections.Generic.IReadOnlyList<Actor>)new ListPool<Actor>(), output };
collectMethod.Invoke(null, args);
Step("步骤4: 完成，候选=" + output.Count);

Step("步骤5: PlanSynchronously");
bool planned = Cultiway.Core.Combat.Tactical.CombatWorldService.PlanSynchronously(actorA);
Step("步骤5: 返回 " + planned);

return new { ok = true, candidates = output.Count, planned };

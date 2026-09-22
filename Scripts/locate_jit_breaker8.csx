// 第八轮：无世界依赖的编译二分。
// 反射逐个调用 Collect 子树的每个方法（空参数），编译发生在执行前：
// 若某方法的首次编译触发 Mono 断言，进程立即中止，日志末行即凶手；
// 若只是空引用异常，则该方法编译正常，继续下一个。
var logPath = "/tmp/tactical_step_log2.txt";
System.IO.File.WriteAllText(logPath, "");
void Step(string name) { System.IO.File.AppendAllText(logPath, name + "\n"); }

var asm = typeof(Cultiway.Core.Combat.Tactical.CombatWorldService).Assembly;
var ns = "Cultiway.Core.Combat.Tactical.";
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

// 1) CombatActionService.ResolveResourceRatio(null, 0, 0)
Step("1: ResolveResourceRatio");
asm.GetType(ns + "CombatActionService")
    .GetMethod("ResolveResourceRatio", flags)
    .Invoke(null, new object[] { null, 0f, 0f });
Step("1: 通过");

// 2) CombatActionService.CreateOriginalActionKey(null)
Step("2: CreateOriginalActionKey");
asm.GetType(ns + "CombatActionService")
    .GetMethod("CreateOriginalActionKey", flags)
    .Invoke(null, new object[] { null });
Step("2: 通过");

// 3) CombatActionProfile 构造（按参数类型自动生成默认值）
Step("3: CombatActionProfile..ctor");
var profileType = asm.GetType(ns + "CombatActionProfile");
System.Func<System.Reflection.ConstructorInfo, object[], object> invokeCtor = null;
System.Func<System.Reflection.ConstructorInfo, object[]> defaultArgs = null;
defaultArgs = c => System.Array.ConvertAll(c.GetParameters(), p =>
    p.ParameterType.IsValueType ? System.Activator.CreateInstance(p.ParameterType) : null);
invokeCtor = (c, a) => c.Invoke(a);
invokeCtor(profileType.GetConstructors()[0], defaultArgs(profileType.GetConstructors()[0]));
Step("3: 通过");

// 4) AdvancedActionPayload 构造（私有嵌套类）
Step("4: AdvancedActionPayload..ctor");
var payloadType = asm.GetType(ns + "AdvancedCombatActionActionProviderPayload") ??
                  asm.GetType(ns + "AdvancedCombatActionProvider/AdvancedActionPayload");
var payloadCtor = payloadType.GetConstructors(
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
    System.Reflection.BindingFlags.Public)[0];
payloadCtor.Invoke(defaultArgs(payloadCtor));
Step("4: 通过");

// 5) CombatActionKey 构造
Step("5: CombatActionKey..ctor");
System.Activator.CreateInstance(asm.GetType(ns + "CombatActionKey"), new object[] { "x", 0L, "y" });
Step("5: 通过");

// 6) CombatActionCandidate 构造
Step("6: CombatActionCandidate..ctor");
var providerType = asm.GetType(ns + "AdvancedCombatActionProvider");
var provider = System.Activator.CreateInstance(providerType);
var keyCtor = asm.GetType(ns + "CombatActionKey").GetConstructor(
    new[] { typeof(string), typeof(long), typeof(string) });
var key = keyCtor.Invoke(new object[] { "p", 0L, "e" });
var pCtor = profileType.GetConstructors()[0];
var profile = pCtor.Invoke(defaultArgs(pCtor));
var candidateType = asm.GetType(ns + "CombatActionCandidate");
System.Activator.CreateInstance(candidateType, new[] {
    provider, key, profile, (object)null, true, 0L });
Step("6: 通过");

// 7) AddPool(null, null, 0, false, null, null) —— 编译后立刻空引用
Step("7: AddPool 空参调用");
var addPool = providerType.GetMethod("AddPool",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
try { addPool.Invoke(provider, new object[] { null, null, 0, false, null, null }); }
catch (System.Reflection.TargetInvocationException) { Step("7: 空引用（编译正常）"); }
Step("7: 通过");

// 8) Collect 空上下文直接调用（default 结构体 → 立刻空引用）
Step("8: AdvancedCombatActionProvider.Collect 空参调用");
var collectMethod = providerType.GetMethod("Collect",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
    null, new[] { asm.GetType(ns + "CombatActionCollectionContext").MakeByRefType(),
                  typeof(System.Collections.Generic.IList<Cultiway.Core.Combat.Tactical.CombatActionCandidate>) },
    null);
var output = new ListPool<Cultiway.Core.Combat.Tactical.CombatActionCandidate>();
var defaultContext = System.Activator.CreateInstance(
    asm.GetType(ns + "CombatActionCollectionContext"));
try { collectMethod.Invoke(provider, new object[] { defaultContext, output }); }
catch (System.Reflection.TargetInvocationException) { Step("8: 空引用（编译正常）"); }
Step("8: 通过");

// 9) CombatActionService.Collect 完整签名空参
Step("9: CombatActionService.Collect 空参调用");
var svcCollect = asm.GetType(ns + "CombatActionService").GetMethod("Collect",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
try { svcCollect.Invoke(null, new object[] { null, null, null, 1f, null, output }); }
catch (System.Reflection.TargetInvocationException) { Step("9: 空引用（编译正常）"); }
Step("9: 通过");

// 10) 委托调用路径：创建并触发 CombatActionCheckStart 委托
Step("10: CombatActionCheckStart 委托触发");
var delType = typeof(Actor).Assembly.GetType("CombatActionCheckStart");
var dummy = delType.GetMethod("Invoke"); // 仅引用类型确认存在
System.IO.File.AppendAllText(logPath, "10: 委托类型已定位: " + delType.FullName + "\n");
return new { ok = true, note = "全部环节编译通过，元凶需要真实角色上下文" };

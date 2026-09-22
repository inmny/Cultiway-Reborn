using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 战术链编译预热。
/// Unity 2022.3 自带的 Mono 运行时在「方法首次调用时才编译」的路径上存在机器码生成断言
/// （mini-amd64.c:7388，fconv_to_r8_x 指令长度估算错误），且只在游戏内首次激活战术系统、
/// 大批方法集中进入首次编译时触发，进程直接中止且无任何托管异常。
/// 实测同一个方法用 PrepareMethod 预先编译则完全安全，因此这里在模组加载阶段
/// （无并发模拟、主线程空闲）把整条战术链一次性编译完毕，
/// 让游戏进行到战斗环节时不再存在任何「首次调用编译」的窗口。
/// </summary>
public static class TacticalCompileWarmup
{
    /// <summary>参与预编译的类型命名空间前缀：战术系统本体与主动能力适配层。</summary>
    private static readonly string[] NamespacePrefixes =
    [
        "Cultiway.Core.Combat",
        "Cultiway.Core.SkillLibV3.ActiveAbilities"
    ];

    /// <summary>
    /// 按命名空间前缀预编译全部方法。只应在模组加载 / 世界加载回调的主线程调用一次。
    /// </summary>
    public static void Run()
    {
        var assembly = typeof(CombatWorldService).Assembly;
        var types = assembly.GetTypes();
        var scanned = 0;
        var prepared = 0;
        foreach (var type in types)
        {
            var fullName = type.FullName;
            if (fullName == null) continue;
            var matched = false;
            foreach (var prefix in NamespacePrefixes)
            {
                if (fullName.StartsWith(prefix))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched) continue;

            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(all))
            {
                scanned++;
                if (method.IsGenericMethodDefinition || method.IsAbstract || method.GetMethodBody() == null)
                    continue;
                try
                {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    prepared++;
                }
                catch
                {
                    // 个别运行时提供的方法无法预编译，跳过即可；它们不走本模组的首次调用编译窗口。
                }
            }
        }
        ModClass.LogInfoConcurrent($"[战术编译预热] 完成：扫描 {scanned} 个方法，预编译 {prepared} 个");
    }
}

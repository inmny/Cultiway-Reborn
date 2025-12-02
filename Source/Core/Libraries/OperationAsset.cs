using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;
using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 一次性操作资产，用于DataGain执行
/// </summary>
public class OperationAsset : Asset
{
    /// <summary>
    /// 预检查逻辑，返回是否可以执行
    /// </summary>
    public Func<ActorExtend, float, Dictionary<string, string>, bool> PreCheck;
    /// <summary>
    /// 执行逻辑，返回是否成功
    /// </summary>
    public Func<ActorExtend, float, Dictionary<string, string>, bool> Action;
    public string GetName()
    {
        return id.Localize();
    }
    public string GetDescription()
    {
        return $"{id}.Info".Localize();
    }
}
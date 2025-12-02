using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 一次性操作资产，用于DataGain执行
/// </summary>
public class OperationAsset : Asset
{
    /// <summary>
    /// 执行逻辑，返回是否成功
    /// </summary>
    public Func<ActorExtend, float, Dictionary<string, string>, bool> Action;
}
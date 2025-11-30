using System.Collections.Generic;
using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

/// <summary>
/// 角色功法状态组件
/// </summary>
public struct ActorCultibookState : IComponent
{
    /// <summary>
    /// 主修功法ID
    /// </summary>
    public string MainCultibookId;
    
    /// <summary>
    /// 主修掌握程度（0-100）
    /// </summary>
    public float MainMastery;
    
    /// <summary>
    /// 累计修炼时间
    /// </summary>
    public float AccumulatedTime;
    
    /// <summary>
    /// 各法术的领悟进度
    /// </summary>
    [Ignore]
    public Dictionary<int, float> SkillProgress;
    
    /// <summary>
    /// 是否拥有主修功法
    /// </summary>
    public bool HasMainCultibook => !string.IsNullOrEmpty(MainCultibookId);
    
    /// <summary>
    /// 获取主修功法Asset
    /// </summary>
    [Ignore]
    public CultibookAsset MainCultibook
    {
        get
        {
            if (_mainCultibook != null && _mainCultibook.id == MainCultibookId) return _mainCultibook;
            if (string.IsNullOrEmpty(MainCultibookId)) return null;
            _mainCultibook = Libraries.Manager.CultibookLibrary.get(MainCultibookId);
            return _mainCultibook;
        }
    }
    
    [Ignore]
    private CultibookAsset _mainCultibook;
    
    /// <summary>
    /// 初始化技能进度字典
    /// </summary>
    public void InitSkillProgress()
    {
        if (SkillProgress == null)
        {
            SkillProgress = new Dictionary<int, float>();
        }
    }
}


using System;
using System.Collections.Generic;

namespace Cultiway.Core.ActorFiltering;

/// <summary>多个世界工具可各自持有的一份角色逻辑筛选配置。</summary>
public sealed class ActorFilterSettings
{
    private readonly List<ActorFilterToken> _expression = new();
    private ActorFilterToken[] _compiledExpression = Array.Empty<ActorFilterToken>();
    private ActorFilterExpressionState _expressionState = ActorFilterExpression.Analyze(null);

    /// <summary>表达式发生变化后通知配置窗口刷新。</summary>
    public event Action Changed;

    /// <summary>按用户编辑顺序保存的中缀表达式。</summary>
    public IReadOnlyList<ActorFilterToken> Expression => _expression;
    /// <summary>当前表达式是否完整，以及下一步允许追加的词元。</summary>
    public ActorFilterExpressionState ExpressionState => _expressionState;

    /// <summary>在表达式末尾追加一个具体过滤条件。</summary>
    public bool AppendPredicate(ActorFilterEntry entry)
    {
        if (string.IsNullOrEmpty(entry.TypeId) || string.IsNullOrEmpty(entry.ValueId)) return false;
        if (!_expressionState.CanAppend(ActorFilterTokenKind.Predicate)) return false;
        _expression.Add(ActorFilterToken.FromPredicate(entry));
        RecompileExpression();
        return true;
    }

    /// <summary>在表达式末尾追加一个当前语法允许的逻辑符号。</summary>
    public bool AppendSymbol(ActorFilterTokenKind kind)
    {
        if (kind == ActorFilterTokenKind.Predicate || !_expressionState.CanAppend(kind)) return false;
        _expression.Add(ActorFilterToken.FromSymbol(kind));
        RecompileExpression();
        return true;
    }

    /// <summary>移除表达式最后一个词元。</summary>
    public void RemoveLastToken()
    {
        if (_expression.Count == 0) return;
        _expression.RemoveAt(_expression.Count - 1);
        RecompileExpression();
    }

    /// <summary>清空整个表达式并恢复为不过滤。</summary>
    public void ClearExpression()
    {
        if (_expression.Count == 0) return;
        _expression.Clear();
        RecompileExpression();
    }

    /// <summary>取得不可变的已编译表达式快照；表达式尚未完成时返回 false。</summary>
    public bool TrySnapshot(out ActorFilterToken[] compiledExpression)
    {
        if (!_expressionState.IsComplete)
        {
            compiledExpression = Array.Empty<ActorFilterToken>();
            return false;
        }
        compiledExpression = (ActorFilterToken[])_compiledExpression.Clone();
        return true;
    }

    /// <summary>切换世界时清除引用世界元对象的表达式；纯修炼体系表达式可以保留。</summary>
    public void ClearWorldExpression()
    {
        for (var i = 0; i < _expression.Count; i++)
        {
            var token = _expression[i];
            if (token.Kind != ActorFilterTokenKind.Predicate ||
                token.Predicate.TypeId == ActorFilterCatalog.CultisysTypeId) continue;
            _expression.Clear();
            RecompileExpression();
            return;
        }
    }

    private void RecompileExpression()
    {
        ActorFilterExpression.TryCompile(_expression, out _compiledExpression, out _expressionState);
        Changed?.Invoke();
    }
}

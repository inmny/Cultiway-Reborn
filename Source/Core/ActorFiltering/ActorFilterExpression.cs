using System;
using System.Collections.Generic;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.ActorFiltering;

/// <summary>角色筛选表达式中的词元类型。</summary>
public enum ActorFilterTokenKind
{
    /// <summary>一个元对象或修炼体系条件。</summary>
    Predicate,
    /// <summary>一元逻辑非，只作用于紧随其后的条件或括号表达式。</summary>
    Not,
    /// <summary>逻辑与，要求左右表达式都成立。</summary>
    And,
    /// <summary>逻辑或，要求左右表达式至少一个成立。</summary>
    Or,
    /// <summary>左括号，开始一个显式优先级分组。</summary>
    LeftParenthesis,
    /// <summary>右括号，结束一个显式优先级分组。</summary>
    RightParenthesis
}

/// <summary>角色筛选表达式中的一个条件或逻辑符号。</summary>
public readonly struct ActorFilterToken
{
    private ActorFilterToken(ActorFilterTokenKind kind, ActorFilterEntry predicate)
    {
        Kind = kind;
        Predicate = predicate;
    }

    /// <summary>词元的语法类型。</summary>
    public ActorFilterTokenKind Kind { get; }

    /// <summary>条件词元携带的过滤项；逻辑符号使用默认值。</summary>
    public ActorFilterEntry Predicate { get; }

    /// <summary>创建一个携带具体角色过滤条件的词元。</summary>
    public static ActorFilterToken FromPredicate(ActorFilterEntry predicate)
    {
        return new ActorFilterToken(ActorFilterTokenKind.Predicate, predicate);
    }

    /// <summary>创建一个不携带过滤条件的逻辑符号词元。</summary>
    public static ActorFilterToken FromSymbol(ActorFilterTokenKind kind)
    {
        if (kind == ActorFilterTokenKind.Predicate)
            throw new ArgumentException("条件词元必须通过 FromPredicate 创建。", nameof(kind));
        return new ActorFilterToken(kind, default);
    }
}

/// <summary>角色筛选表达式当前的错误或未完成原因。</summary>
public enum ActorFilterExpressionError
{
    /// <summary>表达式完整，没有错误。</summary>
    None,
    /// <summary>现有词元不能构成合法表达式前缀。</summary>
    Invalid,
    /// <summary>表达式末尾还需要条件、逻辑非或左括号。</summary>
    ExpectCondition,
    /// <summary>表达式中仍有未闭合的左括号。</summary>
    ExpectRightParenthesis
}

/// <summary>表达式当前是否完整，以及编辑器下一步允许追加哪些词元。</summary>
public readonly struct ActorFilterExpressionState
{
    internal ActorFilterExpressionState(bool syntacticallyValid, bool complete, bool empty,
        bool expectsOperand, int openParentheses, ActorFilterExpressionError error)
    {
        IsSyntacticallyValid = syntacticallyValid;
        IsComplete = complete;
        IsEmpty = empty;
        ExpectsOperand = expectsOperand;
        OpenParentheses = openParentheses;
        Error = error;
    }

    /// <summary>现有词元是否仍构成可以继续补完的合法前缀。</summary>
    public bool IsSyntacticallyValid { get; }
    /// <summary>表达式是否已经可以求值；空表达式也视为完整。</summary>
    public bool IsComplete { get; }
    /// <summary>表达式是否尚未添加任何词元。</summary>
    public bool IsEmpty { get; }
    /// <summary>下一步是否需要条件、逻辑非或左括号。</summary>
    public bool ExpectsOperand { get; }
    /// <summary>尚未闭合的左括号数量。</summary>
    public int OpenParentheses { get; }
    /// <summary>当前错误或未完成原因。</summary>
    public ActorFilterExpressionError Error { get; }

    /// <summary>检查当前表达式后是否允许追加指定类型的词元。</summary>
    public bool CanAppend(ActorFilterTokenKind kind)
    {
        if (!IsSyntacticallyValid) return false;
        return kind switch
        {
            ActorFilterTokenKind.Predicate or ActorFilterTokenKind.Not
                or ActorFilterTokenKind.LeftParenthesis => ExpectsOperand,
            ActorFilterTokenKind.And or ActorFilterTokenKind.Or => !ExpectsOperand && !IsEmpty,
            ActorFilterTokenKind.RightParenthesis => !ExpectsOperand && OpenParentheses > 0,
            _ => false
        };
    }
}

/// <summary>验证、编译并求值可复用的角色逻辑筛选表达式。</summary>
public static class ActorFilterExpression
{
    /// <summary>分析一个表达式前缀，不执行任何角色或世界查询。</summary>
    public static ActorFilterExpressionState Analyze(IReadOnlyList<ActorFilterToken> tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return new ActorFilterExpressionState(true, true, true, true, 0,
                ActorFilterExpressionError.None);

        var expectsOperand = true;
        var openParentheses = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            switch (tokens[i].Kind)
            {
                case ActorFilterTokenKind.Predicate:
                    if (!expectsOperand) return InvalidState(openParentheses);
                    expectsOperand = false;
                    break;
                case ActorFilterTokenKind.Not:
                case ActorFilterTokenKind.LeftParenthesis:
                    if (!expectsOperand) return InvalidState(openParentheses);
                    if (tokens[i].Kind == ActorFilterTokenKind.LeftParenthesis) openParentheses++;
                    break;
                case ActorFilterTokenKind.And:
                case ActorFilterTokenKind.Or:
                    if (expectsOperand) return InvalidState(openParentheses);
                    expectsOperand = true;
                    break;
                case ActorFilterTokenKind.RightParenthesis:
                    if (expectsOperand || openParentheses == 0) return InvalidState(openParentheses);
                    openParentheses--;
                    break;
                default:
                    return InvalidState(openParentheses);
            }
        }

        var complete = !expectsOperand && openParentheses == 0;
        var error = complete
            ? ActorFilterExpressionError.None
            : expectsOperand
                ? ActorFilterExpressionError.ExpectCondition
                : ActorFilterExpressionError.ExpectRightParenthesis;
        return new ActorFilterExpressionState(true, complete, false, expectsOperand, openParentheses, error);
    }

    /// <summary>把完整的中缀表达式编译成便于重复求值的后缀表达式。</summary>
    public static bool TryCompile(IReadOnlyList<ActorFilterToken> tokens,
        out ActorFilterToken[] postfix, out ActorFilterExpressionState state)
    {
        state = Analyze(tokens);
        if (!state.IsComplete)
        {
            postfix = Array.Empty<ActorFilterToken>();
            return false;
        }
        if (state.IsEmpty)
        {
            postfix = Array.Empty<ActorFilterToken>();
            return true;
        }

        var output = new List<ActorFilterToken>(tokens.Count);
        var operators = new Stack<ActorFilterTokenKind>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Kind)
            {
                case ActorFilterTokenKind.Predicate:
                    output.Add(token);
                    break;
                case ActorFilterTokenKind.LeftParenthesis:
                    operators.Push(token.Kind);
                    break;
                case ActorFilterTokenKind.RightParenthesis:
                    while (operators.Peek() != ActorFilterTokenKind.LeftParenthesis)
                        output.Add(ActorFilterToken.FromSymbol(operators.Pop()));
                    operators.Pop();
                    break;
                default:
                    while (operators.Count > 0 && IsOperator(operators.Peek()) &&
                           ShouldPopBefore(token.Kind, operators.Peek()))
                        output.Add(ActorFilterToken.FromSymbol(operators.Pop()));
                    operators.Push(token.Kind);
                    break;
            }
        }

        while (operators.Count > 0) output.Add(ActorFilterToken.FromSymbol(operators.Pop()));
        postfix = output.ToArray();
        return true;
    }

    /// <summary>
    /// 对角色求值。元对象条件直接检查角色；修炼体系条件由调用方提供匹配函数解释。
    /// </summary>
    public static bool Evaluate(ActorFilterToken[] postfix, Actor actor, Func<string, bool> matchesCultisys)
    {
        if (postfix == null || postfix.Length == 0) return true;

        var values = new bool[postfix.Length];
        var count = 0;
        for (var i = 0; i < postfix.Length; i++)
        {
            var token = postfix[i];
            switch (token.Kind)
            {
                case ActorFilterTokenKind.Predicate:
                {
                    var descriptor = ActorFilterCatalog.Get(token.Predicate.TypeId);
                    values[count++] = descriptor != null && (descriptor.IsCultisys
                        ? matchesCultisys != null && matchesCultisys(token.Predicate.ValueId)
                        : descriptor.Matches(actor, token.Predicate.ValueId));
                    break;
                }
                case ActorFilterTokenKind.Not:
                    if (count < 1) return false;
                    values[count - 1] = !values[count - 1];
                    break;
                case ActorFilterTokenKind.And:
                case ActorFilterTokenKind.Or:
                    if (count < 2) return false;
                    var right = values[--count];
                    var left = values[count - 1];
                    values[count - 1] = token.Kind == ActorFilterTokenKind.And
                        ? left && right
                        : left || right;
                    break;
                default:
                    return false;
            }
        }
        return count == 1 && values[0];
    }

    /// <summary>按角色当前所属元对象和已拥有修炼体系求值，供只筛选角色的世界工具复用。</summary>
    public static bool EvaluateActor(ActorFilterToken[] postfix, Actor actor)
    {
        var actorExtend = actor.GetExtend();
        return Evaluate(postfix, actor, cultisysId =>
        {
            var cultisys = ProgressionService.GetRegistered(cultisysId);
            return cultisys != null && cultisys.IsOwnedBy(actorExtend);
        });
    }

    private static ActorFilterExpressionState InvalidState(int openParentheses)
    {
        return new ActorFilterExpressionState(false, false, false, false, openParentheses,
            ActorFilterExpressionError.Invalid);
    }

    private static bool IsOperator(ActorFilterTokenKind kind)
    {
        return kind is ActorFilterTokenKind.Not or ActorFilterTokenKind.And or ActorFilterTokenKind.Or;
    }

    private static bool ShouldPopBefore(ActorFilterTokenKind current, ActorFilterTokenKind stacked)
    {
        var currentPrecedence = GetPrecedence(current);
        var stackedPrecedence = GetPrecedence(stacked);
        return current == ActorFilterTokenKind.Not
            ? currentPrecedence < stackedPrecedence
            : currentPrecedence <= stackedPrecedence;
    }

    private static int GetPrecedence(ActorFilterTokenKind kind)
    {
        return kind switch
        {
            ActorFilterTokenKind.Not => 3,
            ActorFilterTokenKind.And => 2,
            ActorFilterTokenKind.Or => 1,
            _ => 0
        };
    }
}

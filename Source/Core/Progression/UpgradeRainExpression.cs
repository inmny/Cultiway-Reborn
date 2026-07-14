using System;
using System.Collections.Generic;

namespace Cultiway.Core.Progression;

/// <summary>升级雨过滤表达式中的词元类型。</summary>
public enum UpgradeRainFilterTokenKind
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

/// <summary>升级雨过滤表达式中的一个条件或逻辑符号。</summary>
public readonly struct UpgradeRainFilterToken
{
    private UpgradeRainFilterToken(UpgradeRainFilterTokenKind kind, UpgradeRainFilterEntry predicate)
    {
        Kind = kind;
        Predicate = predicate;
    }

    /// <summary>词元的语法类型。</summary>
    public UpgradeRainFilterTokenKind Kind { get; }

    /// <summary>条件词元携带的过滤项；逻辑符号使用默认值。</summary>
    public UpgradeRainFilterEntry Predicate { get; }

    /// <summary>创建一个具体过滤条件词元。</summary>
    public static UpgradeRainFilterToken FromPredicate(UpgradeRainFilterEntry predicate)
    {
        return new UpgradeRainFilterToken(UpgradeRainFilterTokenKind.Predicate, predicate);
    }

    /// <summary>创建一个不携带过滤项的逻辑符号词元。</summary>
    public static UpgradeRainFilterToken FromSymbol(UpgradeRainFilterTokenKind kind)
    {
        if (kind == UpgradeRainFilterTokenKind.Predicate)
            throw new ArgumentException("条件词元必须通过 FromPredicate 创建。", nameof(kind));
        return new UpgradeRainFilterToken(kind, default);
    }
}

/// <summary>表达式当前是否完整，以及编辑器下一步允许追加哪些词元。</summary>
public readonly struct UpgradeRainExpressionState
{
    internal UpgradeRainExpressionState(bool syntacticallyValid, bool complete, bool empty,
        bool expectsOperand, int openParentheses, string errorKey)
    {
        IsSyntacticallyValid = syntacticallyValid;
        IsComplete = complete;
        IsEmpty = empty;
        ExpectsOperand = expectsOperand;
        OpenParentheses = openParentheses;
        ErrorKey = errorKey;
    }

    /// <summary>现有词元是否仍构成一个可以继续补完的合法前缀。</summary>
    public bool IsSyntacticallyValid { get; }

    /// <summary>表达式是否已经可以用于升级雨求值；空表达式也视为完整。</summary>
    public bool IsComplete { get; }

    /// <summary>表达式是否尚未添加任何词元。</summary>
    public bool IsEmpty { get; }

    /// <summary>下一步是否需要条件、逻辑非或左括号。</summary>
    public bool ExpectsOperand { get; }

    /// <summary>尚未闭合的左括号数量。</summary>
    public int OpenParentheses { get; }

    /// <summary>未完成或非法状态的本地化键；完整表达式为 null。</summary>
    public string ErrorKey { get; }

    /// <summary>检查当前表达式后是否允许追加指定类型的词元。</summary>
    public bool CanAppend(UpgradeRainFilterTokenKind kind)
    {
        if (!IsSyntacticallyValid) return false;
        return kind switch
        {
            UpgradeRainFilterTokenKind.Predicate or UpgradeRainFilterTokenKind.Not
                or UpgradeRainFilterTokenKind.LeftParenthesis => ExpectsOperand,
            UpgradeRainFilterTokenKind.And or UpgradeRainFilterTokenKind.Or => !ExpectsOperand && !IsEmpty,
            UpgradeRainFilterTokenKind.RightParenthesis => !ExpectsOperand && OpenParentheses > 0,
            _ => false
        };
    }
}

/// <summary>
///     验证中缀过滤表达式，将其编译为后缀形式，并针对角色的某一个修炼体系进行求值。
/// </summary>
internal static class UpgradeRainExpression
{
    private const string InvalidKey = "Cultiway.UpgradeRain.UI.Expression.Invalid";
    private const string ExpectConditionKey = "Cultiway.UpgradeRain.UI.Expression.ExpectCondition";
    private const string ExpectRightParenthesisKey =
        "Cultiway.UpgradeRain.UI.Expression.ExpectRightParenthesis";

    /// <summary>分析一个表达式前缀，不执行任何角色或世界查询。</summary>
    public static UpgradeRainExpressionState Analyze(IReadOnlyList<UpgradeRainFilterToken> tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return new UpgradeRainExpressionState(true, true, true, true, 0, null);

        var expectsOperand = true;
        var openParentheses = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            switch (tokens[i].Kind)
            {
                case UpgradeRainFilterTokenKind.Predicate:
                    if (!expectsOperand) return InvalidState(openParentheses);
                    expectsOperand = false;
                    break;
                case UpgradeRainFilterTokenKind.Not:
                case UpgradeRainFilterTokenKind.LeftParenthesis:
                    if (!expectsOperand) return InvalidState(openParentheses);
                    if (tokens[i].Kind == UpgradeRainFilterTokenKind.LeftParenthesis) openParentheses++;
                    break;
                case UpgradeRainFilterTokenKind.And:
                case UpgradeRainFilterTokenKind.Or:
                    if (expectsOperand) return InvalidState(openParentheses);
                    expectsOperand = true;
                    break;
                case UpgradeRainFilterTokenKind.RightParenthesis:
                    if (expectsOperand || openParentheses == 0) return InvalidState(openParentheses);
                    openParentheses--;
                    break;
                default:
                    return InvalidState(openParentheses);
            }
        }

        var complete = !expectsOperand && openParentheses == 0;
        var errorKey = complete
            ? null
            : expectsOperand
                ? ExpectConditionKey
                : ExpectRightParenthesisKey;
        return new UpgradeRainExpressionState(true, complete, false, expectsOperand,
            openParentheses, errorKey);
    }

    /// <summary>把完整的中缀表达式编译成便于重复求值的后缀表达式。</summary>
    public static bool TryCompile(IReadOnlyList<UpgradeRainFilterToken> tokens,
        out UpgradeRainFilterToken[] postfix, out UpgradeRainExpressionState state)
    {
        state = Analyze(tokens);
        if (!state.IsComplete)
        {
            postfix = Array.Empty<UpgradeRainFilterToken>();
            return false;
        }
        if (state.IsEmpty)
        {
            postfix = Array.Empty<UpgradeRainFilterToken>();
            return true;
        }

        var output = new List<UpgradeRainFilterToken>(tokens.Count);
        var operators = new Stack<UpgradeRainFilterTokenKind>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Kind)
            {
                case UpgradeRainFilterTokenKind.Predicate:
                    output.Add(token);
                    break;
                case UpgradeRainFilterTokenKind.LeftParenthesis:
                    operators.Push(token.Kind);
                    break;
                case UpgradeRainFilterTokenKind.RightParenthesis:
                    while (operators.Peek() != UpgradeRainFilterTokenKind.LeftParenthesis)
                        output.Add(UpgradeRainFilterToken.FromSymbol(operators.Pop()));
                    operators.Pop();
                    break;
                default:
                    while (operators.Count > 0 && IsOperator(operators.Peek()) &&
                           ShouldPopBefore(token.Kind, operators.Peek()))
                    {
                        output.Add(UpgradeRainFilterToken.FromSymbol(operators.Pop()));
                    }
                    operators.Push(token.Kind);
                    break;
            }
        }

        while (operators.Count > 0)
            output.Add(UpgradeRainFilterToken.FromSymbol(operators.Pop()));
        postfix = output.ToArray();
        return true;
    }

    /// <summary>
    ///     对角色拥有的一个候选修炼体系求值。空表达式恒为真，修炼体系条件只匹配当前候选体系。
    /// </summary>
    public static bool Evaluate(UpgradeRainFilterToken[] postfix, Actor actor, string cultisysId)
    {
        if (postfix == null || postfix.Length == 0) return true;

        var values = new bool[postfix.Length];
        var count = 0;
        for (var i = 0; i < postfix.Length; i++)
        {
            var token = postfix[i];
            switch (token.Kind)
            {
                case UpgradeRainFilterTokenKind.Predicate:
                {
                    var descriptor = UpgradeRainFilterCatalog.Get(token.Predicate.TypeId);
                    values[count++] = descriptor != null && (descriptor.IsCultisys
                        ? string.Equals(token.Predicate.ValueId, cultisysId, StringComparison.Ordinal)
                        : descriptor.Matches(actor, token.Predicate.ValueId));
                    break;
                }
                case UpgradeRainFilterTokenKind.Not:
                    if (count < 1) return false;
                    values[count - 1] = !values[count - 1];
                    break;
                case UpgradeRainFilterTokenKind.And:
                case UpgradeRainFilterTokenKind.Or:
                {
                    if (count < 2) return false;
                    var right = values[--count];
                    var left = values[count - 1];
                    values[count - 1] = token.Kind == UpgradeRainFilterTokenKind.And
                        ? left && right
                        : left || right;
                    break;
                }
                default:
                    return false;
            }
        }
        return count == 1 && values[0];
    }

    private static UpgradeRainExpressionState InvalidState(int openParentheses)
    {
        return new UpgradeRainExpressionState(false, false, false, false,
            openParentheses, InvalidKey);
    }

    private static bool IsOperator(UpgradeRainFilterTokenKind kind)
    {
        return kind is UpgradeRainFilterTokenKind.Not or UpgradeRainFilterTokenKind.And
            or UpgradeRainFilterTokenKind.Or;
    }

    private static bool ShouldPopBefore(UpgradeRainFilterTokenKind current,
        UpgradeRainFilterTokenKind stacked)
    {
        var currentPrecedence = GetPrecedence(current);
        var stackedPrecedence = GetPrecedence(stacked);
        return current == UpgradeRainFilterTokenKind.Not
            ? currentPrecedence < stackedPrecedence
            : currentPrecedence <= stackedPrecedence;
    }

    private static int GetPrecedence(UpgradeRainFilterTokenKind kind)
    {
        return kind switch
        {
            UpgradeRainFilterTokenKind.Not => 3,
            UpgradeRainFilterTokenKind.And => 2,
            UpgradeRainFilterTokenKind.Or => 1,
            _ => 0
        };
    }
}

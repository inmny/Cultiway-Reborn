using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.SkillLibV3.Editor;

public enum SkillValidationSeverity
{
    Warning,
    Error
}

public sealed class SkillValidationIssue
{
    public SkillValidationSeverity Severity;
    public string Code;
    public string MessageKey;
    public object[] Arguments = Array.Empty<object>();
    public string SubjectId;
    public string Message => Arguments.Length == 0
        ? MessageKey.Localize()
        : string.Format(MessageKey.Localize(), Arguments);
}

public sealed class SkillCompatibilityResult
{
    public List<SkillValidationIssue> Issues { get; } = new();
    public bool IsCompatible => Issues.All(issue => issue.Severity != SkillValidationSeverity.Error);

    public SkillCompatibilityResult AddError(string code, string subjectId = null, params object[] arguments)
    {
        Issues.Add(new SkillValidationIssue
        {
            Severity = SkillValidationSeverity.Error,
            Code = code,
            MessageKey = $"Cultiway.SkillBlueprint.Validation.{code}",
            Arguments = arguments,
            SubjectId = subjectId
        });
        return this;
    }

    public SkillCompatibilityResult AddWarning(string code, string subjectId = null, params object[] arguments)
    {
        Issues.Add(new SkillValidationIssue
        {
            Severity = SkillValidationSeverity.Warning,
            Code = code,
            MessageKey = $"Cultiway.SkillBlueprint.Validation.{code}",
            Arguments = arguments,
            SubjectId = subjectId
        });
        return this;
    }

    public SkillCompatibilityResult AddErrorKey(string code, string messageKey, string subjectId = null,
        params object[] arguments)
    {
        Issues.Add(new SkillValidationIssue
        {
            Severity = SkillValidationSeverity.Error,
            Code = code,
            MessageKey = messageKey,
            Arguments = arguments,
            SubjectId = subjectId
        });
        return this;
    }

    public SkillCompatibilityResult AddWarningKey(string code, string messageKey, string subjectId = null,
        params object[] arguments)
    {
        Issues.Add(new SkillValidationIssue
        {
            Severity = SkillValidationSeverity.Warning,
            Code = code,
            MessageKey = messageKey,
            Arguments = arguments,
            SubjectId = subjectId
        });
        return this;
    }

    public void Merge(SkillCompatibilityResult other)
    {
        Issues.AddRange(other.Issues);
    }
}

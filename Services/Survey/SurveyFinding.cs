namespace DynaDocs.Services.Survey;

/// <summary>One survey line: <c>&lt;kind&gt; &lt;KEY&gt; &lt;title&gt; — &lt;reason&gt;</c>.</summary>
internal sealed record SurveyFinding(string Kind, string Key, string Title, string Reason)
{
    public override string ToString() => $"{Kind} {Key} {Title} — {Reason}";
}

namespace DynaDocs.Models;

public class ValidationResult
{
    public List<Violation> Violations { get; } = [];
    public int ErrorCount => Violations.Count(v => v.Severity == ViolationSeverity.Error);
    public int WarningCount => Violations.Count(v => v.Severity == ViolationSeverity.Warning);
    public bool HasErrors => ErrorCount > 0;
    public int TotalFilesChecked { get; set; }

    public void AddViolation(Violation violation) => Violations.Add(violation);
    public void AddRange(IEnumerable<Violation> violations) => Violations.AddRange(violations);
}

namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IValidationService
{
    List<ValidationIssue> ValidateSystem(string basePath);
    List<ValidationIssue> ValidateRoleFile(string basePath, string roleFilePath);
}

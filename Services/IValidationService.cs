namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IValidationService
{
    List<ValidationIssue> ValidateSystem(string basePath);
}

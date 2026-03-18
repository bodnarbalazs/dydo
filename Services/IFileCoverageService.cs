namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IFileCoverageService
{
    FileCoverageReport GenerateReport(FileCoverageOptions options);
}

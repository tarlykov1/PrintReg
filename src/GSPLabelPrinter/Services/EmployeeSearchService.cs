using GSPLabelPrinter.Models;
using GSPLabelPrinter.Utilities;

namespace GSPLabelPrinter.Services;

public sealed class EmployeeSearchService
{
    public IReadOnlyList<Employee> Search(IEnumerable<Employee> employees, string? query, int max = 20)
    {
        var q = TextNormalizer.NormalizeForSearch(query);
        if (q.Length < 2) return [];
        max = Math.Clamp(max, 1, 20);

        return employees
            .Select(e => new { Employee = e, Rank = Rank(e, q) })
            .Where(x => x.Rank < 100)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Employee.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(x => x.Employee)
            .ToList();
    }

    private static int Rank(Employee employee, string query)
    {
        var name = TextNormalizer.NormalizeForSearch(employee.FullName);
        var position = TextNormalizer.NormalizeForSearch(employee.Position);
        if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 2;
        if (position.Contains(query, StringComparison.OrdinalIgnoreCase)) return 3;
        return 100;
    }
}

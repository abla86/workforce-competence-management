using ShiftPlanner.Models;
using ShiftPlanner.Services;

var planner = new PlannerService();
var dataService = new DataService();
var csvService = new CsvExportService();

var dataPath = Path.Combine(
    AppContext.BaseDirectory,
    "planner-data.json"
);

if (!File.Exists(dataPath))
{
    throw new FileNotFoundException(
        "Planner data file was not found in the application output directory.",
        dataPath);
}

var data = dataService.Load(dataPath);

Console.OutputEncoding = System.Text.Encoding.UTF8;

while (true)
{
    Console.Clear();
    Console.WriteLine("Shift & Competence Planner");
    Console.WriteLine("==========================");
    Console.WriteLine("1. Vis ansatte");
    Console.WriteLine("2. Vis vakter og status");
    Console.WriteLine("3. Vis timer per ansatt");
    Console.WriteLine("4. Legg ansatt til vakt");
    Console.WriteLine("5. Fjern ansatt fra vakt");
    Console.WriteLine("6. Eksporter vaktplan til CSV");
    Console.WriteLine("7. Lagre");
    Console.WriteLine("0. Avslutt");
    Console.Write("\nVelg: ");

    var choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            ShowEmployees(data.Employees);
            break;

        case "2":
            ShowShifts(data, planner);
            break;

        case "3":
            ShowHours(data, planner);
            break;

        case "4":
            AddEmployeeToShift(data, planner);
            dataService.Save(dataPath, data);
            break;

        case "5":
            RemoveEmployeeFromShift(data);
            dataService.Save(dataPath, data);
            break;

        case "6":
            var csvPath = Path.Combine(
                Environment.CurrentDirectory,
                "shift-plan.csv"
            );
            csvService.Export(
                csvPath,
                data.Shifts,
                data.Employees
            );
            Console.WriteLine($"Eksportert til: {csvPath}");
            Pause();
            break;

        case "7":
            dataService.Save(dataPath, data);
            Console.WriteLine("Data lagret.");
            Pause();
            break;

        case "0":
            dataService.Save(dataPath, data);
            return;

        default:
            Console.WriteLine("Ugyldig valg.");
            Pause();
            break;
    }
}

static void ShowEmployees(IReadOnlyList<Employee> employees)
{
    Console.Clear();
    Console.WriteLine("Ansatte");
    Console.WriteLine("=======");

    foreach (var employee in employees.OrderBy(e => e.Name))
    {
        Console.WriteLine(
            $"{employee.Id}: {employee.Name} | " +
            $"{employee.Role} | " +
            $"{employee.PositionPercent}%"
        );

        Console.WriteLine(
            $"   Kompetanse: {string.Join(", ", employee.Competencies)}"
        );
    }

    Pause();
}

static void ShowShifts(
    PlannerData data,
    PlannerService planner)
{
    Console.Clear();
    Console.WriteLine("Vakter");
    Console.WriteLine("======");

    var employeeMap = data.Employees.ToDictionary(e => e.Id);

    foreach (var shift in data.Shifts.OrderBy(s => s.Date))
    {
        Console.WriteLine(
            $"\n{shift.Date:dd.MM.yyyy} {shift.ShiftType} " +
            $"({shift.Hours} t)"
        );

        Console.WriteLine(
            $"Minimumsbemanning: {shift.MinimumStaff}"
        );

        Console.WriteLine(
            $"Kompetansekrav: " +
            $"{string.Join(", ", shift.RequiredCompetencies)}"
        );

        Console.WriteLine("Planlagt:");

        foreach (var id in shift.EmployeeIds)
        {
            if (employeeMap.TryGetValue(id, out var employee))
            {
                Console.WriteLine(
                    $"  - {employee.Name} ({employee.Role})"
                );
            }
        }

        Console.WriteLine("Status:");

        foreach (var message in planner.ValidateShift(
            shift,
            data.Employees))
        {
            Console.WriteLine($"  - {message}");
        }
    }

    Pause();
}

static void ShowHours(
    PlannerData data,
    PlannerService planner)
{
    Console.Clear();
    Console.WriteLine("Timer per ansatt");
    Console.WriteLine("================");

    var hours = planner.CalculateHoursPerEmployee(data.Shifts);

    foreach (var employee in data.Employees.OrderBy(e => e.Name))
    {
        Console.WriteLine(
            $"{employee.Name}: " +
            $"{hours.GetValueOrDefault(employee.Id):0.##} t"
        );
    }

    Pause();
}

static void AddEmployeeToShift(
    PlannerData data,
    PlannerService planner)
{
    Console.Clear();

    var shift = SelectShift(data.Shifts);

    if (shift is null)
    {
        return;
    }

    var available = planner.GetAvailableEmployees(
        shift,
        data.Employees
    );

    if (available.Count == 0)
    {
        Console.WriteLine("Ingen tilgjengelige ansatte.");
        Pause();
        return;
    }

    Console.WriteLine("\nTilgjengelige ansatte:");

    foreach (var employee in available)
    {
        Console.WriteLine(
            $"{employee.Id}: {employee.Name} ({employee.Role})"
        );
    }

    Console.Write("\nAnsatt-ID: ");

    if (!int.TryParse(Console.ReadLine(), out var employeeId) ||
        !available.Any(e => e.Id == employeeId))
    {
        Console.WriteLine("Ugyldig ansatt-ID.");
        Pause();
        return;
    }

    var updated = shift with
    {
        EmployeeIds = shift.EmployeeIds
            .Append(employeeId)
            .Distinct()
            .ToList()
    };

    ReplaceShift(data.Shifts, updated);

    Console.WriteLine("Ansatt lagt til vakten.");
    Pause();
}

static void RemoveEmployeeFromShift(PlannerData data)
{
    Console.Clear();

    var shift = SelectShift(data.Shifts);

    if (shift is null)
    {
        return;
    }

    if (shift.EmployeeIds.Count == 0)
    {
        Console.WriteLine("Vakten har ingen ansatte.");
        Pause();
        return;
    }

    var employeeMap = data.Employees.ToDictionary(e => e.Id);

    Console.WriteLine("\nAnsatte på vakten:");

    foreach (var id in shift.EmployeeIds)
    {
        if (employeeMap.TryGetValue(id, out var employee))
        {
            Console.WriteLine($"{id}: {employee.Name}");
        }
    }

    Console.Write("\nAnsatt-ID som skal fjernes: ");

    if (!int.TryParse(Console.ReadLine(), out var employeeId) ||
        !shift.EmployeeIds.Contains(employeeId))
    {
        Console.WriteLine("Ugyldig ansatt-ID.");
        Pause();
        return;
    }

    var updated = shift with
    {
        EmployeeIds = shift.EmployeeIds
            .Where(id => id != employeeId)
            .ToList()
    };

    ReplaceShift(data.Shifts, updated);

    Console.WriteLine("Ansatt fjernet fra vakten.");
    Pause();
}

static Shift? SelectShift(IReadOnlyList<Shift> shifts)
{
    Console.WriteLine("Velg vakt:");

    foreach (var shift in shifts.OrderBy(s => s.Date))
    {
        Console.WriteLine(
            $"{shift.Id}: {shift.Date:dd.MM.yyyy} {shift.ShiftType}"
        );
    }

    Console.Write("\nVakt-ID: ");

    if (!int.TryParse(Console.ReadLine(), out var id))
    {
        Console.WriteLine("Ugyldig vakt-ID.");
        Pause();
        return null;
    }

    var selectedShift = shifts.FirstOrDefault(s => s.Id == id);

    if (selectedShift is null)
    {
        Console.WriteLine("Fant ikke vakten.");
        Pause();
    }

    return selectedShift;
}

static void ReplaceShift(
    List<Shift> shifts,
    Shift updated)
{
    var index = shifts.FindIndex(s => s.Id == updated.Id);

    if (index >= 0)
    {
        shifts[index] = updated;
    }
}

static void Pause()
{
    Console.WriteLine("\nTrykk Enter for å fortsette...");
    Console.ReadLine();
}

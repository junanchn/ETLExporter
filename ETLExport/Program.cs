using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Symbols;
using Microsoft.Windows.EventTracing.Processes;

namespace ETLExport;

record Config
{
    public required GenericEventFilter StartEvent { get; init; }
    public required GenericEventFilter EndEvent { get; init; }
    public required string ProcessRegex { get; init; }
    public List<string> SymbolPaths { get; init; } = [];
    public List<string> SymbolProcesses { get; init; } = [];
    public required List<string> Tables { get; init; }
}

class Program
{
    static void Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            var (etlPath, config) = ParseArguments(args);

            var errors = ValidateArguments(etlPath, config);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Console.WriteLine(error);
                Environment.Exit(1);
            }

            var presets = CreateTablePresets(config!.Tables);
            if (presets.Count == 0)
            {
                throw new InvalidOperationException("No valid tables to export");
            }

            ProcessTrace(etlPath!, config, presets);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Stack:");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ETLExport <etl-file> [config-file] [options]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ETLExport trace.etl                           # Use trace.etl with default config");
        Console.WriteLine("  ETLExport trace.etl custom.json               # Use trace.etl with custom config");
        Console.WriteLine("  ETLExport custom.json trace.etl               # Same as above (order doesn't matter)");
        Console.WriteLine("  ETLExport trace.etl --SymbolPaths C:\\Symbols  # Override config via command line");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --ProcessRegex <regex>         Process name filter");
        Console.WriteLine("  --SymbolPaths <path>           Symbol search paths (can be repeated)");
        Console.WriteLine("  --SymbolProcesses <name>       Processes to load symbols for (can be repeated)");
        Console.WriteLine("  --Tables <name>                Tables to export (can be repeated)");
        Console.WriteLine("  --StartEvent:TaskName <name>   Start event task name");
        Console.WriteLine("  --EndEvent:TaskName <name>     End event task name");
    }

    static (string? etlPath, Config? config) ParseArguments(string[] args)
    {
        var fileArgs = args.TakeWhile((arg, index) => index < 2 && !arg.StartsWith("--")).ToList();
        var configArgs = args.Skip(fileArgs.Count).ToArray();

        string? etlPath = fileArgs.FirstOrDefault(f => f.EndsWith(".etl", StringComparison.OrdinalIgnoreCase));
        string? configPath = fileArgs.FirstOrDefault(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        configPath ??= "config.json";

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true)
            .AddCommandLine(configArgs)
            .Build()
            .Get<Config>();

        return (etlPath, config);
    }

    static List<string> ValidateArguments(string? etlPath, Config? config)
    {
        List<string> errors = [];

        if (string.IsNullOrEmpty(etlPath))
            errors.Add("Error: ETL file not specified");
        else if (!File.Exists(etlPath))
            errors.Add($"Error: ETL file not found: {etlPath}");

        if (config is null)
        {
            errors.Add("Error: Configuration not specified or invalid");
            return errors;
        }

        if (config.Tables.Count == 0)
            errors.Add("Error: At least one output table must be specified");

        return errors;
    }

    static List<AnalysisTableBase> CreateTablePresets(List<string> tables)
    {
        List<AnalysisTableBase> presets = [];

        foreach (var tableName in tables.Distinct())
        {
            AnalysisTableBase? preset = tableName switch
            {
                "CpuUsagePrecise" => new CpuUsagePrecise(),
                "CpuUsageSampled" => new CpuUsageSampled(),
                "DiskUsage" => new DiskUsage(),
                "HeapAllocations" => new HeapAllocations(),
                "HeapAllocationsReverse" => new HeapAllocationsReverse(),
                "Images" => new Images(),
                "TotalCommit" => new TotalCommit(),
                "TotalCommitReverse" => new TotalCommitReverse(),
                _ => null
            };

            if (preset is not null)
                presets.Add(preset);
            else
                Console.WriteLine($"Warning: Unknown table name: {tableName}");
        }

        return presets;
    }

    static void ProcessTrace(string etlPath, Config config, List<AnalysisTableBase> presets)
    {
        using var trace = TraceProcessor.Create(etlPath);

        var processes = trace.UseProcesses();
        var symbols = trace.UseSymbols();

        foreach (var preset in presets)
            preset.UseTrace(trace);

        var startEventFinder = new GenericEventFinder(config.StartEvent!, trace);
        var endEventFinder = new GenericEventFinder(config.EndEvent!, trace);

        trace.Process();

        var (startTime, endTime) = GetTimeRange(startEventFinder, endEventFinder);

        var targetProcesses = GetTargetProcesses(processes, config.ProcessRegex);

        if (config.SymbolPaths.Count > 0 && config.SymbolProcesses.Count > 0)
        {
            symbols.Result.LoadSymbolsForConsoleAsync(
                SymCachePath.Automatic,
                new SymbolPath(config.SymbolPaths.ToArray()),
                config.SymbolProcesses.ToArray()).Wait();
        }

        foreach (var preset in presets)
        {
            preset.Process(targetProcesses, startTime, endTime);
            preset.Table.ExportToJson($"{etlPath}.{preset.TableName}.json");
            Console.WriteLine($"Exported: {etlPath}.{preset.TableName}.json");
        }
    }

    static (long startTime, long endTime) GetTimeRange(GenericEventFinder startEventFinder, GenericEventFinder endEventFinder)
    {
        var startEvents = startEventFinder.FindEvents();
        var endEvents = endEventFinder.FindEvents();

        if (startEvents.Count == 0 || endEvents.Count == 0)
        {
            throw new InvalidOperationException(
                $"Required events not found (Start: {startEvents.Count}, End: {endEvents.Count})");
        }

        if (startEvents.Count > 1 || endEvents.Count > 1)
        {
            Console.WriteLine($"Warning: Multiple events found (Start: {startEvents.Count}, End: {endEvents.Count}).");
        }

        var startTime = startEvents[0].Timestamp.Nanoseconds;
        var endTime = endEvents[0].Timestamp.Nanoseconds;

        if (endTime <= startTime)
        {
            throw new InvalidOperationException(
                $"Invalid time range: End time ({endTime}) must be after start time ({startTime})");
        }

        return (startTime, endTime);
    }

    static IProcess[] GetTargetProcesses(IPendingResult<IProcessDataSource> processes, string processRegex)
    {
        var regex = new Regex(processRegex, RegexOptions.Compiled);

        var result = processes.Result.Processes
            .Where(p => p.CommandLine is not null && regex.IsMatch(p.CommandLine))
            .ToArray();

        if (result.Length == 0)
            throw new InvalidOperationException($"No processes matched the regex: {processRegex}");

        return result;
    }
}

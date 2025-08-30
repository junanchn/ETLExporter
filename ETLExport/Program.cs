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
    public string[] SymbolPaths { get; init; } = [];
    public string[] SymbolProcesses { get; init; } = [];
    public required string[] Tables { get; init; }
    public bool SkipExisting { get; init; } = false;
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

            var (etlPaths, configs) = ParseArguments(args);

            var errors = ValidateArguments(etlPaths, configs);
            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Console.WriteLine($"Error: {error}");
                Environment.Exit(1);
            }

            foreach (var etlPath in etlPaths)
            {
                Console.WriteLine($"Processing: {etlPath}");
                ProcessTrace(etlPath, configs);
            }
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
        Console.WriteLine("  ETLExport <etl-files...> [config-files...] [options]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ETLExport trace.etl                           # Use trace.etl with default config");
        Console.WriteLine("  ETLExport trace.etl custom.json               # Use trace.etl with custom config");
        Console.WriteLine("  ETLExport *.etl *.json                        # Process all ETL files with all configs");
        Console.WriteLine("  ETLExport t1.etl t2.etl c1.json c2.json       # Process 2 ETL files with 2 configs each");
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

    static (List<string> etlPaths, List<(string name, Config config)> configs) ParseArguments(string[] args)
    {
        var fileArgs = args.TakeWhile(arg => !arg.StartsWith("--")).ToList();
        var configArgs = args.Skip(fileArgs.Count).ToArray();

        var etlPaths = new HashSet<string>();
        var configPaths = new HashSet<string>();

        foreach (var arg in fileArgs)
        {
            if (arg.Contains('*') || arg.Contains('?'))
            {
                var dir = Path.GetDirectoryName(arg) ?? ".";
                var pattern = Path.GetFileName(arg);
                foreach (var file in Directory.GetFiles(dir, pattern))
                {
                    var fullPath = Path.GetFullPath(file);
                    if (file.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
                        etlPaths.Add(fullPath);
                    else if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        configPaths.Add(fullPath);
                }
            }
            else if (arg.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(arg))
                    etlPaths.Add(Path.GetFullPath(arg));
            }
            else if (arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(arg))
                    configPaths.Add(Path.GetFullPath(arg));
            }
        }

        if (configPaths.Count == 0)
            configPaths.Add("config.json");

        var configs = new List<(string name, Config config)>();
        foreach (var configPath in configPaths.OrderBy(c => c))
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: configPaths.Count == 1 && configPath == "config.json")
                .AddCommandLine(configArgs)
                .Build()
                .Get<Config>();

            if (config != null)
            {
                var configName = Path.GetFileNameWithoutExtension(configPath);
                if (configName == "config")
                    configName = "";
                configs.Add((configName ?? "", config));
            }
        }

        return (etlPaths.OrderBy(e => e).ToList(), configs);
    }

    static List<string> ValidateArguments(List<string> etlPaths, List<(string name, Config config)> configs)
    {
        List<string> errors = [];

        if (etlPaths.Count == 0)
            errors.Add("No ETL files specified");

        foreach (var etlPath in etlPaths)
        {
            if (!File.Exists(etlPath))
                errors.Add($"ETL file not found: {etlPath}");
        }

        if (configs.Count == 0)
        {
            errors.Add("No valid configurations found");
            return errors;
        }

        foreach (var (name, config) in configs)
        {
            if (config.Tables.Length == 0)
            {
                var configName = string.IsNullOrEmpty(name) ? "default config" : $"config '{name}'";
                errors.Add($"At least one output table must be specified in {configName}");
            }
        }

        return errors;
    }

    static List<AnalysisTableBase> CreateTablePresets(string[] tables)
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

    static string GetOutputFileName(string etlPath, string configName, string tableName)
    {
        return string.IsNullOrEmpty(configName)
            ? $"{etlPath}.{tableName}.json"
            : $"{etlPath}.{configName}.{tableName}.json";
    }

    static void ProcessTrace(string etlPath, List<(string name, Config config)> configs)
    {
        using var trace = TraceProcessor.Create(etlPath);
        
        var processes = trace.UseProcesses();
        var symbols = trace.UseSymbols();

        var validConfigs = new List<(string name, Config config, List<AnalysisTableBase> presets,
                                     GenericEventFinder start, GenericEventFinder end)>();

        foreach (var (name, config) in configs)
        {
            var presets = CreateTablePresets(config.Tables);
            if (presets.Count == 0)
                continue;

            if (config.SkipExisting &&
                presets.All(p => File.Exists(GetOutputFileName(etlPath, name, p.TableName))))
            {
                var configDesc = string.IsNullOrEmpty(name) ? "default config" : $"config '{name}'";
                Console.WriteLine($"Skipping {configDesc} - all reports exist");
                continue;
            }

            foreach (var preset in presets)
                preset.UseTrace(trace);

            validConfigs.Add((name, config, presets,
                            new GenericEventFinder(config.StartEvent, trace),
                            new GenericEventFinder(config.EndEvent, trace)));
        }

        if (validConfigs.Count == 0)
            return;

        trace.Process();

        foreach (var (name, config, presets, startFinder, endFinder) in validConfigs)
        {
            try
            {
                var (startTime, endTime) = GetTimeRange(startFinder, endFinder);
                var targetProcesses = GetTargetProcesses(processes, config.ProcessRegex);

                symbols.Result.LoadSymbolsForConsoleAsync(
                    SymCachePath.Automatic,
                    new SymbolPath(config.SymbolPaths),
                    config.SymbolProcesses).Wait();

                foreach (var preset in presets)
                {
                    preset.Process(targetProcesses, startTime, endTime);

                    var outputName = GetOutputFileName(etlPath, name, preset.TableName);
                    preset.Table.ExportToJson(outputName);
                    Console.WriteLine($"Exported: {outputName}");
                }
            }
            catch (Exception ex)
            {
                var configDesc = string.IsNullOrEmpty(name) ? "default config" : $"config '{name}'";
                Console.WriteLine($"Error processing {configDesc}: {ex.Message}");
            }
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

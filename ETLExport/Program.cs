using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Symbols;

public class ETLExportConfig
{
    public GenericEventFilter StartEvent { get; set; }
    public GenericEventFilter EndEvent { get; set; }
    public string ProcessRegex { get; set; }
    public List<string> SymbolPaths { get; set; } = new List<string>();
    public List<string> SymbolProcesses { get; set; } = new List<string>();
    public List<string> Tables { get; set; } = new List<string>();
}

class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: ETLExport <config.json> <input.etl>");
            return;
        }

        var configPath = args[0];
        var etlPath = args[1];

        ETLExportConfig config;
        try
        {
            config = JsonSerializer.Deserialize<ETLExportConfig>(File.ReadAllText(configPath));
            if (config == null)
            {
                Console.WriteLine("Error: Invalid configuration file.");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to load config - {ex.Message}");
            return;
        }

        var presets = new List<AnalysisTableBase>();
        foreach (var tableName in config.Tables)
        {
            switch (tableName)
            {
                case "CpuUsagePrecise":
                    presets.Add(new CpuUsagePrecise());
                    break;
                case "CpuUsageSampled":
                    presets.Add(new CpuUsageSampled());
                    break;
                case "DiskUsage":
                    presets.Add(new DiskUsage());
                    break;
                case "HeapAllocations":
                    presets.Add(new HeapAllocations());
                    break;
                case "HeapAllocationsReverse":
                    presets.Add(new HeapAllocationsReverse());
                    break;
                case "Images":
                    presets.Add(new Images());
                    break;
                case "TotalCommit":
                    presets.Add(new TotalCommit());
                    break;
                case "TotalCommitReverse":
                    presets.Add(new TotalCommitReverse());
                    break;
                default:
                    Console.WriteLine($"Error: Unknown table: {tableName}");
                    return;
            }
        }

        if (presets.Count == 0)
        {
            Console.WriteLine("Error: No tables specified in configuration.");
            return;
        }

        using (var trace = TraceProcessor.Create(etlPath))
        {
            var processes = trace.UseProcesses();
            var symbols = trace.UseSymbols();
            foreach (var preset in presets)
                preset.UseTrace(trace);

            var startEventFinder = new GenericEventFinder(config.StartEvent, trace);
            var endEventFinder = new GenericEventFinder(config.EndEvent, trace);

            trace.Process();

            var startEvents = startEventFinder.FindEvents();
            var endEvents = endEventFinder.FindEvents();

            if (startEvents.Count > 1 || endEvents.Count > 1)
            {
                Console.WriteLine($"Warning: Multiple events found (Start: {startEvents.Count}, End: {endEvents.Count}). Using first occurrence.");
            }

            if (startEvents.Count == 0 || endEvents.Count == 0)
            {
                Console.WriteLine($"Error: Required events not found (Start: {startEvents.Count}, End: {endEvents.Count}).");
                return;
            }

            var startTime = startEvents[0].Timestamp.Nanoseconds;
            var endTime = endEvents[0].Timestamp.Nanoseconds;

            if (endTime <= startTime)
            {
                Console.WriteLine("Error: Invalid time range. End time must be after start time.");
                return;
            }

            var processRegex = string.IsNullOrEmpty(config.ProcessRegex) ? null : new Regex(config.ProcessRegex);
            var targetProcesses = processes.Result.Processes
                .Where(p => p.CommandLine != null && (processRegex == null || processRegex.IsMatch(p.CommandLine)))
                .ToArray();

            if (targetProcesses.Length == 0)
            {
                Console.WriteLine($"Error: No processes matched the regex filter '{config.ProcessRegex}'");
                return;
            }

            if (config.SymbolPaths?.Count > 0 && config.SymbolProcesses?.Count > 0)
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
    }
}

# ETLExporter

Windows ETL (Event Trace Log) analysis toolkit for extracting and visualizing performance data from ETL trace files.

## What it does

- Leverages Microsoft TraceProcessor API for efficient ETL processing
- Analyzes Windows ETL traces to extract performance metrics (CPU, memory, disk I/O)
- Exports data as hierarchical JSON for easy processing
- Compares performance between different runs or builds
- Visualizes results in an interactive web-based tree table

Common use cases: Performance regression testing, memory leak detection, CPU hotspot analysis.

## Quick Start

### Prerequisites
- .NET Framework 4.8
- Windows 10/11 or Windows Server 2016+

### Installation
```bash
git clone https://github.com/junanchn/ETLExporter.git
cd ETLExporter
msbuild ETLExporter.sln /p:Configuration=Release
```

## Usage

### 1. Capture ETL Trace
```bash
# Using Windows Performance Recorder
wpr -start CPU -start Heap -start DiskIO
# Run your scenario
wpr -stop output.etl
```

### 2. Analyze with ETLExport

Create `config.json`:
```json
{
  "StartEvent": {
    "ProviderId": "ProviderGUID",
    "TaskName": "TaskName",
    "OpcodeName": "win:Start"
  },
  "EndEvent": {
    "ProviderId": "ProviderGUID",
    "TaskName": "TaskName",
    "OpcodeName": "win:Stop"
  },
  "ProcessRegex": "YourApp\\.exe",
  "SymbolPaths": [
    "srv*C:\\Symbols*https://msdl.microsoft.com/download/symbols"
  ],
  "SymbolProcesses": ["YourApp.exe"],
  "Tables": [
    "CpuUsageSampled",
    "HeapAllocations",
    "DiskUsage"
  ]
}
```

Run analysis:
```bash
ETLExport.exe config.json trace.etl
```

Output files:
- `trace.etl.CpuUsageSampled.json`
- `trace.etl.HeapAllocations.json`
- `trace.etl.DiskUsage.json`

### 2. TreeMerge - Merging Results

Merge multiple analysis results:
```bash
TreeMerge.exe merged.json result1.json result2.json result3.json
```

### 3. TreeDiff - Comparing Results

Compare two analysis results:
```bash
TreeDiff.exe diff.json test.json baseline.json
```

The output will contain three values for each metric:
- Test value
- Baseline value
- Difference (Test - Baseline)

### 4. TreeTableViewer - Visualizing Results

1. Open `TreeTableViewer.html` in a browser
2. Drag and drop generated JSON file onto the page
3. Use the interface to:
   - Expand/collapse tree nodes with arrow keys
   - Sort columns by clicking headers
   - Search and filter data

## Analysis Tables

| Table Name | Description | Columns |
|------------|-------------|---------|
| `CpuUsagePrecise` | Context switch based CPU usage | Count, CPU Usage, Ready, Waits |
| `CpuUsageSampled` | Sampled CPU usage | Count, Weight |
| `DiskUsage` | Disk I/O operations | Count, Size, Disk Service Time |
| `HeapAllocations` | Heap allocations | Count, Size |
| `HeapAllocationsReverse` | Heap allocations (reversed stacks) | Count, Size |
| `Images` | DLL/EXE loading | Count, Size |
| `TotalCommit` | Virtual memory commits | Count, Size |
| `TotalCommitReverse` | Virtual memory (reversed stacks) | Count, Size |

## Configuration

### Event Filters
Define analysis time range using any combination:
- `ProviderId`: GUID of the ETL provider
- `ProviderName`: Provider name string
- `TaskName`: Name of the task
- `OpcodeName`: Operation code name (e.g., "win:Start", "win:Stop")
- `Opcode`: opcode value

### Process Filter
- `ProcessRegex`: Regex pattern to filter processes by command line

### Symbol Resolution
- `SymbolPaths`: Symbol server paths array
- `SymbolProcesses`: Process names for symbol resolution

### Analysis Tables
- `Tables`: Specify which analysis tables to generate

## Example Workflows

### Performance Regression
```bash
# 1. Capture baseline and test traces
wpr -start CPU -start Heap
# Run baseline
wpr -stop baseline.etl

wpr -start CPU -start Heap
# Run test
wpr -stop test.etl

# 2. Analyze
ETLExport.exe config.json baseline.etl
ETLExport.exe config.json test.etl

# 3. Compare
TreeDiff.exe comparison.json test.etl.CpuUsageSampled.json baseline.etl.CpuUsageSampled.json

# 4. Visualize
# Open TreeTableViewer.html and load comparison.json
```

### Multi-Run Analysis
```bash
# Analyze multiple runs
ETLExport.exe config.json run1.etl
ETLExport.exe config.json run2.etl
ETLExport.exe config.json run3.etl

# Merge results
TreeMerge.exe merge.json run1.etl.HeapAllocations.json run2.etl.HeapAllocations.json run3.etl.HeapAllocations.json

# View aggregated data
# Open TreeTableViewer.html and load merge.json
```

## Data Format

The exported JSON files use a hierarchical tree structure:
```json
{
  "columnNames": ["Count", "Size"],
  "treeData": [
    {
      "n": "ProcessName",
      "c": [
        {
          "n": "StackFrame1",
          "c": [
            {
              "n": "StackFrame2",
              "0": 100,  // Count
              "1": 4096  // Size in bytes
            }
          ]
        }
      ]
    }
  ]
}
```

## Tips

- Always configure symbol paths for meaningful stack traces
- Use custom events to precisely mark the analysis region
- Use specific regex patterns to focus on relevant processes
- For large ETL files, analyze specific tables separately

## Resources

- [Windows Performance Toolkit Documentation](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/)
- [ETW Documentation](https://docs.microsoft.com/en-us/windows/win32/etw/event-tracing-portal)
- [TraceProcessor Documentation](https://docs.microsoft.com/en-us/windows/apps/trace-processing/)

## License

MIT License - see [LICENSE](LICENSE) file for details.

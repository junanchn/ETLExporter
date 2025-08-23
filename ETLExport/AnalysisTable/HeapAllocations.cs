using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Processes;

namespace ETLExport;

class HeapAllocations : AnalysisTableBase
{
    public override string TableName => "HeapAllocations";
    public override string[] ColumnNames => ["Count", "Size"];
    protected virtual bool StackReverse => false;

    private static readonly HashSet<string> _heapPatterns =
    [
        "ntdll.dll!RtlCreateHeap",
        "ntdll.dll!RtlAllocateHeap",
        "ntdll.dll!RtlpAllocateHeapInternal",
        "ntdll.dll!RtlReAllocateHeap",
        "ntdll.dll!RtlpReAllocateHeapInternal",
        "ntdll.dll!RtlFreeHeap",
        "ntdll.dll!RtlpFreeHeapInternal"
    ];
    private HeapEventParser? _heapParser;

    public override void UseTrace(ITraceProcessor trace)
    {
        _heapParser = new HeapEventParser(trace);
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var allocation in _heapParser!.GetAllocations())
        {
            var process = processes.FirstOrDefault(p => p.Id == allocation.ProcessId);
            if (process is null)
                continue;

            var allocTime = allocation.AllocTime.Nanoseconds;
            var freeTime = allocation.FreeTime?.Nanoseconds ?? long.MaxValue;
            var size = ImpactingSize(allocation.AddressRange.Size.Bytes, allocTime, freeTime, startTime, endTime);
            if (size != 0)
            {
                var stack = StackStringList(allocation.AllocStack);
                var end = stack.FindIndex(_heapPatterns.Contains);
                if (end >= 0)
                    stack.RemoveRange(end + 1, stack.Count - end - 1);
                if (StackReverse)
                    stack.Reverse();
                List<string> path = [process.ImageName, ..stack, ""];
                Table.Add(path, 1, size);
            }
        }
    }
}

class HeapAllocationsReverse : HeapAllocations
{
    public override string TableName => "HeapAllocationsReverse";
    protected override bool StackReverse => true;
}

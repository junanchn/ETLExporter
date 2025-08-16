using System.Collections.Generic;
using System.Linq;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Processes;

public class HeapAllocations : AnalysisTableBase
{
    public override string TableName => "HeapAllocations";
    public override string[] ColumnNames => new[] { "Count", "Size" };
    protected virtual bool StackReverse => false;

    private static HashSet<string> _heapPatterns = new HashSet<string> {
        "ntdll.dll!RtlCreateHeap",
        "ntdll.dll!RtlAllocateHeap",
        "ntdll.dll!RtlpAllocateHeapInternal",
        "ntdll.dll!RtlReAllocateHeap",
        "ntdll.dll!RtlpReAllocateHeapInternal",
        "ntdll.dll!RtlFreeHeap",
        "ntdll.dll!RtlpFreeHeapInternal",
    };
    private HeapEventParser _heapParser;

    public override void UseTrace(ITraceProcessor trace)
    {
        _heapParser = new HeapEventParser(trace);
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var allocation in _heapParser.GetAllocations())
        {
            var process = processes.FirstOrDefault(p => p.Id == allocation.ProcessId);
            if (process == null)
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
                var path = new List<string> { process.ImageName };
                path.AddRange(stack);
                path.Add("");
                Table.Add(path, 1, size);
            }
        }
    }
}

public class HeapAllocationsReverse : HeapAllocations
{
    public override string TableName => "HeapAllocationsReverse";
    protected override bool StackReverse => true;
}

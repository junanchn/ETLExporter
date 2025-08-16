using System.Collections.Generic;
using System.Linq;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Memory;
using Microsoft.Windows.EventTracing.Processes;

public class TotalCommit : AnalysisTableBase
{
    public override string TableName => "TotalCommit";
    public override string[] ColumnNames => new[] { "Count", "Size" };
    protected virtual bool StackReverse => false;

    private IPendingResult<ICommitDataSource> commitData;

    public override void UseTrace(ITraceProcessor trace)
    {
        commitData = trace.UseCommitData();
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        ProcessCommitLifetimes(processes, startTime, endTime);
        ProcessCopyOnWriteLifetimes(processes, startTime, endTime);
        ProcessPageFileSectionLifetimes(processes, startTime, endTime);
    }

    private void ProcessCommitLifetimes(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var lifetime in commitData.Result.CommitLifetimes)
        {
            if (!processes.Contains(lifetime.Process))
                continue;

            var commitTime = lifetime.CommitEvent.Timestamp.Nanoseconds;
            var decommitTime = lifetime.DecommitEvent?.Timestamp.Value.Nanoseconds ?? long.MaxValue;
            var size = ImpactingSize(lifetime.AddressRange.Size.Bytes, commitTime, decommitTime, startTime, endTime);
            if (size != 0)
            {
                var stack = StackStringList(lifetime.CommitEvent.Stack);
                if (StackReverse)
                    stack.Reverse();
                var path = new List<string> { lifetime.Process.ImageName, "Virtual Alloc" };
                path.AddRange(stack);
                path.Add("");
                Table.Add(path, 1, size);
            }
        }
    }

    private void ProcessCopyOnWriteLifetimes(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var lifetime in commitData.Result.CopyOnWriteLifetimes)
        {
            if (!processes.Contains(lifetime.Process))
                continue;

            var commitTime = lifetime.CreateTime.Nanoseconds;
            var decommitTime = lifetime.DeleteTime.Nanoseconds;
            var size = ImpactingSize(lifetime.AddressRange.Size.Bytes, commitTime, decommitTime, startTime, endTime);
            if (size != 0)
            {
                var stack = StackStringList(lifetime.CreateStack);
                if (StackReverse)
                    stack.Reverse();
                var path = new List<string> { lifetime.Process.ImageName, "Copy on Write" };
                path.AddRange(stack);
                path.Add("");
                Table.Add(path, 1, size);
            }
        }
    }

    private void ProcessPageFileSectionLifetimes(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var lifetime in commitData.Result.PageFileSectionLifetimes)
        {
            if (!processes.Contains(lifetime.CreatingProcess))
                continue;

            var commitTime = lifetime.CreateTime.Nanoseconds;
            var decommitTime = lifetime.DeleteTime.Nanoseconds;
            var size = ImpactingSize(lifetime.Size.Bytes, commitTime, decommitTime, startTime, endTime);
            if (size != 0)
            {
                var stack = StackStringList(lifetime.CreateStack);
                if (StackReverse)
                    stack.Reverse();
                var path = new List<string> { lifetime.CreatingProcess.ImageName, "PFMappedSection" };
                path.AddRange(stack);
                path.Add("");
                Table.Add(path, 1, size);
            }
        }
    }
}

public class TotalCommitReverse : HeapAllocations
{
    public override string TableName => "TotalCommitReverse";
    protected override bool StackReverse => true;
}

using System.Collections.Generic;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Symbols;
using Microsoft.Windows.EventTracing.Processes;

public abstract class AnalysisTableBase
{
    public abstract string TableName { get; }
    public abstract string[] ColumnNames { get; }
    public TreeTable Table { get; protected set; }

    protected AnalysisTableBase()
    {
        Table = new TreeTable(ColumnNames);
    }

    public abstract void UseTrace(ITraceProcessor trace);
    public abstract void Process(IProcess[] processes, long startTime, long endTime);

    protected static long ImpactingSize(long size, long commitTime, long decommitTime, long startTime, long endTime)
    {
        if (commitTime >= startTime && commitTime < endTime && decommitTime >= endTime)
            return +size;
        else if (commitTime < startTime && decommitTime >= startTime && decommitTime < endTime)
            return -size;
        else
            return 0;
    }

    protected static List<string> StackStringList(IThreadStack stack)
    {
        if (stack == null || stack.Frames.Count == 0)
            return new List<string> { "N/A" };

        if (stack.IsIdle)
            return new List<string> { "[Idle]" };

        var result = new List<string> { "[Root]" };
        var frames = stack.Frames;

        for (int i = frames.Count - 1; i >= 0; i--)
        {
            var module = frames[i].Image?.FileName ?? "?";
            var function = frames[i].Symbol?.FunctionName ?? "?";
            result.Add($"{module}!{function}");
        }

        return result;
    }
}

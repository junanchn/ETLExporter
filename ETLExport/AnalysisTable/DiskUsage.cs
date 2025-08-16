using System.Collections.Generic;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Disk;
using Microsoft.Windows.EventTracing.Processes;

public class DiskUsage : AnalysisTableBase
{
    public override string TableName => "DiskUsage";
    public override string[] ColumnNames => new[] { "Count", "Size", "Disk Service Time" };

    private IPendingResult<IDiskActivityDataSource> diskIOData;

    public override void UseTrace(ITraceProcessor trace)
    {
        diskIOData = trace.UseDiskIOData();
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var activity in diskIOData.Result.Activity)
        {
            if (activity.InitializeTime.Nanoseconds < startTime || activity.InitializeTime.Nanoseconds >= endTime)
                continue;

            var path = new List<string> { activity.IssuingProcess.ImageName, activity.Path ?? "" };
            path.AddRange(StackStringList(activity.InitializeStack));
            path.Add("");
            var size = activity.Size.Bytes;
            var service = activity.DiskServiceDuration.Nanoseconds;
            Table.Add(path, 1, size, service);
        }
    }
}

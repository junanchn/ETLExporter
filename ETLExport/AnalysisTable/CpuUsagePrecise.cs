using System.Collections.Generic;
using System.Linq;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Cpu;
using Microsoft.Windows.EventTracing.Processes;

public class CpuUsagePrecise : AnalysisTableBase
{
    public override string TableName => "CPUUsagePrecise";
    public override string[] ColumnNames => new[] { "Count", "CPU Usage", "Ready", "Waits" };

    private IPendingResult<ICpuSchedulingDataSource> cpuSchedulingData;

    public override void UseTrace(ITraceProcessor trace)
    {
        cpuSchedulingData = trace.UseCpuSchedulingData();
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var activity in cpuSchedulingData.Result.ThreadActivity)
        {
            if (!processes.Contains(activity.Process))
                continue;
            if (activity.StartTime.Nanoseconds < startTime || activity.StartTime.Nanoseconds >= endTime)
                continue;

            var newStack = StackStringList(activity.SwitchIn.Stack);
            var readyProcess = activity.ReadyingProcess?.ImageName ?? "Unknown";
            var readyStack = StackStringList(activity.ReadyThreadStack);
            var path = new List<string> { activity.Process.ImageName };
            path.AddRange(newStack);
            path.Add($"READIED BY {readyProcess}");
            path.AddRange(readyStack);
            path.Add("");
            var cpuUsage = activity.Duration.Nanoseconds;
            var waits = activity.WaitingDuration != null ? activity.WaitingDuration.Value.Nanoseconds : 0;
            var ready = activity.ReadyDuration.Value.Nanoseconds;
            Table.Add(path, 1, cpuUsage, ready, waits);
        }
    }
}

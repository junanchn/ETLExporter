using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Cpu;
using Microsoft.Windows.EventTracing.Processes;

namespace ETLExport;

class CpuUsagePrecise : AnalysisTableBase
{
    public override string TableName => "CPUUsagePrecise";
    public override string[] ColumnNames => ["Count", "CPU Usage", "Ready", "Waits"];

    private IPendingResult<ICpuSchedulingDataSource>? cpuSchedulingData;

    public override void UseTrace(ITraceProcessor trace)
    {
        cpuSchedulingData = trace.UseCpuSchedulingData();
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var activity in cpuSchedulingData!.Result.ThreadActivity)
        {
            if (!processes.Contains(activity.Process))
                continue;
            if (activity.StartTime.Nanoseconds < startTime || activity.StartTime.Nanoseconds >= endTime)
                continue;

            var newStack = StackStringList(activity.SwitchIn.Stack);
            var readyProcess = activity.ReadyingProcess?.ImageName ?? "Unknown";
            var readyStack = StackStringList(activity.ReadyThreadStack);
            List<string> path = [activity.Process.ImageName, ..newStack, $"READIED BY {readyProcess}", ..readyStack, ""];
            var cpuUsage = activity.Duration.Nanoseconds;
            var waits = activity.WaitingDuration?.Nanoseconds ?? 0;
            var ready = activity.ReadyDuration?.Nanoseconds ?? 0;
            Table.Add(path, 1, cpuUsage, ready, waits);
        }
    }
}

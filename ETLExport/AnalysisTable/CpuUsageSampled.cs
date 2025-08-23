using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Cpu;
using Microsoft.Windows.EventTracing.Processes;

namespace ETLExport;

class CpuUsageSampled : AnalysisTableBase
{
    public override string TableName => "CPUUsageSampled";
    public override string[] ColumnNames => ["Count", "Weight"];

    private IPendingResult<ICpuSampleDataSource>? cpuSampleData;

    public override void UseTrace(ITraceProcessor trace)
    {
        cpuSampleData = trace.UseCpuSamplingData();
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var sample in cpuSampleData!.Result.Samples)
        {
            if (!processes.Contains(sample.Process))
                continue;
            if (sample.Timestamp.Nanoseconds < startTime || sample.Timestamp.Nanoseconds >= endTime)
                continue;

            List<string> path = [sample.Process.ImageName, ..StackStringList(sample.Stack), ""];
            Table.Add(path, 1, sample.Weight.Nanoseconds);
        }
    }
}

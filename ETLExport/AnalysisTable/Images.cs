using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Processes;

namespace ETLExport;

class Images : AnalysisTableBase
{
    public override string[] ColumnNames => ["Count", "Size"];

    public override void UseTrace(ITraceProcessor trace)
    {
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var process in processes)
        {
            foreach (var image in process.Images)
            {
                var loadTime = image.LoadTime?.Nanoseconds ?? 0;
                var unloadTime = image.UnloadTime?.Nanoseconds ?? long.MaxValue;
                var size = ImpactingSize(image.Size.Bytes, loadTime, unloadTime, startTime, endTime);
                if (size != 0)
                {
                    var stack = StackStringList(image.LoadStack);
                    List<string> path = [process.ImageName, image.FileName, ..stack, ""];
                    Table.Add(path, 1, size);
                }
            }
        }
    }
}

using System.Collections.Generic;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Processes;

public class Images : AnalysisTableBase
{
    public override string TableName => "Images";
    public override string[] ColumnNames => new[] { "Count", "Size" };

    public override void UseTrace(ITraceProcessor trace)
    {
    }

    public override void Process(IProcess[] processes, long startTime, long endTime)
    {
        foreach (var process in processes)
        {
            foreach (var image in process.Images)
            {
                var loadTime = image.LoadTime?.Nanoseconds ?? long.MaxValue;
                var unloadTime = image.UnloadTime?.Nanoseconds ?? long.MaxValue;
                var size = ImpactingSize(image.Size.Bytes, loadTime, unloadTime, startTime, endTime);
                if (size != 0)
                {
                    var stack = StackStringList(image.LoadStack);
                    var path = new List<string> { process.ImageName, image.FileName };
                    path.AddRange(stack);
                    path.Add("");
                    Table.Add(path, 1, size);
                }
            }
        }
    }
}

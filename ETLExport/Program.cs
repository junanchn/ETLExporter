using System;
using Microsoft.Windows.EventTracing;

class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("ETLExport <ETLPath>");
            return;
        }
        using (var trace = TraceProcessor.Create(args[0]))
        {
            var heapParser = new HeapEventParser(trace);
            trace.Process();
        }
    }
}

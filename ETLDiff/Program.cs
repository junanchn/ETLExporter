using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("Usage: ETLDiff <output.json> <test.json> <base.json>");
            return;
        }

        try
        {
            if (!File.Exists(args[1]))
                throw new FileNotFoundException($"Test file not found: {args[1]}");
            if (!File.Exists(args[2]))
                throw new FileNotFoundException($"Base file not found: {args[2]}");

            var testTable = TreeTable.ImportFromJson(args[1]);
            Console.WriteLine($"Loaded: {args[1]}");

            var baseTable = TreeTable.ImportFromJson(args[2]);
            Console.WriteLine($"Loaded: {args[2]}");

            var diffTable = testTable.CreateDiff(baseTable);

            diffTable.ExportToJson(args[0]);
            Console.WriteLine($"Output: {args[0]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}

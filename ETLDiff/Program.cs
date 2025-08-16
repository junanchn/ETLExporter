using System;

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
            var testTable = TreeTable.ImportFromJson(args[1]);
            var baseTable = TreeTable.ImportFromJson(args[2]);
            var diffTable = testTable.CreateDiff(baseTable);
            diffTable.ExportToJson(args[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

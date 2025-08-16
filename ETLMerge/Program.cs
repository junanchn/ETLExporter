using System;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: ETLMerge <output.json> <input1.json> <input2.json> [...]");
            return;
        }

        try
        {
            var result = TreeTable.ImportFromJson(args[1]);
            for (int i = 2; i < args.Length; i++)
            {
                result.Add(TreeTable.ImportFromJson(args[i]));
                Console.WriteLine($"Merged file: {args[i]}");
            }
            result.ExportToJson(args[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

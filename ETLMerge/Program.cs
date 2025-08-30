namespace ETLMerge;

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
            for (int i = 1; i < args.Length; i++)
            {
                if (!File.Exists(args[i]))
                    throw new FileNotFoundException($"Input file not found: {args[i]}");
            }

            var mergedTable = TreeTable.ImportFromJson(args[1]);
            Console.WriteLine($"Merged: {args[1]}");

            for (int i = 2; i < args.Length; i++)
            {
                var table = TreeTable.ImportFromJson(args[i]);
                mergedTable.Add(table);
                Console.WriteLine($"Merged: {args[i]}");
            }

            mergedTable.ExportToJson(args[0]);
            Console.WriteLine($"Output: {args[0]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Stack:");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}

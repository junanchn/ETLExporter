namespace ETLDiff;

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
            var outputPath = args[0];
            var testPath = args[1];
            var basePath = args[2];

            if (!File.Exists(testPath))
                throw new FileNotFoundException($"Test file not found: {testPath}");
            if (!File.Exists(basePath))
                throw new FileNotFoundException($"Base file not found: {basePath}");

            var testTable = TreeTable.ImportFromJson(testPath);
            Console.WriteLine($"Loaded: {testPath}");

            var baseTable = TreeTable.ImportFromJson(basePath);
            Console.WriteLine($"Loaded: {basePath}");

            var diffTable = testTable.CreateDiff(baseTable);

            diffTable.ExportToJson(outputPath);
            Console.WriteLine($"Output: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}

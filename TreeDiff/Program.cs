namespace TreeDiff;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var (outputPath, basePath, testPath) = ParseArguments(args);

            var baseTable = TreeTable.ImportFromJson(basePath);
            Console.WriteLine($"Loaded: {basePath}");

            var testTable = TreeTable.ImportFromJson(testPath);
            Console.WriteLine($"Loaded: {testPath}");

            var diffTable = testTable.CreateDiff(baseTable);

            diffTable.ExportToJson(outputPath);
            Console.WriteLine($"Output: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Stack:");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static (string outputPath, string basePath, string testPath) ParseArguments(string[] args)
    {
        if (args.Length != 2 && args.Length != 4)
        {
            Console.WriteLine("Usage: TreeDiff <base.json> <test.json> [--output <output.json>]");
            Environment.Exit(0);
        }

        string? outputPath = null;
        string? basePath = null;
        string? testPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length)
                outputPath = args[++i];
            else if (basePath == null)
                basePath = args[i];
            else if (testPath == null)
                testPath = args[i];
            else
                Console.WriteLine($"Too many arguments:{args[i]}");
        }

        if (!File.Exists(basePath))
            throw new FileNotFoundException($"Base file not found: {basePath}");
        if (!File.Exists(testPath))
            throw new FileNotFoundException($"Test file not found: {testPath}");

        if (outputPath == null)
        {
            var baseName = Path.GetFileNameWithoutExtension(basePath);
            var testName = Path.GetFileNameWithoutExtension(testPath);
            outputPath = Path.Combine(testPath, $"{baseName}.CompareWith.{testName}.json");
        }

        return (outputPath, basePath, testPath);
    }
}

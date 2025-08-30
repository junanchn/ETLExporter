namespace TreeMerge;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var (outputPath, inputFiles) = ParseArguments(args);

            TreeTable? merged = null;
            foreach (var file in inputFiles)
            {
                var table = TreeTable.ImportFromJson(file);
                if (merged == null)
                    merged = table;
                else
                    merged.Add(table);
                Console.WriteLine($"Merged: {file}");
            }

            merged!.ExportToJson(outputPath);
            Console.WriteLine($"Output: {outputPath} ({inputFiles.Count} files)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Stack:");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static (string outputPath, List<string> inputFiles) ParseArguments(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: TreeMerge <input1.json|pattern> [input2.json|pattern ...] [--output <output.json>]");
            Environment.Exit(0);
        }

        string? outputPath = null;
        var inputFiles = new List<string>();
        var seen = new HashSet<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length)
            {
                outputPath = Path.GetFullPath(args[++i]);
            }
            else if (args[i].Contains('*') || args[i].Contains('?'))
            {
                var dir = Path.GetDirectoryName(args[i]) ?? ".";
                var pattern = Path.GetFileName(args[i]);
                foreach (var f in Directory.GetFiles(dir, pattern).OrderBy(f => f))
                {
                    if (seen.Add(Path.GetFullPath(f)))
                        inputFiles.Add(Path.GetFullPath(f));
                }
            }
            else if (seen.Add(Path.GetFullPath(args[i])))
            {
                inputFiles.Add(Path.GetFullPath(args[i]));
            }
        }

        if (inputFiles.Count == 0)
            throw new InvalidOperationException("No input files found");

        if (outputPath == null)
        {
            var dir = Path.GetDirectoryName(inputFiles[0]) ?? ".";
            for (int n = 1; ; n++)
                if (!File.Exists(outputPath = Path.Combine(dir, $"merge{n}.json"))) break;
        }

        return (outputPath, inputFiles);
    }
}

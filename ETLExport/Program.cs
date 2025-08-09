using System;

class Program
{
    private static int Main()
    {
        try
        {
            var table = new TreeTable("Column1", "Column2");
            table.Insert(new[] { "Root", "Child1" }, 1, 2);
            table.Insert(new[] { "Root", "Child2" }, 3, 4);
            table.ExportToJson("output.json");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

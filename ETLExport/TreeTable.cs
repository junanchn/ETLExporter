using System.Diagnostics;
using System.Text.Json;

public class TreeTable
{
    public class Node
    {
        public Dictionary<string, Node> Children = new(StringComparer.Ordinal);
        public long[]? Values;

        public void Add(long[] values)
        {
            Values ??= new long[values.Length];
            for (int i = 0; i < Values.Length && i < values.Length; i++)
                Values[i] += values[i];
        }
    }

    public string[] ColumnNames;
    public readonly Node Root;

    public TreeTable(params string[] columnNames)
    {
        ColumnNames = columnNames;
        Root = new Node();
        Root.Values = new long[columnNames.Length];
    }

    public void Add(IEnumerable<string> path, params long[] values)
    {
        Debug.Assert(values.Length == ColumnNames.Length);

        var current = Root;
        current.Add(values);

        foreach (var name in path)
        {
            current = current.Children.GetValueOrDefault(name) ?? (current.Children[name] = new Node());
            current.Add(values);
        }
    }

    public void Add(TreeTable other)
    {
        AddNode(Root, other.Root);
    }

    private void AddNode(Node node, Node other)
    {
        node.Add(other.Values!);
        foreach (var (key, value) in other.Children)
        {
            if (!node.Children.TryGetValue(key, out var child))
                child = node.Children[key] = new Node();
            AddNode(child, value);
        }
    }

    public TreeTable CreateDiff(TreeTable baseTable)
    {
        var diffColumns = ColumnNames.Select(c => c + "Test")
            .Concat(ColumnNames.Select(c => c + "Base"))
            .Concat(ColumnNames.Select(c => c + "Diff"))
            .ToArray();
        var diffTable = new TreeTable(diffColumns);

        CreateDiffNode(diffTable.Root, Root, baseTable.Root);
        return diffTable;
    }

    private void CreateDiffNode(Node diffNode, Node? testNode, Node? baseNode)
    {
        var testValues = testNode?.Values ?? new long[ColumnNames.Length];
        var baseValues = baseNode?.Values ?? new long[ColumnNames.Length];
        var diffValues = testValues
            .Concat(baseValues)
            .Concat(testValues.Zip(baseValues, (a, b) => a - b))
            .ToArray();
        diffNode.Values = diffValues;

        var allKeys = new HashSet<string>();
        if (testNode is not null)
            allKeys.UnionWith(testNode.Children.Keys);
        if (baseNode is not null)
            allKeys.UnionWith(baseNode.Children.Keys);

        foreach (var key in allKeys)
        {
            Node? testChild = null;
            Node? baseChild = null;
            testNode?.Children.TryGetValue(key, out testChild);
            baseNode?.Children.TryGetValue(key, out baseChild);

            var diffChild = diffNode.Children[key] = new Node();
            CreateDiffNode(diffChild, testChild, baseChild);
        }
    }

    public void ExportToJson(string filePath)
    {
        var directoryName = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (directoryName is not null)
            Directory.CreateDirectory(directoryName);

        using var stream = File.Create(filePath);
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        writer.WriteStartArray("columnNames");
        foreach (var name in ColumnNames)
            writer.WriteStringValue(name);
        writer.WriteEndArray();

        writer.WriteStartArray("treeData");
        foreach (var (key, value) in Root.Children)
            ExportNodeToJson(key, value, writer);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private void ExportNodeToJson(string name, Node node, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("n", name);
        if (node.Children.Count > 0)
        {
            writer.WriteStartArray("c");
            foreach (var (key, value) in node.Children)
                ExportNodeToJson(key, value, writer);
            writer.WriteEndArray();
        }
        else
        {
            for (int i = 0; i < ColumnNames.Length; i++)
                writer.WriteNumber(i.ToString(), node.Values![i]);
        }
        writer.WriteEndObject();
    }

    public static TreeTable ImportFromJson(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 4096 });

        var root = document.RootElement;
        var columnNames = root.GetProperty("columnNames")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();
        var table = new TreeTable(columnNames);
        var treeData = root.GetProperty("treeData");
        foreach (var element in treeData.EnumerateArray())
            table.ImportNodeFromJson(element, table.Root);
        return table;
    }

    private void ImportNodeFromJson(JsonElement element, Node parent)
    {
        var name = element.GetProperty("n").GetString() ?? string.Empty;
        var node = parent.Children[name] = new Node();
        if (element.TryGetProperty("c", out var children))
        {
            foreach (var child in children.EnumerateArray())
                ImportNodeFromJson(child, node);
        }
        else
        {
            var values = new long[ColumnNames.Length];
            for (int i = 0; i < ColumnNames.Length; i++)
                values[i] = element.GetProperty(i.ToString()).GetInt64();
            node.Values = values;
        }
        parent.Add(node.Values!);
    }

    public void RenameLeaves(string name)
    {
        RenameLeaves(Root, name);
    }

    private void RenameLeaves(Node node, string name)
    {
        if (node.Children.Count == 0)
            return;

        List<(string key, Node node)> leavesToRename = [];

        foreach (var (key, value) in node.Children)
        {
            if (value.Children.Count > 0)
                RenameLeaves(value, name);
            else
                leavesToRename.Add((key, value));
        }

        foreach (var (key, value) in leavesToRename)
        {
            node.Children.Remove(key);
            node.Children[name] = value;
        }
    }
}

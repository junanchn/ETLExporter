using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

public class TreeTable
{
    public class Node
    {
        public Dictionary<string, Node> Children = new Dictionary<string, Node>(StringComparer.Ordinal);
        public long[] Values;

        public void Add(long[] values)
        {
            if (Values == null)
                Values = new long[values.Length];
            for (int i = 0; i < Values.Length && i < values.Length; i++)
                Values[i] += values[i];
        }
    }

    public string[] Columns;
    public readonly Node Root;

    public TreeTable(params string[] columns)
    {
        Columns = columns;
        Root = new Node();
    }

    public void Insert(IEnumerable<string> path, params long[] values)
    {
        Debug.Assert(values.Length == Columns.Length);

        var current = Root;
        current.Add(values);

        foreach (var key in path)
        {
            if (!current.Children.TryGetValue(key, out var child))
                child = current.Children[key] = new Node();
            current = child;
            current.Add(values);
        }
    }

    public void ExportToJson(string filePath)
    {
        using (var stream = File.Create(filePath))
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var kvp in Root.Children)
                ExportNode(kvp.Key, kvp.Value, writer);
            writer.WriteEndArray();
        }
    }

    private void ExportNode(string name, Node node, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        
        if (node.Children.Count > 0)
        {
            writer.WriteStartArray("children");
            foreach (var kvp in node.Children)
                ExportNode(kvp.Key, kvp.Value, writer);
            writer.WriteEndArray();
        }
        else
        {
            for (int i = 0; i < Columns.Length; i++)
                writer.WriteNumber(Columns[i], node.Values[i]);
        }
        
        writer.WriteEndObject();
    }

    public static TreeTable ImportFromJson(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        using (var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 4096 }))
        {
            var table = new TreeTable();
            var root = document.RootElement;
            foreach (var child in root.EnumerateArray())
                table.ImportNode(child, table.Root);
            return table;
        }
    }

    private void ImportNode(JsonElement element, Node parent)
    {
        var name = element.GetProperty("name").GetString();
        var node = parent.Children[name] = new Node();
        if (element.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
                ImportNode(child, node);
        }
        else
        {
            if (Columns.Length == 0)
            {
                var columns = new List<string>();
                foreach (var prop in element.EnumerateObject())
                    if (prop.Name != "name" && prop.Name != "children" && prop.Value.ValueKind == JsonValueKind.Number)
                        columns.Add(prop.Name);
                Columns = columns.ToArray();
            }
            var values = new long[Columns.Length];
            for (int i = 0; i < Columns.Length; i++)
                values[i] = element.GetProperty(Columns[i]).GetInt64();
            node.Values = values;
        }
        parent.Add(node.Values);
    }
}

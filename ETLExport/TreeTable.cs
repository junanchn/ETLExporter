using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    public string[] ColumnNames;
    public readonly Node Root;

    public TreeTable(params string[] columnNames)
    {
        ColumnNames = columnNames;
        Root = new Node();
    }

    public void Add(IEnumerable<string> path, params long[] values)
    {
        Debug.Assert(values.Length == ColumnNames.Length);

        var current = Root;
        current.Add(values);

        foreach (var name in path)
        {
            if (!current.Children.TryGetValue(name, out var child))
                child = current.Children[name] = new Node();
            current = child;
            current.Add(values);
        }
    }

    public void ExportToJson(string filePath)
    {
        using (var stream = File.Create(filePath))
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            writer.WriteStartArray("columnNames");
            foreach (var name in ColumnNames)
                writer.WriteStringValue(name);
            writer.WriteEndArray();

            writer.WriteStartArray("treeData");
            foreach (var kvp in Root.Children)
                ExportNodeToJson(kvp.Key, kvp.Value, writer);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }

    private void ExportNodeToJson(string name, Node node, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("n", name);
        if (node.Children.Count > 0)
        {
            writer.WriteStartArray("c");
            foreach (var kvp in node.Children)
                ExportNodeToJson(kvp.Key, kvp.Value, writer);
            writer.WriteEndArray();
        }
        else
        {
            for (int i = 0; i < ColumnNames.Length; i++)
                writer.WriteNumber(i.ToString(), node.Values[i]);
        }
        writer.WriteEndObject();
    }

    public static TreeTable ImportFromJson(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        using (var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 4096 }))
        {
            var root = document.RootElement;
            var columnNames = root.GetProperty("columnNames")
                .EnumerateArray()
                .Select(e => e.GetString())
                .ToArray();
            var table = new TreeTable(columnNames);
            var treeData = root.GetProperty("treeData");
            foreach (var element in treeData.EnumerateArray())
                table.ImportNodeFromJson(element, table.Root);
            return table;
        }
    }

    private void ImportNodeFromJson(JsonElement element, Node parent)
    {
        var name = element.GetProperty("n").GetString();
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
        parent.Add(node.Values);
    }
}

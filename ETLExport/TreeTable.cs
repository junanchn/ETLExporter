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

    public void Add(TreeTable other)
    {
        AddNode(Root, other.Root);
    }

    private void AddNode(Node node, Node other)
    {
        node.Add(other.Values);
        foreach (var kvp in other.Children)
        {
            if (!node.Children.TryGetValue(kvp.Key, out var child))
                child = node.Children[kvp.Key] = new Node();
            AddNode(child, kvp.Value);
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

    private void CreateDiffNode(Node diffNode, Node testNode, Node baseNode)
    {
        var testValues = testNode?.Values ?? new long[ColumnNames.Length];
        var baseValues = baseNode?.Values ?? new long[ColumnNames.Length];
        var diffValues = testValues
            .Concat(baseValues)
            .Concat(testValues.Zip(baseValues, (a, b) => a - b))
            .ToArray();
        diffNode.Values = diffValues;

        var allKeys = new HashSet<string>();
        if (testNode != null)
            allKeys.UnionWith(testNode.Children.Keys);
        if (baseNode != null)
            allKeys.UnionWith(baseNode.Children.Keys);

        foreach (var key in allKeys)
        {
            Node testChild = null;
            Node baseChild = null;
            testNode?.Children.TryGetValue(key, out testChild);
            baseNode?.Children.TryGetValue(key, out baseChild);

            var diffChild = diffNode.Children[key] = new Node();
            CreateDiffNode(diffChild, testChild, baseChild);
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

    public void RenameLeaves(string name)
    {
        RenameLeaves(Root, name);
    }

    private void RenameLeaves(Node node, string name)
    {
        if (node.Children.Count == 0)
            return;

        var leavesToRename = new List<KeyValuePair<string, Node>>();

        foreach (var kvp in node.Children)
        {
            if (kvp.Value.Children.Count > 0)
                RenameLeaves(kvp.Value, name);
            else
                leavesToRename.Add(kvp);
        }

        foreach (var kvp in leavesToRename)
        {
            node.Children.Remove(kvp.Key);
            node.Children[name] = kvp.Value;
        }
    }
}

// 配置导入导出。
// 借鉴 ClassIsland 的 ExcelImport/ExportWindow 思路(配置可备份迁移、批量部署),
// 但这里只做 JSON 往返, 并对可能含本机信息的键做脱敏。
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartSideBAR.Core.Configuration;

public static class ConfigTransfer
{
    /// <summary>导出时会被抹掉的键(含本机路径/账号类信息)。</summary>
    public static readonly string[] SensitiveKeys =
    [
        "capture.dir",
        "recorder.dir",
        "policy.",
    ];

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>把配置根节点导出为 JSON 文本(自动脱敏)。</summary>
    public static string Export(JsonNode root, bool desensitize = true)
    {
        var clone = root.DeepClone();
        if (desensitize) Desensitize(clone);
        return clone.ToJsonString(Options);
    }

    /// <summary>解析导入的 JSON; 失败时返回 null 而不抛异常。</summary>
    public static JsonNode? Import(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var node = JsonNode.Parse(json);
            return node as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 把配置树摊平成 (点分路径, 叶子值)。导入时逐键走 ConfigService.Set, 由白名单决定能否落地。
    /// </summary>
    public static IEnumerable<(string Path, JsonNode? Value)> EnumerateLeaves(
        JsonNode root, string prefix = "")
    {
        if (root is not JsonObject obj)
        {
            if (!string.IsNullOrEmpty(prefix)) yield return (prefix, root);
            yield break;
        }

        foreach (var kv in obj)
        {
            var path = string.IsNullOrEmpty(prefix) ? kv.Key : $"{prefix}.{kv.Key}";
            if (kv.Value is JsonObject child && child.Count > 0)
            {
                foreach (var leaf in EnumerateLeaves(child, path)) yield return leaf;
            }
            else
            {
                yield return (path, kv.Value);
            }
        }
    }

    private static void Desensitize(JsonNode root)
    {
        foreach (var key in SensitiveKeys)
        {
            if (key.EndsWith(".", StringComparison.Ordinal))
            {
                RemovePrefix(root, key);
                continue;
            }
            SetByPath(root, key, null);
        }
    }

    private static void RemovePrefix(JsonNode root, string prefix)
    {
        if (root is not JsonObject obj) return;
        var name = prefix.TrimEnd('.');
        var tail = name.Split('.').Last();
        obj.Remove(tail);
    }

    private static bool SetByPath(JsonNode root, string dotPath, JsonNode? value)
    {
        var parts = dotPath.Split('.');
        var node = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (node is not JsonObject o) return false;
            if (o[parts[i]] is not JsonNode next) return false;
            node = next;
        }
        if (node is not JsonObject leaf) return false;
        leaf[parts[^1]] = value;
        return true;
    }
}

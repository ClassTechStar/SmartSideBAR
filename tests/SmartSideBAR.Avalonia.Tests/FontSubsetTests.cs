// 字体子集门禁 (方案 §4 资源占用 P0): 全量 MiSans 7.8MB 曾被复制回仓库。
// 子集化后应 <2MB 且含 CJK 字形 (中文 UI 渲染依赖)。
// 回退方式只有一种: 把全量字体复制回 Assets/Fonts —— 体积测试当场拦住。
using System.IO;

namespace SmartSideBAR.Avalonia.Tests;

public class FontSubsetTests
{
    private const long MaxFontBytes = 2 * 1024 * 1024; // 2MB 红线 (方案 §4)

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void 源字体保持子集体积()
    {
        var font = Path.Combine(RepoRoot, "src", "SmartSideBAR.Avalonia",
            "Assets", "Fonts", "MiSans-Normal.ttf");
        Assert.True(File.Exists(font), $"未找到源字体: {font}");
        var size = new FileInfo(font).Length;
        Assert.True(size <= MaxFontBytes,
            $"MiSans-Normal.ttf 达 {size / 1048576.0:F1}MB —— 疑似全量字体回潮, 请跑 py -3 scripts/gen_font_subset.py");
    }

    [Fact]
    public void 子集字体仍含CJK与族名()
    {
        // 防另一个方向的回退: 子集脚本 bug 曾产出只剩拉丁字形的"假子集"
        // (命令行 53KB 参数被 Windows 32K 上限截断), 中文渲染全变方框
        var font = Path.Combine(RepoRoot, "src", "SmartSideBAR.Avalonia",
            "Assets", "Fonts", "MiSans-Normal.ttf");
        Assert.True(File.Exists(font));
        var cmap = FontToolsLite.Open(font);
        Assert.Contains(0x4E2Du, cmap.Keys);          // 「中」
        Assert.Equal("MiSans Normal", FontToolsLite.FamilyName(font));
    }
}

/// <summary>fontTools 不在 .NET 侧 —— 这里用最小 TTF 解析读 cmap 与 name 表。</summary>
internal static class FontToolsLite
{
    public static Dictionary<uint, uint> Open(string path) // cmap: codepoint -> glyph index
    {
        var bytes = File.ReadAllBytes(path);
        var result = new Dictionary<uint, uint>();
        var numTables = ReadU16(bytes, 4);
        for (var i = 0; i < numTables; i++)
        {
            var off = 12 + i * 16;
            var tag = System.Text.Encoding.ASCII.GetString(bytes, off, 4);
            if (tag != "cmap") continue;
            var cmapOff = (int)ReadU32(bytes, off + 8);
            var subTables = ReadU16(bytes, (int)(cmapOff + 2));
            for (var s = 0; s < subTables; s++)
            {
                var rec = cmapOff + 4 + s * 8;
                var platform = ReadU16(bytes, (int)rec);
                var subOff = (int)(cmapOff + ReadU32(bytes, (int)(rec + 4)));
                var isUnicode = platform == 3 || platform == 0;
                if (!isUnicode) continue;
                var format = ReadU16(bytes, (int)subOff);
                if (format == 4) { ParseCmap4(bytes, subOff, result); }
                else if (format == 12) { ParseCmap12(bytes, subOff, result); }
            }
            if (result.Count > 0) break;
        }
        return result;
    }

    public static string FamilyName(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var numTables = ReadU16(bytes, 4);
        for (var i = 0; i < numTables; i++)
        {
            var off = 12 + i * 16;
            var tag = System.Text.Encoding.ASCII.GetString(bytes, off, 4);
            if (tag != "name") continue;
            var nameOff = (int)ReadU32(bytes, off + 8);
            var count = ReadU16(bytes, (int)(nameOff + 2));
            var storage = ReadU16(bytes, (int)(nameOff + 4));
            for (var r = 0; r < count; r++)
            {
                var rec = nameOff + 6 + r * 12;
                var nameId = ReadU16(bytes, (int)(rec + 6));
                if (nameId != 1) continue; // family name
                var platform = ReadU16(bytes, (int)rec);
                var len = ReadU16(bytes, (int)(rec + 8));
                var strOff = nameOff + storage + ReadU16(bytes, (int)(rec + 10));
                if (platform == 3) // Windows UTF-16BE
                {
                    var chars = new byte[len];
                    Array.Copy(bytes, strOff, chars, 0, len);
                    for (var b = 0; b < len; b += 2) (chars[b], chars[b + 1]) = (chars[b + 1], chars[b]);
                    return System.Text.Encoding.Unicode.GetString(chars);
                }
                return System.Text.Encoding.ASCII.GetString(bytes, strOff, len);
            }
        }
        return "";
    }

    private static ushort ReadU16(byte[] b, int off) => (ushort)((b[off] << 8) | b[off + 1]);
    private static uint ReadU32(byte[] b, int off) =>
        ((uint)b[off] << 24) | ((uint)b[off + 1] << 16) | ((uint)b[off + 2] << 8) | b[off + 3];

    private static void ParseCmap4(byte[] b, int subOff, Dictionary<uint, uint> into)
    {
        var segCountX2 = ReadU16(b, subOff + 6);
        var segCount = segCountX2 / 2;
        var endOff = subOff + 14;
        var startOff = endOff + segCountX2 + 2;
        var deltaOff = startOff + segCountX2;
        var rangeOff = deltaOff + segCountX2;
        for (var i = 0; i < segCount; i++)
        {
            var end = ReadU16(b, endOff + i * 2);
            var start = ReadU16(b, startOff + i * 2);
            var delta = ReadU16(b, deltaOff + i * 2);
            var range = ReadU16(b, rangeOff + i * 2);
            for (var cp = start; cp <= end && cp != 0xFFFF; cp++)
            {
                uint glyph;
                if (range == 0) glyph = (uint)(cp + delta) & 0xFFFF;
                else
                {
                    var idx = rangeOff + range + (cp - start) * 2;
                    if (idx + 1 >= b.Length) continue;
                    var g = ReadU16(b, idx);
                    if (g == 0) continue;
                    glyph = (uint)(g + delta) & 0xFFFF;
                }
                if (glyph != 0) into[cp] = glyph;
            }
        }
    }

    private static void ParseCmap12(byte[] b, int subOff, Dictionary<uint, uint> into)
    {
        var nGroups = ReadU32(b, subOff + 12);
        for (var i = 0; i < nGroups; i++)
        {
            var rec = subOff + 16 + i * 12;
            var start = ReadU32(b, (int)rec);
            var end = ReadU32(b, (int)(rec + 4));
            var startGlyph = ReadU32(b, (int)(rec + 8));
            for (var cp = start; cp <= end; cp++) into[cp] = startGlyph + (cp - start);
        }
    }
}

# -*- coding: utf-8 -*-
"""MiSans 字体子集化 (方案 §4 资源占用 P0, 全方案性价比之最)。

7.9MB 全量 TTF → ~1MB 子集 TTF。字符集 = GB2312 全部 6763 汉字 +
ASCII 可打印 + 源码字符串实际用字 (增量保险)。CI 门禁: 字体 >2MB 即红
(AuditFix/FontSubsetTests), 防止有人把全量字体复制回来。

用法: py -3 scripts/gen_font_subset.py
"""
import glob
import io
import os
import string
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
FONT = os.path.join(ROOT, "src", "SmartSideBAR.Avalonia", "Assets", "Fonts", "MiSans-Normal.ttf")


def source_chars():
    """从 src/ 的 UI 字符串里收集实际用字 (增量保险, 防漏字)。"""
    chars = set()
    patterns = []
    for ext in ("cs", "axaml"):
        patterns += glob.glob(os.path.join(ROOT, "src", "SmartSideBAR.Avalonia", "**", f"*.{ext}"), recursive=True)
        patterns += glob.glob(os.path.join(ROOT, "src", "SmartSideBAR.Windows", "**", f"*.{ext}"), recursive=True)
    for path in patterns:
        if os.sep + "obj" + os.sep in path or os.sep + "bin" + os.sep in path:
            continue
        try:
            with io.open(path, encoding="utf-8", errors="ignore") as f:
                chars.update(f.read())
        except OSError:
            pass
    return chars


def gb2312_chars():
    """GB2312 全集 (6763 汉字 + 符号)。"""
    result = set()
    for hi in range(0xA1, 0xF8):
        for lo in range(0xA1, 0xFF):
            try:
                ch = bytes([hi, lo]).decode("gb2312")
                result.add(ch)
            except UnicodeDecodeError:
                pass
    return result


def main():
    from fontTools import subset
    from fontTools.ttLib import TTFont

    keep = set(string.printable)
    keep |= gb2312_chars()
    keep |= source_chars()
    keep |= set("×○✓▸▾•—…；：（）「」、。！？·")
    unicodes = sorted({ord(c) for c in keep if ord(c) >= 0x20})

    before = os.path.getsize(FONT)
    backup = FONT + ".bak"
    with io.open(FONT, "rb") as src, io.open(backup, "wb") as dst:
        dst.write(src.read())

    # 必须走 Options/Subsetter API —— --unicodes 命令行传 7555 个码点的 53KB
    # 参数会被 Windows 32K 命令行上限截断, 产出只剩拉丁字形的"假子集" (实测踩坑)
    try:
        font = TTFont(FONT)
        options = subset.Options()
        options.layout_features = ["*"]  # 保留全部 OpenType 特性 (字距/连字)
        options.glyph_names = True
        options.notdef_outline = True
        options.recommended_glyphs = True
        options.name_IDs = ["*"]         # 保留全部 name 记录 —— 族名 "MiSans Normal"
        options.name_legacy = True       # 丢了 Avalonia 按族名找不到字体
        subsetter = subset.Subsetter(options)
        subsetter.populate(unicodes=unicodes)
        subsetter.subset(font)
        font.save(FONT)
    except Exception:
        # 子集化失败 → 还原全量字体, 不留半成品
        try:
            os.replace(backup, FONT)
        except OSError:
            pass
        raise
    finally:
        try:
            os.remove(backup)
        except OSError:
            pass
    after = os.path.getsize(FONT)
    print(f"OK: {before/1048576:.1f}MB -> {after/1048576:.2f}MB ({len(unicodes)} codepoints)")


if __name__ == "__main__":
    sys.exit(main())

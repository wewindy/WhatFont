using System.Buffers.Binary;

namespace WhatFont.Fonts;

public static class NameTableParser
{
    private const int NameIdFamily = 1;
    private const int NameIdPostScript = 6;
    private const int NameIdTypographicFamily = 16;

    private const uint TagTtcf = 0x74746366;   // 'ttcf'
    private const uint TagTrue = 0x00010000;   // 0x00010000
    private const uint TagOtto = 0x4F54544F;   // 'OTTO'
    private const uint TagTrueMac = 0x74727565; // 'true'
    private const uint TagName = 0x6E616D65;   // 'name'

    private const string MacRomanHigh =
        "\u00C4\u00C5\u00C7\u00C9\u00D1\u00D6\u00DC\u00E1\u00E0\u00E2\u00E4\u00E3\u00E5\u00E7\u00E9\u00E8" +
        "\u00EA\u00EB\u00ED\u00EC\u00EE\u00EF\u00F1\u00F3\u00F2\u00F4\u00F6\u00F5\u00FA\u00F9\u00FB\u00FC" +
        "\u2020\u00B0\u00A2\u00A3\u00A7\u2022\u00B6\u00DF\u00AE\u00A9\u2122\u00B4\u00A8\u2260\u00C6\u00D8" +
        "\u221E\u00B1\u2264\u2265\u00A5\u00B5\u2202\u2211\u220F\u03C0\u222B\u00AA\u00BA\u03A9\u00E6\u00F8" +
        "\u00BF\u00A1\u00AC\u221A\u0192\u2248\u2206\u00AB\u00BB\u2026\u00A0\u00C0\u00C3\u00D5\u0152\u0153" +
        "\u2013\u2014\u201C\u201D\u2018\u2019\u00F7\u25CA\u00FF\u0178\u2044\u20AC\u2039\u203A\uFB01\uFB02" +
        "\u2021\u00B7\u201A\u201E\u2030\u00C2\u00CA\u00C1\u00CB\u00C8\u00CD\u00CE\u00CF\u00CC\u00D3\u00D4" +
        "\uF8FF\u00D2\u00DA\u00DB\u00D9\u0131\u02C6\u02DC\u00AF\u02D8\u02D9\u02DA\u00B8\u02DD\u02DB\u02C7";

    private sealed record NameRecord(int NameId, int PlatformId, int EncodingId, string Text);

    public static bool TryParse(string filePath, out string familyName, out string postScriptName)
    {
        familyName = string.Empty;
        postScriptName = string.Empty;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var r = new BinaryReader(fs);

            var faceOffsets = new List<long>();
            uint tag = r.ReadUInt32BE();

            if (tag == TagTtcf)
            {
                r.ReadUInt32BE();
                int faceCount = checked((int)r.ReadUInt32BE());
                if (faceCount <= 0 || faceCount > 64)
                    return false;

                for (int i = 0; i < faceCount; i++)
                    faceOffsets.Add(r.ReadUInt32BE());
            }
            else if (tag is TagTrue or TagOtto or TagTrueMac)
            {
                faceOffsets.Add(0);
            }
            else
            {
                return false;
            }

            foreach (var faceOffset in faceOffsets)
            {
                var names = ReadFaceNames(r, fs, faceOffset);
                if (names.Count == 0)
                    continue;

                familyName = SelectBest(names, NameIdTypographicFamily)
                             ?? SelectBest(names, NameIdFamily)
                             ?? string.Empty;
                postScriptName = SelectBest(names, NameIdPostScript) ?? familyName;

                if (familyName.Length > 0)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static List<NameRecord> ReadFaceNames(BinaryReader r, FileStream fs, long faceOffset)
    {
        var names = new List<NameRecord>();
        if (faceOffset < 0 || faceOffset > fs.Length - 12)
            return names;

        fs.Position = faceOffset + 4;

        int numTables = checked((int)r.ReadUInt16BE());
        if (numTables <= 0 || faceOffset + 12L + numTables * 16L > fs.Length)
            return names;

        r.ReadUInt16BE(); // searchRange
        r.ReadUInt16BE(); // entrySelector
        r.ReadUInt16BE(); // rangeShift

        long nameTableOffset = -1;
        long nameTableLength = -1;
        for (int i = 0; i < numTables; i++)
        {
            uint tableTag = r.ReadUInt32BE();
            r.ReadUInt32BE(); // checksum
            uint offset = r.ReadUInt32BE();
            uint length = r.ReadUInt32BE();
            if (tableTag == TagName)
            {
                nameTableOffset = offset;
                nameTableLength = length;
                break;
            }
        }

        if (nameTableOffset < 0)
            return names;

        // Table offsets in both standalone fonts and TTC files are relative to
        // the beginning of the file, not to the face's offset table.
        if (nameTableOffset > fs.Length || nameTableLength > fs.Length - nameTableOffset)
            return names;

        fs.Position = nameTableOffset;
        var buffer = r.ReadBytes(checked((int)Math.Min(nameTableLength, int.MaxValue)));
        if (buffer.Length < 6)
            return names;

        int pos = 0;
        ReadUInt16BE(buffer, ref pos); // format
        int count = checked((int)ReadUInt16BE(buffer, ref pos));
        int stringOffset = checked((int)ReadUInt16BE(buffer, ref pos));

        for (int i = 0; i < count; i++)
        {
            if (pos + 12 > buffer.Length)
                break;
            int platform = checked((int)ReadUInt16BE(buffer, ref pos));
            int encoding = checked((int)ReadUInt16BE(buffer, ref pos));
            ReadUInt16BE(buffer, ref pos); // language
            int nameId = checked((int)ReadUInt16BE(buffer, ref pos));
            int length = checked((int)ReadUInt16BE(buffer, ref pos));
            int offset = checked((int)ReadUInt16BE(buffer, ref pos));

            if (stringOffset + offset + length > buffer.Length)
                continue;

            string text = string.Concat(
                DecodeString(buffer, stringOffset + offset, length, platform, encoding)
                    .Where(character => !char.IsControl(character)))
                .Trim();
            if (text.Length > 0)
                names.Add(new NameRecord(nameId, platform, encoding, text));
        }

        return names;
    }

    private static string? SelectBest(List<NameRecord> names, int nameId)
    {
        NameRecord? best = null;
        int bestScore = -1;

        foreach (var record in names)
        {
            if (record.NameId != nameId)
                continue;

            int score = record.PlatformId switch
            {
                3 => record.EncodingId is 1 or 10 ? 100 : 90, // Windows
                0 => 80,                                      // Unicode
                1 => 60,                                      // Macintosh
                _ => 0,
            };

            if (score > bestScore)
            {
                bestScore = score;
                best = record;
            }
        }

        return best?.Text;
    }

    private static string DecodeString(byte[] buffer, int offset, int length, int platform, int encoding)
    {
        if (length <= 0)
            return string.Empty;

        if (platform == 1 && encoding == 0)
        {
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                byte b = buffer[offset + i];
                chars[i] = b < 0x80 ? (char)b : MacRomanHigh[b - 0x80];
            }
            return new string(chars);
        }

        if (platform is not (0 or 3))
            return string.Empty;

        var charCount = length / 2;
        var sb = new System.Text.StringBuilder(charCount);
        for (int i = 0; i + 1 < length; i += 2)
            sb.Append((char)BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(offset + i, 2)));
        return sb.ToString();
    }

    private static ushort ReadUInt16BE(byte[] buffer, ref int pos)
    {
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos, 2));
        pos += 2;
        return value;
    }
}

internal static class BinaryReaderExtensions
{
    public static uint ReadUInt32BE(this BinaryReader reader)
    {
        Span<byte> span = stackalloc byte[4];
        reader.ReadExactly(span);
        return BinaryPrimitives.ReadUInt32BigEndian(span);
    }

    public static ushort ReadUInt16BE(this BinaryReader reader)
    {
        Span<byte> span = stackalloc byte[2];
        reader.ReadExactly(span);
        return BinaryPrimitives.ReadUInt16BigEndian(span);
    }
}

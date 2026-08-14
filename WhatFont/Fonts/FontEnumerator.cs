using Microsoft.Win32;

namespace WhatFont.Fonts;

public static class FontEnumerator
{
    private static readonly string[] Extensions = [".ttf", ".otf", ".ttc"];

    public static IReadOnlyList<string> EnumerateFontFiles()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var windowsFonts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        var userFonts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");

        CollectFromRegistry(Registry.LocalMachine, set, windowsFonts);
        CollectFromRegistry(Registry.CurrentUser, set, windowsFonts);
        CollectFromDirectory(windowsFonts, set);
        CollectFromDirectory(userFonts, set);

        return set
            .Where(File.Exists)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectFromRegistry(RegistryKey root, HashSet<string> set, string fontsDir)
    {
        using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        if (key is null)
            return;

        foreach (var valueName in key.GetValueNames())
        {
            if (key.GetValue(valueName) is not string data || string.IsNullOrWhiteSpace(data))
                continue;

            var resolved = Path.IsPathRooted(data)
                ? data
                : Path.Combine(fontsDir, data);
            resolved = Environment.ExpandEnvironmentVariables(resolved);

            if (Extensions.Contains(Path.GetExtension(resolved), StringComparer.OrdinalIgnoreCase))
                set.Add(Path.GetFullPath(resolved));
        }
    }

    private static void CollectFromDirectory(string directory, HashSet<string> set)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                set.Add(file);
        }
    }
}

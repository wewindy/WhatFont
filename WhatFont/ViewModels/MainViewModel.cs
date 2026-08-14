using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WhatFont.Fonts;

namespace WhatFont.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<FontItem> VisibleFonts { get; } = [];

    [ObservableProperty]
    private string _statusText = "正在扫描字体…";

    private List<FontItem> _allFonts = [];

    public async Task LoadFontsAsync()
    {
        try
        {
            var items = await Task.Run(() =>
            {
                var files = FontEnumerator.EnumerateFontFiles();
                return files
                    .AsParallel()
                    .WithDegreeOfParallelism(Math.Max(2, Environment.ProcessorCount))
                    .Select(file =>
                    {
                        if (NameTableParser.TryParse(file, out var family, out var postScript))
                            return new FontItem
                            {
                                FamilyName = family,
                                PostScriptName = postScript,
                                FilePath = file,
                                PreviewImage = FontPreviewRenderer.Render(file),
                            };
                        return null;
                    })
                    .Where(item => item is not null)
                    .Cast<FontItem>()
                    .OrderBy(item => item.FamilyName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

            _allFonts = items;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusText = $"扫描失败: {ex.Message}";
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        VisibleFonts.Clear();

        IEnumerable<FontItem> filtered = _allFonts;
        if (query.Length > 0)
        {
            filtered = _allFonts.Where(item =>
                item.FamilyName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.PostScriptName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in filtered)
            VisibleFonts.Add(item);

        StatusText = VisibleFonts.Count == _allFonts.Count
            ? $"共 {_allFonts.Count} 款字体"
            : $"{VisibleFonts.Count} / {_allFonts.Count} 款字体";
    }
}

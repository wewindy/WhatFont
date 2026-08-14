using Avalonia.Media.Imaging;

namespace WhatFont.ViewModels;

public sealed class FontItem
{
    public required string FamilyName { get; init; }
    public required string PostScriptName { get; init; }
    public required string FilePath { get; init; }
    public required WriteableBitmap? PreviewImage { get; init; }
}

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace WhatFont.Fonts;

public static class FontPreviewRenderer
{
    public const string SampleText = "AaBbCc 0123 汉字";

    private const float FontSizeLogical = 22f;
    private const float Scale = 2f;

    public static WriteableBitmap? Render(string filePath)
    {
        try
        {
            using var typeface = SKTypeface.FromFile(filePath);
            return typeface is null ? null : RenderWithTypeface(typeface);
        }
        catch
        {
            return null;
        }
    }

    private static WriteableBitmap RenderWithTypeface(SKTypeface typeface)
    {
        using var font = new SKFont(typeface, FontSizeLogical * Scale);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x1A, 0x1C, 0x20),
        };

        font.MeasureText(SampleText, out var bounds);
        var metrics = font.Metrics;

        int padding = (int)(6 * Scale);
        int width = (int)Math.Ceiling(bounds.Width) + padding * 2;
        int height = (int)Math.Ceiling(metrics.Descent - metrics.Ascent) + padding * 2;

        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawText(
            SampleText,
            -bounds.Left + padding,
            -metrics.Ascent + padding,
            SKTextAlign.Left, font, paint);

        var writable = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var fb = writable.Lock())
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var pixmap = bitmap.PeekPixels();
            pixmap.ReadPixels(info, fb.Address, fb.RowBytes, 0, 0);
        }

        return writable;
    }
}

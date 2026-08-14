using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using WhatFont.ViewModels;

namespace WhatFont.Views;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _toastCts;
    private CancellationTokenSource? _scrollbarCts;

    public MainWindow()
    {
        InitializeComponent();

        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Mica,
            WindowTransparencyLevel.AcrylicBlur,
        ];
        TransparencyBackgroundFallback = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xFA));
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            await vm.LoadFontsAsync();
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnCopyFamilyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FontItem item })
            return;

        await CopyNameAsync(item.FamilyName, "Family");
    }

    private async void OnCopyPostScriptClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FontItem item })
            return;

        await CopyNameAsync(item.PostScriptName, "PS");
    }

    private async Task CopyNameAsync(string name, string kind)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is null)
            {
                ShowToast("剪贴板不可用");
                return;
            }

            await topLevel.Clipboard.SetTextAsync(name);
            ShowToast($"已复制 {kind}  {name}");
        }
        catch
        {
            ShowToast("复制失败");
        }
    }

    private async void OnFontListScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        scrollViewer.Classes.Add("scrolling");
        _scrollbarCts?.Cancel();
        var cts = new CancellationTokenSource();
        _scrollbarCts = cts;

        try
        {
            await Task.Delay(900, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_scrollbarCts, cts))
            {
                scrollViewer.Classes.Remove("scrolling");
                _scrollbarCts = null;
            }

            cts.Dispose();
        }
    }

    private async void ShowToast(string text)
    {
        _toastCts?.Cancel();
        var cts = new CancellationTokenSource();
        _toastCts = cts;

        try
        {
            ToastText.Text = text;
            Toast.IsVisible = true;
            await AnimateToast(0, 1, 120, cts.Token);
            await Task.Delay(1500, cts.Token);
            await AnimateToast(1, 0, 200, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_toastCts, cts))
            {
                Toast.IsVisible = false;
                _toastCts = null;
            }

            cts.Dispose();
        }
    }

    private Task AnimateToast(double from, double to, double durationMs, CancellationToken token)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(OpacityProperty, from) },
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(OpacityProperty, to) },
                },
            },
        };

        return animation.RunAsync(Toast, token);
    }
}

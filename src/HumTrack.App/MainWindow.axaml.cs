using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using HumTrack.App.ViewModels;

namespace HumTrack.App;

public partial class MainWindow : Window
{
    private static readonly string[] VideoPatterns = { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv" };
    private static readonly string[] AnyPatterns   = { "*.*" };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    // ── File Picker ───────────────────────────────────────────────────────────

    private async void OnLoadVideoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Video File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video Files") { Patterns = VideoPatterns },
                new FilePickerFileType("All Files")   { Patterns = AnyPatterns   }
            ]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
        {
            vm.LoadVideo(path);
            // Once the image renders, its Bounds will be correct — force a layout pass
            VideoImage.LayoutUpdated += OnImageLayoutUpdated;
        }
    }

    // ── Coordinate scaling ────────────────────────────────────────────────────

    /// <summary>
    /// Fired after every layout pass so we can keep the scale factor accurate
    /// for click → native-pixel mapping.
    /// </summary>
    private void OnImageLayoutUpdated(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var bounds = VideoImage.Bounds;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            vm.UpdateScale(bounds.Width, bounds.Height);

            // Sync the overlay canvas dimensions so bounding boxes line up
            OverlayCanvas.Width  = bounds.Width;
            OverlayCanvas.Height = bounds.Height;
        }
    }

    // ── Marker picking ────────────────────────────────────────────────────────

    /// <summary>
    /// User clicked on the video. Translate from Image display pixels → ViewModel.
    /// </summary>
    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Position relative to the Image element itself
        var pt = e.GetPosition(VideoImage);
        
        // The Image uses Stretch=Uniform so there may be letterbox bars.
        // We need the position relative to the actual rendered image content,
        // not the element bounds.
        var imgBounds = VideoImage.Bounds;
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0) return;

        // Clamp to image bounds
        double cx = Math.Clamp(pt.X, 0, imgBounds.Width);
        double cy = Math.Clamp(pt.Y, 0, imgBounds.Height);

        vm.AddMarkerAt(cx, cy);
    }
}
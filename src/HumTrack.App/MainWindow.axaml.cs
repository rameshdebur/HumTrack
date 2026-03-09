using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HumTrack.App.ViewModels;

namespace HumTrack.App;

public partial class MainWindow : Window
{
    private static readonly string[] VideoPatterns = { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv" };
    private static readonly string[] AnyPatterns   = { "*.*" };

    // Brushes reused for every overlay box
    private static readonly IBrush BoxFill   = new SolidColorBrush(Color.FromArgb(50, 0, 255, 200));
    private static readonly IBrush BoxStroke = new SolidColorBrush(Color.FromRgb(0, 255, 200));
    private static readonly IBrush LabelFg   = new SolidColorBrush(Color.FromRgb(0, 255, 200));

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // Watch the TrackingBoxes collection and redraw on every change
        if (DataContext is MainViewModel vm)
            vm.TrackingBoxes.CollectionChanged += OnTrackingBoxesChanged;
    }

    // ── Collection → Canvas drawing ───────────────────────────────────────────

    private void OnTrackingBoxesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildOverlayCanvas();
    }

    /// <summary>
    /// Clears and rebuilds all overlay shapes directly on OverlayCanvas.
    /// This is 100% reliable compared to ItemsControl Canvas positioning.
    /// </summary>
    private void RebuildOverlayCanvas()
    {
        OverlayCanvas.Children.Clear();

        if (DataContext is not MainViewModel vm) return;

        foreach (var box in vm.TrackingBoxes)
        {
            // Bounding rectangle
            var rect = new Rectangle
            {
                Width  = box.W,
                Height = box.H,
                Fill   = BoxFill,
                Stroke = BoxStroke,
                StrokeThickness = 2
            };
            Canvas.SetLeft(rect, box.X);
            Canvas.SetTop(rect,  box.Y);
            OverlayCanvas.Children.Add(rect);

            // Centre dot
            var dot = new Ellipse
            {
                Width  = 8,
                Height = 8,
                Fill   = BoxStroke
            };
            Canvas.SetLeft(dot, box.CX);
            Canvas.SetTop(dot,  box.CY);
            OverlayCanvas.Children.Add(dot);

            // Label
            var lbl = new TextBlock
            {
                Text       = box.Label,
                Foreground = LabelFg,
                FontSize   = 11
            };
            Canvas.SetLeft(lbl, box.X + 2);
            Canvas.SetTop(lbl,  box.LabelY);
            OverlayCanvas.Children.Add(lbl);
        }
    }

    // ── File Picker ───────────────────────────────────────────────────────────

    private async void OnLoadVideoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title          = "Select Video File",
            AllowMultiple  = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video Files") { Patterns = VideoPatterns },
                new FilePickerFileType("All Files")   { Patterns = AnyPatterns   }
            ]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
        {
            vm.LoadVideo(path);

            // Once the image has been laid out we can measure it and sync the overlay canvas
            VideoImage.LayoutUpdated += OnImageLayoutUpdated;
        }
    }

    // ── Scale sync ────────────────────────────────────────────────────────────

    private void OnImageLayoutUpdated(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var bounds = VideoImage.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        vm.UpdateScale(bounds.Width, bounds.Height);

        // Keep overlay canvas exactly on top of the image content area
        OverlayCanvas.Width  = bounds.Width;
        OverlayCanvas.Height = bounds.Height;
    }

    // ── Marker picking ────────────────────────────────────────────────────────

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // GetPosition(VideoImage) gives coords relative to the Image element
        var pt = e.GetPosition(VideoImage);
        var b  = VideoImage.Bounds;

        // Clamp to image element bounds
        double cx = Math.Clamp(pt.X, 0, b.Width  > 0 ? b.Width  : double.MaxValue);
        double cy = Math.Clamp(pt.Y, 0, b.Height > 0 ? b.Height : double.MaxValue);

        vm.AddMarkerAt(cx, cy);
    }
}
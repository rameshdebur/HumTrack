using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HumTrack.App.ViewModels;

namespace HumTrack.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Setup simple value converter for Play/Pause text
        var boolToPlayStr = new Avalonia.Data.Converters.FuncValueConverter<bool, string>(
            isPlaying => isPlaying ? "Pause" : "Play");
        
        this.Resources.Add("BoolToPlayStr", boolToPlayStr);
        
        // Attach DataContext
        DataContext = new MainViewModel();
    }

    private static readonly string[] VideoPatterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv" };
    private static readonly string[] AnyPatterns = new[] { "*.*" };

    private async void OnLoadVideoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Open local file dialog
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Video File",
            AllowMultiple = false,
            FileTypeFilter = new[] 
            {
                new FilePickerFileType("Video Files") { Patterns = VideoPatterns },
                new FilePickerFileType("All Files") { Patterns = AnyPatterns }
            }
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is string filePath)
        {
            vm.LoadVideo(filePath);
        }
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Get the click position relative to the Video canvas
        // (This correctly scales UI clicks back to native video resolution thanks to the Viewbox)
        var pos = e.GetPosition(sender as Control);
        
        // Pass X/Y to the ViewModel to register an intentional tracking marker
        vm.AddManualMarker((float)pos.X, (float)pos.Y);
    }
}
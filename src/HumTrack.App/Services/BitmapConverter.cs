using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace HumTrack.App.Services;

/// <summary>
/// Converts pure EmguCV Mat structures into rendering-ready Avalonia Bitmap classes safely without
/// leaking unmanaged memory buffers. 
/// Extremely fast memory pointer copy.
/// </summary>
public static class BitmapConverter
{
    /// <summary>
    /// Creates an Avalonia Bitmap representing the provided BGR Mat image.
    /// Caller is responsible for disposing the returning bitmap.
    /// </summary>
    public static unsafe WriteableBitmap? CreateWriteableBitmapFromMat(Mat mat)
    {
        if (mat.IsEmpty)
            return null;

        // Make sure it's 3-channel BGR 
        if (mat.NumberOfChannels != 3)
            throw new ArgumentException("Only 3-channel BGR images are supported for direct UI mapping.");

        // Define a fast unmanaged UI surface buffer
        var bitmap = new WriteableBitmap(
            new PixelSize(mat.Width, mat.Height),
            new Vector(96, 96), // Standard desktop DPI
            PixelFormat.Bgra8888, // UI elements use Bgra natively in Avalonia
            AlphaFormat.Opaque);  // Completely opaque

        using var locked = bitmap.Lock();

        // EmguCV uses pure BGR by default, but UI frameworks usually want BGRA
        // A direct memory copy isn't perfect unless channels match
        // So we do a sub-millisecond conversion out of the Mat directly into the Avalonia target buffer
        using var destMat = new Mat(mat.Size, DepthType.Cv8U, 4, locked.Address, locked.RowBytes);
        CvInvoke.CvtColor(mat, destMat, ColorConversion.Bgr2Bgra);

        return bitmap;
    }
}

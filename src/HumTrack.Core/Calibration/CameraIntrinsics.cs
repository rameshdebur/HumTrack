using Emgu.CV;

namespace HumTrack.Core.Calibration;

/// <summary>
/// Holds the intrinsic camera parameters required to correct lens distortion and
/// map 2D image coordinates to 3D rays.
/// Includes the generated Camera Matrix and Lens Distortion Coefficients.
/// </summary>
public sealed class CameraIntrinsics : IDisposable
{
    /// <summary>
    /// The 3x3 camera matrix (fx, fy, cx, cy) mapping focal lengths and principal points.
    /// </summary>
    public Mat CameraMatrix { get; }

    /// <summary>
    /// The distortion coefficients describing geometric lens distortion (k1, k2, p1, p2, k3).
    /// </summary>
    public Mat DistortionCoefficients { get; }

    /// <summary>Width of the image sensor pixel array used to calculate this.</summary>
    public int ImageWidth { get; }

    /// <summary>Height of the image sensor pixel array used to calculate this.</summary>
    public int ImageHeight { get; }

    /// <summary>Creates a new intrinsic mapping model.</summary>
    public CameraIntrinsics(Mat cameraMatrix, Mat distortionCoefficients, int imageWidth, int imageHeight)
    {
        CameraMatrix = cameraMatrix ?? throw new ArgumentNullException(nameof(cameraMatrix));
        DistortionCoefficients = distortionCoefficients ?? throw new ArgumentNullException(nameof(distortionCoefficients));
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
    }

    /// <summary>Calculates and caches the maps needed to unwarp frames in real-time.</summary>
    public void GenerateRectificationMaps(out Mat map1, out Mat map2)
    {
        map1 = new Mat();
        map2 = new Mat();
        CvInvoke.InitUndistortRectifyMap(
            CameraMatrix,
            DistortionCoefficients,
            null, // Monocular, no stereo rectification target needed
            CameraMatrix, // Target the same flat projection
            new System.Drawing.Size(ImageWidth, ImageHeight),
            Emgu.CV.CvEnum.DepthType.Cv32F,
            1, // channels
            map1,
            map2);
    }

    /// <summary>Releases the unmanaged matrices.</summary>
    public void Dispose()
    {
        CameraMatrix.Dispose();
        DistortionCoefficients.Dispose();
    }
}

using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using HumTrack.Core.Diagnostics;

namespace HumTrack.Core.Calibration;

/// <summary>
/// Engine responsible for calculating intrinsic camera parameters using a physical checkerboard.
/// Essential for establishing a mathematically sound relationship between curved pixels and reality.
/// </summary>
public sealed class CalibrationEngine
{
    private readonly Size _patternSize; // Number of inner corners, e.g., 9x6
    private readonly float _squareSizeMm; // Real-world dimensions of black/white squares
    private readonly Serilog.ILogger _log = HumTrackLogger.ForContext<CalibrationEngine>();

    /// <param name="cellsX">Number of inner corners horizontally (squares X - 1)</param>
    /// <param name="cellsY">Number of inner corners vertically (squares Y - 1)</param>
    /// <param name="squareSizeMm">Exact physical size of a square in millimeters</param>
    public CalibrationEngine(int cellsX, int cellsY, float squareSizeMm)
    {
        _patternSize = new Size(cellsX, cellsY);
        _squareSizeMm = squareSizeMm;
    }

    /// <summary>
    /// Computes the complex camera matrix and geometry mapping out of diverse checkerboard images.
    /// Needs at least 5 frames with varied angles (some close, some far, tilted, rotated).
    /// </summary>
    /// <param name="calibrationImages">A set of raw BGR capture frames containing the chessboard.</param>
    /// <returns>A rigid mathematical camera mapping model, or null if it fails.</returns>
    public CameraIntrinsics? Calibrate(IReadOnlyList<Mat> calibrationImages)
    {
        var objectPointsLog = new List<MCvPoint3D32f[]>();
        var imagePointsLog = new List<PointF[]>();
        int validImageCount = 0;
        Size imageSize = default;

        // Generate perfect 3D real-world coordinates of the checkerboard corners (Z=0 assumes a flat board)
        var objPts = new MCvPoint3D32f[_patternSize.Width * _patternSize.Height];
        for (int i = 0; i < _patternSize.Height; i++)
        {
            for (int j = 0; j < _patternSize.Width; j++)
            {
                objPts[i * _patternSize.Width + j] = new MCvPoint3D32f(j * _squareSizeMm, i * _squareSizeMm, 0);
            }
        }

        foreach (var frame in calibrationImages)
        {
            if (frame.IsEmpty) continue;
            
            if (imageSize == default) 
            {
                imageSize = frame.Size;
            }

            using var gray = new Mat();
            CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);

            using var corners = new Emgu.CV.Util.VectorOfPointF();
            
            // Search frame for the exact intersection points of the chessboard
            bool found = CvInvoke.FindChessboardCorners(
                gray,
                _patternSize,
                corners,
                CalibCbType.AdaptiveThresh | CalibCbType.NormalizeImage | CalibCbType.FastCheck);

            if (found)
            {
                // Push detection beyond whole pixels into sub-pixel mathematical precision
                CvInvoke.CornerSubPix(
                    gray,
                    corners,
                    new Size(11, 11), // Search window
                    new Size(-1, -1), // No dead-zone
                    new MCvTermCriteria(30, 0.1));

                imagePointsLog.Add(corners.ToArray());
                objectPointsLog.Add(objPts); // Push the perfect 3D model
                validImageCount++;
            }
        }

        if (validImageCount < 5)
        {
            _log.Warning("Calibration Failed: Only detected chessboard corners in {Valid} images representing less than the minimum geometry array (5).", validImageCount);
            return null;
        }

        // We now have valid matched arrays mapping 3D Reality -> 2D Pixels for multiple rotations!
        // Time to solve the heavy lifting Matrix mathematics
        var cameraMatrix = new Mat(3, 3, DepthType.Cv64F, 1);
        var distCoeffs = new Mat(1, 5, DepthType.Cv64F, 1); // Extract k1, k2, p1, p2, k3

        var objectPoints = objectPointsLog.ToArray();
        var imagePoints = imagePointsLog.ToArray();

        _log.Information("Solving multidimensional projection geometry mathematically from {Views} camera viewpoints...", validImageCount);

        double projectionError = CvInvoke.CalibrateCamera(
            objectPoints,
            imagePoints,
            imageSize,
            cameraMatrix,
            distCoeffs,
            CalibType.Default,
            new MCvTermCriteria(30, 0.1),
            out Mat[] rvecs,
            out Mat[] tvecs);

        _log.Information("Calibration Math Complete. Root-Mean-Square Reprojection Error: {Error:F3} pixels.", projectionError);

        return new CameraIntrinsics(cameraMatrix, distCoeffs, imageSize.Width, imageSize.Height);
    }
}

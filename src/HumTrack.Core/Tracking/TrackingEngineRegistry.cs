namespace HumTrack.Core.Tracking;

/// <summary>
/// Central registry of all available tracking engines.
/// The UI queries this to populate the engine selection dropdown.
/// Engines are registered in priority order (recommended engines first).
/// </summary>
public static class TrackingEngineRegistry
{
    private static readonly List<Func<ITrackingEngine>> Factories = new()
    {
        static () => new Engines.HybridEngine(),
        static () => new Engines.ArucoDetectionEngine(),
        static () => new Engines.BlobDetectionEngine(),
        static () => new Engines.OpticalFlowEngine(),
        static () => new Engines.KcfEngine(),
        static () => new Engines.CsrtEngine(),
        static () => new Engines.TemplateMatchingEngine(),
    };

    /// <summary>
    /// Gets the display names of all available engines, in recommended order.
    /// </summary>
    public static IReadOnlyList<string> AvailableEngines { get; } =
        Factories.Select(f =>
        {
            using var engine = f();
            return engine.Name;
        }).ToList().AsReadOnly();

    /// <summary>
    /// Creates a new instance of the tracking engine with the given display name.
    /// </summary>
    /// <param name="name">The display name (must match <see cref="ITrackingEngine.Name"/>).</param>
    /// <returns>A new engine instance. Caller is responsible for disposal.</returns>
    /// <exception cref="ArgumentException">Thrown if no engine matches the given name.</exception>
    public static ITrackingEngine Create(string name)
    {
        foreach (var factory in Factories)
        {
            using var probe = factory();
            if (string.Equals(probe.Name, name, StringComparison.Ordinal))
            {
                // Create a fresh instance (the probe was just for name matching)
                return factory();
            }
        }

        throw new ArgumentException($"No tracking engine found with name '{name}'.", nameof(name));
    }

    /// <summary>
    /// Returns the recommended engine for the given marker type.
    /// </summary>
    /// <param name="markerType">The physical marker type being used.</param>
    /// <returns>A new engine instance optimized for the marker type.</returns>
    public static ITrackingEngine CreateForMarkerType(MarkerType markerType)
    {
        return markerType switch
        {
            MarkerType.ArUco => new Engines.ArucoDetectionEngine(),
            MarkerType.Retroreflective => new Engines.HybridEngine(),
            MarkerType.ColoredSticker => new Engines.BlobDetectionEngine(),
            MarkerType.NaturalFeature => new Engines.KcfEngine(),
            _ => new Engines.HybridEngine(),
        };
    }
}

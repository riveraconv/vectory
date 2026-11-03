using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Vectory.Circuit;

namespace Vectory.Test.CircuitTest;

// Lee los datos de un circuito de su .tres real (circuits/) sin Godot, para probar el eje y sus datos
// con los circuitos del juego. Solo entiende los campos que usan CenterlineBuilder y TrackFrame.
public sealed class TrackFile
{
    public Vector2[] ControlPoints = Array.Empty<Vector2>();
    public float MetersPerPixel = 1.4f;
    public Vector2 ImageCenter = Vector2.Zero;
    public bool Clockwise = true;
    public float TrackWidth = 28f;
    public int CheckpointCount = 5;
    public bool IsTest;
    public string Id = "";
    public Vector2[] ElevationKeys = Array.Empty<Vector2>();
    public Vector2[] BankKeys = Array.Empty<Vector2>();

    // Altura de la pista en la carrera (Track.DefaultTrackY): la de la firma de los fantasmas
    public const float TrackY = -2f;

    public static TrackFile Load(string name)
    {
        string text = File.ReadAllText(Path.Combine(ProjectRoot(), "circuits", name + ".tres"));
        var file = new TrackFile();

        file.ControlPoints = Vector2Array(text, "ControlPoints");
        file.ElevationKeys = Vector2Array(text, "ElevationKeys");
        file.BankKeys = Vector2Array(text, "BankKeys");

        Match center = Regex.Match(text, @"ImageCenter = Vector2\(([^,]+),([^)]+)\)");
        if (center.Success)
            file.ImageCenter = new Vector2(Float(center.Groups[1].Value), Float(center.Groups[2].Value));

        file.MetersPerPixel = FloatField(text, "MetersPerPixel", file.MetersPerPixel);
        file.TrackWidth = FloatField(text, "TrackWidth", file.TrackWidth);
        file.CheckpointCount = (int)FloatField(text, "CheckpointCount", file.CheckpointCount);
        file.Clockwise = !Regex.IsMatch(text, @"Clockwise = false");
        file.IsTest = Regex.IsMatch(text, @"IsTest = true");
        Match id = Regex.Match(text, "Id = \"([^\"]*)\"");
        file.Id = id.Success ? id.Groups[1].Value : "";
        return file;
    }

    public List<Vector3> Centerline() =>
        CenterlineBuilder.Build(ControlPoints, ImageCenter, MetersPerPixel, Clockwise, TrackY);

    // Marco 3D como el de Track.BuildFrame: eje en planta más los perfiles de altura y peralte
    public TrackFrame Frame(ICollection<string> warnings = null)
    {
        List<Vector3> plan = Centerline();
        float lap = CenterlineBuilder.CumulativeDistances(plan)[plan.Count];
        return new TrackFrame(plan, new TrackProfile(ElevationKeys, lap, warnings), new TrackProfile(BankKeys, lap, warnings));
    }

    private static Vector2[] Vector2Array(string text, string name)
    {
        Match m = Regex.Match(text, $@"^{name} = PackedVector2Array\(([^)]*)\)", RegexOptions.Multiline);
        if (!m.Success)
            return Array.Empty<Vector2>();
        float[] values = m.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return Enumerable.Range(0, values.Length / 2).Select(i => new Vector2(values[2 * i], values[2 * i + 1])).ToArray();
    }

    private static float FloatField(string text, string name, float fallback)
    {
        Match m = Regex.Match(text, $@"^{name} = ([-0-9.eE]+)", RegexOptions.Multiline);
        return m.Success ? Float(m.Groups[1].Value) : fallback;
    }

    private static float Float(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);

    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encuentra project.godot");
    }
}

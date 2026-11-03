using Vectory.Circuit;
using Vectory.Ghost;
using Xunit;

namespace Vectory.Test.CircuitTest;

public class CenterlineBuilderTests
{
    // Firma del trazado de circuit_01 antes de sacar el eje de Track (la de los fantasmas guardados).
    // Si cambia, los récords siguen pero los fantasmas de circuit_01 dejan de valer.
    private const string Circuit01Signature = "57dfd23466f3c04b";

    [Fact]
    public void Circuit01_SignatureUnchanged()
    {
        TrackFile circuit = TrackFile.Load("circuit_01");

        string signature = TrackSignature.Compute(circuit.Centerline(), circuit.TrackWidth);

        Assert.Equal(Circuit01Signature, signature);
    }

    [Fact]
    public void Build_StartsAtFirstControlPoint()
    {
        TrackFile circuit = TrackFile.Load("circuit_01");

        var centerline = circuit.Centerline();

        var first = (circuit.ControlPoints[0] - circuit.ImageCenter) * circuit.MetersPerPixel;
        Assert.Equal(first.X, centerline[0].X, 3);
        Assert.Equal(TrackFile.TrackY, centerline[0].Y, 3);
        Assert.Equal(first.Y, centerline[0].Z, 3);
    }

    [Fact]
    public void CheckpointIndices_EvenlySpacedInOrder()
    {
        var centerline = TrackFile.Load("circuit_01").Centerline();
        float[] cumulative = CenterlineBuilder.CumulativeDistances(centerline);
        float lap = cumulative[centerline.Count];

        var indices = CenterlineBuilder.CheckpointIndices(centerline, 3);

        Assert.Equal(3, indices.Count);
        for (int k = 0; k < 3; k++)
            Assert.InRange(cumulative[indices[k]], lap * (k + 1) / 4f - 8f, lap * (k + 1) / 4f + 1f);
    }

    [Fact]
    public void CheckpointIndices_NoneWithZero()
    {
        var centerline = TrackFile.Load("circuit_01").Centerline();

        Assert.Empty(CenterlineBuilder.CheckpointIndices(centerline, 0));
    }
}

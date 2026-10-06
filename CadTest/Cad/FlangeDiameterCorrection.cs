using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public double VerifyCorrectedFlangeVolume(double expected)
    {
        if (!double.IsFinite(expected) || expected <= 0) throw new ArgumentOutOfRangeException(nameof(expected));
        double actual = CurrentTrialVolume();
        if (Math.Abs(actual - expected) > .1) throw new InvalidOperationException("Saved D19 volume mismatch.");
        return actual;
    }
    public object CorrectFlangeDiameter19()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        _ = Drawing.Flange19Study.RunChecks();
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name.StartsWith("DRAWING_D19_")) throw new InvalidOperationException("D19 correction already present.");
        double before = CurrentTrialVolume();
        for (int i = 0; i < 12; i++)
        {
            double angle = (15 + 30 * i) * Math.PI / 180;
            double y = 113 * Math.Sin(angle), z = -113 * Math.Cos(angle);
            CutChannelCylinder($"DRAWING_D19_{i + 1:D2}", [247, y, z], [265, y, z], 9.5, [0, 1, 0]);
        }
        double after = CurrentTrialVolume(), expected = 12 * 18 * Math.PI * (9.5 * 9.5 - 7.5 * 7.5);
        if (Math.Abs(before - after - expected) > .1)
            throw new InvalidOperationException($"D19 removed volume {before - after} vs {expected}.");
        return new { BeforeMm3 = before, AfterMm3 = after, RemovedMm3 = before - after, ExpectedMm3 = expected,
            Geometry = InspectFlangeDiameter19(), Scope = "Corrects stage9 misreading: drawing nominal12xD19+0.21, not D15. Phase15 and PCD226 unchanged; dimensional tolerance not certified." };
    }

    public object InspectFlangeDiameter19()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        int count = 0, samples = 0;
        for (int i = 0; i < 12; i++)
        {
            Feature? feature = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == $"DRAWING_D19_{i + 1:D2}") feature = f;
            if (feature == null || feature.GetErrorCode2(out bool warning) != 0 || warning)
                throw new InvalidOperationException("D19 correction missing or failed.");
            double a = (15 + 30 * i) * Math.PI / 180, y = .113 * Math.Sin(a), z = -.113 * Math.Cos(a);
            int cylinders = 0; double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            foreach (Face2 face in (object[])feature.GetFaces())
            {
                var surface = (Surface)face.GetSurface();
                if (!surface.IsCylinder()) continue;
                var c = (double[])surface.CylinderParams;
                if (Math.Abs(c[6] - .0095) > 1e-8 || Math.Abs(Math.Abs(c[3]) - 1) > 1e-8
                    || Math.Abs(c[1] - y) > 1e-8 || Math.Abs(c[2] - z) > 1e-8)
                    throw new InvalidOperationException("D19 radius/centre/axis mismatch.");
                cylinders++;
                foreach (Edge edge in (object[])face.GetEdges())
                {
                    var b = edge.GetCurveParams3(); Drawing.M12Study.ValidateEdgeInterval(b.UMinValue, b.UMaxValue);
                    foreach (double t in new[] { b.UMinValue, (b.UMinValue + b.UMaxValue) / 2, b.UMaxValue })
                    {
                        var p = (double[])edge.Evaluate2(t, 0);
                        if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                            throw new InvalidOperationException("D19 edge evaluation failed.");
                        if (Math.Abs(Math.Sqrt(Math.Pow(p[1] - y, 2) + Math.Pow(p[2] - z, 2)) - .0095) > 1e-8)
                            throw new InvalidOperationException("D19 edge off cylinder.");
                        minX = Math.Min(minX, p[0] * 1000); maxX = Math.Max(maxX, p[0] * 1000); samples++;
                    }
                }
            }
            if (cylinders != 1 || Math.Abs(minX - 247) > .0001 || Math.Abs(maxX - 265) > .0001)
                throw new InvalidOperationException("D19 cylinder/end stations mismatch.");
            count++;
        }
        Console.WriteLine($"[OK] Drawing D19: {count} cylinders, {samples} samples, X247..265; PCD226/phase15.");
        return new { Count = count, DiameterMm = 19, PitchCircleMm = 226, PhaseDegrees = 15, EdgeSamples = samples, StartX = 247, EndX = 265 };
    }
}

using CadTest.Drawing;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object CreateDkTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        DkTrialStudy.RunChecks();
        Feature? plane = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name == "APPROX_DK_floor260") throw new InvalidOperationException("DK trial already present.");
            if (f.GetTypeName2() == "RefPlane" && f.Name == "Спереди") plane = f;
        }
        if (plane == null) throw new InvalidOperationException("No front plane.");
        double before = CurrentTrialVolume(), b = DkTrialStudy.FlatRadius;
        _model.ClearSelection2(true); plane.Select2(false, 0);
        var sm = _model.SketchManager; sm.InsertSketch(true);
        using (var exact = new ExactSketchCoordinates(sm))
        {
            void Line(double x1, double r1, double x2, double r2)
            {
                if (sm.CreateLine(x1 / 1000, (-74 + r1) / 1000, 0, x2 / 1000, (-74 + r2) / 1000, 0) == null)
                    throw new InvalidOperationException("DK profile line failed.");
            }
            Line(246.5, 0, 273, 0); // first line is revolution axis and closing boundary
            Line(246.5, 0, 246.5, b);
            double midAngle = (Math.PI + Math.Acos(-2.5 / 16)) / 2;
            if (sm.Create3PointArc(.2465, (-74 + b) / 1000, 0, .260, (-74 + 16.5) / 1000, 0,
                (262.5 + 16 * Math.Cos(midAngle)) / 1000, (-74 + b + 16 * Math.Sin(midAngle)) / 1000, 0) == null)
                throw new InvalidOperationException("DK profile arc failed.");
            Line(260, 16.5, 273, 16.5); Line(273, 16.5, 273, 0);
        }
        sm.InsertSketch(true);
        Feature? profile = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "ProfileFeature") profile = f;
        if (profile == null) throw new InvalidOperationException("DK profile missing.");
        _model.ClearSelection2(true); profile.Select2(false, 0);
        if (!_model.Extension.SelectByID2($"Line1@{profile.Name}", "EXTSKETCHSEGMENT", 0, 0, 0, true, 4, null, 0))
            throw new InvalidOperationException("DK local revolution axis selection failed.");
        var cut = (Feature?)_model.FeatureManager.FeatureRevolveCut(2 * Math.PI, false, 0,
            (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees, 0, false, true);
        if (cut == null) throw new InvalidOperationException("DK trial cut failed.");
        cut.Name = "APPROX_DK_floor260"; _model.ForceRebuild3(false);
        if (cut.GetErrorCode2(out bool warning) != 0 || warning) throw new InvalidOperationException("DK trial operation error.");
        double removed = before - CurrentTrialVolume(), expected = DkTrialStudy.Volume(20000);
        if (Math.Abs(removed - expected) > .1) throw new InvalidOperationException($"DK removed volume {removed} vs {expected}.");
        return new { RemovedMm3 = removed, ExpectedMm3 = expected, Geometry = InspectDkTrial(),
            UserApprovedApproximation = true, DrawingDatumConfirmed = false,
            Assumptions = "Compound R16 bowl, mouth D33 at cavity floor X260 (NOT original rear X273), depth13.5 to X246.5, local axis Y-74, flat bottom R0.696519371. Intersects existing D12 at Y-84. Not uniquely established drawing DK geometry." };
    }

    public object InspectDkTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        Feature? feature = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name == "APPROX_DK_floor260") feature = f;
        if (feature == null || TiffSuppressed(feature) || feature.GetErrorCode2(out bool warning) != 0 || warning) throw new InvalidOperationException("DK feature missing/failed.");
        int samples = 0, connected = 0; double residual = 0, minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        foreach (Face2 face in (object[])feature.GetFaces())
        {
            if (((Surface)face.GetSurface()).IsPlane()) continue;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                foreach (Face2 adj in ((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>())
                {
                    var s = (Surface)adj.GetSurface();
                    if (!s.IsCylinder()) continue;
                    var c = (double[])s.CylinderParams;
                    if (Math.Abs(c[6] - .006) < 1e-8 && Math.Abs(c[1] + .084) < 1e-8 && Math.Abs(c[2]) < 1e-8 && Math.Abs(Math.Abs(c[3]) - 1) < 1e-8) connected++;
                }
                var bounds = edge.GetCurveParams3(); M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
                for (int i = 0; i <= 64; i++)
                {
                    var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * i / 64, 0);
                    if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite)) throw new InvalidOperationException("DK edge evaluation failed.");
                    double x = p[0] * 1000, r = Math.Sqrt(Math.Pow(p[1] * 1000 + 74, 2) + Math.Pow(p[2] * 1000, 2));
                    residual = Math.Max(residual, Math.Abs(Math.Sqrt(Math.Pow(x - 262.5, 2) + Math.Pow(r - DkTrialStudy.FlatRadius, 2)) - 16));
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); samples++;
                }
            }
        }
        if (samples == 0 || connected == 0 || residual > .0001 || Math.Abs(minX - 246.5) > .0001 || Math.Abs(maxX - 260) > .0001)
            throw new InvalidOperationException($"DK geometry: N={samples}, connections={connected}, residual={residual}, X={minX}..{maxX}.");
        Console.WriteLine($"[OK] APPROX DK: {samples} samples, residual={residual:F9}, D12 connections={connected}.");
        return new { Samples = samples, MaxProfileResidualMm = residual, MinX = minX, MaxX = maxX, D12SharedEdges = connected };
    }
}

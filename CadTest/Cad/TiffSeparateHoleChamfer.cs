using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private const string PpChamferName = "TIFF_PP_D22_REAR_CHAMFER_2x45";

    private List<double[]> PpCircles()
    {
        var body = CopyPilotBody();
        var found = new List<double[]>();
        foreach (Edge edge in (object[])body.GetEdges())
        {
            var curve = (Curve)edge.GetCurve();
            if (!curve.IsCircle()) continue;
            var p = (double[])curve.CircleParams;
            if (Math.Abs(p[1] * 1000 - 111.5) < 1e-5 && Math.Abs(p[2] * 1000) < 1e-5) found.Add(p);
        }
        GC.KeepAlive(body);
        return found;
    }

    public object InspectTiffPpChamfer()
    {
        var feature = TiffFeature(PpChamferName);
        if (TiffSuppressed(feature)) throw new InvalidOperationException("P-P chamfer suppressed.");
        var circles = PpCircles();
        foreach (var (x, r) in new[] { (247.0, 11.0), (263.0, 11.0), (265.0, 13.0) })
            if (circles.Count(p => Math.Abs(p[0] * 1000 - x) < 1e-5 && Math.Abs(p[6] * 1000 - r) < 1e-5) != 1)
                throw new InvalidOperationException($"P-P circle mismatch at X{x}/R{r}.");
        var cones = ((object[])feature.GetFaces()).Cast<Face2>().Where(f => ((Surface)f.GetSurface()).IsCone()).ToArray();
        if (cones.Length != 1) throw new InvalidOperationException("Expected one P-P chamfer cone.");
        int samples = 0;
        foreach (Edge edge in (object[])cones[0].GetEdges())
        {
            var b = edge.GetCurveParams3();
            var curve = (Curve)edge.GetCurve();
            if (curve.IsCircle())
            {
                var c = (double[])curve.CircleParams;
                double expectedSpan = Math.Abs(c[6] * 1000 - 13) < 1e-5 ? Drawing.PpChamferStudy.MouthArcRadians : 2 * Math.PI;
                if (Math.Abs(b.UMaxValue - b.UMinValue - expectedSpan) > 1e-5)
                    throw new InvalidOperationException("P-P circular arc coverage mismatch.");
            }
            Console.WriteLine("[PP EDGE] " + System.Text.Json.JsonSerializer.Serialize(new {
                Circle = curve.IsCircle(), Parameters = curve.IsCircle() ? (double[])curve.CircleParams : null,
                b.UMinValue, b.UMaxValue }));
            Drawing.M12Study.ValidateEdgeInterval(b.UMinValue, b.UMaxValue);
            for (int i = 0; i <= 32; i++)
            {
                var p = (double[])edge.Evaluate2(b.UMinValue + (b.UMaxValue - b.UMinValue) * i / 32, 0);
                if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                    throw new InvalidOperationException("P-P cone evaluation failed.");
                double x = p[0] * 1000, r = Math.Sqrt(Math.Pow(p[1] * 1000 - 111.5, 2) + Math.Pow(p[2] * 1000, 2));
                if (x < 262.9999 || x > 265.0001 || Math.Abs(r - (11 + x - 263)) > .0001)
                    throw new InvalidOperationException("P-P chamfer envelope mismatch.");
                samples++;
            }
        }
        if (samples == 0) throw new InvalidOperationException("No P-P cone samples.");
        double finalVolume = CurrentTrialVolume(), beforeVolume;
        Body2 beforeBody;
        try
        {
            if (!feature.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration, null))
                throw new InvalidOperationException("Cannot isolate P-P chamfer.");
            _model!.ForceRebuild3(false); beforeBody = CopyPilotBody(); beforeVolume = CurrentTrialVolume();
        }
        finally
        {
            if (!feature.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration, null))
                throw new InvalidOperationException("Cannot restore P-P chamfer.");
            _model!.ForceRebuild3(false);
        }
        if (Math.Abs(CurrentTrialVolume() - finalVolume) > .001) throw new InvalidOperationException("P-P volume not restored.");
        var afterBody = CopyPilotBody();
        var removed = (object[]?)((Body2)beforeBody.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,
            (Body2)afterBody.Copy(), out int error);
        if (error != 0 || removed?.Length != 1) throw new InvalidOperationException("Expected one P-P removed ring.");
        double isolated = MeasureRemovedBodiesInScratchPart(removed), expected = Drawing.PpChamferStudy.ClippedRemovedVolume;
        int bossBoundaryCount = 0;
        foreach (Face2 face in (object[])((Body2)removed[0]).GetFaces())
        {
            var s = (Surface)face.GetSurface();
            if (s.IsCylinder())
            {
                var c = (double[])s.CylinderParams;
                if (Math.Abs(c[6] - .1) < 1e-8 && Math.Abs(c[1]) < 1e-8 && Math.Abs(c[2]) < 1e-8 && Math.Abs(Math.Abs(c[3]) - 1) < 1e-8)
                    bossBoundaryCount++;
            }
            Console.WriteLine("[PP REMOVED FACE] " + System.Text.Json.JsonSerializer.Serialize(new {
                Plane = s.IsPlane(), Cone = s.IsCone(), Cylinder = s.IsCylinder(), Box = (double[])face.GetBox(),
                CylinderParameters = s.IsCylinder() ? (double[])s.CylinderParams : null,
                ConeParameters = s.IsCone() ? (double[])s.ConeParams2 : null }));
        }
        Console.WriteLine($"[PP ISOLATED] {isolated:R}; expected={expected:R}; whole delta={beforeVolume-finalVolume:R}");
        // 0.01 mm3 is a numerical integration budget, not a dimensional tolerance.
        // The original 0.02 whole-part criterion now uses the clipped, not full-ring integral.
        if (bossBoundaryCount != 1 || Math.Abs(isolated - expected) > .01 || Math.Abs(beforeVolume-finalVolume-expected) > .02)
            throw new InvalidOperationException("P-P isolated/whole volume differs from boss-clipped analytic chamfer.");
        var reconstruction = CheckM6BooleanReconstruction(beforeBody, afterBody, removed, "P-P");
        Console.WriteLine($"[TIFF PP OK] D22/X247..263; mouthD26/X265; 2x45; {samples} samples.");
        return new { CylinderDiameterMm = 22, CentreY = 111.5, CentreZ = 0, CylinderStartX = 247,
            CylinderEndX = 263, MouthX = 265, MouthDiameterMm = 26, ChamferMm = 2, AngleDegrees = 45,
            EdgeSamples = samples, OutsideRadialWallMm = 3.0, IsolatedRemovedMm3 = isolated, ExpectedRemovedMm3 = expected,
            WholePartDeltaMm3 = beforeVolume - finalVolume, WholePartVolumeComparisonPassed = Math.Abs(beforeVolume-finalVolume-expected) <= .02,
            WholePartVolumeComparisonToleranceMm3 = .02, BooleanReconstruction = reconstruction,
            IsolatedVolumeComparisonToleranceMm3 = .01, BossBoundaryDiameterMm = 200, MouthIsFullCircle = false,
            ExpectedMouthArcRadians = Drawing.PpChamferStudy.MouthArcRadians,
            DrawingSource = "TIFF section P-P, explicit 2x45 callout" };
    }

    public object BuildTiffPpChamfer()
    {
        Drawing.PpChamferStudy.RunChecks();
        // Select a persistent model edge, not an edge of the temporary inspection body.
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name == PpChamferName) throw new InvalidOperationException("P-P chamfer already exists.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        var circles = PpCircles();
        foreach (var x in new[] { 247.0, 265.0 })
            if (circles.Count(p => Math.Abs(p[0] * 1000 - x) < 1e-5 && Math.Abs(p[6] * 1000 - 11) < 1e-5) != 1)
                throw new InvalidOperationException("Expected unchamfered P-P predecessor.");
        var bodies = (object[])((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false);
        if (bodies.Length != 1) throw new InvalidOperationException("Expected one body.");
        Edge? mouth = null; int count = 0;
        foreach (Edge edge in (object[])((Body2)bodies[0]).GetEdges())
        {
            var curve = (Curve)edge.GetCurve(); if (!curve.IsCircle()) continue;
            var c = (double[])curve.CircleParams;
            if (Math.Abs(c[0] - .265) < 1e-8 && Math.Abs(c[1] - .1115) < 1e-8 &&
                Math.Abs(c[2]) < 1e-8 && Math.Abs(c[6] - .011) < 1e-8) { mouth = edge; count++; }
        }
        if (count != 1) throw new InvalidOperationException("P-P persistent mouth selection ambiguous.");
        double before = CurrentTrialVolume();
        _model.ClearSelection2(true);
        if (!((Entity)mouth!).Select4(false, null)) throw new InvalidOperationException("Cannot select P-P mouth.");
        var chamfer = (Feature?)_model.FeatureManager.InsertFeatureChamfer(0,
            (int)swChamferType_e.swChamferAngleDistance, .002, Math.PI / 4, 0, 0, 0, 0)
            ?? throw new InvalidOperationException("P-P chamfer creation failed.");
        chamfer.Name = PpChamferName; _model.ForceRebuild3(false);
        double final = CurrentTrialVolume(), expected = Drawing.PpChamferStudy.ClippedRemovedVolume;
        Console.WriteLine($"[PP VOLUME] before={before:R}, after={final:R}, delta={before-final:R}, expected={expected:R}");
        var geometry = InspectTiffPpChamfer();
        // Whole-part integrated volume is diagnostic. The isolated removed ring is
        // checked against the analytic volume and rejoined above; no budget is enlarged.
        CheckTiffSuppressionStates(prior, []);
        var health = InspectTiffFeatureHealth();
        return new { BeforeMm3 = before, FinalMm3 = final, RemovedMm3 = before - final, ExpectedRemovedMm3 = expected,
            Geometry = geometry, Health = health, PpChamferBuilt = true, M6PatternBuilt = true,
            RearCollarRebuilt = true, OuterProfileRebuilt = true, AaChannelRebuilt = true, RightM8Rebuilt = true,
            LeftM8Rebuilt = true, Bore68StartRebuilt = true, Bore80PartialRebuilt = true, Bore80Restored = true,
            DetailLCoreBuilt = true, IsManufacturingReady = false,
            Scope = "P-P D22 rear 2x45 only; inherited nodes require fresh verification. Existing source uncertainties remain." };
    }
}

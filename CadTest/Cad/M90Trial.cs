using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public void PrintMetricDieSizes()
    {
        if (_swApp == null) throw new InvalidOperationException("No application.");
        int errors = 0, warnings = 0;
        var library = (ModelDoc2?)_swApp.OpenDoc6(@"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2025\Thread Profiles\Metric Die.SLDLFP",
            (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly | (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            "", ref errors, ref warnings);
        if (library == null || errors != 0) throw new InvalidOperationException($"Library open errors={errors}, warnings={warnings}.");
        foreach (string name in (string[])library.GetConfigurationNames()) Console.WriteLine($"[LIBRARY] {name}");
    }

    public object CreateM90Trial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        const string libraryPath = @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2025\Thread Profiles\Metric Die.SLDLFP";
        var configurations = (string[]?)_swApp!.GetConfigurationNames(libraryPath);
        if (configurations == null || !configurations.Contains("M16x2.0", StringComparer.Ordinal))
            throw new InvalidOperationException("Required P2 Metric Die configuration is unavailable.");
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name.StartsWith("TRIAL_M90", StringComparison.Ordinal))
                throw new InvalidOperationException("M90 trial already present; refusing duplicate cut.");
        var body = (Body2)((object[])((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false))[0];
        var candidates = new List<Edge>();
        foreach (Face2 face in (object[])body.GetFaces())
        {
            var surface = (Surface)face.GetSurface();
            if (!surface.IsCylinder()) continue;
            var cylinder = (double[])surface.CylinderParams;
            if (Math.Abs(cylinder[6] - .045) > 1e-8 || Math.Abs(Math.Abs(cylinder[3]) - 1) > 1e-8 ||
                Math.Abs(cylinder[1]) > 1e-8 || Math.Abs(cylinder[2]) > 1e-8) continue;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var curve = (Curve)edge.GetCurve();
                if (!curve.IsCircle()) continue;
                var c = (double[])curve.CircleParams;
                if (Math.Abs(c[0] - .005) < 1e-8 && Math.Abs(c[1]) < 1e-8 && Math.Abs(c[2]) < 1e-8 &&
                    Math.Abs(c[6] - .045) < 1e-8 && Math.Abs(Math.Abs(c[3]) - 1) < 1e-8) candidates.Add(edge);
            }
        }
        if (candidates.Count != 1) throw new InvalidOperationException($"M90 start arc count={candidates.Count}.");
        Console.WriteLine("[AUDIT] M90 start arc X5/R45 selected uniquely.");
        _model.ClearSelection2(true);
        // Measure before preparing the definition; do not interleave unrelated COM calls with creation.
        double before = CurrentTrialVolume();
        var data = (IThreadFeatureData)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweepThread);
        data.InitializeThreadData(); data.Edge = candidates[0];
        data.Type = libraryPath;
        // Installed library has no M90x2 configuration. Reuse its external P2 tooth section,
        // with the helix diameter explicitly set to 90 mm; never label the library size M90.
        data.Size = "M16x2.0";
        data.DiameterOverride = true; data.Diameter = .09;
        data.EndCondition = (int)swThreadEndCondition_e.swThreadEndCondition_Blind;
        data.BlindDepth = .033; data.ThreadMethod = (int)swThreadMethod_e.swThreadMethod_Cut;
        data.RightHanded = true; data.MultipleStart = false;
        data.PitchOverride = true; data.Pitch = .002;
        data.ReverseDirection = false; data.TrimStartFace = true; data.TrimEndFace = true;
        _model.ClearSelection2(true);
        Console.WriteLine($"[AUDIT] Prepared M90: Type={data.Type}, Size={data.Size}, pitch={data.Pitch}, depth={data.BlindDepth}.");
        if (data.Size != "M16x2.0" || Math.Abs(data.Pitch - .002) > 1e-9 || Math.Abs(data.BlindDepth - .033) > 1e-9 ||
            !data.Type.EndsWith("Metric Die.SLDLFP", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("M90 definition did not accept requested parameters.");
        var feature = (Feature?)_model.FeatureManager.CreateFeature(data);
        if (feature == null) throw new InvalidOperationException("M90 creation failed.");
        feature.Name = "TRIAL_M90x2_X5_to_X38";
        _model.ForceRebuild3(false);
        int error = feature.GetErrorCode2(out bool warning);
        if (error != 0 || warning) throw new InvalidOperationException($"M90 error={error}, warning={warning}.");
        double removal = before - CurrentTrialVolume();
        Console.WriteLine($"[AUDIT] M90 removed volume={removal:F6} mm3.");
        if (removal <= 100 || removal >= Math.PI * (45 * 45 - 43 * 43) * 33)
            throw new InvalidOperationException($"M90 removal outside envelope: {removal}.");
        return new { RemovedMm3 = removal, Geometry = InspectM90Trial(), ActualVolumeMm3 = CurrentTrialVolume() };
    }

    public object InspectM90Trial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var matches = new List<Feature>();
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name == "TRIAL_M90x2_X5_to_X38") matches.Add(f);
        if (matches.Count != 1) throw new InvalidOperationException("Expected exactly one M90 operation.");
        var feature = matches[0];
        var parameters = (IThreadFeatureData)feature.GetDefinition();
        Console.WriteLine($"[AUDIT] M90 parameters: Type={parameters.Type}, Size={parameters.Size}, Pitch={parameters.Pitch}, Depth={parameters.BlindDepth}, Reverse={parameters.ReverseDirection}, Diameter={parameters.Diameter}, Override={parameters.DiameterOverride}.");
        Drawing.M90TrialChecks.ValidateDefinition(parameters.Type, parameters.Size, parameters.Pitch * 1000,
            parameters.Diameter * 1000, parameters.BlindDepth * 1000, parameters.DiameterOverride,
            parameters.RightHanded, parameters.MultipleStart, parameters.ReverseDirection,
            parameters.TrimStartFace, parameters.TrimEndFace,
            parameters.ThreadMethod == (int)swThreadMethod_e.swThreadMethod_Cut,
            parameters.EndCondition == (int)swThreadEndCondition_e.swThreadEndCondition_Blind);
        int error = feature.GetErrorCode2(out bool warning);
        if (error != 0 || warning) throw new InvalidOperationException($"M90 error={error}, warning={warning}.");
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity, minR = double.PositiveInfinity, maxR = 0;
        int samples = 0;
        int inspectedFaces = 0;
        foreach (Face2 face in (object[])feature.GetFaces())
        {
            if (((Surface)face.GetSurface()).IsPlane()) continue; // flat interruptions are existing local cuts
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var bounds = edge.GetCurveParams3();
                Drawing.M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
                for (int j = 0; j <= 32; j++)
                {
                    var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * j / 32, 0);
                    if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                        throw new InvalidOperationException("M90 edge evaluation failed.");
                    double x = p[0] * 1000, r = 1000 * Math.Sqrt(p[1] * p[1] + p[2] * p[2]);
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minR = Math.Min(minR, r); maxR = Math.Max(maxR, r); samples++;
                }
            }
            inspectedFaces++;
            if (inspectedFaces % 4 == 0) Console.WriteLine($"[AUDIT] M90 progress: {inspectedFaces} faces, {samples} edge samples.");
        }
        Console.WriteLine($"[AUDIT] M90: N={samples}, X={minX:F6}..{maxX:F6}, R={minR:F6}..{maxR:F6}.");
        // Blind depth ends the helix at X38, not the swept tooth boundary. The installed
        // P2 section extends the last boundary to X39.75, still beyond 38 min and before X42.
        Drawing.M90TrialChecks.ValidateBounds(samples, minX, maxX, minR, maxR);
        var actual = (IThreadFeatureData)feature.GetDefinition();
        if (actual.Size != "M16x2.0" || !actual.DiameterOverride || Math.Abs(actual.Diameter - .09) > 1e-9 ||
            !actual.Type.EndsWith("Metric Die.SLDLFP", StringComparison.OrdinalIgnoreCase) ||
            Math.Abs(actual.Pitch - .002) > 1e-9 || Math.Abs(actual.BlindDepth - .033) > 1e-9 ||
            !actual.RightHanded || actual.MultipleStart || actual.ReverseDirection || !actual.TrimStartFace || !actual.TrimEndFace ||
            actual.ThreadMethod != (int)swThreadMethod_e.swThreadMethod_Cut ||
            actual.EndCondition != (int)swThreadEndCondition_e.swThreadEndCondition_Blind)
            throw new InvalidOperationException("M90 feature parameters mismatch.");
        return new { Samples = samples, MinX = minX, MaxX = maxX, MinRadiusMm = minR, MaxRadiusMm = maxR,
            LibraryProfile = actual.Size, HelixDiameterMm = actual.Diameter * 1000,
            HelixEndXmm = 38, DrawingMinimumEndStationMm = 38, SplineLeadInXmm = 42,
            Scope = "Nominal external M90x2 using Metric Die M16x2.0 P2 tooth section with D90 helix override. Helix X5..38; actual swept boundary ends X39.75, beyond 38 min and before spline lead-in X42. Complete-profile length, runout, entry chamfer partial thread and 6g tolerance not verified." };
    }
}

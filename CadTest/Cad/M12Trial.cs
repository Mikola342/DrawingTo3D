using CadTest.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    // Nominal ISO metric basic minor diameter, not a selected tapping drill or 7H limit.
    public static double M12BasicMinorDiameter => Drawing.M12Study.BasicMinorDiameter;

    private double CurrentTrialVolume()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var bodies = (object[]?)((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false);
        if (bodies?.Length != 1) throw new InvalidOperationException("Expected one solid body.");
        var mass = (MassProperty2)_model.Extension.CreateMassProperty2();
        mass.UseSystemUnits = true;
        mass.AccuracyLevel = (int)swMassPropertyAccuracyLevel_e.swMassPropertyAccuracyLevel_Higher;
        mass.Recalculate();
        double volume = mass.Volume * 1e9;
        if (!double.IsFinite(volume) || volume <= 0)
            throw new InvalidOperationException("Invalid solid-body volume.");
        return volume;
    }

    public object CreateM12Trial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        double initial = CurrentTrialVolume(), r = M12BasicMinorDiameter / 2;
        Drawing.M12Study.RunChecks();
        var holes = new PartDescription();
        foreach (int sign in new[] { 1, -1 })
            holes.Holes.Add(new HoleDescription { Type = HoleType.Simple, ThroughAll = true, Depth = 18,
                Diameter = 2 * r, X = sign * 113 * Math.Cos(Math.PI / 6), Y = sign * 56.5 });
        CreateHolesFromDescription(holes);
        double drilled = CurrentTrialVolume();
        double expectedDrillRemoval = 2 * Math.PI * r * r * 18;
        if (Math.Abs(initial - drilled - expectedDrillRemoval) > .05)
            throw new InvalidOperationException("M12 basic bores volume mismatch.");

        List<(Edge Edge, double[] Circle)>? cachedCircles = null;
        Edge HoleEdge(double station, double y, double z, double radius)
        {
            if (cachedCircles == null)
            {
                cachedCircles = new();
                var body = (Body2)((object[])((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false))[0];
                foreach (Edge edge in (object[])body.GetEdges())
                {
                    var curve = (Curve)edge.GetCurve();
                    if (curve.IsCircle()) cachedCircles.Add((edge, (double[])curve.CircleParams));
                }
            }
            var candidates = cachedCircles.Where(e => Math.Abs(e.Circle[0] * 1000 - station) < 1e-5 &&
                Math.Abs(e.Circle[1] * 1000 - y) < 1e-5 && Math.Abs(e.Circle[2] * 1000 - z) < 1e-5 &&
                Math.Abs(e.Circle[6] * 1000 - radius) < 1e-5).Select(e => e.Edge).ToList();
            if (candidates.Count != 1) throw new InvalidOperationException($"M12 edge X={station}, Y={y}, Z={z}: count {candidates.Count}.");
            return candidates[0];
        }

        // Right-plane sketch X corresponds to global -Z.
        var centres = holes.Holes.Select(h => (Y: h.Y, Z: -h.X)).ToArray();
        _model.ClearSelection2(true);
        foreach (var c in centres)
        {
            var edge = HoleEdge(265, c.Y, c.Z, r);
            if (!((Entity)edge).Select4(true, null)) throw new InvalidOperationException("M12 rear edge selection failed.");
        }
        var chamfer = (Feature?)_model.FeatureManager.InsertFeatureChamfer(0,
            (int)swChamferType_e.swChamferAngleDistance, .002, Math.PI / 4, 0, 0, 0, 0);
        if (chamfer == null) throw new InvalidOperationException("M12 2x45 chamfers failed.");
        chamfer.Name = "M12_rear_chamfers_2x45";
        _model.ForceRebuild3(false);
        cachedCircles = null;
        double chamfered = CurrentTrialVolume();
        double expectedChamferRemoval = 2 * Math.PI * (4 * r + 8.0 / 3);
        if (Math.Abs(drilled - chamfered - expectedChamferRemoval) > .05)
            throw new InvalidOperationException("M12 chamfer volume mismatch.");
        foreach (var c in centres) { HoleEdge(263, c.Y, c.Z, r); HoleEdge(265, c.Y, c.Z, r + 2); }
        Console.WriteLine($"[OK] M12 bores/chamfers: removed {initial - chamfered:F6}; independent {expectedDrillRemoval + expectedChamferRemoval:F6} mm3.");

        var reports = new List<object>();
        for (int i = 0; i < centres.Length; i++)
        {
            var c = centres[i];
            var edge = HoleEdge(247, c.Y, c.Z, r);
            var data = (IThreadFeatureData)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweepThread);
            data.InitializeThreadData();
            data.Edge = edge;
            data.Type = "Metric Tap.SLDLFP";
            data.Size = "M12x1.75";
            data.EndCondition = (int)swThreadEndCondition_e.swThreadEndCondition_Blind;
            data.BlindDepth = .018;
            data.ThreadMethod = (int)swThreadMethod_e.swThreadMethod_Cut;
            data.RightHanded = true;
            data.MultipleStart = false;
            data.PitchOverride = true;
            data.Pitch = .00175;
            data.TrimStartFace = true;
            data.TrimEndFace = true;
            _model.ClearSelection2(true);
            double before = CurrentTrialVolume();
            var feature = (Feature?)_model.FeatureManager.CreateFeature(data);
            if (feature == null) throw new InvalidOperationException($"M12 thread {i + 1} failed.");
            feature.Name = $"TRIAL_M12x1_75_7H_nominal_{i + 1}";
            _model.ForceRebuild3(false);
            cachedCircles = null;
            int error = feature.GetErrorCode2(out bool warning);
            if (error != 0) throw new InvalidOperationException($"M12 thread error {error}, warning={warning}.");
            double after = CurrentTrialVolume(), removal = before - after;
            double envelope = Math.PI * (6.2 * 6.2 - r * r) * 18;
            if (removal < 10 || removal > envelope)
                throw new InvalidOperationException($"M12 thread removal outside conservative envelope: {removal}.");
            var actual = (IThreadFeatureData)feature.GetDefinition();
            if (Math.Abs(actual.Pitch - .00175) > 1e-9 || !actual.RightHanded || actual.MultipleStart ||
                Math.Abs(actual.BlindDepth - .018) > 1e-9 || actual.ThreadMethod != (int)swThreadMethod_e.swThreadMethod_Cut)
                throw new InvalidOperationException("M12 thread feature parameters differ from requested ones.");
            reports.Add(new { Feature = feature.Name, CentreY = c.Y, CentreZ = c.Z,
                actual.Type, actual.Size, actual.Pitch, actual.BlindDepth, actual.ReverseDirection,
                RemovedVolumeMm3 = removal, ConservativeEnvelopeMm3 = envelope });
            Console.WriteLine($"[TRIAL] {feature.Name}: pitch={actual.Pitch * 1000}, removed={removal:F6}.");
        }
        double final = CurrentTrialVolume();
        return new { InitialVolumeMm3 = initial, BasicMinorDiameterMm = 2 * r,
            ExpectedBoresRemovalMm3 = expectedDrillRemoval, ExpectedChamfersRemovalMm3 = expectedChamferRemoval,
            ActualVolumeMm3 = final, Threads = reports,
            Scope = "Nominal library thread TRIAL, not verified 7H production profile. Bore/chamfer volumes independent; thread volumes only conservative envelope checks." };
    }

    public object InspectM12Trial(bool completedEntries = false, bool completedStartTrim = false)
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var reports = new List<object>();
        for (Feature? feature = (Feature?)_model.FirstFeature(); feature != null; feature = (Feature?)feature.GetNextFeature())
        {
            int error = feature.GetErrorCode2(out bool warning);
            if (error != 0 && !warning) throw new InvalidOperationException($"Feature {feature.Name}: error {error}.");
            if (!feature.Name.StartsWith("TRIAL_M12x1_75_7H_nominal_")) continue;
            int sign = feature.Name.EndsWith("_1") ? 1 : -1;
            double y = sign * 56.5, z = -sign * 113 * Math.Cos(Math.PI / 6);
            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity,
                minR = double.PositiveInfinity, maxR = double.NegativeInfinity;
            int samples = 0;
            foreach (Face2 face in (object[])feature.GetFaces())
                foreach (Edge edge in (object[])face.GetEdges())
                {
                    var curve = (Curve)edge.GetCurve();
                    // Sample the trimmed edge interval, not the supporting curve's domain.
                    var bounds = edge.GetCurveParams3();
                    double start = bounds.UMinValue, end = bounds.UMaxValue;
                    Drawing.M12Study.ValidateEdgeInterval(start, end);
                    double[] EvaluateEdge(double parameter)
                    {
                        var point = (double[])edge.Evaluate2(parameter, 0);
                        if (point.Length != 4 || (BitConverter.DoubleToInt64Bits(point[3]) & 0xffffffffL) != 1 ||
                            !point.Take(3).All(double.IsFinite))
                            throw new InvalidOperationException("M12 edge evaluation failed.");
                        return point;
                    }
                    var a = EvaluateEdge(start);
                    var b = EvaluateEdge(end);
                    var edgeStart = (double[])bounds.StartPoint;
                    var edgeEnd = (double[])bounds.EndPoint;
                    static double Distance(double[] p, double[] q) => Math.Sqrt(
                        Math.Pow(p[0] - q[0], 2) + Math.Pow(p[1] - q[1], 2) + Math.Pow(p[2] - q[2], 2));
                    if (Math.Min(Math.Max(Distance(a, edgeStart), Distance(b, edgeEnd)),
                        Math.Max(Distance(a, edgeEnd), Distance(b, edgeStart))) > 1e-7)
                        throw new InvalidOperationException($"M12 endpoints mismatch: sense={bounds.Sense}; U={start}..{end}; a={string.Join(',', a)}; b={string.Join(',', b)}; expected={string.Join(',', edgeStart)} / {string.Join(',', edgeEnd)}.");
                    for (int i = 0; i <= 32; i++)
                    {
                        var p = EvaluateEdge(start + (end - start) * i / 32);
                        double x = p[0] * 1000, radius = Math.Sqrt(Math.Pow(p[1] * 1000 - y, 2) + Math.Pow(p[2] * 1000 - z, 2));
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                        minR = Math.Min(minR, radius); maxR = Math.Max(maxR, radius); samples++;
                    }
                }
            double nominalMinorRadius = M12BasicMinorDiameter / 2;
            double nominalChamferExit = 263 + 6 - nominalMinorRadius;
            if (samples == 0 || Math.Abs(minX - 247) > .0001 || Math.Abs(maxX - nominalChamferExit) > .0001 ||
                Math.Abs(minR - nominalMinorRadius) > .0001 || Math.Abs(maxR - 6) > .0001)
                throw new InvalidOperationException($"M12 sampled edge envelope mismatch: X={minX}..{maxX}; R={minR}..{maxR}; N={samples}.");
            var data = (IThreadFeatureData)feature.GetDefinition();
            if (Math.Abs(data.Pitch - .00175) > 1e-9 || !data.RightHanded || data.MultipleStart ||
                Math.Abs(data.BlindDepth - (completedEntries?.01975:.018)) > 1e-9 || data.ThreadMethod != (int)swThreadMethod_e.swThreadMethod_Cut ||
                data.Offset!=completedEntries || (completedEntries && (Math.Abs(data.OffsetDistance-.00175)>1e-9||!data.ReverseOffset||!data.MaintainThreadLength||data.TrimStartFace!=completedStartTrim)))
                throw new InvalidOperationException("M12 persisted thread parameters mismatch.");
            reports.Add(new { Feature = feature.Name, Samples = samples, MinX = minX, MaxX = maxX,
                MinRadius = minR, MaxRadius = maxR, PitchMm = data.Pitch * 1000, data.RightHanded,
                data.Type, data.Size, data.ReverseDirection, data.Offset, data.OffsetDistance, data.BlindDepth, data.TrimStartFace, data.TrimEndFace });
            Console.WriteLine($"[OK] {feature.Name}: {samples} edge samples, X={minX:F6}..{maxX:F6}, R={minR:F6}..{maxR:F6}.");
        }
        if (reports.Count != 2) throw new InvalidOperationException("Expected exactly two M12 thread features.");
        return new { Threads = reports, ActualVolumeMm3 = CurrentTrialVolume(),
            Scope = "Sampled trimmed thread edge bounds and persisted feature parameters; not a continuous surface or 7H tolerance certification." };
    }
}

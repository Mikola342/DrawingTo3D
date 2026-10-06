using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private object CheckM6BooleanReconstruction(Body2 before, Body2 after, object[] removed, string context = "M6")
    {
        var rebuilt = (Body2)after.Copy();
        foreach (Body2 piece in removed)
        {
            var united = (object[]?)rebuilt.Operations2((int)swBodyOperationType_e.SWBODYADD, (Body2)piece.Copy(), out int error);
            if (error != 0 || united?.Length != 1) throw new InvalidOperationException($"M6 reconstruction union failed: {error}/{united?.Length}");
            rebuilt = (Body2)united[0];
        }
        int CheckEmpty(Body2 a, Body2 b)
        {
            var difference = (object[]?)((Body2)a.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT, (Body2)b.Copy(), out int error);
            if ((difference?.Length ?? 0) != 0 || (error != 0 && error != (int)swBodyOperationError_e.swBodyOperationEmptyBody))
                throw new InvalidOperationException($"M6 reconstruction differs from unthreaded body: {error}/{difference?.Length}");
            return error;
        }
        int forward = CheckEmpty(before, rebuilt), reverse = CheckEmpty(rebuilt, before);
        Console.WriteLine($"[{context} BOOLEAN RECONSTRUCTION OK] {removed.Length} removed bodies rejoined; both differences empty ({forward}/{reverse}).");
        GC.KeepAlive(before); GC.KeepAlive(after); GC.KeepAlive(removed);
        return new { RemovedBodyCount = removed.Length, ForwardDifferenceEmpty = true, ReverseDifferenceEmpty = true, ForwardCode = forward, ReverseCode = reverse };
    }

    public object FinishTiffM6Pattern()
    {
        var pattern = InspectTiffM6Pattern();
        var exterior = InspectTiffExterior(); var collar = InspectTiffRearCollar();
        var ii = InspectTiffIiChannel(true); var aa = InspectTiffAaChannel(true);
        var bore = InspectTiffDetailLCore(); var health = InspectTiffFeatureHealth();
        return new { FinalMm3 = CurrentTrialVolume(), Pattern = pattern, Exterior = exterior, Collar = collar,
            II = ii, AA = aa, Bore = bore, Health = health, M6PatternBuilt = true, RearCollarRebuilt = true, OuterProfileRebuilt = true,
            AaChannelRebuilt = true, RightM8Rebuilt = true, LeftM8Rebuilt = true, Bore68StartRebuilt = true,
            Bore80PartialRebuilt = true, Bore80Restored = true, DetailLCoreBuilt = true, IsManufacturingReady = false,
            Scope = "M6 checkpoint rechecked; numerical volume comparison disclosed separately from Boolean reconstruction. Drill points/runout and inherited limitations remain." };
    }
    private const string TiffM6Size = Drawing.TiffM6LibraryStudy.Size;
    private const string TiffM6Library = @"C:\ProgramData\SolidWorks\SOLIDWORKS 2025\thread profiles\Metric Tap.SLDLFP";
    private object CheckTiffM6Thread(Feature thread, bool extendedStart = false)
    {
        if (TiffSuppressed(thread)) throw new InvalidOperationException("M6 thread suppressed.");
        var d = (IThreadFeatureData)thread.GetDefinition();
        var parameters = new { Feature = thread.Name, d.Type, d.Size, d.PitchOverride, d.Pitch, d.BlindDepth,
            d.RightHanded, d.MultipleStart, d.ReverseDirection, d.TrimStartFace, d.TrimEndFace, d.ThreadMethod, d.EndCondition,
            d.DiameterOverride, d.Diameter, d.Offset, d.OffsetDistance, d.ReverseOffset, d.MaintainThreadLength };
        string diagnostic = System.Text.Json.JsonSerializer.Serialize(parameters);
        Console.WriteLine("[M6 PARAMETERS] " + diagnostic);
        if (!string.Equals(Path.GetFileName(d.Type), "Metric Tap.SLDLFP", StringComparison.OrdinalIgnoreCase) || d.Size != TiffM6Size || !d.PitchOverride || Math.Abs(d.Pitch - .001) > 1e-9 || Math.Abs(d.BlindDepth - (extendedStart?.016:.015)) > 1e-9 ||
            !d.RightHanded || d.MultipleStart || d.ReverseDirection || d.TrimStartFace == extendedStart || !d.TrimEndFace ||
            d.Offset != extendedStart || (extendedStart && (Math.Abs(d.OffsetDistance-.001)>1e-9 || !d.ReverseOffset || !d.MaintainThreadLength)) ||
            d.ThreadMethod != (int)swThreadMethod_e.swThreadMethod_Cut || d.EndCondition != (int)swThreadEndCondition_e.swThreadEndCondition_Blind)
            throw new InvalidOperationException("M6 thread parameter mismatch: " + diagnostic);
        return parameters;
    }
    private static double TiffM6PilotRadius => (6 - 5 * Math.Sqrt(3) / 8) / 2;
    private static (double Y, double Z)[] TiffM6Centres => Enumerable.Range(0, 4).Select(i => {
        double a = (45 + 90 * i) * Math.PI / 180;
        return (47.5 * Math.Sin(a), -47.5 * Math.Cos(a));
    }).ToArray();

    private Edge TiffM6Circle(string featureName, double x, double y, double z)
    {
        var found = new List<Edge>();
        foreach (Face2 face in (object[])TiffFeature(featureName).GetFaces())
        {
            if (!((Surface)face.GetSurface()).IsCylinder()) continue;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var c = (Curve)edge.GetCurve(); if (!c.IsCircle()) continue;
                var p = (double[])c.CircleParams;
                if (Math.Abs(p[0] * 1000 - x) < 1e-5 && Math.Abs(p[1] * 1000 - y) < 1e-5 &&
                    Math.Abs(p[2] * 1000 - z) < 1e-5 && Math.Abs(p[6] * 1000 - TiffM6PilotRadius) < 1e-5)
                    found.Add(edge);
            }
        }
        if (found.Count != 1) throw new InvalidOperationException($"M6 circle selection: {featureName}/{x}/{found.Count}");
        return found[0];
    }

    public object BuildTiffM6Pattern()
    {
        var configurations = (string[]?)_swApp!.GetConfigurationNames(TiffM6Library)
            ?? throw new InvalidOperationException("Cannot read Metric Tap library configurations.");
        Console.WriteLine("[M6 LIBRARY] " + string.Join(", ", configurations.Where(n => n.StartsWith("M6x", StringComparison.Ordinal))));
        _ = Drawing.TiffM6LibraryStudy.SelectConfiguration(configurations);
        _ = InspectTiffRearCollar();
        _ = InspectTiffExterior();
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_M6_")) throw new InvalidOperationException("M6 pattern already present.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        double before = CurrentTrialVolume(), r = TiffM6PilotRadius;
        var steps = new List<object>();
        for (int i = 0; i < 4; i++)
        {
            var c = TiffM6Centres[i]; string prefix = $"TIFF_M6_{i + 1}_";
            double start = CurrentTrialVolume();
            CutChannelCylinder(prefix + "PILOT", [265, c.Y, c.Z], [250, c.Y, c.Z], r, [0, 1, 0]);
            double drilled = CurrentTrialVolume();
            if (Math.Abs(start - drilled - Math.PI * r * r * 15) > .02)
                throw new InvalidOperationException("M6 pilot volume mismatch.");
            var mouth = TiffM6Circle(prefix + "PILOT", 265, c.Y, c.Z);
            _model.ClearSelection2(true);
            if (!((Entity)mouth).Select4(false, null)) throw new InvalidOperationException("M6 mouth selection failed.");
            var chamfer = (Feature?)_model.FeatureManager.InsertFeatureChamfer(0,
                (int)swChamferType_e.swChamferAngleDistance, .001, Math.PI / 4, 0, 0, 0, 0)
                ?? throw new InvalidOperationException("M6 entry chamfer failed.");
            chamfer.Name = prefix + "CHAMFER_1x45";
            _model.ForceRebuild3(false);
            double chamfered = CurrentTrialVolume();
            if (Math.Abs(drilled - chamfered - Math.PI * (r + 1.0 / 3)) > .02)
                throw new InvalidOperationException("M6 chamfer volume mismatch.");
            var data = (IThreadFeatureData)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweepThread);
            data.InitializeThreadData(); data.Edge = TiffM6Circle(prefix + "PILOT", 250, c.Y, c.Z);
            data.Type = TiffM6Library; data.Size = TiffM6Size;
            data.EndCondition = (int)swThreadEndCondition_e.swThreadEndCondition_Blind;
            data.BlindDepth = .015; data.ThreadMethod = (int)swThreadMethod_e.swThreadMethod_Cut;
            data.RightHanded = true; data.MultipleStart = false; data.PitchOverride = true; data.Pitch = .001;
            data.ReverseDirection = false; data.TrimStartFace = true; data.TrimEndFace = true;
            if (data.Size != TiffM6Size) throw new InvalidOperationException("SolidWorks rejected the requested M6 library profile before creation: " + data.Size);
            _model.ClearSelection2(true);
            var thread = (Feature?)_model.FeatureManager.CreateFeature(data) ?? throw new InvalidOperationException("M6 thread creation failed.");
            thread.Name = prefix + "THREAD"; _model.ForceRebuild3(false);
            _ = CheckTiffM6Thread(thread);
            double threaded = CurrentTrialVolume();
            if (chamfered - threaded < 1 || chamfered - threaded > Math.PI * (9 - r * r) * 15)
                throw new InvalidOperationException("M6 thread removal outside nominal envelope.");
            // Extend the pilot after threading to retain a stable start circle atX250.
            // The22mm dimension is represented with a flat bottom; drill point/runout remain pending.
            CutChannelCylinder(prefix + "DEEP_PILOT_PENDING_POINT", [250, c.Y, c.Z], [243, c.Y, c.Z], r, [0, 1, 0]);
            double extended = CurrentTrialVolume();
            if (Math.Abs(threaded - extended - Math.PI * r * r * 7) > .02)
                throw new InvalidOperationException("M6 deep pilot volume mismatch.");
            steps.Add(new { Index = i + 1, CentreY = c.Y, CentreZ = c.Z, PilotRemovedMm3 = start - drilled,
                ChamferRemovedMm3 = drilled - chamfered, ThreadRemovedMm3 = chamfered - threaded,
                ExtensionRemovedMm3 = threaded - extended });
            Console.WriteLine($"[TIFF M6 BUILT] {i + 1}/4; X243..265 pilot, nominal15mm thread,1x45 mouth.");
        }
        CheckTiffSuppressionStates(prior, []);
        var pattern = InspectTiffM6Pattern();
        var ii = InspectTiffIiChannel(true); var aa = InspectTiffAaChannel(true);
        var bore = InspectTiffDetailLCore(); var health = InspectTiffFeatureHealth();
        return new { BeforeMm3 = before, FinalMm3 = CurrentTrialVolume(), Steps = steps, Pattern = pattern,
            II = ii, AA = aa, Bore = bore, Health = health, M6PatternBuilt = true, RearCollarRebuilt = true, OuterProfileRebuilt = true,
            AaChannelRebuilt = true, RightM8Rebuilt = true, LeftM8Rebuilt = true, Bore68StartRebuilt = true,
            Bore80PartialRebuilt = true, Bore80Restored = true, DetailLCoreBuilt = true, IsManufacturingReady = false,
            Scope = "Four nominalM6x1 atPCD95/45+90i,15mm thread operation,22mm flat-ended pilot and1x45 mouths. Drill-point angle, runout and thread tolerances not accepted." };
    }

    public object InspectTiffM6Pattern(bool experimentalPoints = false, bool massAudit = false, bool extendedStarts = false)
    {
        var names = Enumerable.Range(1, 4).Select(i => $"TIFF_M6_{i}_THREAD").ToArray();
        var features = TiffFeatures(names);
        var threads = names.Select(n => features[n]).ToArray();
        var parameters = threads.Select(t=>CheckTiffM6Thread(t,extendedStarts)).ToArray();
        double final = CurrentTrialVolume(); Body2 beforeBody; double beforeVolume;
        try
        {
            foreach (var thread in threads.Reverse())
                if (!thread.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null))
                    throw new InvalidOperationException("M6 isolation failed.");
            _model!.ForceRebuild3(false); beforeBody = CopyPilotBody(); beforeVolume = CurrentTrialVolume();
        }
        finally
        {
            foreach (var thread in threads)
                if (!thread.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null))
                    throw new InvalidOperationException("M6 restoration failed.");
            _model!.ForceRebuild3(false);
        }
        if (Math.Abs(CurrentTrialVolume() - final) > .001) throw new InvalidOperationException("M6 verification changed volume.");
        var afterBody = CopyPilotBody();
        var removed = (object[]?)((Body2)beforeBody.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT, (Body2)afterBody.Copy(), out int error);
        if (error != 0 || removed == null || removed.Length == 0) throw new InvalidOperationException($"M6 body difference failed: {error}");
        double removedVolume = removed.Cast<Body2>().Sum(b => ((double[])b.GetMassProperties(1))[3] * 1e9);
        object? massDiagnostic = null;
        if (massAudit)
        {
            double Measure(object[] selected)
            {
                var mass=(MassProperty)_model!.Extension.CreateMassProperty();
                mass.UseSystemUnits=true;
                if(!mass.AddBodies(selected))throw new InvalidOperationException("M6 temporary body mass selection failed.");
                double value=mass.Volume*1e9;
                if(!double.IsFinite(value)||value<=0)throw new InvalidOperationException("M6 invalid temporary volume.");
                return value;
            }
            double tempBefore=Measure([beforeBody]),tempAfter=Measure([afterBody]);
            double together=Measure(removed);
            var separately=removed.Cast<Body2>().Select(b=>Measure([b])).ToArray();
            // Some COM/kernel configurations report success but return the document's
            // whole volume. Reject this diagnostic rather than inferring zero removal.
            double removalEnvelope=Math.PI*(9-TiffM6PilotRadius*TiffM6PilotRadius)*(extendedStarts?16:15)*4;
            bool selectionHonored=together>0&&together<removalEnvelope&&
                separately.All(v=>v>0&&v<removalEnvelope)&&tempBefore>tempAfter&&
                Math.Abs(together-separately.Sum())<.001;
            massDiagnostic=new{TemporaryBeforeMm3=tempBefore,TemporaryAfterMm3=tempAfter,
                TemporaryDeltaMm3=tempBefore-tempAfter,RemovedTogetherMm3=together,
                RemovedSeparatelyMm3=separately,RemovedSeparateSumMm3=separately.Sum(),
                DocumentBeforeMm3=beforeVolume,DocumentAfterMm3=final,
                SelectedBodiesHonored=selectionHonored,RemovalEnvelopeMm3=removalEnvelope,
                DiagnosticUsable=selectionHonored,
                SameKernelDiagnostic=true,IndependentGeometricAcceptance=false};
            Console.WriteLine($"[M6 MASS AUDIT] temporary delta={tempBefore-tempAfter:R},together={together:R},separate={separately.Sum():R},document delta={beforeVolume-final:R}.");
        }
        int[] samples = new int[4]; double minR = double.PositiveInfinity, maxR = 0, minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        var centres = TiffM6Centres;
        foreach (Body2 body in removed)
        foreach (Edge edge in (object[])body.GetEdges())
        {
            var interval = edge.GetCurveParams3(); Drawing.M12Study.ValidateEdgeInterval(interval.UMinValue, interval.UMaxValue);
            for (int j = 0; j <= 32; j++)
            {
                var p = (double[])edge.Evaluate2(interval.UMinValue + (interval.UMaxValue - interval.UMinValue) * j / 32, 0);
                if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                    throw new InvalidOperationException("M6 edge evaluation failed.");
                var distances = centres.Select(c => Math.Sqrt(Math.Pow(p[1] * 1000 - c.Y, 2) + Math.Pow(p[2] * 1000 - c.Z, 2))).ToArray();
                int index = Array.IndexOf(distances, distances.Min()); samples[index]++;
                minR = Math.Min(minR, distances[index]); maxR = Math.Max(maxR, distances[index]);
                minX = Math.Min(minX, p[0] * 1000); maxX = Math.Max(maxX, p[0] * 1000);
            }
        }
        double volumeComparisonTolerance = Math.Max(.04, .0005 * Math.Abs(removedVolume));
        bool volumeComparisonPassed = Math.Abs(removedVolume - (beforeVolume - final)) <= volumeComparisonTolerance;
        if (!double.IsFinite(removedVolume) || removedVolume <= 0 ||
            samples.Any(n => n == 0) || minR < TiffM6PilotRadius - .0001 || maxR > 3.0001 || minX < (extendedStarts?248.9999:249.9999) || maxX > 265.0001)
            throw new InvalidOperationException($"M6 isolated-removal envelope mismatch: V={removedVolume}, delta={beforeVolume - final}, R={minR}..{maxR}, X={minX}..{maxX}");
        // Independent CAD set equality supplements the approximate integrated-volume
        // comparison. A numerical miss remains explicitly false, never relabelled passed.
        var reconstruction = CheckM6BooleanReconstruction(beforeBody, afterBody, removed);
        var pilotGeometry = InspectTiffM6Pilots(experimentalPoints);
        Console.WriteLine($"[TIFF M6 OK] four threads, samples={string.Join(',', samples)}, V={removedVolume:F9}, R={minR:F9}..{maxR:F9}.");
        return new { Count = 4, BoltCircleDiameterMm = 95, PhaseDegrees = 45, PitchMm = 1,
            ThreadParameters = parameters,
            NominalThreadOperationDepthMm = extendedStarts?16:15, StartExtensionMm=extendedStarts?1:0, PilotNominalDepthMm = 22, PilotRadiusMm = TiffM6PilotRadius,
            IsolatedRemovedMm3 = removedVolume, WholePartDeltaMm3 = beforeVolume - final, SamplesPerThread = samples,
            VolumeComparisonToleranceMm3 = volumeComparisonTolerance, VolumeRelativeBudget = .0005,
            VolumeComparisonPassed = volumeComparisonPassed, BooleanReconstruction = reconstruction,
            MassDiagnostic = massDiagnostic,
            MinimumRadiusMm = minR, MaximumRadiusMm = maxR, MinimumX = minX, MaximumX = maxX,
            PilotGeometry = pilotGeometry, DrillPointAccepted = false, FullProfileRunoutAccepted = false };
    }

    private object InspectTiffM6Pilots(bool experimentalPoints = false)
    {
        var centres = TiffM6Centres; double r = TiffM6PilotRadius;
        var min = Enumerable.Repeat(double.PositiveInfinity, 4).ToArray();
        var max = Enumerable.Repeat(double.NegativeInfinity, 4).ToArray(); int[] samples = new int[4];
        var body = CopyPilotBody();
        foreach (Face2 face in (object[])body.GetFaces())
        {
            var surface = (Surface)face.GetSurface(); if (!surface.IsCylinder()) continue;
            var c = (double[])surface.CylinderParams;
            if (Math.Abs(c[6] * 1000 - r) > 1e-5 || Math.Abs(Math.Abs(c[3]) - 1) > 1e-8) continue;
            int index = Array.FindIndex(centres, q => Math.Abs(c[1] * 1000 - q.Y) < 1e-5 && Math.Abs(c[2] * 1000 - q.Z) < 1e-5);
            if (index < 0) continue;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var b = edge.GetCurveParams3(); Drawing.M12Study.ValidateEdgeInterval(b.UMinValue, b.UMaxValue);
                for (int j = 0; j <= 32; j++)
                {
                    var p = (double[])edge.Evaluate2(b.UMinValue + (b.UMaxValue - b.UMinValue) * j / 32, 0);
                    if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                        throw new InvalidOperationException("M6 pilot edge evaluation failed.");
                    min[index] = Math.Min(min[index], p[0] * 1000); max[index] = Math.Max(max[index], p[0] * 1000); samples[index]++;
                }
            }
        }
        GC.KeepAlive(body);
        var chamfers = TiffFeatures(Enumerable.Range(1, 4).Select(i => $"TIFF_M6_{i}_CHAMFER_1x45"));
        for (int i = 0; i < 4; i++)
        {
            if (samples[i] == 0 || Math.Abs(min[i] - (experimentalPoints ? M6PointBaseX : 243)) > .0001 || Math.Abs(max[i] - 264) > .0001)
                throw new InvalidOperationException($"M6 pilot extent mismatch: {i + 1}/{min[i]}..{max[i]}");
            int mouths = 0;
            foreach (Face2 face in (object[])chamfers[$"TIFF_M6_{i + 1}_CHAMFER_1x45"].GetFaces())
            {
                if (!((Surface)face.GetSurface()).IsCone()) continue;
                foreach (Edge edge in (object[])face.GetEdges())
                {
                    var curve = (Curve)edge.GetCurve(); if (!curve.IsCircle()) continue;
                    var c = (double[])curve.CircleParams;
                    if (Math.Abs(c[0] * 1000 - 265) < 1e-5 && Math.Abs(c[1] * 1000 - centres[i].Y) < 1e-5 &&
                        Math.Abs(c[2] * 1000 - centres[i].Z) < 1e-5 && Math.Abs(c[6] * 1000 - r - 1) < 1e-5) mouths++;
                }
            }
            if (mouths != 1) throw new InvalidOperationException($"M6 mouth circle mismatch: {i + 1}/{mouths}");
        }
        return new { MinimumXs = min, MaximumXs = max, EdgeSamples = samples, MouthCirclesChecked = 4, MouthRadiusMm = r + 1 };
    }
}

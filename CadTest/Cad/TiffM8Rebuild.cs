using CadTest.Drawing;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectCurrentM8Definitions(bool expectedEndTrim = true)
    {
        var results=new List<object>();
        foreach(bool aa in new[]{false,true})
        {
            string name=$"TIFF_REAR_{(aa?"AA":"II")}_M8_NOMINAL_PENDING_LENGTH";
            var feature=TiffFeature(name);
            if(TiffSuppressed(feature))throw new InvalidOperationException("M8 definition suppressed.");
            var data=(IThreadFeatureData)feature.GetDefinition();
            double expected=6/Math.Cos((aa?4:10)*Math.PI/180)/1000;
            M8DefinitionChecks.Validate(data.Type,data.Size,data.Pitch,data.BlindDepth,expected);
            if(!data.RightHanded||data.MultipleStart||data.ReverseDirection||!data.TrimStartFace||data.TrimEndFace!=expectedEndTrim||
                data.DiameterOverride||!data.PitchOverride||data.ThreadMethod!=(int)swThreadMethod_e.swThreadMethod_Cut||
                data.EndCondition!=(int)swThreadEndCondition_e.swThreadEndCondition_Blind)
                throw new InvalidOperationException("M8 definition flags mismatch.");
            results.Add(new{Feature=name,data.Type,data.Size,data.Pitch,data.BlindDepth,data.DiameterOverride,data.PitchOverride,
                data.RightHanded,data.MultipleStart,data.ReverseDirection,data.TrimStartFace,data.TrimEndFace});
        }
        return new{Definitions=results,NominalLibraryContractChecked=true,FullProfileLengthVerified=false,
            RunoutVerified=false,Tolerance6HVerified=false,GeometryChanged=false};
    }
    private double MeasureRemovedBodiesInScratchPart(object[] bodies)
    {
        var original = _model!; double total = 0;
        foreach (Body2 body in bodies)
        {
            var scratch = (ModelDoc2?)_swApp!.NewPart() ?? throw new InvalidOperationException("Mass scratch part creation failed.");
            string title = scratch.GetTitle();
            try
            {
                var copy = (Body2)body.Copy();
                var feature = ((PartDoc)scratch).CreateFeatureFromBody3(copy, false, (int)swCreateFeatureBodyOpts_e.swCreateFeatureBodyCheck);
                if (feature == null) throw new InvalidOperationException("Mass scratch solid import failed.");
                scratch.ForceRebuild3(false);
                var solids = (object[]?)((PartDoc)scratch).GetBodies2((int)swBodyType_e.swSolidBody, false);
                if (solids?.Length != 1) throw new InvalidOperationException("Expected one scratch solid.");
                var mass = (MassProperty2)scratch.Extension.CreateMassProperty2();
                mass.UseSystemUnits = true; mass.AccuracyLevel = (int)swMassPropertyAccuracyLevel_e.swMassPropertyAccuracyLevel_Higher;
                mass.Recalculate();
                double v = mass.Volume * 1e9;
                if (!double.IsFinite(v) || v <= 0) throw new InvalidOperationException("Invalid scratch volume.");
                total += v; GC.KeepAlive(copy);
            }
            finally
            {
                // Only this newly created diagnostic document is discarded, never the work part.
                _swApp.CloseDoc(title);
                int activationError = 0;
                _swApp.ActivateDoc3(original.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref activationError);
                if (activationError != 0) throw new InvalidOperationException("Cannot reactivate work document after mass check.");
            }
        }
        return total;
    }
    public object InspectTiffM8(bool aa = false, bool rearDatum = false)
    {
        string prefix = (rearDatum ? "TIFF_REAR_" : "TIFF_") + (aa ? "AA_M8_" : "II_M8_");
        double entryX = rearDatum ? 265 : 260;
        double degrees = aa ? 120 : 60, inclination = (aa ? 4 : 10) * Math.PI / 180;
        var thread = TiffFeature(prefix + "NOMINAL_PENDING_LENGTH");
        if (TiffSuppressed(thread)) throw new InvalidOperationException("TIFF M8 is suppressed.");
        double final = CurrentTrialVolume();
        Body2 beforeBody;
        double beforeVolume;
        try
        {
            if (!thread.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration, null))
                throw new InvalidOperationException("Cannot isolate TIFF M8 for verification.");
            _model!.ForceRebuild3(false);
            beforeBody = CopyPilotBody(); beforeVolume = CurrentTrialVolume();
        }
        finally
        {
            if (!thread.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration, null))
                throw new InvalidOperationException("Cannot restore TIFF M8 after verification.");
            _model!.ForceRebuild3(false);
        }
        if (Math.Abs(CurrentTrialVolume() - final) > .001)
            throw new InvalidOperationException("M8 verification did not restore the original volume.");
        var removal = InspectTiffM8Removal(beforeBody, beforeVolume, degrees, inclination, entryX);
        var geometry = InspectM8ThreadFeature(thread, degrees, inclination, entryX, 51,
            planarInteriorValidatedByBodyDifference: true, startX: entryX - 6, planarMouth: rearDatum);
        return new { Geometry = geometry, IsolatedRemoval = removal };
    }

    public object RebuildTiffM8(bool aa = false, bool rearDatum = false)
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        string prefix = (rearDatum ? "TIFF_REAR_" : "TIFF_") + (aa ? "AA_M8_" : "II_M8_");
        double entryX = rearDatum ? 265 : 260, startX = entryX - 6;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name.StartsWith(prefix)) throw new InvalidOperationException("TIFF M8 already present.");
        _ = aa ? InspectTiffAaChannel(rearDatum) : InspectTiffIiChannel(rearDatum);
        double before = CurrentTrialVolume();
        double degrees = aa ? 120 : 60, a = degrees * Math.PI / 180, b = (aa ? 4 : 10) * Math.PI / 180;
        double[] At(double x) { double r = 51 + (x - entryX) * Math.Tan(b); return [x, r * Math.Sin(a), -r * Math.Cos(a)]; }
        CutChannelCylinder(prefix + "PILOT_PENDING_LENGTH", At(entryX + (rearDatum ? 2 : 0)), At(startX), M8PilotStudy.Radius,
            [0, Math.Cos(a), Math.Sin(a)]);
        double afterPilot = CurrentTrialVolume();
        double[] centre = At(startX);
        var edges = new List<Edge>();
        foreach (Face2 face in (object[])TiffFeature(prefix + "PILOT_PENDING_LENGTH").GetFaces())
        {
            if (!((Surface)face.GetSurface()).IsCylinder()) continue;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var curve = (Curve)edge.GetCurve(); if (!curve.IsCircle()) continue;
                var c = (double[])curve.CircleParams;
                if (Math.Abs(c[6] * 1000 - M8PilotStudy.Radius) < 1e-5 &&
                    Enumerable.Range(0, 3).All(i => Math.Abs(c[i] * 1000 - centre[i]) < 1e-5)) edges.Add(edge);
            }
        }
        if (edges.Count != 1) throw new InvalidOperationException($"Expected one TIFF M8 start circle: {edges.Count}");
        var data = (IThreadFeatureData)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweepThread);
        data.InitializeThreadData(); data.Edge = edges[0]; data.Type = "Metric Tap.SLDLFP"; data.Size = "M8x1.25";
        data.EndCondition = (int)swThreadEndCondition_e.swThreadEndCondition_Blind;
        data.BlindDepth = 6 / Math.Cos(b) / 1000;
        data.ThreadMethod = (int)swThreadMethod_e.swThreadMethod_Cut;
        data.RightHanded = true; data.MultipleStart = false; data.PitchOverride = true; data.Pitch = .00125;
        data.ReverseDirection = false; data.TrimStartFace = true; data.TrimEndFace = true;
        var beforeThreadBody = CopyPilotBody();
        _model.ClearSelection2(true);
        var thread = (Feature?)_model.FeatureManager.CreateFeature(data) ?? throw new InvalidOperationException("TIFF M8 creation failed.");
        thread.Name = prefix + "NOMINAL_PENDING_LENGTH";
        _model.ForceRebuild3(false);
        var removal = InspectTiffM8Removal(beforeThreadBody, afterPilot, degrees, b, entryX);
        var geometry = InspectM8ThreadFeature(thread, degrees, b, entryX, 51,
            planarInteriorValidatedByBodyDifference: true, startX: startX, planarMouth: rearDatum);
        var channel = aa ? InspectTiffAaChannel(rearDatum) : InspectTiffIiChannel(rearDatum);
        double final = CurrentTrialVolume();
        if (before <= afterPilot || afterPilot - final < 1 || afterPilot - final > 150)
            throw new InvalidOperationException("Unexpected TIFF M8 removed volume.");
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (!TiffSuppressed(f))
            {
                int error = f.GetErrorCode2(out bool warning);
                if (error != 0 || warning)
                    throw new InvalidOperationException($"Active feature issue: {f.Name}/{error}/{warning}");
            }
        return new { BeforeMm3 = before, AfterPilotMm3 = afterPilot, FinalMm3 = final,
            Geometry = geometry, IsolatedRemoval = removal, Channel = channel, BranchDegrees = degrees,
            RightM8Rebuilt = !aa, LeftM8Rebuilt = aa,
            TiffThreadLengthAccepted = false, Tolerance6HAccepted = false, IsManufacturingReady = false,
            EntryX = entryX, TemporaryAxialSpanMm = 6,
            Scope = "Nominal M8 on selected branch axis. Temporary6mm axial span is not read from TIFF; full profile length/runout/6H/sealing unaccepted. Inherited TIFF body conversion incomplete." };
    }

    private object InspectTiffM8Removal(Body2 beforeThreadBody, double afterPilot, double degrees, double inclination, double entryX)
    {
        double a = degrees * Math.PI / 180, b = inclination, startRadius = 51 - 6 * Math.Tan(b);
        double[] centre = [entryX - 6, startRadius * Math.Sin(a), -startRadius * Math.Cos(a)];
        var finalBody = CopyPilotBody();
        double beforeBodyVolume = ((double[])beforeThreadBody.GetMassProperties(1))[3] * 1e9;
        double finalBodyVolume = ((double[])finalBody.GetMassProperties(1))[3] * 1e9;
        var removed = (object[]?)beforeThreadBody.Operations2((int)swBodyOperationType_e.SWBODYCUT,
            finalBody, out int booleanError);
        if (booleanError != 0 || removed == null || removed.Length == 0)
            throw new InvalidOperationException($"M8 removed-material boolean failed: {booleanError}.");
        double removedVolume = removed.Cast<Body2>().Sum(body => ((double[])body.GetMassProperties(1))[3] * 1e9);
        double defaultRemovedVolume = removedVolume;
        Console.WriteLine($"[M8 MASS] removed={removedVolume:F9}, same-body-calculator delta={beforeBodyVolume - finalBodyVolume:F9}, document-high delta={afterPilot - CurrentTrialVolume():F9}.");
        removedVolume = MeasureRemovedBodiesInScratchPart(removed);
        Console.WriteLine($"[M8 MASS SCRATCH HIGH] {removedVolume:F9}");
        double[] axis = [Math.Cos(b), Math.Sin(b) * Math.Sin(a), -Math.Sin(b) * Math.Cos(a)];
        double minR = double.PositiveInfinity, maxR = 0, minS = double.PositiveInfinity, maxS = double.NegativeInfinity;
        int sampleCount = 0;
        double maxX = double.NegativeInfinity;
        foreach (Body2 body in removed)
        foreach (Edge edge in (object[])body.GetEdges())
        {
            var bounds = edge.GetCurveParams3();
            M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
            for (int j = 0; j <= 64; j++)
            {
                var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * j / 64, 0);
                if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                    throw new InvalidOperationException("M8 removed-material edge evaluation failed.");
                var d = Enumerable.Range(0, 3).Select(i => p[i] * 1000 - centre[i]).ToArray();
                double s = d.Zip(axis).Sum(v => v.First * v.Second);
                double r = Math.Sqrt(d.Select((v, i) => Math.Pow(v - s * axis[i], 2)).Sum());
                maxX = Math.Max(maxX, p[0] * 1000);
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                minS = Math.Min(minS, s); maxS = Math.Max(maxS, s); sampleCount++;
            }
        }
        Console.WriteLine($"[DIAGNOSTIC] M8 isolated removal: V={removedVolume:F9}, bodies={removed.Length}, samples={sampleCount}, R={minR:F9}..{maxR:F9}, S={minS:F9}..{maxS:F9}; whole-part delta={afterPilot - CurrentTrialVolume():F9}.");
        // Comparison of separately integrated curved B-reps is a numerical consistency
        // check, not a drawing tolerance. Retain the absolute floor and explicit 0.05%
        // relative budget for removed material; coordinate/radius bounds stay unchanged.
        double volumeComparisonTolerance = Math.Max(.01, .0005 * Math.Abs(removedVolume));
        if (sampleCount == 0 || !double.IsFinite(removedVolume) || removedVolume <= 0 ||
            Math.Abs(removedVolume - (afterPilot - CurrentTrialVolume())) > volumeComparisonTolerance ||
            minR < M8PilotStudy.Radius - .0001 || maxR > 4.0001 || minS < -.0001 ||
            maxS > 6 / Math.Cos(b) + (entryX == 265 ? 4 * Math.Abs(Math.Tan(b)) : 0) + .0001 ||
            (entryX == 265 && maxX > entryX + .0001))
            throw new InvalidOperationException("M8 isolated removed material outside the pilot/thread envelope.");
        return new { RemovedMm3 = removedVolume, WholePartDeltaMm3 = afterPilot - CurrentTrialVolume(),
            DefaultBodyRemovedMm3 = defaultRemovedVolume,
            VolumeComparisonToleranceMm3 = volumeComparisonTolerance, VolumeRelativeBudget = .0005,
            RemovedBodies = removed.Length, EdgeSamples = sampleCount, MinimumRadiusMm = minR, MaximumRadiusMm = maxR,
            MinimumAxialMm = minS, MaximumAxialMm = maxS, MaximumGlobalX = maxX,
            Scope = "Sampled edges of isolated removed solid bodies; planar rear mouth checked in globalX, oblique end envelope derived from radius. Not exact surface-extrema or tolerance certification." };
    }
}

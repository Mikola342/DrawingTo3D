using CadTest.Drawing;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectM8Threads()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var reports = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            var matches = new List<Feature>();
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == $"TRIAL_M8_thread_{degrees}") matches.Add(f);
            if (matches.Count != 1) throw new InvalidOperationException("Expected exactly one M8 per branch.");
            reports.Add(new { AngleDegrees = degrees, Geometry = InspectM8ThreadFeature(matches[0], degrees) });
        }
        return reports;
    }

    public object CreateM8ThreadTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var reports = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            Feature? existing = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == $"TRIAL_M8_thread_{degrees}") existing = f;
            if (existing != null)
            {
                reports.Add(new { AngleDegrees = degrees, ExistingOperation = true, Geometry = InspectM8ThreadFeature(existing, degrees) });
                continue;
            }
            InspectPilotGeometry(degrees);
            double a = degrees * Math.PI / 180, r = ChannelTrialStudy.CentreRadius(254);
            double[] centre = [254, r * Math.Sin(a), -r * Math.Cos(a)];
            var edges = new List<Edge>();
            Feature? pilot = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == $"TRIAL_M8_pilot_{degrees}") pilot = f;
            if (pilot == null) throw new InvalidOperationException("Missing M8 pilot feature.");
            foreach (Face2 face in (object[])pilot.GetFaces())
            {
                if (!((Surface)face.GetSurface()).IsCylinder()) continue;
                foreach (Edge edge in (object[])face.GetEdges())
                {
                    var curve = (Curve)edge.GetCurve();
                    if (!curve.IsCircle()) continue;
                    var c = (double[])curve.CircleParams;
                    if (Math.Abs(c[6] * 1000 - M8PilotStudy.Radius) < 1e-5 &&
                        Enumerable.Range(0, 3).All(i => Math.Abs(c[i] * 1000 - centre[i]) < 1e-5)) edges.Add(edge);
                }
            }
            if (edges.Count != 1) throw new InvalidOperationException($"Expected one M8 start edge, found {edges.Count}.");
            var data = (IThreadFeatureData)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweepThread);
            data.InitializeThreadData();
            data.Edge = edges[0]; data.Type = "Metric Tap.SLDLFP"; data.Size = "M8x1.25";
            data.EndCondition = (int)swThreadEndCondition_e.swThreadEndCondition_Blind;
            data.BlindDepth = 6 / Math.Cos(ChannelTrialStudy.Angle) / 1000;
            data.ThreadMethod = (int)swThreadMethod_e.swThreadMethod_Cut;
            data.RightHanded = true; data.MultipleStart = false;
            data.PitchOverride = true; data.Pitch = .00125;
            data.ReverseDirection = false;
            data.TrimStartFace = true; data.TrimEndFace = true;
            _model.ClearSelection2(true);
            double before = CurrentTrialVolume();
            var feature = (Feature?)_model.FeatureManager.CreateFeature(data);
            if (feature == null) throw new InvalidOperationException("M8 library thread creation failed.");
            feature.Name = $"TRIAL_M8_thread_{degrees}";
            _model.ForceRebuild3(false);
            int error = feature.GetErrorCode2(out bool warning);
            if (error != 0) throw new InvalidOperationException($"M8 feature error {error}, warning={warning}.");
            double removal = before - CurrentTrialVolume();
            double bound = Math.PI * (4.1 * 4.1 - M8PilotStudy.Radius * M8PilotStudy.Radius) * 7;
            if (removal < 1 || removal > bound) throw new InvalidOperationException($"M8 thread removal {removal}.");
            var check = InspectM8ThreadFeature(feature, degrees);
            reports.Add(new { AngleDegrees = degrees, RemovedMm3 = removal, ConservativeEnvelopeMm3 = bound, Geometry = check });
        }
        return new { ActualVolumeMm3 = CurrentTrialVolume(), Threads = reports,
            Scope = "Nominal M8x1.25 right-hand single-start library threads; experimental 6/cos4 length. Not 6H tolerance, tap runout or sealing certification." };
    }

    private object InspectM8ThreadFeature(Feature feature, double degrees, double? inclination = null,
        double anchorX = 265, double anchorRadius = 51, bool planarInteriorValidatedByBodyDifference = false, double startX = 254,
        bool planarMouth = false)
    {
        int error = feature.GetErrorCode2(out bool warning);
        if (error != 0 || warning) throw new InvalidOperationException($"M8 error={error}, warning={warning}.");
        double a = degrees * Math.PI / 180, b = inclination ?? ChannelTrialStudy.Angle;
        double r = anchorRadius + (startX - anchorX) * Math.Tan(b);
        double[] origin = [startX, r * Math.Sin(a), -r * Math.Cos(a)];
        double[] axis = [Math.Cos(b), Math.Sin(b) * Math.Sin(a), -Math.Sin(b) * Math.Cos(a)];
        double minS = double.PositiveInfinity, maxS = double.NegativeInfinity, minR = double.PositiveInfinity, maxR = 0;
        double minThreadRadius = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        int samples = 0;
        foreach (Face2 face in (object[])feature.GetFaces())
        {
            bool plane = ((Surface)face.GetSurface()).IsPlane();
            double faceMinR = double.PositiveInfinity, faceMaxR = 0;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var bounds = edge.GetCurveParams3();
                M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
                for (int j = 0; j <= 32; j++)
                {
                    var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * j / 32, 0);
                    if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                        throw new InvalidOperationException("M8 edge evaluation failed.");
                    var d = Enumerable.Range(0, 3).Select(i => p[i] * 1000 - origin[i]).ToArray();
                    double s = d.Zip(axis).Sum(v => v.First * v.Second);
                    double rho = Math.Sqrt(d.Select((v, i) => Math.Pow(v - s * axis[i], 2)).Sum());
                    maxX = Math.Max(maxX, p[0] * 1000);
                    minS = Math.Min(minS, s); maxS = Math.Max(maxS, s);
                    minR = Math.Min(minR, rho); maxR = Math.Max(maxR, rho); samples++;
                    faceMinR = Math.Min(faceMinR, rho); faceMaxR = Math.Max(faceMaxR, rho);
                    if (!plane) minThreadRadius = Math.Min(minThreadRadius, rho);
                }
            }
            Console.WriteLine($"[AUDIT] M8 face plane={plane}, R={faceMinR:F9}..{faceMaxR:F9}.");
        }
        Console.WriteLine($"[AUDIT] M8 {degrees}: {samples} samples; S={minS:F9}..{maxS:F9}, R={minR:F9}..{maxR:F9}.");
        double length = 6 / Math.Cos(b);
        // An oblique cylinder clipped by the axial face X=anchorX has no constant-S end.
        // X=startX+S*cos(b)+qX and |qX|<=4*sin(b), hence S<=length+4*tan(b).
        bool endInvalid = planarMouth
            ? maxX > anchorX + .0001 || maxS > length + 4 * Math.Abs(Math.Tan(b)) + .0001 || maxS < length - 4 * Math.Abs(Math.Tan(b)) - .0001
            : Math.Abs(maxS - length) > .0001;
        if (samples == 0 || Math.Abs(minS) > .0001 || endInvalid ||
            (!planarInteriorValidatedByBodyDifference && minR < 3.15 - .0001) || Math.Abs(minThreadRadius - M8PilotStudy.Radius) > .0001 || Math.Abs(maxR - 4) > .0001)
            throw new InvalidOperationException("M8 thread lies outside the intended envelope.");
        var data = (IThreadFeatureData)feature.GetDefinition();
        M8DefinitionChecks.Validate(data.Type,data.Size,data.Pitch,data.BlindDepth,length/1000);
        if (Math.Abs(data.Pitch - .00125) > 1e-9 || !data.RightHanded || data.MultipleStart ||
            Math.Abs(data.BlindDepth - length / 1000) > 1e-9 || data.ThreadMethod != (int)swThreadMethod_e.swThreadMethod_Cut ||
            data.Size != "M8x1.25" || data.ReverseDirection || !data.TrimStartFace || !data.TrimEndFace ||
            data.EndCondition != (int)swThreadEndCondition_e.swThreadEndCondition_Blind)
            throw new InvalidOperationException("M8 persisted parameters mismatch.");
        return new { NominalLibraryContractChecked = true, FullProfileLengthVerified = false,
            Samples = samples, MinAlongAxisMm = minS, MaxAlongAxisMm = maxS, MinRadiusMm = minR,
            MaxRadiusMm = maxR, MinNonPlanarRadiusMm = minThreadRadius, PlanarInteriorValidatedByBodyDifference = planarInteriorValidatedByBodyDifference,
            PlanarMouth = planarMouth, MaximumGlobalX = maxX,
            data.Type, data.Size, data.Pitch, data.BlindDepth, data.ReverseDirection };
    }
}

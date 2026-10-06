using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private static readonly string[] OldAaCuts = ["TRIAL_M8_thread_120", "TRIAL_M8_pilot_120", "TRIAL_D68_feed_120",
        "TRIAL_D6_entry_cone_120", "TRIAL_D6_radial_120", "TRIAL_D63_extended_120"];

    public object InspectTiffAaChannel(bool rearDatum = false, bool experimentalPoints = false, bool feedPoints = false, bool radialPoint = false)
    {
        if (experimentalPoints && !rearDatum) throw new ArgumentException("Experimental channel points require the rear X265 datum.");
        if (feedPoints && (!rearDatum || !experimentalPoints)) throw new ArgumentException("Feed points require the experimental main-channel revision.");
        if (radialPoint && !feedPoints) throw new ArgumentException("Radial point requires the feed-point revision.");
        string prefix = rearDatum ? "TIFF_REAR_AA_" : "TIFF_AA_";
        double entryX = rearDatum ? 265 : 260;
        string mainName = experimentalPoints ? "EXPERIMENT_AA_POINT_CYLINDER" : prefix + "D63_4deg";
        string feedName = feedPoints ? "EXPERIMENT_AA_FEED_CYLINDER" : prefix + "D68_7deg";
        string radialName = radialPoint ? "EXPERIMENT_AA_RADIAL_CYLINDER" : prefix + "D6_X132";
        var required = OldAaCuts.Concat(new[] { mainName, radialName, feedName }).ToHashSet();
        var features = new Dictionary<string, Feature>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (required.Contains(f.Name) && !features.TryAdd(f.Name, f)) throw new InvalidOperationException("Duplicate AA feature.");
        if (features.Count != required.Count) throw new InvalidOperationException("Missing AA feature.");
        foreach (string name in OldAaCuts)
            if (!TiffSuppressed(features[name])) throw new InvalidOperationException($"Old AA feature active: {name}");
        double a = 2 * Math.PI / 3, b = 4 * Math.PI / 180, feedAngle = 7 * Math.PI / 180;
        double[] At(double x, double r) => [x, r * Math.Sin(a), -r * Math.Cos(a)];
        int Check(string name, double radius, double[] origin, double[] axis, bool checkNeighbors)
        {
            var feature = features[name];
            if (TiffSuppressed(feature)) throw new InvalidOperationException($"AA feature suppressed: {name}");
            int count = 0, radialEdges = 0, feedEdges = 0;
            foreach (Face2 face in (object[])feature.GetFaces())
            {
                var surface = (Surface)face.GetSurface();
                if (!surface.IsCylinder()) continue;
                var c = (double[])surface.CylinderParams;
                if (Math.Abs(c[6] * 1000 - radius) > 1e-5) continue;
                var d = Enumerable.Range(0, 3).Select(i => c[i] * 1000 - origin[i]).ToArray();
                double dot = Enumerable.Range(0, 3).Sum(i => axis[i] * c[i + 3]);
                double projection = d.Zip(axis).Sum(p => p.First * p.Second);
                if (Math.Abs(Math.Abs(dot) - 1) > 1e-8 ||
                    Math.Sqrt(d.Select((v, i) => Math.Pow(v - projection * axis[i], 2)).Sum()) > 1e-4)
                    throw new InvalidOperationException($"AA axis/radius mismatch: {name}");
                count++;
                if (!checkNeighbors) continue;
                foreach (Edge edge in (object[])face.GetEdges())
                foreach (Face2 adjacent in ((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>())
                {
                    var s = (Surface)adjacent.GetSurface(); if (!s.IsCylinder()) continue;
                    var p = (double[])s.CylinderParams;
                    if (Math.Abs(p[6] * 1000 - 3) < 1e-5) radialEdges++;
                    if (Math.Abs(p[6] * 1000 - 3.4) < 1e-5) feedEdges++;
                }
            }
            if (count == 0 || (checkNeighbors && (radialEdges == 0 || feedEdges == 0)))
                throw new InvalidOperationException($"AA connectivity failed: {name}/{count}/{radialEdges}/{feedEdges}");
            Console.WriteLine($"[TIFF AA OK] {name}: cylinders={count}, radial edges={radialEdges}, feed edges={feedEdges}.");
            return count;
        }
        var main = Check(mainName, 3.15, At(entryX, 51), [Math.Cos(b), Math.Sin(b) * Math.Sin(a), -Math.Sin(b) * Math.Cos(a)], true);
        var radial = Check(radialName, 3, At(132, 0), [0, Math.Sin(a), -Math.Cos(a)], false);
        var feed = Check(feedName, 3.4, At(257.5, 127), [Math.Sin(feedAngle), Math.Cos(feedAngle) * Math.Sin(a), -Math.Cos(feedAngle) * Math.Cos(a)], false);
        return new { MainCylinders = main, RadialCylinders = radial, FeedCylinders = feed, RadialX = 132,
            InclinationDegrees = 4, EntryX = entryX, AxesAndConnectionsChecked = true, DrillBottomGeometryAccepted = false };
    }

    public object RebuildTiffAaChannel()
    {
        Drawing.TiffAaStudy.RunChecks();
        var prior = new Dictionary<string, bool>(); var features = new Dictionary<string, Feature>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_AA_")) throw new InvalidOperationException("AA already rebuilt.");
            prior.Add(f.Name, TiffSuppressed(f));
            if (OldAaCuts.Contains(f.Name)) features.Add(f.Name, f);
        }
        if (features.Count != OldAaCuts.Length || OldAaCuts.Any(name => prior[name]))
            throw new InvalidOperationException("Unexpected old AA state.");
        double before = CurrentTrialVolume();
        foreach (string name in OldAaCuts)
        {
            if (!features[name].SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration, null)) throw new InvalidOperationException($"AA suppression failed: {name}");
            Console.WriteLine($"[TIFF AA] suppressed {name}");
        }
        _model.ForceRebuild3(false); CheckTiffSuppressionStates(prior, OldAaCuts);
        double restored = CurrentTrialVolume();
        if (restored <= before) throw new InvalidOperationException("Old AA material not restored.");
        double a = 2 * Math.PI / 3, b = 4 * Math.PI / 180;
        double RadiusAt(double x) => 51 + (x - 260) * Math.Tan(b);
        double[] At(double x, double r) => [x, r * Math.Sin(a), -r * Math.Cos(a)];
        double[] tangent = [0, Math.Cos(a), Math.Sin(a)];
        // Working flat-ended cutter extends 0.25mm past the D6 near-side envelope.
        // This is a construction choice, NOT a drill-depth dimension read from TIFF.
        const double radialX = 265 - 26 - 107, endX = radialX - 3.25;
        CutChannelCylinder("TIFF_AA_D63_4deg", At(260, 51), At(endX, RadiusAt(endX)), 3.15, tangent);
        CutChannelCylinder("TIFF_AA_D6_X132", At(radialX, 55), At(radialX, RadiusAt(radialX)), 3, tangent);
        double tf = Math.Tan(7 * Math.PI / 180), tm = Math.Tan(b);
        double crossing = (51 + (257.5 - 127 * tf - 260) * tm) / (1 - tf * tm);
        double FeedX(double r) => 257.5 + (r - 127) * tf;
        CutChannelCylinder("TIFF_AA_D68_7deg", At(FeedX(130), 130), At(FeedX(crossing - 1), crossing - 1), 3.4, tangent);
        var geometry = InspectTiffAaChannel();
        var m8 = RebuildTiffM8(aa: true);
        _ = InspectTiffIiChannel();
        CheckTiffSuppressionStates(prior, OldAaCuts);
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (!TiffSuppressed(f))
            {
                int error = f.GetErrorCode2(out bool warning);
                if (error != 0 || warning) throw new InvalidOperationException($"AA feature issue: {f.Name}/{error}/{warning}");
            }
        return new { BeforeMm3 = before, RestoredMm3 = restored, FinalMm3 = CurrentTrialVolume(), Geometry = geometry, M8 = m8,
            AaChannelRebuilt = true, LeftM8Rebuilt = true, RightM8Rebuilt = true, Bore80Restored = true,
            Bore80PartialRebuilt = true, Bore68StartRebuilt = true, DetailLCoreBuilt = true, IsManufacturingReady = false,
            Scope = "AA nominal axes rebuilt at X132/4deg; old X108 network suppressed. Flat drill ends, chosen endpoint X128.75, feed datum, M8 length/6H and radial mouth detail are not manufacturing-accepted." };
    }
}

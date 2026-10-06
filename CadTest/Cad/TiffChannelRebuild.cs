using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private Dictionary<string, Feature> TiffFeatures(IEnumerable<string> names)
    {
        var required = names.ToHashSet(); var result = new Dictionary<string, Feature>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (required.Contains(f.Name) && !result.TryAdd(f.Name, f)) throw new InvalidOperationException($"Duplicate feature: {f.Name}");
        if (result.Count != required.Count) throw new InvalidOperationException("Missing features: " + string.Join(", ", required.Except(result.Keys)));
        return result;
    }
    private Feature TiffFeature(string name)
    {
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name == name) return f;
        throw new InvalidOperationException($"Missing feature {name}");
    }
    private static bool TiffSuppressed(Feature f)
    {
        var states = (Array)f.IsSuppressed2((int)swInConfigurationOpts_e.swThisConfiguration, null);
        if (states.Length != 1) throw new InvalidOperationException("Expected one active configuration suppression state.");
        return Convert.ToBoolean(states.GetValue(0));
    }
    private void CheckTiffSuppressionStates(Dictionary<string, bool> prior, string[] replaced)
    {
        var seen = new HashSet<string>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            string name = f.Name;
            if (!prior.TryGetValue(name, out bool wasSuppressed)) continue;
            if (!seen.Add(name) || TiffSuppressed(f) != (replaced.Contains(name) || wasSuppressed))
                throw new InvalidOperationException($"Unexpected dependent suppression: {name}");
        }
        if (seen.Count != prior.Count) throw new InvalidOperationException("An inherited feature disappeared.");
    }

    public object RebuildTiffIiChannel()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        string[] replaced = ["TRIAL_M8_thread_60", "TRIAL_M8_pilot_60", "TRIAL_D68_feed_60",
            "TRIAL_D6_entry_cone_60", "TRIAL_D6_radial_60", "TRIAL_D63_extended_60"];
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            prior.Add(f.Name, TiffSuppressed(f));
        if (prior.ContainsKey("TIFF_II_D63_10deg")) throw new InvalidOperationException("Already rebuilt.");
        foreach (string name in replaced)
            if (!prior.ContainsKey(name) || prior[name]) throw new InvalidOperationException($"Unexpected initial state {name}");
        double before = CurrentTrialVolume();
        foreach (string name in replaced)
        {
            if (!TiffFeature(name).SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration, null))
                throw new InvalidOperationException($"Suppression failed {name}");
            Console.WriteLine($"[TIFF] Suppressed reversibly: {name}");
        }
        _model.ForceRebuild3(false);
        CheckTiffSuppressionStates(prior, replaced);
        double restored = CurrentTrialVolume();
        if (restored <= before) throw new InvalidOperationException("Old cuts did not restore material.");
        const double az = Math.PI / 3, inclination = Math.PI / 18;
        // TIFF: 273 overall including rear boss, mouth plane 13 inward -> X260.
        // Nominal R51 mouth station, nominal lower limit of length110+1.4.
        double[] At(double x, double r) => [x, r * Math.Sin(az), -r * Math.Cos(az)];
        double[] tangent = [0, Math.Cos(az), Math.Sin(az)];
        CutChannelCylinder("TIFF_II_D63_10deg", At(260, 51),
            At(260 - 110 * Math.Cos(inclination), 51 - 110 * Math.Sin(inclination)), 3.15, tangent);
        double afterMain = CurrentTrialVolume();
        // Existing 7 degree feed entry datum retained pending full TIFF outer-profile rebuild.
        double tanFeed = Math.Tan(7 * Math.PI / 180), tanMain = Math.Tan(inclination);
        double crossingRadius = (51 + (257.5 - 127 * tanFeed - 260) * tanMain) / (1 - tanFeed * tanMain);
        double FeedX(double r) => 257.5 + (r - 127) * tanFeed;
        CutChannelCylinder("TIFF_II_D68_feed_7deg", At(FeedX(130), 130),
            At(FeedX(crossingRadius - 1), crossingRadius - 1), 3.4, tangent);
        var geometry = InspectTiffIiChannel();
        CheckTiffSuppressionStates(prior, replaced);
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (!TiffSuppressed(f) && f.GetErrorCode2(out bool warning) != 0)
                throw new InvalidOperationException($"Active feature error: {f.Name}");
        return new { BeforeMm3 = before, RestoredMm3 = restored, AfterMainMm3 = afterMain,
            FinalMm3 = CurrentTrialVolume(), SuppressedFeatures = replaced, Geometry = geometry,
            MainEntryX = 260, MainEntryRadius = 51, LengthMm = 110, InclinationDegrees = 10,
            FeedCrossingRadius = crossingRadius, FeedCrossingX = FeedX(crossingRadius),
            RightM8Rebuilt = false, FullTiffBodyRebuilt = false, IsManufacturingReady = false,
            Scope = "Transitional TIFF channel revision only. Right M8 intentionally absent pending rebuild; other body dimensions still historical PDF. Flat tool end, nominal datums; no thread, tolerance or seal acceptance." };
    }

    public object InspectTiffIiChannel(bool rearDatum = false, bool experimentalPoints = false)
    {
        if (experimentalPoints && !rearDatum) throw new ArgumentException("Experimental channel points require the rear X265 datum.");
        string prefix = rearDatum ? "TIFF_REAR_II_" : "TIFF_II_";
        double entryX = rearDatum ? 265 : 260;
        string[] oldNames = ["TRIAL_M8_thread_60", "TRIAL_M8_pilot_60", "TRIAL_D68_feed_60",
            "TRIAL_D6_entry_cone_60", "TRIAL_D6_radial_60", "TRIAL_D63_extended_60"];
        // One traversal: repeated COM tree scans made every fresh verification unnecessarily slow.
        string mainName = experimentalPoints ? "EXPERIMENT_II_POINT_CYLINDER" : prefix + "D63_10deg";
        var required = oldNames.Append(mainName).ToHashSet();
        var found = new Dictionary<string, Feature>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            string name = f.Name;
            if (required.Contains(name) && !found.TryAdd(name, f))
                throw new InvalidOperationException($"Duplicate TIFF feature: {name}");
        }
        if (found.Count != required.Count) throw new InvalidOperationException("Missing required TIFF channel feature.");
        foreach (string name in oldNames)
            if (!TiffSuppressed(found[name])) throw new InvalidOperationException($"Old II cut is still active: {name}");
        const double a = Math.PI / 3, b = Math.PI / 18;
        double[] axis = [Math.Cos(b), Math.Sin(b) * Math.Sin(a), -Math.Sin(b) * Math.Cos(a)];
        double[] origin = [entryX, 51 * Math.Sin(a), -51 * Math.Cos(a)];
        int cylinders = 0, boreEdges = 0, feedEdges = 0;
        var feature = found[mainName];
        if (TiffSuppressed(feature)) throw new InvalidOperationException("TIFF II channel is suppressed.");
        foreach (Face2 face in (object[])feature.GetFaces())
        {
            var surface = (Surface)face.GetSurface();
            if (!surface.IsCylinder()) continue;
            var c = (double[])surface.CylinderParams;
            if (Math.Abs(c[6] * 1000 - 3.15) > 1e-5) continue;
            double dot = Enumerable.Range(0, 3).Sum(i => axis[i] * c[i + 3]);
            var d = Enumerable.Range(0, 3).Select(i => c[i] * 1000 - origin[i]).ToArray();
            double projection = d.Zip(axis).Sum(v => v.First * v.Second);
            if (Math.Abs(Math.Abs(dot) - 1) > 1e-8 ||
                Math.Sqrt(d.Select((v, i) => Math.Pow(v - projection * axis[i], 2)).Sum()) > 1e-4)
                throw new InvalidOperationException("TIFF main cylinder axis mismatch.");
            cylinders++;
            foreach (Edge edge in (object[])face.GetEdges())
                foreach (Face2 adjacent in ((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>())
                {
                    var s = (Surface)adjacent.GetSurface();
                    if (!s.IsCylinder()) continue;
                    var p = (double[])s.CylinderParams;
                    if (Math.Abs(p[6] * 1000 - 31) < 1e-5 && Math.Abs(Math.Abs(p[3]) - 1) < 1e-8
                        && Math.Abs(p[1]) < 1e-7 && Math.Abs(p[2]) < 1e-7) boreEdges++;
                    if (Math.Abs(p[6] * 1000 - 3.4) < 1e-5) feedEdges++;
                }
        }
        if (cylinders == 0 || boreEdges == 0 || feedEdges == 0)
            throw new InvalidOperationException($"TIFF connectivity failed: cylinders={cylinders}, bore={boreEdges}, feed={feedEdges}");
        Console.WriteLine($"[TIFF OK] 10deg/D6.3 cylinder; bore edges={boreEdges}; feed edges={feedEdges}.");
        return new { Cylinders = cylinders, SharedEdgesWithD62Bore = boreEdges, SharedEdgesWithD68Feed = feedEdges,
            AxisChecked = true, EntryX = entryX, ToolLengthVerifiedFromSavedParameters = false };
    }
}

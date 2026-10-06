using CadTest.Drawing;
using CadTest.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private void CreateTiffExteriorEnvelope(bool cut)
    {
        _model!.ClearSelection2(true);
        if (!_model.Extension.SelectByID2("Спереди", "PLANE", 0, 0, 0, false, 0, null, 0))
            throw new InvalidOperationException("Exterior front plane selection failed.");
        var p = TiffExteriorStudy.Profile(); double boundary = cut ? 130 : 30;
        var sm = _model.SketchManager; sm.InsertSketch(true);
        using (var exact = new ExactSketchCoordinates(sm))
        {
            var axis = sm.CreateLine(0, 0, 0, .273, 0, 0) ?? throw new InvalidOperationException("Exterior axis failed.");
            axis.ConstructionGeometry = true;
            void Line(double x0, double r0, double x1, double r1)
            {
                if (sm.CreateLine(x0 / 1000, r0 / 1000, 0, x1 / 1000, r1 / 1000, 0) == null)
                    throw new InvalidOperationException("Exterior profile line failed.");
            }
            Line(p[0].StartX, boundary, p[0].StartX, p[0].StartRadius);
            foreach (var e in p)
            {
                if (e.Type != RevolveProfileElementType.Arc) { Line(e.StartX, e.StartRadius, e.EndX, e.EndRadius); continue; }
                double a0 = Math.Atan2(e.StartRadius - e.CenterRadius, e.StartX - e.CenterX);
                double delta = Math.Atan2(e.EndRadius - e.CenterRadius, e.EndX - e.CenterX) - a0;
                if (e.Clockwise) { while (delta >= 0) delta -= 2 * Math.PI; }
                else { while (delta <= 0) delta += 2 * Math.PI; }
                double mid = a0 + delta / 2;
                if (sm.Create3PointArc(e.StartX / 1000, e.StartRadius / 1000, 0, e.EndX / 1000, e.EndRadius / 1000, 0,
                    (e.CenterX + e.Radius * Math.Cos(mid)) / 1000, (e.CenterRadius + e.Radius * Math.Sin(mid)) / 1000, 0) == null)
                    throw new InvalidOperationException("Exterior profile arc failed.");
            }
            Line(p[^1].EndX, p[^1].EndRadius, p[^1].EndX, boundary);
            Line(p[^1].EndX, boundary, p[0].StartX, boundary);
        }
        sm.InsertSketch(true);
        Feature? sketch = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "ProfileFeature") sketch = f;
        _model.ClearSelection2(true);
        if (sketch == null || !sketch.Select2(false, 0) ||
            !_model.Extension.SelectByID2($"Line1@{sketch.Name}", "EXTSKETCHSEGMENT", 0, 0, 0, true, 4, null, 0))
            throw new InvalidOperationException("Exterior sketch/axis selection failed.");
        var feature = cut
            ? (Feature?)_model.FeatureManager.FeatureRevolveCut(2 * Math.PI, false, 0, (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees, 0, false, true)
            : (Feature?)_model.FeatureManager.FeatureRevolve(2 * Math.PI, false, 0, (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees, 0, true, false, true);
        if (feature == null) throw new InvalidOperationException("Exterior revolution failed.");
        feature.Name = cut ? "TIFF_OUTER_TRIM" : "TIFF_OUTER_FILL";
        // Replay every existing bore/channel/local cut after the new stock profile.
        // Keeping this at the tree end would fill previously drilled passages.
        if (!_model.Extension.ReorderFeature(feature.Name, cut ? "TIFF_OUTER_FILL" : "Повернуть1", (int)swMoveLocation_e.swMoveAfter))
            throw new InvalidOperationException("Cannot put exterior stock correction before existing cuts.");
        _model.ForceRebuild3(false);
        Console.WriteLine($"[TIFF OUTER] {feature.Name} moved before bore/cut operations.");
    }

    public object RebuildTiffExterior()
    {
        double expectedDelta = TiffExteriorStudy.RunChecks();
        _ = InspectTiffRearCollar(); _ = InspectTiffIiChannel(true); _ = InspectTiffAaChannel(true);
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_OUTER_")) throw new InvalidOperationException("Exterior already rebuilt.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        double before = CurrentTrialVolume();
        CreateTiffExteriorEnvelope(false); CreateTiffExteriorEnvelope(true);
        CheckTiffSuppressionStates(prior, []);
        double final = CurrentTrialVolume();
        if (Math.Abs(final - before - expectedDelta) > .05)
            throw new InvalidOperationException($"Exterior net volume mismatch: {final - before}/{expectedDelta}");
        var exterior = InspectTiffExterior();
        var ii = InspectTiffIiChannel(true); var aa = InspectTiffAaChannel(true);
        var bore = InspectTiffDetailLCore(); var collar = InspectTiffRearCollar();
        var health = InspectTiffFeatureHealth();
        return new { BeforeMm3 = before, FinalMm3 = final, VolumeDeltaMm3 = final - before, ExpectedDeltaMm3 = expectedDelta,
            Exterior = exterior, II = ii, AA = aa, Bore = bore, Collar = collar, Health = health,
            OuterProfileRebuilt = true, RearCollarRebuilt = true, AaChannelRebuilt = true, RightM8Rebuilt = true,
            LeftM8Rebuilt = true, Bore68StartRebuilt = true, Bore80PartialRebuilt = true, Bore80Restored = true,
            DetailLCoreBuilt = true, M6PatternBuilt = false, IsManufacturingReady = false,
            Scope = "TIFF nominal exterior interpretation: R5 pair at D105/D110, R6 root, D135/2mm step, D184 theoretical45deg corner/R3. Reordered before all cuts. L transitions, M6, KG thread and tool ends still pending." };
    }

    public object InspectTiffExterior()
    {
        var order = new List<string>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature()) order.Add(f.Name);
        string[] sequence = ["Повернуть1", "TIFF_OUTER_FILL", "TIFF_OUTER_TRIM", "Вырез-Повернуть1"];
        var indices = sequence.Select(name => order.IndexOf(name)).ToArray();
        if (indices.Any(i => i < 0) || indices.Zip(indices.Skip(1)).Any(p => p.First >= p.Second) ||
            sequence.Any(n => TiffSuppressed(TiffFeature(n)))) throw new InvalidOperationException("Incorrect exterior/bore feature order.");
        var profile = TiffExteriorStudy.Profile(); var arcs = profile.Where(e => e.Type == RevolveProfileElementType.Arc).ToArray();
        int[] matched = new int[arcs.Length]; int cones = 0;
        var body = CopyPilotBody();
        foreach (Face2 face in (object[])body.GetFaces())
        {
            var surface = (Surface)face.GetSurface();
            if (!surface.IsTorus() && !surface.IsCone()) continue;
            var rings = new List<(double X, double R)>();
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var curve = (Curve)edge.GetCurve(); if (!curve.IsCircle()) continue;
                var c = (double[])curve.CircleParams;
                if (Math.Abs(Math.Abs(c[3]) - 1) < 1e-8 && Math.Abs(c[1]) < 1e-8 && Math.Abs(c[2]) < 1e-8)
                    rings.Add((c[0] * 1000, c[6] * 1000));
            }
            bool Ring(double x, double r) => rings.Any(c => Math.Abs(c.X - x) < 1e-4 && Math.Abs(c.R - r) < 1e-4);
            if (surface.IsCone())
            {
                foreach (var e in profile.Where(e => e.Type != RevolveProfileElementType.Arc && e.EndX > e.StartX && Math.Abs(e.EndRadius - e.StartRadius) > 1e-8))
                    if (Ring(e.StartX, e.StartRadius) && Ring(e.EndX, e.EndRadius)) cones++;
                continue;
            }
            var t = (double[])surface.TorusParams;
            if (Math.Abs(Math.Abs(t[3]) - 1) > 1e-8 || Math.Abs(t[1]) > 1e-8 || Math.Abs(t[2]) > 1e-8) continue;
            for (int i = 0; i < arcs.Length; i++)
            {
                var e = arcs[i];
                if (Math.Abs(t[0] * 1000 - e.CenterX) < 1e-5 && Math.Abs(t[6] * 1000 - e.CenterRadius) < 1e-5 && Math.Abs(t[7] * 1000 - e.Radius) < 1e-5 &&
                    Ring(e.StartX, e.StartRadius) && Ring(e.EndX, e.EndRadius)) matched[i]++;
            }
        }
        GC.KeepAlive(body);
        if (matched.Any(n => n != 1) || cones != 3) throw new InvalidOperationException($"Exterior surface mismatch: arcs={string.Join(',', matched)}, cones={cones}");
        var faces = ReadTiffCoaxialBoreFaces(55, 62.5);
        if (faces[62.5].Count != 0) throw new InvalidOperationException("Legacy D125 shaft shoulder remains.");
        var cylinder = InspectTiffBoreCylinder(55, 183, 233, faces[55]);
        return new { ArcFaces = matched, ConeFaces = cones, Cylinder110 = cylinder, FeatureOrderChecked = true,
            TheoreticalCornerDiametersMm = new[] { 135, 184 }, LegacyD125Removed = true,
            Scope = "Nominal profile surfaces and circular boundary rings checked; not dimensional tolerance acceptance." };
    }
}

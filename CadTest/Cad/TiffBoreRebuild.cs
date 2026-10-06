using CadTest.Drawing;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    // Keep the temporary body alive for the entire multi-face inspection. Face RCWs alone
    // do not guarantee the copied body's lifetime across later COM calls/GC.
    private Body2? _tiffBoreInspectionBody;
    public object RebuildTiffBore80()
    {
        TiffBoreStudy.RunChecks();
        _ = InspectTiffBoreStart();
        double before = CurrentTrialVolume();
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_BORE80_")) throw new InvalidOperationException("D80 already present.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        // TIFF A-A: X181 + 56 = X237. Only the interval with removable excess material is corrected.
        // The legacy D90 behind X254 must later be restored/rebuilt, not hidden by this operation.
        CutChannelCylinder("TIFF_BORE80_X237_254_PARTIAL", [237, 0, 0], [254, 0, 0], 40, [0, 1, 0]);
        double final = CurrentTrialVolume(), expected = TiffBoreStudy.Bore80RemovedMm3;
        if (Math.Abs(before - final - expected) > .02)
            throw new InvalidOperationException($"D80 removal mismatch: {before - final}/{expected}.");
        var geometry = InspectTiffBore80();
        var channel = InspectTiffIiChannel();
        CheckTiffSuppressionStates(prior, []);
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (!TiffSuppressed(f))
            {
                int error = f.GetErrorCode2(out bool warning);
                if (error != 0 || warning) throw new InvalidOperationException($"D80 feature issue: {f.Name}/{error}/{warning}");
            }
        return new { BeforeMm3 = before, FinalMm3 = final, RemovedMm3 = before - final, ExpectedRemovedMm3 = expected,
            Geometry = geometry, Channel = channel, Bore80PartialRebuilt = true, Bore68StartRebuilt = true, RightM8Rebuilt = true,
            IsManufacturingReady = false, Scope = "TIFF D80 corrected only X237..254; legacy oversized D90 behind X254 needs material restoration. Detail L, fillets, tolerances and full body remain pending." };
    }

    public object InspectTiffBore80()
    {
        var faces = ReadTiffCoaxialBoreFaces(34, 40);
        var d68 = InspectTiffBoreCylinder(34, 181, 237, faces[34]);
        var d80 = InspectTiffBoreCylinder(40, 237, 254, faces[40]);
        return new { D68 = d68, D80 = d80, FullRearBoreAccepted = false };
    }

    public object InspectTiffBoreStart(double expectedStart = TiffBoreStudy.Start)
        => InspectTiffBoreCylinder(34, expectedStart, 240);

    private Dictionary<double, List<Face2>> ReadTiffCoaxialBoreFaces(params double[] radii)
    {
        var result = radii.Distinct().ToDictionary(r => r, _ => new List<Face2>());
        _tiffBoreInspectionBody = CopyPilotBody();
        foreach (Face2 face in (object[])_tiffBoreInspectionBody.GetFaces())
        {
            var surface = (Surface)face.GetSurface();
            if (!surface.IsCylinder()) continue;
            var c = (double[])surface.CylinderParams;
            if (Math.Abs(Math.Abs(c[3]) - 1) > 1e-8 ||
                Math.Abs(c[1]) > 1e-8 || Math.Abs(c[2]) > 1e-8) continue;
            foreach (double radius in result.Keys)
                if (Math.Abs(c[6] * 1000 - radius) <= 1e-5)
                {
                    // Coarse spatial rejection only: the left M90 also has radius45.
                    // GetBox is approximate; never use it to accept a dimension. Exact edge
                    // sampling below measures every retained face. The 100mm divider is far
                    // from both the left thread and all bore regions being inspected here.
                    var box = (double[])face.GetBox();
                    if (box.Length != 6 || !box.All(double.IsFinite))
                        throw new InvalidOperationException("Invalid bore candidate bounding box.");
                    if (Math.Max(box[0], box[3]) * 1000 < 100) continue;
                    result[radius].Add(face);
                }
        }
        return result;
    }

    private object InspectTiffBoreCylinder(double radius, double expectedStart, double expectedEnd, List<Face2>? candidates = null)
    {
        candidates ??= ReadTiffCoaxialBoreFaces(radius)[radius];
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        int samples = 0, cylinders = 0;
        foreach (Face2 face in candidates)
        {
            cylinders++;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var bounds = edge.GetCurveParams3();
                M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
                for (int j = 0; j <= 32; j++)
                {
                    var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * j / 32, 0);
                    if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                        throw new InvalidOperationException("TIFF bore edge evaluation failed.");
                    minX = Math.Min(minX, p[0] * 1000); maxX = Math.Max(maxX, p[0] * 1000); samples++;
                }
            }
        }
        if (cylinders == 0 || samples == 0 || Math.Abs(minX - expectedStart) > .0001 || Math.Abs(maxX - expectedEnd) > .0001)
            throw new InvalidOperationException($"TIFF D{radius * 2} extent mismatch: {minX}..{maxX}, expected {expectedStart}..{expectedEnd}.");
        Console.WriteLine($"[TIFF BORE OK] D{radius * 2}: X={minX:F9}..{maxX:F9}, samples={samples}.");
        return new { DiameterMm = radius * 2, MinimumX = minX, MaximumX = maxX, Cylinders = cylinders, EdgeSamples = samples,
            ExtentsCheckedByEdgeSamples = true, EntryR1MaxAccepted = false, DiameterToleranceAccepted = false };
    }

    public object RebuildTiffBoreStart()
    {
        double expected = TiffBoreStudy.RunChecks();
        _ = InspectTiffBoreStart(TiffBoreStudy.OldStart);
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_BORE68_")) throw new InvalidOperationException("Bore extension already present.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        double before = CurrentTrialVolume();
        CutChannelCylinder("TIFF_BORE68_START_X181", [181, 0, 0], [203.5, 0, 0], 34, [0, 1, 0]);
        double final = CurrentTrialVolume(), removed = before - final;
        if (Math.Abs(removed - expected) > .02)
            throw new InvalidOperationException($"TIFF bore removed volume mismatch: {removed} vs {expected}.");
        var geometry = InspectTiffBoreStart();
        var channel = InspectTiffIiChannel();
        CheckTiffSuppressionStates(prior, []);
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (!TiffSuppressed(f))
            {
                int error = f.GetErrorCode2(out bool warning);
                if (error != 0 || warning) throw new InvalidOperationException($"Feature issue after bore extension: {f.Name}/{error}/{warning}");
            }
        return new { BeforeMm3 = before, FinalMm3 = final, RemovedMm3 = removed, ExpectedRemovedMm3 = expected,
            Geometry = geometry, Channel = channel, Bore68StartRebuilt = true, RightM8Rebuilt = true,
            TiffThreadLengthAccepted = false, Tolerance6HAccepted = false, IsManufacturingReady = false,
            Scope = "Only D68 front datum moved from X203.5 to TIFF nominal X181. Rear bore, detail L, entry fillet, other body diameters and AA channel remain pending." };
    }
}

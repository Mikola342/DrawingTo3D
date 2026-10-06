using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectTiffRestoredBore()
    {
        var faces = ReadTiffCoaxialBoreFaces(34, 40, 45);
        var d68 = InspectTiffBoreCylinder(34, 181, 237, faces[34]);
        var d80 = InspectTiffBoreCylinder(40, 237, 265, faces[40]);
        if (faces[45].Count != 0) throw new InvalidOperationException("Legacy coaxial D90 bore still present.");
        return new { D68 = d68, D80 = d80, LegacyD90Removed = true, FullRearBoreAccepted = false,
            Scope = "Nominal D80 continuous to X265; detail L, exit chamfer, entry radii and tolerances pending." };
    }

    public object RestoreTiffBore80()
    {
        Drawing.TiffBoreStudy.RunChecks();
        _ = InspectTiffBore80();
        _ = InspectTiffBoreCylinder(45, 254, 265);
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_BORE80_RESTORE")) throw new InvalidOperationException("Restoration already present.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        double before = CurrentTrialVolume();
        _model.ClearSelection2(true);
        if (!_model.Extension.SelectByID2("Спереди", "PLANE", 0, 0, 0, false, 0, null, 0))
            throw new InvalidOperationException("Front plane not selected for restoration.");
        var sm = _model.SketchManager;
        sm.InsertSketch(true);
        using (var exact = new ExactSketchCoordinates(sm))
        {
            var axis = sm.CreateLine(0, 0, 0, .273, 0, 0) ?? throw new InvalidOperationException("Restoration axis failed.");
            axis.ConstructionGeometry = true;
            (double X, double R)[] points = [(254, 40), (265, 40), (265, 45), (254, 45)];
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i]; var q = points[(i + 1) % points.Length];
                if (sm.CreateLine(p.X / 1000, p.R / 1000, 0, q.X / 1000, q.R / 1000, 0) == null)
                    throw new InvalidOperationException("Restoration segment failed.");
            }
        }
        sm.InsertSketch(true);
        Feature? sketch = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "ProfileFeature") sketch = f;
        _model.ClearSelection2(true);
        if (sketch == null || !sketch.Select2(false, 0) ||
            !_model.Extension.SelectByID2($"Line1@{sketch.Name}", "EXTSKETCHSEGMENT", 0, 0, 0, true, 4, null, 0))
            throw new InvalidOperationException("Restoration profile/axis selection failed.");
        var restore = (Feature?)_model.FeatureManager.FeatureRevolve(2 * Math.PI, false, 0,
            (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees, 0, true, false, true)
            ?? throw new InvalidOperationException("Restoration revolve failed.");
        restore.Name = "TIFF_BORE80_RESTORE_X254_265";
        _model.ForceRebuild3(false);
        double final = CurrentTrialVolume(), expected = Drawing.TiffBoreStudy.Bore80RestoredMm3;
        if (Math.Abs(final - before - expected) > .02)
            throw new InvalidOperationException($"Restored volume mismatch: {final - before}/{expected}.");
        var geometry = InspectTiffRestoredBore();
        var channel = InspectTiffIiChannel();
        CheckTiffSuppressionStates(prior, []);
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (!TiffSuppressed(f))
            {
                int error = f.GetErrorCode2(out bool warning);
                if (error != 0 || warning) throw new InvalidOperationException($"Restored feature issue: {f.Name}/{error}/{warning}");
            }
        return new { BeforeMm3 = before, FinalMm3 = final, AddedMm3 = final - before, ExpectedAddedMm3 = expected,
            Geometry = geometry, Channel = channel, Bore80Restored = true, Bore80PartialRebuilt = true,
            Bore68StartRebuilt = true, RightM8Rebuilt = true, IsManufacturingReady = false,
            Scope = "Restored missing material R40..45/X254..265; nominal D80 continuous X237..265. L, radii, exit chamfer, full TIFF body and tolerances pending." };
    }
}

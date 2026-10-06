using CadTest.Models;
using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectTiffDetailLCore()
    {
        var faces = ReadTiffCoaxialBoreFaces(34, 35.5, 40, 45);
        var groove = InspectTiffBoreCylinder(35.5, 232, 233.9, faces[35.5]);
        var bore = InspectTiffBoreCylinder(34, 181, 237, faces[34]);
        var rear = InspectTiffBoreCylinder(40, 237, 265, faces[40]);
        if (faces[34].Count != 2 || faces[35.5].Count != 1 || faces[45].Count != 0)
            throw new InvalidOperationException("Unexpected bore/groove topology after detail L core.");
        // Check each retained D68 segment, not only their combined bounding interval.
        var left = faces[34].Where(f => ((double[])f.GetBox())[0] * 1000 < 200).ToList();
        var right = faces[34].Except(left).ToList();
        _ = InspectTiffBoreCylinder(34, 181, 232, left);
        _ = InspectTiffBoreCylinder(34, 233.9, 237, right);
        return new { Groove = groove, Bore = bore, Rear = rear,
            NominalCoreOnly = true,
            // These checks measure the implemented interval, not its drawing datum.
            // The endpoint of dimension 51 on the raster must be resolved separately.
            DrawingDatum51EndpointAccepted = false,
            ImplementedDatumInterpretation = "51 locates the left wall: X181+51=232; not source-accepted",
            Transition30DegreesAccepted = false,
            Mark10IsSurfaceFinishNotAngle = true, FullDetailLAccepted = false };
    }

    public object BuildTiffDetailLCore()
    {
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_L_CORE")) throw new InvalidOperationException("L core already present.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        _ = InspectTiffRestoredBore();
        var cut = new AnnularCutDescription { Name = "TIFF_L_CORE_D71_PENDING_TRANSITIONS", StartX = 181 + 51,
            EndX = 181 + 51 + 1.9, InnerRadius = 34, OuterRadius = 35.5 };
        cut.Validate();
        double before = CurrentTrialVolume();
        CreateAnnularCuts(new PartDescription { TotalLength = 273, AnnularCuts = [cut] });
        _model.ForceRebuild3(false);
        double final = CurrentTrialVolume();
        if (Math.Abs(before - final - cut.VolumeMm3) > .02)
            throw new InvalidOperationException($"L core volume mismatch: {before - final}/{cut.VolumeMm3}.");
        var geometry = InspectTiffDetailLCore();
        var channel = InspectTiffIiChannel();
        CheckTiffSuppressionStates(prior, []);
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (!TiffSuppressed(f))
            {
                int error = f.GetErrorCode2(out bool warning);
                if (error != 0 || warning) throw new InvalidOperationException($"L core feature issue: {f.Name}/{error}/{warning}");
            }
        return new { BeforeMm3 = before, FinalMm3 = final, RemovedMm3 = before - final, ExpectedRemovedMm3 = cut.VolumeMm3,
            Geometry = geometry, Channel = channel, DetailLCoreBuilt = true, Bore80Restored = true, Bore80PartialRebuilt = true,
            Bore68StartRebuilt = true, RightM8Rebuilt = true, IsManufacturingReady = false,
            Scope = "TIFF L cylindrical core D71/1.9 atX232 only. Datum51 endpoint,30-degree transition,1.2 and full detail remain unaccepted. Mark10 is surface finish, not an angle." };
    }
}

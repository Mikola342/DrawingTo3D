using CadTest.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object DiagnoseKnownRearM8(string path, string checkpoint)
    {
        _model = (ModelDoc2?)_swApp!.GetOpenDocumentByName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException("Known rear work must be open.");
        if (!string.Equals(Path.GetFullPath(_model.GetPathName()), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Wrong work document.");
        int errors = 0, warnings = 0;
        if (File.Exists(checkpoint) || !_model.Extension.SaveAs(checkpoint,
            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy,
            null, ref errors, ref warnings) || errors != 0 || warnings != 0)
            throw new InvalidOperationException($"Unverified checkpoint save failed: {errors}/{warnings}.");
        Console.WriteLine($"[UNVERIFIED CHECKPOINT ONLY] {checkpoint}");
        return InspectTiffM8(aa: true, rearDatum: true);
    }
    public object ResumeTiffRearDatum(string path)
    {
        path = Path.GetFullPath(path);
        _model = (ModelDoc2?)_swApp!.GetOpenDocumentByName(path);
        if (_model == null || !string.Equals(Path.GetFullPath(_model.GetPathName()), path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The known unsaved rear work document must still be open.");
        var collar = InspectTiffRearCollar();
        var ii = InspectTiffIiChannel(true); var aa = InspectTiffAaChannel(true);
        var right = InspectTiffM8(rearDatum: true);
        var left = InspectTiffM8(aa: true, rearDatum: true);
        var bore = InspectTiffDetailLCore(); var health = InspectTiffFeatureHealth();
        return new { FinalMm3 = CurrentTrialVolume(), Collar = collar, II = ii, AA = aa, RightM8 = right, LeftM8 = left,
            Bore = bore, Health = health, RearCollarRebuilt = true, AaChannelRebuilt = true, RightM8Rebuilt = true,
            LeftM8Rebuilt = true, Bore68StartRebuilt = true, Bore80PartialRebuilt = true, Bore80Restored = true,
            DetailLCoreBuilt = true, M6PatternBuilt = false, IsManufacturingReady = false,
            ResumedAfterObliqueMouthCheck = true, Scope = "Known interrupted rear work resumed; both existing M8 rechecked against planarX265 mouth with high-accuracy removed-body volume. M8 length remains provisional." };
    }
    private static readonly string[] RearDatumReplaced = [
        "TIFF_AA_M8_NOMINAL_PENDING_LENGTH", "TIFF_AA_M8_PILOT_PENDING_LENGTH",
        "TIFF_AA_D68_7deg", "TIFF_AA_D6_X132", "TIFF_AA_D63_4deg",
        "TIFF_II_M8_NOMINAL_PENDING_LENGTH", "TIFF_II_M8_PILOT_PENDING_LENGTH",
        "TIFF_II_D68_feed_7deg", "TIFF_II_D63_10deg",
        "Rear_recess_lip", "Collar_groove_nominal", "Rear_recess_left"];

    public object InspectTiffRearCollar()
    {
        var features = TiffFeatures(RearDatumReplaced.Append("TIFF_REAR_RECESS_D115"));
        foreach (string name in RearDatumReplaced)
            if (!TiffSuppressed(features[name])) throw new InvalidOperationException($"Legacy rear feature active: {name}");
        if (TiffSuppressed(features["TIFF_REAR_RECESS_D115"])) throw new InvalidOperationException("Rear recess suppressed.");
        var faces = ReadTiffCoaxialBoreFaces(57.5, 48.5, 47.5);
        if (faces[48.5].Count != 0 || faces[47.5].Count != 0)
            throw new InvalidOperationException("Historical collar/groove cylinder remains.");
        var collar = InspectTiffBoreCylinder(57.5, 260, 265, faces[57.5]);
        return new { Collar = collar, BoltCircleDiameterMm = 95, M6PatternNotInspectedHere = true,
            MouthNominalX = 265, SelectedCollarInsetMm = 0, AllowedInsetMaxMm = 1.6 };
    }

    public object RebuildTiffRearDatum()
    {
        Drawing.TiffRearStudy.RunChecks();
        _ = InspectTiffAaChannel(); _ = InspectTiffIiChannel();
        var prior = new Dictionary<string, bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("TIFF_REAR_")) throw new InvalidOperationException("Rear datum already rebuilt.");
            prior.Add(f.Name, TiffSuppressed(f));
        }
        if (RearDatumReplaced.Any(n => !prior.ContainsKey(n) || prior[n]))
            throw new InvalidOperationException("Unexpected rear baseline feature state.");
        var replacedFeatures = TiffFeatures(RearDatumReplaced);
        double before = CurrentTrialVolume();
        foreach (string name in RearDatumReplaced)
        {
            if (!replacedFeatures[name].SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
                (int)swInConfigurationOpts_e.swThisConfiguration, null))
                throw new InvalidOperationException($"Rear suppression failed: {name}");
            Console.WriteLine($"[TIFF REAR] suppressed {name}");
        }
        _model.ForceRebuild3(false); CheckTiffSuppressionStates(prior, RearDatumReplaced);
        double restored = CurrentTrialVolume();
        if (restored <= before) throw new InvalidOperationException("Rear material not restored.");
        CreateAnnularCuts(new PartDescription { TotalLength = 273, AnnularCuts = [new AnnularCutDescription {
            Name = "TIFF_REAR_RECESS_D115", StartX = 260, EndX = 265, InnerRadius = 57.5, OuterRadius = 95 }] });
        double recessed = CurrentTrialVolume();
        if (Math.Abs(restored - recessed - Drawing.TiffRearStudy.RecessVolume) > .05)
            throw new InvalidOperationException($"Rear recess volume mismatch: {restored - recessed}/{Drawing.TiffRearStudy.RecessVolume}");
        var collar = InspectTiffRearCollar();
        foreach (bool aa in new[] { false, true })
        {
            double a = (aa ? 120 : 60) * Math.PI / 180, b = (aa ? 4 : 10) * Math.PI / 180;
            string prefix = aa ? "TIFF_REAR_AA_" : "TIFF_REAR_II_";
            double[] At(double x, double r) => [x, r * Math.Sin(a), -r * Math.Cos(a)];
            double[] tangent = [0, Math.Cos(a), Math.Sin(a)];
            double endX = aa ? 128.75 : 265 - 110 * Math.Cos(b);
            double RadiusAt(double x) => 51 + (x - 265) * Math.Tan(b);
            // Extend the cutter outside the axial end face: a tilted tool starting exactly
            // at its centre onX265 otherwise leaves half of its flat end inside the collar.
            // The II bottom remains110mm from the nominal mouth centre, not from the overrun.
            CutChannelCylinder(prefix + (aa ? "D63_4deg" : "D63_10deg"), At(267, RadiusAt(267)), At(endX, RadiusAt(endX)), 3.15, tangent);
            if (aa) CutChannelCylinder(prefix + "D6_X132", At(132, 55), At(132, RadiusAt(132)), 3, tangent);
            double tf = Math.Tan(7 * Math.PI / 180), tm = Math.Tan(b);
            double crossing = (51 + (257.5 - 127 * tf - 265) * tm) / (1 - tf * tm);
            double FeedX(double r) => 257.5 + (r - 127) * tf;
            CutChannelCylinder(prefix + (aa ? "D68_7deg" : "D68_feed_7deg"),
                At(FeedX(130), 130), At(FeedX(crossing - 1), crossing - 1), 3.4, tangent);
        }
        var ii = InspectTiffIiChannel(true); var aaGeometry = InspectTiffAaChannel(true);
        var rightM8 = RebuildTiffM8(rearDatum: true);
        var leftM8 = RebuildTiffM8(aa: true, rearDatum: true);
        var bore = InspectTiffDetailLCore();
        CheckTiffSuppressionStates(prior, RearDatumReplaced);
        var health = InspectTiffFeatureHealth();
        return new { BeforeMm3 = before, RestoredMm3 = restored, RecessedMm3 = recessed, FinalMm3 = CurrentTrialVolume(),
            Collar = collar, II = ii, AA = aaGeometry, RightM8 = rightM8, LeftM8 = leftM8, Bore = bore, Health = health,
            RearCollarRebuilt = true, AaChannelRebuilt = true, RightM8Rebuilt = true, LeftM8Rebuilt = true,
            Bore68StartRebuilt = true, Bore80PartialRebuilt = true, Bore80Restored = true, DetailLCoreBuilt = true,
            IsManufacturingReady = false, M6PatternBuilt = false,
            Scope = "Rear collar D115 replaces legacy D97/groove; nominal collar endX265 with zero inset. Channels use collar mouth, not recess floorX260. FourM6 still pending; M8 length, drill ends, L transitions and exterior pending." };
    }
}

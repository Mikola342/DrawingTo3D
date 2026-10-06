using CadTest.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object CompleteExperimentalLProfile()
    {
        var chain = Drawing.ExperimentalLProfileStudy.RunChecks();
        _ = InspectExperimentalLExit();
        const string oldName = "TIFF_L_CORE_D71_PENDING_TRANSITIONS", newName = "EXPERIMENT_L_GROOVE_RIGHT_WALL_51";
        var prior = new Dictionary<string,bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name == newName) throw new InvalidOperationException("Experimental full L already present.");
            prior.Add(f.Name,TiffSuppressed(f));
        }
        if (!prior.TryGetValue(oldName,out bool suppressed) || suppressed)
            throw new InvalidOperationException("Expected active old L core.");
        double before = CurrentTrialVolume();
        if (!TiffFeature(oldName).SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
            (int)swInConfigurationOpts_e.swThisConfiguration,null)) throw new InvalidOperationException("Cannot restore old L groove.");
        _model.ForceRebuild3(false); double restored = CurrentTrialVolume();
        double expected = Drawing.ExperimentalLProfileStudy.GrooveVolume;
        if (Math.Abs(restored-before-expected) > .02) throw new InvalidOperationException($"L restoration volume mismatch:{restored-before:R}/{expected:R}");
        CreateAnnularCuts(new PartDescription { TotalLength=273, AnnularCuts=[new AnnularCutDescription {
            Name=newName,StartX=Drawing.ExperimentalLProfileStudy.GrooveLeft,EndX=Drawing.ExperimentalLProfileStudy.GrooveRight,
            InnerRadius=34,OuterRadius=35.5 }] });
        _model.ForceRebuild3(false); double final = CurrentTrialVolume();
        if (Math.Abs(restored-final-expected) > .02 || Math.Abs(final-before) > .02)
            throw new InvalidOperationException("L relocated groove/net volume mismatch.");
        CheckTiffSuppressionStates(prior,[oldName]);
        var geometry = InspectExperimentalLExit(true); var health = InspectTiffFeatureHealth();
        return new { BeforeMm3=before,RestoredMm3=restored,FinalMm3=final,ExpectedGrooveMm3=expected,
            RestoredMm3Delta=restored-before,RemovedMm3=restored-final,NetDeltaMm3=final-before,
            Geometry=geometry,DimensionChain=chain,Health=health,ExperimentalLProfileCompleted=true,ExperimentalLExitBuilt=true,
            UserAuthorizedExperimentalBranch=true,DrawingInterpretationAccepted=false,FullDetailLSourceAccepted=false,
            PpChamferBuilt=true,M6PatternBuilt=true,RearCollarRebuilt=true,OuterProfileRebuilt=true,AaChannelRebuilt=true,
            RightM8Rebuilt=true,LeftM8Rebuilt=true,Bore68StartRebuilt=true,Bore80PartialRebuilt=true,Bore80Restored=true,
            DetailLCoreBuilt=true,IsManufacturingReady=false,
            Scope="Complete selected experimental L profile;51 to right wall,1.2 axial,30deg to axis. TIFF interpretation remains unconfirmed." };
    }
}

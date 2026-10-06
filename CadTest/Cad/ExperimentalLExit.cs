using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private const string LExitName = "EXPERIMENT_L_EXIT_1p2_30deg_TO_AXIS";
    public object InspectExperimentalLExit(bool rightWallDatum = false)
    {
        double grooveLeft = rightWallDatum ? Drawing.ExperimentalLProfileStudy.GrooveLeft : 232;
        double grooveRight = rightWallDatum ? Drawing.ExperimentalLProfileStudy.GrooveRight : 233.9;
        object? chain = rightWallDatum ? Drawing.ExperimentalLProfileStudy.RunChecks() : null;
        if (rightWallDatum && (!TiffSuppressed(TiffFeature("TIFF_L_CORE_D71_PENDING_TRANSITIONS")) ||
            TiffSuppressed(TiffFeature("EXPERIMENT_L_GROOVE_RIGHT_WALL_51"))))
            throw new InvalidOperationException("Experimental L replacement state mismatch.");
        var feature = TiffFeature(LExitName);
        if (TiffSuppressed(feature)) throw new InvalidOperationException("Experimental L exit suppressed.");
        var faces = ReadTiffCoaxialBoreFaces(34, 35.5, 40, 45);
        if (faces[34].Count != 2 || faces[35.5].Count != 1 || faces[40].Count != 1 || faces[45].Count != 0)
            throw new InvalidOperationException("Experimental L bore topology mismatch.");
        var left = faces[34].Where(f => ((double[])f.GetBox())[0] * 1000 < 200).ToList();
        _ = InspectTiffBoreCylinder(34, 181, grooveLeft, left);
        _ = InspectTiffBoreCylinder(34, grooveRight, 235.8, faces[34].Except(left).ToList());
        _ = InspectTiffBoreCylinder(35.5, grooveLeft, grooveRight, faces[35.5]);
        _ = InspectTiffBoreCylinder(40, 237, 265, faces[40]);
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity; int cones = 0, samples = 0;
        foreach (Face2 face in (object[])feature.GetFaces())
        {
            var surface = (Surface)face.GetSurface(); if (!surface.IsCone()) continue;
            cones++; var c = (double[])surface.ConeParams2;
            if (Math.Abs(Math.Abs(c[7]) - Math.PI / 6) > 1e-8 || Math.Abs(c[1]) > 1e-8 || Math.Abs(c[2]) > 1e-8 || Math.Abs(Math.Abs(c[3]) - 1) > 1e-8)
                throw new InvalidOperationException("Experimental L cone angle/axis mismatch.");
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var b = edge.GetCurveParams3(); Drawing.M12Study.ValidateEdgeInterval(b.UMinValue, b.UMaxValue);
                for (int i = 0; i <= 32; i++)
                {
                    var p = (double[])edge.Evaluate2(b.UMinValue + (b.UMaxValue-b.UMinValue)*i/32, 0);
                    if (p.Length != 4 || !p.Take(3).All(double.IsFinite) || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1)
                        throw new InvalidOperationException("Experimental L edge evaluation failed.");
                    double x = p[0]*1000, r = Math.Sqrt(p[1]*p[1]+p[2]*p[2])*1000;
                    if (Math.Abs(r - (34 + (x-235.8)*Math.Tan(Math.PI/6))) > .0001)
                        throw new InvalidOperationException("Experimental L cone envelope mismatch.");
                    lo = Math.Min(lo,x); hi = Math.Max(hi,x); samples++;
                }
            }
        }
        if (cones != 1 || samples == 0 || Math.Abs(lo-235.8) > .0001 || Math.Abs(hi-237) > .0001)
            throw new InvalidOperationException("Experimental L cone extent mismatch.");
        Console.WriteLine($"[EXPERIMENT L OK] X{lo}..{hi}; angle30 to axis; samples={samples}; NOT drawing acceptance.");
        return new { StartX = lo, EndX = hi, AxialLengthMm = 1.2, AngleToAxisDegrees = 30,
            EndRadiusMm = 34+1.2*Math.Tan(Math.PI/6), EdgeSamples = samples,
            DrawingInterpretationAccepted = false, GrooveDatum51Accepted = false, FullDetailLAccepted = false,
            ExperimentalProfileCompleted = rightWallDatum, DimensionChain = chain,
            Assumption = rightWallDatum ? "51 to right groove wall;1.2 axial before X237;30deg to axis; not source-accepted" :
                "1.2 taken axially before X237;30deg to axis; groove X232..233.9 unchanged" };
    }

    public object BuildExperimentalLExit()
    {
        _ = InspectTiffDetailLCore();
        var prior = new Dictionary<string,bool>();
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (f.Name.StartsWith("EXPERIMENT_L_EXIT")) throw new InvalidOperationException("Experimental L exit already exists.");
            prior.Add(f.Name,TiffSuppressed(f));
        }
        double before = CurrentTrialVolume(), delta = 1.2*Math.Tan(Math.PI/6);
        // Tapered cutter starts at midpoint X237 and shrinks toward X235.8.
        CutChannelCylinder(LExitName, [238.2,0,0], [235.8,0,0], 34+delta, [0,1,0], true, Math.PI/6);
        double final = CurrentTrialVolume(), expected = Math.PI*1.2*(34*delta+delta*delta/3);
        if (Math.Abs(before-final-expected) > .02)
            throw new InvalidOperationException($"Experimental L volume mismatch: {before-final:R}/{expected:R}");
        var geometry = InspectExperimentalLExit();
        CheckTiffSuppressionStates(prior, []);
        var health = InspectTiffFeatureHealth();
        return new { BeforeMm3=before, FinalMm3=final, RemovedMm3=before-final, ExpectedRemovedMm3=expected,
            Geometry=geometry, Health=health, ExperimentalLExitBuilt=true, UserAuthorizedExperimentalBranch=true,
            DrawingInterpretationAccepted=false, PpChamferBuilt=true, M6PatternBuilt=true, RearCollarRebuilt=true,
            OuterProfileRebuilt=true, AaChannelRebuilt=true, RightM8Rebuilt=true, LeftM8Rebuilt=true,
            Bore68StartRebuilt=true, Bore80PartialRebuilt=true, Bore80Restored=true, DetailLCoreBuilt=true,
            IsManufacturingReady=false, Scope="Experimental L exit only; datum51 and all other inherited limitations remain open." };
    }
}

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using CadTest.Drawing;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public void ExportTrialPreview(string path)
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        _model.ClearSelection2(true);
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "RefPlane")
            {
                f.Select2(false, 0);
                _model.BlankRefGeom();
            }
        _model.ClearSelection2(true);
        _model.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
        _model.ViewZoomtofit2();
        int errors = 0, warnings = 0;
        if (!_model.Extension.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings))
            throw new InvalidOperationException($"Preview export failed: {errors}/{warnings}.");
    }
    public object VerifyTrialSplineSurfaces()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var bodies = (object[]?)((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false);
        if (bodies == null || bodies.Length != 1) throw new InvalidOperationException("Expected one trial body.");
        int roots = 0, fillets = 0, runouts = 0;
        foreach (Face2 face in (object[])((Body2)bodies[0]).GetFaces())
        {
            var s = (Surface)face.GetSurface();
            if (s.IsCylinder())
            {
                var c = (double[])s.CylinderParams;
                if (Math.Abs(Math.Abs(c[3]) - 1) < 1e-7)
                {
                    if (Math.Abs(c[6] * 1000 - 47) < 1e-5) roots++;
                    if (Math.Abs(c[6] * 1000 - SplineTrialProfile.Root.FilletRadius) < 1e-5) fillets++;
                }
            }
            if (s.IsTorus())
            {
                var t = (double[])s.TorusParams;
                if (Math.Abs(t[6] * 1000 - 97) < 1e-5 && Math.Abs(t[7] * 1000 - 47) < 1e-5)
                {
                    if (Math.Abs(t[0] * 1000 - 70) > 1e-5 ||
                        Math.Abs(Math.Sqrt(t[1] * t[1] + t[2] * t[2]) * 1000 - 97) > 1e-5 || Math.Abs(t[3]) > 1e-7)
                        throw new InvalidOperationException("R50 trial cutter centre/axis mismatch.");
                    runouts++;
                }
            }
        }
        if (roots < 38 || fillets < 76 || runouts != 38)
            throw new InvalidOperationException($"Trial surface counts: roots={roots}, fillets={fillets}, runouts={runouts}.");
        Console.WriteLine($"[OK] Trial spline surfaces: {roots} root faces, {fillets} fillet faces, {runouts} R50 runouts at X70.");
        return new {RootFaces=roots,RootFilletFaces=fillets,RunoutFaces=runouts,RunoutStationXmm=70,
            FullInvoluteProfileVerified=false,AngularPhaseVerified=false,DrawingInterpretationAccepted=false,
            Scope="Inherited experimental surface counts and runout cutter placement; not full spline profile or TIFF conformity."};
    }
    public double TransferTrialSplines()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        Feature? right = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "RefPlane" && f.Name is "Справа" or "Right Plane") right = f;
        if (right == null) throw new InvalidOperationException("No right plane.");
        Feature LastSketch()
        {
            Feature? result = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.GetTypeName2() == "ProfileFeature") result = f;
            return result ?? throw new InvalidOperationException("No trial sketch.");
        }
        void DrawSpace(int index, bool axis)
        {
            double angle = index * 2 * Math.PI / 38;
            SplineTrialProfile.Point Rot(SplineTrialProfile.Point p) => new(
                (p.X * Math.Cos(angle) - p.Y * Math.Sin(angle)) / 1000,
                (p.X * Math.Sin(angle) + p.Y * Math.Cos(angle)) / 1000);
            var sm = _model.SketchManager;
            using var exact = new ExactSketchCoordinates(sm);
            if (axis)
            {
                var a = Rot(new(97, -10)); var b = Rot(new(97, 10));
                var line = sm.CreateCenterLine(a.X, a.Y, 0, b.X, b.Y, 0);
                if (line == null) throw new InvalidOperationException("No cutter axis.");
            }
            foreach (var s in SplineTrialProfile.SpaceSegments())
            {
                var p = s.Points.Select(Rot).ToArray();
                SketchSegment? entity;
                if (s.Arc)
                {
                    var a = p[0]; var b = p[^1]; var m = p[p.Length / 2];
                    entity = sm.Create3PointArc(a.X, a.Y, 0, b.X, b.Y, 0, m.X, m.Y, 0);
                }
                else if (p.Length == 2) entity = sm.CreateLine(p[0].X, p[0].Y, 0, p[1].X, p[1].Y, 0);
                else entity = sm.CreateSpline2(p.SelectMany(v => new[] { v.X, v.Y, 0.0 }).ToArray(), false);
                if (entity == null) throw new InvalidOperationException("Trial space segment failed.");
            }
        }
        _model.ClearSelection2(true);
        right.Select2(false, 0);
        _model.SketchManager.InsertSketch(true);
        for (int k = 0; k < 38; k++) DrawSpace(k, false);
        _model.SketchManager.InsertSketch(true);
        var straightSketch = LastSketch();
        _model.ClearSelection2(true);
        straightSketch.Select2(false, 0);
        var cut = (Feature?)_model.FeatureManager.FeatureCut3(true, false, true,
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            .070, 0, false, false, false, false, 0, 0, false, false, false, false, false,
            true, true, false, false, false, (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (cut == null) throw new InvalidOperationException("Straight spline trial failed.");
        cut.Name = "TRIAL_38_spaces_to_X70_RootApprox";
        Console.WriteLine("[TRIAL] 38 straight spaces built to X70.");
        _model.ClearSelection2(true);
        right.Select2(false, 0);
        var reference = _model.FeatureManager.InsertRefPlane(8, .070, 0, 0, 0, 0);
        if (reference == null) throw new InvalidOperationException("No X70 cutter plane.");
        Feature? runoutPlane = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "RefPlane") runoutPlane = f;
        if (runoutPlane == null) throw new InvalidOperationException("No offset plane feature.");
        for (int k = 0; k < 38; k++)
        {
            _model.ClearSelection2(true);
            runoutPlane.Select2(false, 0);
            _model.SketchManager.InsertSketch(true);
            DrawSpace(k, true);
            _model.SketchManager.InsertSketch(true);
            var sketch = LastSketch();
            _model.ClearSelection2(true);
            sketch.Select2(false, 0);
            if (!_model.Extension.SelectByID2($"Line1@{sketch.Name}", "EXTSKETCHSEGMENT", 0, 0, 0, true, 4, null, 0))
                throw new InvalidOperationException("Cutter axis selection failed.");
            var feature = (Feature?)_model.FeatureManager.FeatureRevolveCut(2 * Math.PI, false, 0,
                (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees, 0, false, true);
            if (feature == null) throw new InvalidOperationException($"Trial R50 failed for space {k}.");
            feature.Name = $"TRIAL_R50_disk_space_{k + 1:D2}";
            Console.WriteLine($"[TRIAL] R50 space {k + 1}/38 built.");
        }
        _model.ForceRebuild3(false);
        var bodies = (object[]?)((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false);
        if (bodies == null || bodies.Length != 1) throw new InvalidOperationException("Trial transfer split the body.");
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            int error = f.GetErrorCode2(out bool isWarning);
            if (error != 0 && !isWarning) throw new InvalidOperationException($"Trial feature error {f.Name}: {error}.");
        }
        var mass = (MassProperty2)_model.Extension.CreateMassProperty2();
        mass.UseSystemUnits = true;
        mass.AccuracyLevel = (int)swMassPropertyAccuracyLevel_e.swMassPropertyAccuracyLevel_Higher;
        mass.Recalculate();
        _model.ViewZoomtofit2();
        return mass.Volume * 1e9;
    }
}

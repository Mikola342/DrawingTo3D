using CadTest.Drawing;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectRadialEntryChamfers(bool requireCone = false)
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var reports = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            Feature? feature = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == (requireCone ? $"TRIAL_D6_entry_cone_{degrees}" : $"D6_entry_chamfer_1x45_{degrees}")) feature = f;
            if (feature == null) throw new InvalidOperationException("Missing radial entry chamfer.");
            double angle = degrees * Math.PI / 180;
            var faces = new List<object>();
            foreach (Face2 face in (object[])feature.GetFaces())
            {
                var surface = (Surface)face.GetSurface();
                double maxResidual = 0, minDepth = double.PositiveInfinity, maxDepth = double.NegativeInfinity;
                int samples = 0;
                foreach (Edge edge in (object[])face.GetEdges())
                {
                    var bounds = edge.GetCurveParams3();
                    M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
                    for (int j = 0; j <= 128; j++)
                    {
                        var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * j / 128, 0);
                        if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                            throw new InvalidOperationException("Chamfer edge evaluation failed.");
                        double radial = 1000 * (p[1] * Math.Sin(angle) - p[2] * Math.Cos(angle));
                        double transverse = 1000 * (p[1] * Math.Cos(angle) + p[2] * Math.Sin(angle));
                        double rho = Math.Sqrt(Math.Pow(p[0] * 1000 - 108, 2) + transverse * transverse);
                        maxResidual = Math.Max(maxResidual, Math.Abs(rho - (radial - 48.5)));
                        minDepth = Math.Min(minDepth, 52.5 - radial); maxDepth = Math.Max(maxDepth, 52.5 - radial);
                        samples++;
                    }
                }
                if (requireCone && (!surface.IsCone() || samples == 0 || maxResidual > .0001 || Math.Abs(maxDepth - 1) > .0001))
                    throw new InvalidOperationException("Entry cone boundary verification failed.");
                if (requireCone)
                {
                    var c = (double[])surface.ConeParams2;
                    double alignment = c[4] * Math.Sin(angle) - c[5] * Math.Cos(angle);
                    if (Math.Abs(Math.Abs(alignment) - 1) > 1e-8 || Math.Abs(Math.Abs(c[7]) - Math.PI / 4) > 1e-8)
                        throw new InvalidOperationException("Entry cone axis/angle verification failed.");
                }
                faces.Add(new { IsCone = surface.IsCone(), ConeParametersSI = surface.IsCone() ? (double[])surface.ConeParams2 : null,
                    EdgeSamples = samples, MaxNominalConeResidualMm = maxResidual, MinDepthFromTangentPlaneMm = minDepth,
                    MaxDepthFromTangentPlaneMm = maxDepth });
                Console.WriteLine($"[AUDIT] Entry {degrees}: cone={surface.IsCone()}, edge samples={samples}, nominal cone residual={maxResidual:F9} mm, depth={minDepth:F9}..{maxDepth:F9}.");
            }
            if (faces.Count == 0) throw new InvalidOperationException("Entry feature has no inspectable faces.");
            reports.Add(new { AngleDegrees = degrees, Faces = faces });
        }
        return reports;
    }

    public object InspectSavedConeTrial(CadTest.Models.PartDescription part, bool approximateDk = false, bool correctedFlange19 = false)
    {
        var cones = InspectRadialEntryChamfers(requireCone: true);
        var channels = VerifyChannelTrial();
        VerifyDraftBody(part, checkVolume: false, approximateDk: approximateDk, correctedFlange19: correctedFlange19);
        VerifyTrialSplineSurfaces();
        var threads = InspectM12Trial();
        return new { Cones = cones, Channels = channels, M12 = threads, VolumeMm3 = CurrentTrialVolume(),
            BaseGeometryChecked = true, SplineSurfacesChecked = true, ExactTotalVolumeChecked = false };
    }

    public object CreateRadialEntryChamfers()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        VerifyChannelTrial();
        double initial = CurrentTrialVolume();
        var reports = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            Feature? radial = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            {
                if (f.Name == $"D6_entry_chamfer_1x45_{degrees}")
                    throw new InvalidOperationException("Entry chamfer already exists.");
                if (f.Name == $"TRIAL_D6_radial_{degrees}") radial = f;
            }
            if (radial == null) throw new InvalidOperationException("Missing radial channel.");
            var edges = new List<Edge>();
            foreach (Face2 face in (object[])radial.GetFaces())
                foreach (Edge edge in (object[])face.GetEdges())
                    foreach (Face2 adjacent in ((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>())
                    {
                        var surface = (Surface)adjacent.GetSurface();
                        if (!surface.IsCylinder()) continue;
                        var c = (double[])surface.CylinderParams;
                        if (Math.Abs(c[6] * 1000 - 52.5) < 1e-5 && Math.Abs(Math.Abs(c[3]) - 1) < 1e-8)
                            edges.Add(edge);
                    }
            if (edges.Count != 1) throw new InvalidOperationException($"Expected one D6/D105 entry edge, found {edges.Count}.");
            _model.ClearSelection2(true);
            if (!((Entity)edges[0]).Select4(false, null)) throw new InvalidOperationException("Entry edge selection failed.");
            double before = CurrentTrialVolume();
            var feature = (Feature?)_model.FeatureManager.InsertFeatureChamfer(0,
                (int)swChamferType_e.swChamferAngleDistance, .001, Math.PI / 4, 0, 0, 0, 0);
            if (feature == null) throw new InvalidOperationException("Radial entry chamfer failed.");
            feature.Name = $"D6_entry_chamfer_1x45_{degrees}";
            _model.ForceRebuild3(false);
            int error = feature.GetErrorCode2(out bool warning);
            if (error != 0 || warning) throw new InvalidOperationException($"Entry chamfer error={error}, warning={warning}.");
            double removed = before - CurrentTrialVolume();
            // Broad sanity bound only: curved D105 entry is not a plane countersink.
            if (removed <= 1 || removed >= 40) throw new InvalidOperationException($"Unexpected entry removal {removed}.");
            reports.Add(new { AngleDegrees = degrees, Feature = feature.Name, DistanceMm = 1,
                AngleDegreesRequested = 45, RemovedMm3 = removed, FeatureError = error });
            Console.WriteLine($"[OK] {feature.Name}: removed {removed:F6} mm3.");
        }
        return new { InitialVolumeMm3 = initial, FinalVolumeMm3 = CurrentTrialVolume(), Operations = reports,
            Scope = "Native 1 mm/45 degree edge chamfer at curved D105/D6 intersection; volume sanity bound only, not independently verified conical countersink geometry." };
    }

    public object CreateRadialEntryCones()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name.StartsWith("D6_entry_chamfer_") || f.Name.StartsWith("TRIAL_D6_entry_cone_"))
                throw new InvalidOperationException("Use the channel source without entry chamfers/cones.");
        VerifyChannelTrial();
        double coarse = EntryConeStudy.RemovedVolume(400), fine = EntryConeStudy.RemovedVolume(800);
        if (Math.Abs(coarse - fine) > .001) throw new InvalidOperationException("Entry cone integral convergence failed.");
        double initial = CurrentTrialVolume();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            double a = degrees * Math.PI / 180;
            double[] At(double r) => [108, r * Math.Sin(a), -r * Math.Cos(a)];
            double before = CurrentTrialVolume();
            CutChannelCylinder($"TRIAL_D6_entry_cone_{degrees}", At(54.5), At(50.5), 4,
                [0, Math.Cos(a), Math.Sin(a)], cone: true);
            double removed = before - CurrentTrialVolume();
            if (Math.Abs(removed - fine) > .005) throw new InvalidOperationException($"Entry cone volume {removed} vs {fine}.");
            Console.WriteLine($"[OK] Entry cone {degrees}: CAD={removed:F9}, integral={fine:F9} mm3.");
        }
        var shape = InspectRadialEntryChamfers(requireCone: true);
        return new { InitialVolumeMm3 = initial, FinalVolumeMm3 = CurrentTrialVolume(),
            ExpectedOneConeMm3 = fine, CoarseIntegralMm3 = coarse, Shape = shape };
    }

    private static void ValidateChannelCutExtent(double length, double radius, bool cone, double coneAngle, bool exactApex)
    {
        double endRadius=radius-length/2*Math.Tan(coneAngle);
        // Exact drill points are valid. Accept only coordinate roundoff here,
        // not a finite overrun past the apex (units:mm). Other cuts stay strict.
        if(!double.IsFinite(length)||length<=1e-8||(exactApex&&!cone)||
            (cone&&(exactApex ? Math.Abs(endRadius)>1e-10 : endRadius<=0)))
            throw new ArgumentException("Degenerate channel cutter or cone beyond apex.");
    }
    private void CutChannelCylinder(string name, double[] from, double[] to, double radius, double[] tangent, bool cone = false, double coneAngle = Math.PI / 4, bool exactApex = false)
    {
        if (_model == null || _swApp == null) throw new InvalidOperationException("No model.");
        if (from.Length != 3 || to.Length != 3 || tangent.Length != 3 ||
            !from.Concat(to).Concat(tangent).All(double.IsFinite) || !double.IsFinite(radius) || radius <= 0 ||
            !double.IsFinite(coneAngle) || (cone && (coneAngle <= 0 || coneAngle >= Math.PI / 2)))
            throw new ArgumentException("Invalid channel cutter geometry.");
        double[] delta = Enumerable.Range(0, 3).Select(i => to[i] - from[i]).ToArray();
        double length = Math.Sqrt(delta.Sum(v => v * v));
        ValidateChannelCutExtent(length,radius,cone,coneAngle,exactApex);
        double[] direction = delta.Select(v => v / length).ToArray();
        double[] mid = Enumerable.Range(0, 3).Select(i => (to[i] + from[i]) / 2).ToArray();
        double[] cross = [direction[1] * tangent[2] - direction[2] * tangent[1],
            direction[2] * tangent[0] - direction[0] * tangent[2], direction[0] * tangent[1] - direction[1] * tangent[0]];
        _model.ClearSelection2(true);
        var sm = _model.SketchManager;
        sm.Insert3DSketch(true);
        var points = new List<SketchPoint>();
        using (var exact = new ExactSketchCoordinates(sm))
            foreach (double[] p in new[] { mid, Enumerable.Range(0, 3).Select(i => mid[i] + 10 * tangent[i]).ToArray(),
                Enumerable.Range(0, 3).Select(i => mid[i] + 10 * cross[i]).ToArray() })
                points.Add(sm.CreatePoint(p[0] / 1000, p[1] / 1000, p[2] / 1000)
                    ?? throw new InvalidOperationException("Channel plane point failed."));
        sm.Insert3DSketch(true);
        _model.ClearSelection2(true);
        for (int i = 0; i < 3; i++)
            if (!points[i].Select2(i != 0, i)) throw new InvalidOperationException("Channel plane point selection failed.");
        if (_model.FeatureManager.InsertRefPlane(4, 0, 4, 0, 4, 0) == null)
            throw new InvalidOperationException("Channel three-point plane failed.");
        Feature? plane = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "RefPlane") plane = f;
        if (plane == null) throw new InvalidOperationException("No channel plane feature.");
        plane.Name = name + "_plane";
        _model.ClearSelection2(true);
        plane.Select2(false, 0);
        sm.InsertSketch(true);
        var sketch = (Sketch)sm.ActiveSketch;
        var math = (MathUtility)_swApp.GetMathUtility();
        double[] Local(double[] p) => (double[])((MathPoint)((MathPoint)math.CreatePoint(p.Select(v => v / 1000).ToArray()))
            .MultiplyTransform(sketch.ModelToSketchTransform)).ArrayData;
        var centre = Local(mid);
        var normalPoint = Local(Enumerable.Range(0, 3).Select(i => mid[i] + 10 * direction[i]).ToArray());
        if (Math.Abs(centre[2]) > 1e-8 || Math.Abs(normalPoint[0] - centre[0]) > 1e-8 ||
            Math.Abs(normalPoint[1] - centre[1]) > 1e-8 || Math.Abs(Math.Abs(normalPoint[2]) - .01) > 1e-8)
            throw new InvalidOperationException("Channel sketch plane does not match the calculated cutter axis.");
        using (var exact = new ExactSketchCoordinates(sm))
            if (sm.CreateCircleByRadius(centre[0], centre[1], 0, radius / 1000) == null)
                throw new InvalidOperationException("Channel circle failed.");
        sm.InsertSketch(true);
        Feature? profile = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.GetTypeName2() == "ProfileFeature") profile = f;
        if (profile == null) throw new InvalidOperationException("Missing channel sketch.");
        _model.ClearSelection2(true);
        profile.Select2(false, 0);
        var cut = (Feature?)_model.FeatureManager.FeatureCut3(cone, false, cone && normalPoint[2] > centre[2],
            (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
            length / 2000, length / 2000, cone, false, false, false, cone ? coneAngle : 0, 0, false, false, false, false,
            false, true, true, false, false, false, (int)swStartConditions_e.swStartSketchPlane, 0, false);
        if (cut == null) throw new InvalidOperationException($"Channel cut failed: {name}.");
        cut.Name = name;
        _model.ForceRebuild3(false);
        int error = cut.GetErrorCode2(out bool warning);
        if (error != 0) throw new InvalidOperationException($"Channel feature {name}: {error}, warning={warning}.");
        Console.WriteLine($"[TRIAL] {name}: D={radius * 2}, finite length={length:F6}.");
    }

    public object CreateChannelTrial(ChannelTrialStudy.Integral expected)
    {
        ChannelTrialStudy.RunChecks();
        double initial = CurrentTrialVolume();
        var operations = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            double a = degrees * Math.PI / 180;
            double[] At(double x, double r) => [x, r * Math.Sin(a), -r * Math.Cos(a)];
            double[] tangent = [0, Math.Cos(a), Math.Sin(a)];
            double before = CurrentTrialVolume();
            CutChannelCylinder($"TRIAL_D63_extended_{degrees}", At(265, 51),
                At(ChannelTrialStudy.EndX, ChannelTrialStudy.CentreRadius(ChannelTrialStudy.EndX)), 3.15, tangent);
            double afterMain = CurrentTrialVolume(), mainRemoval = before - afterMain;
            if (Math.Abs(mainRemoval - expected.MainPairMm3 / 2) > .25)
                throw new InvalidOperationException($"Main channel volume mismatch: {mainRemoval} vs {expected.MainPairMm3 / 2}.");
            CutChannelCylinder($"TRIAL_D6_radial_{degrees}", At(108, 55),
                At(108, ChannelTrialStudy.CentreRadius(108)), 3, tangent);
            double afterRadial = CurrentTrialVolume(), radialRemoval = afterMain - afterRadial;
            if (Math.Abs(radialRemoval - (expected.ConnectedPairMm3 - expected.MainPairMm3) / 2) > .25)
                throw new InvalidOperationException($"Radial channel volume mismatch: {radialRemoval}.");
            operations.Add(new { AngleDegrees = degrees, MainRemovedMm3 = mainRemoval, RadialRemovedMm3 = radialRemoval });
        }
        double final = CurrentTrialVolume();
        if (Math.Abs(initial - final - expected.ConnectedPairMm3) > .5)
            throw new InvalidOperationException("Connected channel pair volume mismatch.");
        return new { InitialVolumeMm3 = initial, ActualVolumeMm3 = final,
            ExpectedRemovedMm3 = expected.ConnectedPairMm3, ActualRemovedMm3 = initial - final,
            Operations = operations };
    }

    public object VerifyChannelTrial(bool requireOriginalMouth = true)
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var found = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            double a = degrees * Math.PI / 180;
            double[] mainAxis = [Math.Cos(ChannelTrialStudy.Angle), Math.Sin(ChannelTrialStudy.Angle) * Math.Sin(a),
                -Math.Sin(ChannelTrialStudy.Angle) * Math.Cos(a)];
            double[] radialAxis = [0, Math.Sin(a), -Math.Cos(a)];
            bool Matches(Surface surface, double radius, double[] axis, double[] point)
            {
                if (!surface.IsCylinder()) return false;
                var c = (double[])surface.CylinderParams;
                if (Math.Abs(c[6] * 1000 - radius) > 1e-4 ||
                    Math.Abs(Math.Abs(c[3] * axis[0] + c[4] * axis[1] + c[5] * axis[2]) - 1) > 1e-8) return false;
                var d = Enumerable.Range(0, 3).Select(i => c[i] * 1000 - point[i]).ToArray();
                double projection = d.Zip(axis).Sum(p => p.First * p.Second);
                return Math.Sqrt(d.Select((v, i) => Math.Pow(v - projection * axis[i], 2)).Sum()) < 1e-4;
            }
            double[] mainPoint = [265, 51 * Math.Sin(a), -51 * Math.Cos(a)];
            double[] radialPoint = [108, 0, 0];
            Feature? Find(string name)
            {
                for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                    if (f.Name == name) return f;
                return null;
            }
            var main = Find($"TRIAL_D63_extended_{degrees}");
            var radial = Find($"TRIAL_D6_radial_{degrees}");
            if (main == null || radial == null) throw new InvalidOperationException("Missing channel features.");
            var mainFaces = ((object[])main.GetFaces()).Cast<Face2>().Where(f => Matches((Surface)f.GetSurface(), 3.15, mainAxis, mainPoint)).ToArray();
            var radialFaces = ((object[])radial.GetFaces()).Cast<Face2>().Where(f => Matches((Surface)f.GetSurface(), 3, radialAxis, radialPoint)).ToArray();
            if (mainFaces.Length == 0 || radialFaces.Length == 0) throw new InvalidOperationException("Missing channel cylindrical faces.");
            int connections = 0;
            foreach (Face2 face in radialFaces)
                foreach (Edge edge in (object[])face.GetEdges())
                    if (((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>()
                        .Any(adjacent => Matches((Surface)adjacent.GetSurface(), 3.15, mainAxis, mainPoint))) connections++;
            if (connections == 0) throw new InvalidOperationException("No actual BREP connection between channel cylinders.");
            int rearFloorEdges = 0;
            foreach (Face2 face in mainFaces)
                foreach (Edge edge in (object[])face.GetEdges())
                    foreach (Face2 adjacent in ((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>())
                    {
                        var surface = (Surface)adjacent.GetSurface();
                        if (!surface.IsPlane()) continue;
                        var p = (double[])surface.PlaneParams;
                        if (Math.Abs(Math.Abs(p[0]) - 1) < 1e-7 && Math.Abs(p[1]) < 1e-7 && Math.Abs(p[2]) < 1e-7 &&
                            Math.Abs(p[3] * 1000 - DrawingRevision.RearRecessStart) < 1e-4) rearFloorEdges++;
                    }
            if (requireOriginalMouth && rearFloorEdges == 0) throw new InvalidOperationException("Expected trial channel opening at rear annular floor not found.");
            found.Add(new { AngleDegrees = degrees, MainFaces = mainFaces.Length, RadialFaces = radialFaces.Length,
                SharedEdges = connections, RearRecessFloorIntersectionEdges = rearFloorEdges, ClosedRearM8SeatVerified = false });
            Console.WriteLine($"[LIMITATION] Channel {degrees}: {rearFloorEdges} edges at rear cavity floor X260; no sealed M8 seat claimed.");
            Console.WriteLine($"[OK] Channel {degrees}: correct D6.3/4deg and D6 axes; {connections} shared intersection edges.");
        }
        return found;
    }
}

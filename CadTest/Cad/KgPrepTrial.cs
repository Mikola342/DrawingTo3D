using CadTest.Drawing;
using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object CreateKgPrepTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name.StartsWith("APPROX_KG_")) throw new InvalidOperationException("KG preparation already present.");
        KgPrepStudy.RunChecks();
        double expected = KgPrepStudy.RemovedVolume(800);
        var reports = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            double a = degrees * Math.PI / 180, before = CurrentTrialVolume();
            double[] tangent = [0, Math.Cos(a), Math.Sin(a)];
            CutChannelCylinder($"APPROX_KG_D8_transition_{degrees}", KgPrepStudy.Point(7, degrees), KgPrepStudy.Point(9, degrees), 4, tangent);
            // Midpoint at t=-2, one-sided tapered cut to t=7; first endpoint is construction only.
            CutChannelCylinder($"APPROX_KG_tapered_pilot_{degrees}", KgPrepStudy.Point(-11, degrees), KgPrepStudy.Point(7, degrees),
                KgPrepStudy.Radius(-2), tangent, cone: true, coneAngle: Math.Atan(1.0 / 32));
            double removed = before - CurrentTrialVolume();
            if (Math.Abs(removed - expected) > .1) throw new InvalidOperationException($"KG pilot removal {removed} vs {expected}.");
            reports.Add(new { AngleDegrees = degrees, RemovedMm3 = removed, ExpectedMm3 = expected });
        }
        return new { Operations = reports, Geometry = InspectKgPrepTrial(), VolumeMm3 = CurrentTrialVolume(),
            IsThread = false, UserApprovedApproximation = true,
            Assumptions = "Tapered pilot D8.8 at R127, 1:16 diameter taper, inward length7; D8 transition t7..9, flat bottom into existing D6.8. No helical KG profile, no OST or sealing acceptance." };
    }

    public object InspectKgPrepTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var reports = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            Feature? feature = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == $"APPROX_KG_tapered_pilot_{degrees}") feature = f;
            if (feature == null || feature.GetErrorCode2(out bool warning) != 0 || warning)
                throw new InvalidOperationException("KG pilot missing or failed.");
            double a = degrees * Math.PI / 180;
            var origin = KgPrepStudy.Point(0, degrees);
            double[] axis = [-Math.Sin(RearFeedTrial.Angle), -Math.Cos(RearFeedTrial.Angle) * Math.Sin(a), Math.Cos(RearFeedTrial.Angle) * Math.Cos(a)];
            int samples = 0, cones = 0, connections = 0;
            double maxError = 0, maxT = double.NegativeInfinity;
            foreach (Face2 face in (object[])feature.GetFaces())
            {
                var surface = (Surface)face.GetSurface();
                if (!surface.IsCone()) continue;
                cones++;
                var c = (double[])surface.ConeParams2;
                if (Math.Abs(Math.Abs(c[7]) - Math.Atan(1.0 / 32)) > 1e-8)
                    throw new InvalidOperationException("KG pilot taper angle mismatch.");
                foreach (Edge edge in (object[])face.GetEdges())
                {
                    foreach (Face2 adj in ((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>())
                    {
                        var s = (Surface)adj.GetSurface();
                        if (s.IsCylinder() && Math.Abs(((double[])s.CylinderParams)[6] - .004) < 1e-8) connections++;
                    }
                    var bounds = edge.GetCurveParams3();
                    M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
                    for (int i = 0; i <= 32; i++)
                    {
                        var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * i / 32, 0);
                        if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                            throw new InvalidOperationException("KG pilot edge evaluation.");
                        double[] d = Enumerable.Range(0, 3).Select(k => p[k] * 1000 - origin[k]).ToArray();
                        double t = d.Zip(axis).Sum(v => v.First * v.Second);
                        double r = Math.Sqrt(d.Select((v, k) => Math.Pow(v - t * axis[k], 2)).Sum());
                        maxError = Math.Max(maxError, Math.Abs(r - (4.4 - t / 32)));
                        maxT = Math.Max(maxT, t); samples++;
                    }
                }
            }
            if (cones == 0 || samples == 0 || maxError > .0001 || Math.Abs(maxT - 7) > .0001)
                throw new InvalidOperationException($"KG pilot boundary mismatch {maxError}, {maxT}.");
            Feature? transition = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == $"APPROX_KG_D8_transition_{degrees}") transition = f;
            if (transition == null || transition.GetErrorCode2(out bool transitionWarning) != 0 || transitionWarning)
                throw new InvalidOperationException("KG D8 transition missing/failed.");
            double minTransition = double.PositiveInfinity, maxTransition = double.NegativeInfinity;
            int transitionSamples = 0;
            foreach (Face2 face in (object[])transition.GetFaces())
            {
                var s = (Surface)face.GetSurface();
                if (!s.IsCylinder()) continue;
                var c = (double[])s.CylinderParams;
                if (Math.Abs(c[6] * 1000 - 4) > .0001 ||
                    Math.Abs(Math.Abs(Enumerable.Range(0, 3).Sum(k => c[k + 3] * axis[k])) - 1) > 1e-8)
                    throw new InvalidOperationException("KG D8 transition radius/axis mismatch.");
                foreach (Edge edge in (object[])face.GetEdges())
                {
                    var b = edge.GetCurveParams3(); M12Study.ValidateEdgeInterval(b.UMinValue, b.UMaxValue);
                    for (int i = 0; i <= 16; i++)
                    {
                        var p = (double[])edge.Evaluate2(b.UMinValue + (b.UMaxValue - b.UMinValue) * i / 16, 0);
                        if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                            throw new InvalidOperationException("KG D8 edge evaluation failed.");
                        var d = Enumerable.Range(0, 3).Select(k => p[k] * 1000 - origin[k]).ToArray();
                        double t = d.Zip(axis).Sum(v => v.First * v.Second);
                        double r = Math.Sqrt(d.Select((v, k) => Math.Pow(v - t * axis[k], 2)).Sum());
                        if (Math.Abs(r - 4) > .0001) throw new InvalidOperationException("KG D8 transition position mismatch.");
                        minTransition = Math.Min(minTransition, t); maxTransition = Math.Max(maxTransition, t); transitionSamples++;
                    }
                }
            }
            if (transitionSamples == 0 || Math.Abs(minTransition - 7) > .0001 || Math.Abs(maxTransition - 9) > .0001)
                throw new InvalidOperationException("KG D8 transition depth mismatch.");
            // Cone joins D8 via an annular planar shoulder, not necessarily a shared cone/cylinder edge.
            reports.Add(new { AngleDegrees = degrees, ConeFaces = cones, Samples = samples, MaxResidualMm = maxError, MaxT = maxT,
                DirectConeD8Edges = connections, TransitionSamples = transitionSamples, MinTransition = minTransition, MaxTransition = maxTransition });
            Console.WriteLine($"[OK] APPROX KG pilot {degrees}: {samples} samples, residual={maxError:F9}, end={maxT:F6}.");
        }
        return reports;
    }
}

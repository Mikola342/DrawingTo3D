using CadTest.Drawing;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private Body2 CopyPilotBody()
    {
        var bodies = (object[])((PartDoc)_model!).GetBodies2((int)swBodyType_e.swSolidBody, false);
        if (bodies.Length != 1) throw new InvalidOperationException("Pilot requires one solid body.");
        return (Body2)((Body2)bodies[0]).Copy();
    }

    private object InspectPilotGeometry(double degrees)
    {
        Feature? feature = null;
        for (Feature? f = (Feature?)_model!.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name == $"TRIAL_M8_pilot_{degrees}") feature = f;
        if (feature == null) throw new InvalidOperationException("Pilot missing.");
        double a = degrees * Math.PI / 180, b = ChannelTrialStudy.Angle;
        double[] axis = [Math.Cos(b), Math.Sin(b) * Math.Sin(a), -Math.Sin(b) * Math.Cos(a)];
        double r = ChannelTrialStudy.CentreRadius(254);
        double[] origin = [254, r * Math.Sin(a), -r * Math.Cos(a)];
        int cylinders = 0, samples = 0;
        double minS = double.PositiveInfinity, maxS = double.NegativeInfinity, maxError = 0;
        foreach (Face2 face in (object[])feature.GetFaces())
        {
            var surface = (Surface)face.GetSurface();
            if (!surface.IsCylinder()) continue;
            var c = (double[])surface.CylinderParams;
            if (Math.Abs(c[6] * 1000 - M8PilotStudy.Radius) > 1e-6) throw new InvalidOperationException("Pilot radius mismatch.");
            if (Math.Abs(Math.Abs(Enumerable.Range(0, 3).Sum(i => axis[i] * c[i + 3])) - 1) > 1e-8)
                throw new InvalidOperationException("Pilot direction mismatch.");
            cylinders++;
            foreach (Edge edge in (object[])face.GetEdges())
            {
                var bounds = edge.GetCurveParams3();
                M12Study.ValidateEdgeInterval(bounds.UMinValue, bounds.UMaxValue);
                for (int j = 0; j <= 64; j++)
                {
                    var p = (double[])edge.Evaluate2(bounds.UMinValue + (bounds.UMaxValue - bounds.UMinValue) * j / 64, 0);
                    if (p.Length != 4 || (BitConverter.DoubleToInt64Bits(p[3]) & 0xffffffffL) != 1 || !p.Take(3).All(double.IsFinite))
                        throw new InvalidOperationException("Pilot edge evaluation failed.");
                    var d = Enumerable.Range(0, 3).Select(i => p[i] * 1000 - origin[i]).ToArray();
                    double s = d.Zip(axis).Sum(v => v.First * v.Second);
                    double rho = Math.Sqrt(d.Select((v, i) => Math.Pow(v - s * axis[i], 2)).Sum());
                    maxError = Math.Max(maxError, Math.Abs(rho - M8PilotStudy.Radius));
                    minS = Math.Min(minS, s); maxS = Math.Max(maxS, s); samples++;
                }
            }
        }
        double length = 6 / Math.Cos(b);
        if (cylinders == 0 || samples == 0 || maxError > .0001 || Math.Abs(minS) > .0001 || Math.Abs(maxS - length) > .0001)
            throw new InvalidOperationException($"Pilot geometry mismatch: N={samples}, error={maxError}, ends={minS}/{maxS}.");
        Console.WriteLine($"[OK] Pilot geometry {degrees}: {samples} samples, radial error {maxError:F9}, ends {minS:F9}/{maxS:F9}.");
        return new { Cylinders = cylinders, EdgeSamples = samples, MaximumRadialErrorMm = maxError, MinimumAxialMm = minS, MaximumAxialMm = maxS };
    }

    public object CreateM8PilotTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name.StartsWith("TRIAL_M8_pilot_")) throw new InvalidOperationException("Pilot already exists.");
        double coarse = M8PilotStudy.RemovedVolume(400), fine = M8PilotStudy.RemovedVolume(800);
        if (fine <= 0 || Math.Abs(coarse - fine) > .01) throw new InvalidOperationException($"Pilot integral {coarse}/{fine}.");
        double initial = CurrentTrialVolume();
        var operations = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            double a = degrees * Math.PI / 180;
            double[] At(double x) => [x, ChannelTrialStudy.CentreRadius(x) * Math.Sin(a), -ChannelTrialStudy.CentreRadius(x) * Math.Cos(a)];
            double before = CurrentTrialVolume();
            var beforeBody = CopyPilotBody();
            CutChannelCylinder($"TRIAL_M8_pilot_{degrees}", At(260), At(254), M8PilotStudy.Radius, [0, Math.Cos(a), Math.Sin(a)]);
            double removal = before - CurrentTrialVolume();
            var geometry = InspectPilotGeometry(degrees);
            var pieces = (object[]?)beforeBody.Operations2((int)swBodyOperationType_e.SWBODYCUT, CopyPilotBody(), out int error);
            if (error != 0 || pieces == null || pieces.Length == 0) throw new InvalidOperationException($"Pilot removed-body boolean failed: {error}.");
            double directRemoval = pieces.Cast<Body2>().Sum(p => ((double[])p.GetMassProperties(1))[3] * 1e9);
            Console.WriteLine($"[DIAGNOSTIC] Pilot {degrees}: whole-part difference={removal:F9}; isolated removed-body={directRemoval:F9}; integral={fine:F9}.");
            if (!double.IsFinite(directRemoval) || Math.Abs(directRemoval - fine) > .02)
                throw new InvalidOperationException($"Pilot isolated removal {directRemoval} vs {fine}.");
            operations.Add(new { AngleDegrees = degrees, WholePartVolumeDifferenceMm3 = removal, IsolatedRemovedMm3 = directRemoval, Geometry = geometry });
        }
        return new { InitialVolumeMm3 = initial, FinalVolumeMm3 = CurrentTrialVolume(), DiameterMm = 2 * M8PilotStudy.Radius,
            CoarseIntegralMm3 = coarse, ExpectedOnePilotMm3 = fine, Operations = operations,
            Limitations = "Nominal basic minor-diameter envelope, NOT tapping drill selection or M8-6H thread. Experimental end-centres X254/260, flat ends normal to 4-degree axis. No chamfer, thread or plug seal." };
    }
}

using CadTest.Drawing;
using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object CreateRearFeedTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name.StartsWith("TRIAL_D68_feed_")) throw new InvalidOperationException("Feed already exists.");
        double coarse = RearFeedTrial.RemovedVolume(400), fine = RearFeedTrial.RemovedVolume(800);
        if (Math.Abs(coarse - fine) > .05) throw new InvalidOperationException($"Feed integral convergence: {coarse}/{fine}.");
        var reports = new List<object>();
        double initial = CurrentTrialVolume();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            double a = degrees * Math.PI / 180;
            double[] At(double r) => [RearFeedTrial.X(r), r * Math.Sin(a), -r * Math.Cos(a)];
            double before = CurrentTrialVolume();
            string name = $"TRIAL_D68_feed_{degrees}";
            CutChannelCylinder(name, At(RearFeedTrial.StartRadius), At(RearFeedTrial.EndRadius), 3.4,
                [0, Math.Cos(a), Math.Sin(a)]);
            double removal = before - CurrentTrialVolume();
            if (Math.Abs(removal - fine) > .1) throw new InvalidOperationException($"Feed removal {removal} vs {fine}.");
            reports.Add(new { AngleDegrees = degrees, RemovedMm3 = removal });
            Console.WriteLine($"[OK] Feed {degrees}: removal={removal:F6}, expected={fine:F6}.");
        }
        var geometry = InspectRearFeedTrial();
        return new { InitialVolumeMm3 = initial, FinalVolumeMm3 = CurrentTrialVolume(), ExpectedOneFeedMm3 = fine,
            CoarseIntegralMm3 = coarse, EndRadius = RearFeedTrial.EndRadius, CutterLengthMm = RearFeedTrial.Length, Operations = reports,
            Geometry = geometry,
            Assumptions = "Coplanar 60/120 degrees, X257.5 at R127, 7 degrees from radial, flat cutter ends 1 radial mm beyond axis crossing. No thread or drill-tip claim." };
    }

    public object InspectRearFeedTrial()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        var reports = new List<object>();
        foreach (double degrees in ChannelTrialStudy.BranchAngles)
        {
            double a = degrees * Math.PI / 180;
            double[] At(double r) => [RearFeedTrial.X(r), r * Math.Sin(a), -r * Math.Cos(a)];
            string name = $"TRIAL_D68_feed_{degrees}";
            Feature? feature = null;
            for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                if (f.Name == name) feature = f;
            if (feature == null) throw new InvalidOperationException($"Missing {name}.");
            int error = feature.GetErrorCode2(out bool warning);
            if (error != 0 || warning) throw new InvalidOperationException($"Feed feature error {error}, warning {warning}.");
            int connections = 0, cylinders = 0;
            foreach (Face2 face in (object[])feature.GetFaces())
            {
                var surface = (Surface)face.GetSurface();
                if (!surface.IsCylinder()) continue;
                var c = (double[])surface.CylinderParams;
                if (Math.Abs(c[6] * 1000 - 3.4) > 1e-5) continue;
                double[] axis = [Math.Sin(RearFeedTrial.Angle), Math.Cos(RearFeedTrial.Angle) * Math.Sin(a), -Math.Cos(RearFeedTrial.Angle) * Math.Cos(a)];
                double dot = Enumerable.Range(0, 3).Sum(i => c[i + 3] * axis[i]);
                var p = At(127);
                var d = Enumerable.Range(0, 3).Select(i => c[i] * 1000 - p[i]).ToArray();
                double projection = d.Zip(axis).Sum(v => v.First * v.Second);
                if (Math.Abs(Math.Abs(dot) - 1) > 1e-8 || Math.Sqrt(d.Select((v, i) => Math.Pow(v - projection * axis[i], 2)).Sum()) > 1e-4)
                    throw new InvalidOperationException("Feed cylinder axis mismatch.");
                cylinders++;
                foreach (Edge edge in (object[])face.GetEdges())
                    foreach (Face2 adjacent in ((object[])edge.GetTwoAdjacentFaces2()).OfType<Face2>())
                    {
                        var s = (Surface)adjacent.GetSurface();
                        if (!s.IsCylinder()) continue;
                        var main = (double[])s.CylinderParams;
                        if (Math.Abs(main[6] * 1000 - 3.15) > 1e-5) continue;
                        double[] mainAxis = [Math.Cos(ChannelTrialStudy.Angle), Math.Sin(ChannelTrialStudy.Angle) * Math.Sin(a), -Math.Sin(ChannelTrialStudy.Angle) * Math.Cos(a)];
                        double[] mainPoint = [265, 51 * Math.Sin(a), -51 * Math.Cos(a)];
                        var offset = Enumerable.Range(0, 3).Select(i => main[i] * 1000 - mainPoint[i]).ToArray();
                        double axial = offset.Zip(mainAxis).Sum(v => v.First * v.Second);
                        if (Math.Abs(Math.Abs(Enumerable.Range(0, 3).Sum(i => main[i + 3] * mainAxis[i])) - 1) > 1e-8 ||
                            Math.Sqrt(offset.Select((v, i) => Math.Pow(v - axial * mainAxis[i], 2)).Sum()) > 1e-4)
                            throw new InvalidOperationException("Feed connects to an unexpected D6.3 axis.");
                        connections++;
                    }
            }
            if (cylinders == 0 || connections == 0) throw new InvalidOperationException("Missing feed/long-channel connection.");
            reports.Add(new { AngleDegrees = degrees, Cylinders = cylinders, SharedEdgesWithD63 = connections, ConnectedMainAxisChecked = true });
            Console.WriteLine($"[OK] Saved feed {degrees}: cylinder axis checked; {connections} shared edges with correct D6.3 axis.");
        }
        return reports;
    }
}

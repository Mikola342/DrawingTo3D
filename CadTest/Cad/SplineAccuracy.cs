using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using CadTest.Drawing;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object CheckTrialInvolutes()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        Feature? cut = null;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
            if (f.Name == "TRIAL_38_spaces_to_X70_RootApprox") cut = f;
        if (cut == null) throw new InvalidOperationException("Trial straight-cut feature missing.");
        Sketch? sketch = null;
        for (Feature? f = (Feature?)cut.GetFirstSubFeature(); f != null; f = (Feature?)f.GetNextSubFeature())
            if (f.GetTypeName2() == "ProfileFeature") sketch = (Sketch)f.GetSpecificFeature2();
        if (sketch == null) throw new InvalidOperationException("Trial sketch missing.");
        int curves = 0, samples = 0;
        double maxError = 0, maxPlaneError = 0;
        var seen = new HashSet<(int Space, int Side)>();
        var perCurve = new List<object>();
        double pitch = 2 * Math.PI / 38;
        foreach (SketchSegment segment in (object[])sketch.GetSketchSegments())
        {
            if (segment.GetType() != (int)swSketchSegments_e.swSketchSPLINE) continue;
            var curve = (Curve)segment.GetCurve();
            if (!curve.GetEndParams(out double start, out double end, out bool closed, out bool periodic) || closed || periodic || end <= start)
                throw new InvalidOperationException("Unexpected spline parameter interval.");
            double error = 0, minRadius = double.PositiveInfinity, maxRadius = 0;
            (int Space, int Side)? identity = null;
            for (int i = 0; i <= 128; i++)
            {
                var p = (double[])curve.Evaluate2(start + (end - start) * i / 128, 0);
                if (p.Length < 3 || !p.Take(3).All(double.IsFinite)) throw new InvalidOperationException("Curve evaluation failed.");
                double x = p[0] * 1000, y = p[1] * 1000, z = p[2] * 1000;
                double r = Math.Sqrt(x * x + y * y), angle = Math.Atan2(y, x);
                if (r < 47.5 - 1e-5 || r > 50 + 1e-5) throw new InvalidOperationException($"CAD flank radius outside D95..100: {r}.");
                int space = (int)Math.Round(angle / pitch);
                double local = angle - space * pitch;
                int side = Math.Sign(local);
                var id = ((space % 38 + 38) % 38, side);
                identity ??= id;
                if (identity != id || side == 0) throw new InvalidOperationException("Flank crosses a space centre.");
                double expected = Math.PI / 38 - SplineSectionStudy.HalfToothAngle(r, SplineSectionStudy.PreviewThickness);
                double distance = 2 * r * Math.Sin(Math.Abs(Math.Abs(local) - expected) / 2);
                error = Math.Max(error, distance);
                minRadius = Math.Min(minRadius, r); maxRadius = Math.Max(maxRadius, r);
                maxPlaneError = Math.Max(maxPlaneError, Math.Abs(z));
                samples++;
            }
            if (Math.Abs(minRadius - 47.5) > 1e-5 || Math.Abs(maxRadius - 50) > 1e-5 || !seen.Add(identity!.Value))
                throw new InvalidOperationException("Incomplete or duplicate CAD involute.");
            maxError = Math.Max(maxError, error);
            curves++;
            perCurve.Add(new { Space = identity.Value.Space, Side = identity.Value.Side, MaximumSampledChordErrorMm = error });
            if (curves % 10 == 0) Console.WriteLine($"[ACCURACY] {curves}/76 CAD flanks checked, max sampled error {maxError:E6} mm.");
        }
        const double threshold = 0.0001; // 0.1 micrometre, internal numerical check, not a drawing tolerance.
        if (curves != 76 || maxError > threshold || maxPlaneError > 1e-6)
            throw new InvalidOperationException($"CAD sketch involute check failed: count={curves}, error={maxError}, plane={maxPlaneError}.");
        Console.WriteLine($"[OK] CAD involutes: {curves} curves, {samples} samples, maximum error {maxError:E9} mm.");
        return new {
            Curves = curves, Samples = samples, SamplesPerCurve = 129,
            MaximumSampledChordErrorMm = maxError, InternalThresholdMm = threshold, MaximumPlaneErrorMm = maxPlaneError,
            Scope = "Underlying CAD sketch curves driving the straight cut, in sketch coordinates. Same-radius chord comparison to analytic involute. Discrete samples, NOT a continuous supremum bound or finished-surface metrology. R50 sketches and trimmed B-rep surfaces not sampled here.",
            PerCurve = perCurve
        };
    }
}

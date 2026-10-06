namespace CadTest.Drawing;

public static class SplineTrialProfile
{
    public static void WritePreview(string path)
    {
        static string F(double v) => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var points = Segments(100).SelectMany(s => s.Points.SkipLast(1));
        string polygon = string.Join(" ", points.Select(p => $"{F(450 + 7 * p.X)},{F(460 - 7 * p.Y)}"));
        File.WriteAllText(path, $"<svg xmlns='http://www.w3.org/2000/svg' width='900' height='900'><rect width='900' height='900' fill='white'/><g font-family='Arial' font-size='17'><text x='25' y='30'>TRIAL ONLY: 38 teeth, circular root R1.016129 (user-approved approximation)</text><text x='25' y='58'>Standalone 20 mm coupon. Not the full part; no R50 runout or local cuts.</text></g><polygon points='{polygon}' fill='#e4e7eb' stroke='#bd6b15' stroke-width='1.5'/><circle cx='450' cy='460' r='217' fill='white' stroke='#1368ba' stroke-width='2'/><text x='25' y='865' font-family='Arial' font-size='17'>D100 / D94 nominal; D62 bore. This is a calculated section, not a CAD screenshot.</text></svg>");
    }
    public const double Length = 20, BoreRadius = 31;
    public readonly record struct Point(double X, double Y);
    public sealed record Segment(bool Arc, Point[] Points);
    public static readonly SplineRootStudy.Candidate Root = SplineRootStudy.Evaluate(47.5);
    public static List<Segment> SpaceSegments(int samples = 32)
    {
        var tooth = Segments(samples);
        var right = new[] { tooth[0], tooth[1], tooth[2] };
        var left = new[] { tooth[2], tooth[1] }.Select(s => new Segment(s.Arc,
            s.Points.Reverse().Select(p => new Point(p.X, -p.Y)).ToArray()));
        // From negative crest down to root, across root and up to positive crest.
        var result = left.Concat(right).ToList();
        var end = result[^1].Points[^1]; var start = result[0].Points[0];
        result.Add(new(false, [end, new(60, end.Y)]));
        result.Add(new(false, [new(60, end.Y), new(60, start.Y)]));
        result.Add(new(false, [new(60, start.Y), start]));
        return result;
    }
    static Point Polar(double r, double a) => new(r * Math.Cos(a), r * Math.Sin(a));
    static Point Rotate(Point p, double a) => new(p.X * Math.Cos(a) - p.Y * Math.Sin(a), p.X * Math.Sin(a) + p.Y * Math.Cos(a));
    static double Theta(double r) => Math.PI / 38 - SplineSectionStudy.HalfToothAngle(r, SplineSectionStudy.PreviewThickness);
    public static List<Segment> Segments(int samples = 32)
    {
        if (samples < 2) throw new ArgumentOutOfRangeException(nameof(samples));
        var result = new List<Segment>();
        double pitch = 2 * Math.PI / 38, beta = Root.RootHalfAngle;
        double start = beta - Math.PI;
        double finish = Math.Atan2(Root.JoinY - Root.CentreY, Root.JoinX - Root.CentreX);
        while (finish > start) finish -= 2 * Math.PI;
        Point Fillet(double t) => new(Root.CentreX + Root.FilletRadius * Math.Cos(start + (finish - start) * t),
            Root.CentreY + Root.FilletRadius * Math.Sin(start + (finish - start) * t));
        void Add(bool arc, double rotation, Func<double, Point> f) => result.Add(new(arc,
            Enumerable.Range(0, samples + 1).Select(i => Rotate(f((double)i / samples), rotation)).ToArray()));
        for (int k = 0; k < 38; k++)
        {
            double a = k * pitch;
            Add(true, a, t => Polar(47, -beta + 2 * beta * t));
            Add(true, a, Fillet);
            Add(false, a, t => { double r = 47.5 + 2.5 * t; return Polar(r, Theta(r)); });
            Add(true, a, t => Polar(50, Theta(50) + (pitch - 2 * Theta(50)) * t));
            Add(false, a + pitch, t => { double r = 50 - 2.5 * t; return Polar(r, -Theta(r)); });
            Add(true, a + pitch, t => { var p = Fillet(1 - t); return new(p.X, -p.Y); });
        }
        return result;
    }
    public static double Volume(int samples = 2000)
    {
        var points = Segments(samples).SelectMany(s => s.Points.SkipLast(1)).ToArray();
        double area = 0;
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i]; var q = points[(i + 1) % points.Length];
            area += (p.X * q.Y - p.Y * q.X) / 2;
        }
        return (area - Math.PI * BoreRadius * BoreRadius) * Length;
    }
    public static void RunChecks()
    {
        SplineRootStudy.RunChecks();
        var s = Segments();
        for (int i = 0; i < s.Count; i++)
        {
            var p = s[i].Points[^1]; var q = s[(i + 1) % s.Count].Points[0];
            if (Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Y - q.Y, 2)) > 1e-9)
                throw new InvalidOperationException("Trial spline profile is not closed.");
        }
        double previousAngle = -Root.RootHalfAngle;
        foreach (var segment in s)
            foreach (var p in segment.Points)
            {
                double r = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                double angle = Math.Atan2(p.Y, p.X);
                while (angle < previousAngle - Math.PI) angle += 2 * Math.PI;
                if (r < 47 - 1e-9 || r > 50 + 1e-9 || angle < previousAngle - 1e-9)
                    throw new InvalidOperationException("Trial profile crosses itself or violates radial bounds.");
                previousAngle = angle;
            }
        if (Math.Abs(Volume(1000) - Volume(2000)) > 0.01 || Volume() <= 0)
            throw new InvalidOperationException("Trial spline volume did not converge.");
        Console.WriteLine($"[TEST OK] Trial: 38 teeth, closed 228-segment contour, root R{Root.FilletRadius}, volume {Volume():F6} mm3.");
    }
}

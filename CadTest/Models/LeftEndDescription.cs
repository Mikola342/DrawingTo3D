namespace CadTest.Models;

public sealed class LeftEndDescription
{
    public double FlatHeight { get; set; } = 84.5 - 45;
    public double FlatLength { get; set; } = 25;
    public double RunoutRadius { get; set; } = 35;
    public double BottomHeight { get; set; } = -(45 - 20);
    public double BottomLength { get; set; } = 3;

    public double TopBoundary(double x) => x <= FlatLength ? FlatHeight :
        FlatHeight + RunoutRadius - Math.Sqrt(Math.Max(0, RunoutRadius * RunoutRadius - Math.Pow(x - FlatLength, 2)));

    public void Validate()
    {
        if (FlatHeight != 39.5 || FlatLength != 25 || RunoutRadius != 35 || BottomHeight != -25 || BottomLength != 3)
            throw new InvalidOperationException("Left end dimensions differ from nominal drawing interpretation.");
    }

    public static double CircularCap(double radius, double height)
    {
        if (!double.IsFinite(radius) || !double.IsFinite(height) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (height >= radius) return 0;
        if (height <= -radius) return Math.PI * radius * radius;
        return radius * radius * Math.Acos(height / radius) - height * Math.Sqrt(radius * radius - height * height);
    }

    public double RemovedVolumeMm3(PartDescription part, int samples = 20000)
    {
        Validate();
        if (samples < 1) throw new ArgumentOutOfRangeException(nameof(samples));
        // Explicit breakpoints avoid integrating across steps in radius or cut shape.
        double end = FlatLength + RunoutRadius;
        var breaks = part.RevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX })
            .Concat(part.BoreRevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX }))
            .Concat(new[] { 0.0, FlatLength, BottomLength, end })
            .Where(x => x >= 0 && x <= end).Distinct().Order().ToArray();
        double result = 0;
        for (int j = 0; j + 1 < breaks.Length; j++)
        {
            double dx = (breaks[j + 1] - breaks[j]) / samples;
            for (int i = 0; i < samples; i++)
            {
                double x = breaks[j] + (i + 0.5) * dx;
                var e = part.RevolveProfile.Single(e => e.StartX <= x && e.EndX > x);
                if (e.Type != RevolveProfileElementType.Line) throw new InvalidOperationException("Left volume integration requires reviewed linear envelope.");
                double outer = e.StartRadius + (e.EndRadius - e.StartRadius) * (x - e.StartX) / (e.EndX - e.StartX);
                var b = part.BoreRevolveProfile.Single(e => e.StartX <= x && e.EndX > x);
                double inner = b.StartRadius + (b.EndRadius - b.StartRadius) * (x - b.StartX) / (b.EndX - b.StartX);
                double h = TopBoundary(x);
                double area = CircularCap(outer, h) - CircularCap(inner, h);
                if (x < BottomLength)
                    area += CircularCap(outer, -BottomHeight) - CircularCap(inner, -BottomHeight);
                result += area * dx;
            }
        }
        return result;
    }
}

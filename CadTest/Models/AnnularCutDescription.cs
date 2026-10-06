namespace CadTest.Models;

// Annular section with optional quarter-circle root fillets, revolved around X (mm).
public sealed class AnnularCutDescription
{
    public string Name { get; set; } = "Annular cut";
    public double StartX { get; set; }
    public double EndX { get; set; }
    public double InnerRadius { get; set; }
    public double OuterRadius { get; set; }
    public double RootFilletRadius { get; set; }
    // Material retained by the two quarter-circle root fillets, integrated around X.
    public double FilletRetainedVolumeMm3 => Math.PI *
        (4 * InnerRadius * RootFilletRadius * RootFilletRadius
        + 10.0 / 3 * Math.Pow(RootFilletRadius, 3)
        - Math.PI * (InnerRadius + RootFilletRadius) * RootFilletRadius * RootFilletRadius);
    public double VolumeMm3 => Math.PI * (EndX - StartX) *
        (OuterRadius * OuterRadius - InnerRadius * InnerRadius) - FilletRetainedVolumeMm3;

    public double InnerRadiusAt(double x)
    {
        double f = RootFilletRadius;
        double distance = Math.Min(x - StartX, EndX - x);
        if (f == 0 || distance >= f) return InnerRadius;
        double u = f - Math.Max(0, distance);
        return InnerRadius + f - Math.Sqrt(Math.Max(0, f * f - u * u));
    }

    public IEnumerable<(double X, double R)> BoundaryPoints(int arcSamples = 40)
    {
        for (int i = 0; i <= arcSamples; i++)
        {
            double x = StartX + (EndX - StartX) * i / arcSamples;
            yield return (x, InnerRadiusAt(x));
        }
    }

    public void Validate()
    {
        if (!new[] { StartX, EndX, InnerRadius, OuterRadius, RootFilletRadius }.All(double.IsFinite) ||
            StartX < 0 || EndX <= StartX || InnerRadius <= 0 || OuterRadius <= InnerRadius ||
            RootFilletRadius < 0 || 2 * RootFilletRadius > EndX - StartX || InnerRadius + RootFilletRadius >= OuterRadius)
            throw new InvalidOperationException($"Invalid annular cut: {Name}.");
    }
}

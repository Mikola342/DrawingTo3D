using CadTest.Models;

namespace CadTest.Drawing;

public static class MassLocalizationStudy
{
    public sealed record Band(double StartX, double EndX, double AxisymmetricEnvelopeMm3,
        double ReferenceMassKg, string Scope);
    private static readonly double[] Stations = [0, 95, 183, 239, 247, 265, 273];

    // Independent midpoint integration split at every profile and recess boundary.
    // These are envelope volumes, NOT final CAD volumes partitioned by X.
    private static Band[] Integrate(int n)
    {
        var part = DrawingRevision.Create();
        var bands = new List<Band>();
        for (int k = 0; k < Stations.Length - 1; k++)
        {
            double lo = Stations[k], hi = Stations[k + 1], total = 0;
            var breaks = part.RevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX })
                .Concat(part.BoreRevolveProfile.SelectMany(e => new[] { e.StartX, e.EndX }))
                .Concat(part.AnnularCuts.SelectMany(c => new[] { c.StartX, c.EndX, (c.StartX + c.EndX) / 2 }))
                .Append(lo).Append(hi).Where(x => x >= lo && x <= hi).Distinct().Order().ToArray();
            for (int j = 0; j < breaks.Length - 1; j++)
            {
                double dx = (breaks[j + 1] - breaks[j]) / n;
                for (int i = 0; i < n; i++)
                {
                    double x = breaks[j] + (i + .5) * dx;
                    double outer = ChannelTrialStudy.OuterRadius(part, x);
                    double bore = ChannelTrialStudy.BoreRadius(part, x);
                    double area = Math.PI * (outer * outer - bore * bore);
                    foreach (var c in part.AnnularCuts.Where(c => x >= c.StartX && x < c.EndX))
                    {
                        double r0 = Math.Max(bore, c.InnerRadiusAt(x)), r1 = Math.Min(outer, c.OuterRadius);
                        if (r1 > r0) area -= Math.PI * (r1 * r1 - r0 * r0);
                    }
                    if (area < 0) throw new InvalidOperationException("Negative material section.");
                    total += area * dx;
                }
            }
            bands.Add(new(lo, hi, total, total * 7850e-9,
                "Axisymmetric envelope with bore/recess, BEFORE local flats, holes, splines, channels and threads."));
        }
        return bands.ToArray();
    }

    public static object CreateReport(double actualVolume)
    {
        var mass = DrawingConformanceAudit.Mass(actualVolume);
        var part = DrawingRevision.Create();
        var fine = Integrate(4000); var coarse = Integrate(2000);
        double delta = fine.Zip(coarse).Max(p => Math.Abs(p.First.AxisymmetricEnvelopeMm3 - p.Second.AxisymmetricEnvelopeMm3));
        double envelope = fine.Sum(b => b.AxisymmetricEnvelopeMm3);
        double local = 12 * Math.PI * 7.5 * 7.5 * 18 + DrawingRevision.LocalHoleRemovedVolumeMm3
            + DrawingRevision.SeparateHoleVolumeMm3 + part.LeftEnd!.RemovedVolumeMm3(part);
        double reference = DrawingRevision.ExpectedDraftVolumeMm3(part);
        if (delta > .1 || Math.Abs(envelope - local - reference) > .1)
            throw new InvalidOperationException("Mass localization does not reconcile with analytical base.");
        double maxRadius = part.RevolveProfile.Max(e => e.Type == RevolveProfileElementType.Arc
            ? Math.Max(Math.Max(e.StartRadius, e.EndRadius), e.CenterRadius + e.Radius)
            : Math.Max(e.StartRadius, e.EndRadius));
        // Deliberately loose rigorous enclosing cylinder: even removing ALL of it
        // cannot account for the current mass discrepancy. Not a proposed cut.
        double upperRemoval = Math.PI * maxRadius * maxRadius * (247 - 239);
        double requiredRemoval = actualVolume - mass.VolumeAtDrawingMassMm3;
        Console.WriteLine($"[AUDIT] Mass bands reconciled: integral delta={delta:F6}, base={reference:F3} mm3.");
        return new
        {
            Bands = fine, RefinementDifferenceMm3 = delta, EnvelopeVolumeMm3 = envelope,
            BaseLocalRemovalMm3 = local, AnalyticalBaseVolumeMm3 = reference,
            FinalVerifiedVolumeMm3 = actualVolume, SubsequentNetRemovalMm3 = reference - actualVolume,
            Scope = "Numerical decomposition of construction parameters reconciled to analytical base; not an independent confirmation of drawing dimensions or actual CAD band masses.",
            TemporaryRightZone = new
            {
                StartX = 239, EndX = 247, EnclosingRadiusMm = maxRadius,
                AbsoluteRemovalUpperBoundMm3 = upperRemoval,
                RequiredRemovalToTitleMassMm3 = requiredRemoval,
                MassLowerBoundEvenIfEnclosingCylinderRemovedKg = (actualVolume - upperRemoval) * 7850e-9,
                ZoneAloneCannotResolveMass = requiredRemoval > upperRemoval,
                Scope = "Assumes fixed verified geometry outside X239..247 and reference density7850. Removing the entire enclosing cylinder is an impossible/overgenerous bound, NOT a CAD proposal."
            }
        };
    }
}

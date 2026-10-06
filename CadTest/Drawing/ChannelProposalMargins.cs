namespace CadTest.Drawing;

// Nominal geometric margins of proposal A, NOT manufacturing tolerances or flow acceptance.
public static class ChannelProposalMargins
{
    public sealed record AngularConnection(double AngleErrorDegrees, double AxisDisplacementAtDiskCentreMm,
        double MaximumDiskDistanceToAxisMm, double RadialContainmentMarginMm,
        double BlindCapMarginMm, double EntryCapMarginMm, bool FullDiskStrictlyContained);

    public static AngularConnection EvaluateAngle(double errorDegrees)
    {
        if (!double.IsFinite(errorDegrees) || Math.Abs(errorDegrees) > 1)
            throw new ArgumentOutOfRangeException(nameof(errorDegrees), "Sensitivity domain is +/-1 degree, not a manufacturing tolerance.");
        double angle = ChannelTrialStudy.Angle + errorDegrees * Math.PI / 180;
        double c = Math.Cos(angle), s = Math.Sin(angle), r = ChannelTrialStudy.RadialRadius;
        double dx = ChannelTrialStudy.RadialX - ChannelTrialStudy.AnchorX;
        double dv = ChannelTrialStudy.CentreRadius(ChannelTrialStudy.RadialX) - ChannelTrialStudy.AnchorRadius;
        double q = -dx * s + dv * c, axial = dx * c + dv * s;
        // Fixed radial terminal disk: (dx+r*cos(phi), dv, r*sin(phi)).
        // Squared distance to rotated main axis is a concave quadratic in z=cos(phi).
        double z = Math.Clamp(-q * s / (r * c * c), -1, 1);
        double farthest = Math.Sqrt(Math.Pow(q - r * s * z, 2) + r * r * (1 - z * z));
        double margin = ChannelTrialStudy.MainRadius - farthest;
        double blind = ChannelTrialStudy.Length + axial - r * Math.Abs(c);
        double entry = -axial - r * Math.Abs(c);
        return new(errorDegrees, Math.Abs(q), farthest, margin, blind, entry,
            margin > 1e-12 && blind > 1e-12 && entry > 1e-12);
    }

    public sealed record Connection(double DrillUndershootMm, double TransverseAxisErrorBoundMm,
        double EndCapMarginMm, double ContainmentMarginLowerBoundMm, bool FullDiskStrictlyContained);
    public static Connection EvaluateConnection(double undershoot, double transverseError)
    {
        if (!double.IsFinite(undershoot) || !double.IsFinite(transverseError) || undershoot < 0 || transverseError < 0)
            throw new ArgumentOutOfRangeException(nameof(undershoot));
        double c = Math.Cos(ChannelTrialStudy.Angle);
        // Radial tool terminal disk is in the radial-axis-normal plane through
        // the intersection of the two nominal axes. Its most rearward-in-tool
        // projection lies r*cos(angle) from its centre along the main axis.
        double cap = (ChannelTrialStudy.RadialX - ChannelTrialStudy.EndX) / c
            - ChannelTrialStudy.RadialRadius * c - undershoot;
        // Distance from that disk to the main axis is at most radialRadius.
        // Triangle inequality gives a conservative bound for perpendicular error.
        double radial = ChannelTrialStudy.MainRadius - ChannelTrialStudy.RadialRadius - transverseError;
        return new(undershoot, transverseError, cap, radial, cap > 1e-12 && radial > 1e-12);
    }

    public sealed record Wall(double X, double ToolRadiusMm, double BoreRadiusMm, double RadialGapLowerBoundMm);
    public static Wall MinimumWallBeforeRearCavity(double toolRadius, double startX)
    {
        if (!double.IsFinite(toolRadius) || toolRadius <= 0 || !double.IsFinite(startX)
            || startX < ChannelTrialStudy.EndX || startX >= 260)
            throw new ArgumentOutOfRangeException(nameof(toolRadius));
        var part = DrawingRevision.Create();
        var candidates = new List<Wall>();
        foreach (var segment in part.BoreRevolveProfile)
        {
            double lo = Math.Max(startX, segment.StartX), hi = Math.Min(260, segment.EndX);
            if (lo >= hi) continue;
            // Both endpoints/one-sided limits suffice: bore profile segments
            // and channel centre radius are linear over each interval.
            foreach (double x in new[] { lo, hi })
            {
                double bore = segment.StartRadius + (segment.EndRadius - segment.StartRadius)
                    * (x - segment.StartX) / (segment.EndX - segment.StartX);
                double gap = ChannelTrialStudy.CentreRadius(x) - toolRadius / Math.Cos(ChannelTrialStudy.Angle) - bore;
                candidates.Add(new(x, toolRadius, bore, gap));
            }
        }
        return candidates.MinBy(x => x.RadialGapLowerBoundMm)!;
    }

    public static object CreateReport() => new {
        NominalConnection = EvaluateConnection(0, 0),
        RadialTerminalDiskAreaMm2 = Math.PI * Math.Pow(ChannelTrialStudy.RadialRadius, 2),
        UndershootSensitivity = new[] { .1, .25, .3 }.Select(x => EvaluateConnection(x, 0)).ToArray(),
        TransverseSensitivity = new[] { .1, .15, .2 }.Select(x => EvaluateConnection(0, x)).ToArray(),
        AngularSensitivity = new[] { -.5, -.25, -.1, -.05, 0, .05, .1, .25, .5 }.Select(EvaluateAngle).ToArray(),
        AngularScope = "Main axis rotated in its nominal radial plane about fixed X265/R51; length fixed, radial terminal disk fixed. Exact ideal-cylinder full-disk containment, no diameter/position/length errors combined and no tool wander. Failure is not proof of complete disconnection.",
        MainD63ToBore = MinimumWallBeforeRearCavity(3.15, ChannelTrialStudy.EndX),
        M8MajorEnvelopeToBore = MinimumWallBeforeRearCavity(4, 254),
        Scope = "Nominal terminal-disk containment and axisymmetric bore radial-gap bounds before X260 only. Sensitivity inputs are NOT accepted tolerances. No strength, sealing, flow rate or minimum wall including all intersecting cuts established. A failed full-disk bound does not prove channels are disconnected. Rear cavity X260..265 excluded.",
        IsManufacturingReady = false
    };

    public static void RunChecks()
    {
        foreach (double error in new[] { -.5, -.25, -.1, 0, .1, .25, .5 })
        {
            var result = EvaluateAngle(error);
            double angle = ChannelTrialStudy.Angle + error * Math.PI / 180;
            double max = 0, minAxial = double.PositiveInfinity, maxAxial = double.NegativeInfinity;
            for (int i = 0; i < 7200; i++)
            {
                double phi = i * 2 * Math.PI / 7200;
                double dx = ChannelTrialStudy.RadialX + 3 * Math.Cos(phi) - 265;
                double dv = ChannelTrialStudy.CentreRadius(ChannelTrialStudy.RadialX) - 51;
                double t = 3 * Math.Sin(phi);
                double q = -dx * Math.Sin(angle) + dv * Math.Cos(angle);
                double axial = dx * Math.Cos(angle) + dv * Math.Sin(angle);
                max = Math.Max(max, Math.Sqrt(q * q + t * t));
                minAxial = Math.Min(minAxial, axial); maxAxial = Math.Max(maxAxial, axial);
            }
            if (Math.Abs(max - result.MaximumDiskDistanceToAxisMm) > 1e-5
                || Math.Abs(minAxial + ChannelTrialStudy.Length - result.BlindCapMarginMm) > 1e-9
                || Math.Abs(-maxAxial - result.EntryCapMarginMm) > 1e-9)
                throw new InvalidOperationException("Angular closed form and independent disk sampling disagree.");
        }
        if (!EvaluateAngle(0).FullDiskStrictlyContained || !EvaluateAngle(.25).FullDiskStrictlyContained
            || EvaluateAngle(.5).FullDiskStrictlyContained || EvaluateAngle(-.5).FullDiskStrictlyContained)
            throw new InvalidOperationException("Angular sensitivity regression failed.");
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1.01, 1.01 })
        {
            bool rejected = false;
            try { EvaluateAngle(bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid angle sensitivity input accepted.");
        }
        if (!EvaluateConnection(0, 0).FullDiskStrictlyContained
            || !EvaluateConnection(.25, .1).FullDiskStrictlyContained
            || EvaluateConnection(.3, 0).FullDiskStrictlyContained
            || EvaluateConnection(0, .15).FullDiskStrictlyContained
            || Math.Abs(MinimumWallBeforeRearCavity(3.15, ChannelTrialStudy.EndX).X - 254) > 1e-9
            || Math.Abs(MinimumWallBeforeRearCavity(4, 254).X - 254) > 1e-9)
            throw new InvalidOperationException("Proposal margin checks failed.");
        // Independent disk-edge projection check, same nominal geometry, 720 directions.
        double minCap = double.PositiveInfinity, maxDistance = 0;
        for (int i = 0; i < 720; i++)
        {
            double a = i * Math.PI / 360, x = ChannelTrialStudy.RadialX + 3 * Math.Cos(a), t = 3 * Math.Sin(a);
            double v = ChannelTrialStudy.CentreRadius(ChannelTrialStudy.RadialX);
            double s = (x - 265) * Math.Cos(ChannelTrialStudy.Angle) + (v - 51) * Math.Sin(ChannelTrialStudy.Angle);
            minCap = Math.Min(minCap, s + ChannelTrialStudy.Length);
            maxDistance = Math.Max(maxDistance, Math.Sqrt(Math.Pow((v - ChannelTrialStudy.CentreRadius(x)) * Math.Cos(ChannelTrialStudy.Angle), 2) + t * t));
        }
        var nominal = EvaluateConnection(0, 0);
        if (Math.Abs(minCap - nominal.EndCapMarginMm) > 1e-9
            || Math.Abs(3.15 - maxDistance - nominal.ContainmentMarginLowerBoundMm) > 1e-9)
            throw new InvalidOperationException("Independent disk sampling disagrees with analytic margins.");
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, -1.0 })
        {
            bool rejected = false;
            try { EvaluateConnection(bad, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid margin input accepted.");
        }
        Console.WriteLine("[TEST OK] Proposal margins: full-disk projection, 720 samples, sensitivity boundaries and wall stations. Not manufacturing acceptance.");
        Console.WriteLine("[TEST OK] Angular sensitivity: exact disk extrema, 50400 independent samples, 5 invalid inputs; not a tolerance assignment.");
    }
}

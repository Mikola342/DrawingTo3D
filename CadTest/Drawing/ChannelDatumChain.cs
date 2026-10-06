namespace CadTest.Drawing;

// Necessary axial-overlap condition only; never authorizes a CAD change.
public static class ChannelDatumChain
{
    public sealed record Bound(double ShoulderToRearMm, double HoleToShoulderMm,
        double MaximumEntrySetbackMm, double ChannelLengthMm, double MainRadiusMm,
        double RadialRadiusMm, double MinimumCentreSeparationMm,
        double AngleIndependentGapLowerBoundMm, bool AxialOverlapExcluded);

    public static Bound Evaluate(double shoulderToRear, double holeToShoulder,
        double maximumEntrySetback, double length, double mainRadius, double radialRadius)
    {
        double[] dimensions = [shoulderToRear, holeToShoulder, length, mainRadius, radialRadius];
        if (dimensions.Any(x => !double.IsFinite(x) || x <= 0)
            || !double.IsFinite(maximumEntrySetback) || maximumEntrySetback < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Finite positive dimensions and nonnegative setback required.");
        // The common rear datum (including its absolute X and tolerance) cancels:
        // Xentry >= Xrear - setback; Xradial = Xrear - shoulder - offset.
        // A finite main cylinder projects at most length + radius onto X,
        // for ANY axis angle. The radial cylinder extends radialRadius in X.
        // Deliberately loose bound: positive gap proves separation; nonpositive
        // gap does NOT prove 3D connection or adequate wall thickness.
        double separation = shoulderToRear + holeToShoulder - maximumEntrySetback;
        double gap = separation - length - mainRadius - radialRadius;
        return new(shoulderToRear, holeToShoulder, maximumEntrySetback, length,
            mainRadius, radialRadius, separation, gap, gap > 0);
    }

    public static object CreateReport() => new
    {
        Source = "chertezh_corrected_final.pdf, A-A: visible 50, corrected 107, right zone 26, channel 110, D6.3 and D6.",
        Nominal = Evaluate(50, 107, 26, 110, 3.15, 3),
        // Sensitivity envelope, NOT ISO/GOST tolerance interpretation.
        DeliberatelyRelaxedSensitivity = Evaluate(49, 106, 27, 111, 4, 3.5),
        CommonRearDatumCancels = true,
        Assumptions = new[] {
            "50 and 107 share the shoulder datum shown in A-A.",
            "Channel entry lies somewhere in the rightmost 26 mm zone (a deliberately broad hypothesis).",
            "110 is the full finite D6.3 channel length, not a partial segment or separate operation.",
            "The D6.3 channel must directly intersect radial D6 as drawn; no unmodelled connecting cut."
        },
        Scope = "Positive gap excludes connection for these assumptions at any channel angle. Does not prove the source drawing is erroneous; at least one datum/length/topology interpretation must change. Relaxed numbers are sensitivity inputs, not accepted tolerances.",
        CadChangeAuthorized = false
    };

    public static void RunChecks()
    {
        var nominal = Evaluate(50, 107, 26, 110, 3.15, 3);
        var relaxed = Evaluate(49, 106, 27, 111, 4, 3.5);
        if (Math.Abs(nominal.AngleIndependentGapLowerBoundMm - 14.85) > 1e-10
            || Math.Abs(relaxed.AngleIndependentGapLowerBoundMm - 9.5) > 1e-10
            || !nominal.AxialOverlapExcluded || !relaxed.AxialOverlapExcluded
            || Evaluate(50, 107, 26, 124.85, 3.15, 3).AngleIndependentGapLowerBoundMm > 1e-10
            || Evaluate(50, 107, 26, 160, 3.15, 3).AxialOverlapExcluded)
            throw new InvalidOperationException("Channel datum-chain bounds failed.");
        int rejected = 0;
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1 })
            for (int index = 0; index < 6; index++)
            {
                if (index == 2 && bad == 0) continue; // zero setback is valid
                double[] args = [50, 107, 26, 110, 3.15, 3]; args[index] = bad;
                try { Evaluate(args[0], args[1], args[2], args[3], args[4], args[5]); }
                catch (ArgumentOutOfRangeException) { rejected++; }
            }
        if (rejected != 29 || !Evaluate(50, 107, 0, 110, 3.15, 3).AxialOverlapExcluded)
            throw new InvalidOperationException("Channel datum-chain input checks failed.");
        Console.WriteLine("[TEST OK] Channel datum chain: any-angle exclusion, relaxed sensitivity, boundary, 29 invalid inputs. No CAD change.");
    }
}

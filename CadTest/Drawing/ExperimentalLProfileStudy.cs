namespace CadTest.Drawing;

// Selected experimental interpretation, not an authoritative transcription of TIFF.
public static class ExperimentalLProfileStudy
{
    public const double BoreStart = 265 - 84;
    public const double Shoulder = BoreStart + 56;
    public const double GrooveRight = Shoulder - (56 - 51);
    public const double GrooveWidth = 1.9;
    public const double GrooveLeft = GrooveRight - GrooveWidth;
    public const double ExitStart = Shoulder - 1.2;
    public static double GrooveVolume => Math.PI * (35.5 * 35.5 - 34 * 34) * GrooveWidth;

    public static object RunChecks()
    {
        // Forward stationing and independent backward chain must agree.
        if (Math.Abs(GrooveRight - (BoreStart + 51)) > 1e-10 ||
            Math.Abs(Shoulder - 237) > 1e-10 || Math.Abs(GrooveLeft - 230.1) > 1e-10 ||
            !(BoreStart < GrooveLeft && GrooveLeft < GrooveRight && GrooveRight < ExitStart && ExitStart < Shoulder))
            throw new InvalidOperationException("Experimental L dimension-chain mismatch.");
        double coneVolume = Math.PI * 1.2 * (34 * 1.2 * Math.Tan(Math.PI/6) + Math.Pow(1.2*Math.Tan(Math.PI/6),2)/3);
        double integrated = 0;
        for (int i = 0; i < 10000; i++)
        {
            double x = (i+.5)*1.2/10000, r = 34+x*Math.Tan(Math.PI/6);
            integrated += Math.PI*(r*r-34*34)*1.2/10000;
        }
        if (Math.Abs(coneVolume-integrated) > 1e-6 || 34+1.2*Math.Tan(Math.PI/6) >= 35.5)
            throw new InvalidOperationException("Experimental L exit independent integration mismatch.");
        Console.WriteLine($"[TEST OK] Experimental L selected chain:181+51=232=237-(56-51); groove230.1..232; land={ExitStart-GrooveRight:F1}; exit235.8..237. NOT source acceptance.");
        return new { BoreStart, Shoulder, GrooveLeft, GrooveRight, GrooveWidth, ExitStart,
            RetainedLandMm=ExitStart-GrooveRight, GrooveDepthMm=1.5, GrooveVolumeMm3=GrooveVolume,
            ExitVolumeMm3=coneVolume, DrawingInterpretationAccepted=false,
            SelectedInterpretation="51 to RIGHT groove wall;1.2 axial before shoulder;30deg to axis" };
    }
}

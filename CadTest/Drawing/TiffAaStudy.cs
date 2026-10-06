namespace CadTest.Drawing;

public static class TiffAaStudy
{
    public static void RunChecks()
    {
        const double x = 265 - 26 - 107, end = x - 3.25;
        double a = 4 * Math.PI / 180, tf = Math.Tan(7 * Math.PI / 180), tm = Math.Tan(a);
        double radialR = 51 + (x - 260) * tm;
        double crossingR = (51 + (257.5 - 127 * tf - 260) * tm) / (1 - tf * tm);
        double crossingX = 257.5 + (crossingR - 127) * tf;
        double channelR = 51 + (crossingX - 260) * tm;
        double minBoreWall = 51 + (end - 260) * tm - 3.15 / Math.Cos(a) - 31;
        double beforeBoreStep = 51 + (181 - 260) * tm - 3.15 / Math.Cos(a) - 34;
        if (x != 132 || Math.Abs(crossingR - channelR) > 1e-10 || minBoreWall <= 0 || beforeBoreStep <= 0 ||
            end >= x - 3 || crossingX <= 132 || crossingX >= 254 || radialR >= 52.5)
            throw new InvalidOperationException("AA axis/crossing calculation failed.");
        Console.WriteLine($"[TEST OK] TIFF AA: radialX={x}; crossing={crossingX:F9}/{crossingR:F9}; nominal bore walls={minBoreWall:F6}/{beforeBoreStep:F6}. Flat end is provisional.");
    }
}

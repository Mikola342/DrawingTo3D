namespace CadTest.Drawing;

public static class TiffM6LibraryStudy
{
    public const string Size = "M6x1.0";

    public static string SelectConfiguration(string[] configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        if (configurations.Count(n => string.Equals(n, Size, StringComparison.Ordinal)) != 1)
            throw new InvalidOperationException("Exactly one installed Metric Tap configuration required: " + Size);
        return Size;
    }

    public static void RunChecks()
    {
        if (SelectConfiguration(["M1.2x0.25", Size, "M6x0.75"]) != Size)
            throw new InvalidOperationException("M6 library selection failed.");
        string[][] rejected = [[], ["M6x1"], ["M1.2x0.25"], ["M6x0.75"], [Size, Size]];
        foreach (var names in rejected)
        {
            bool rejectedInput = false;
            try { _ = SelectConfiguration(names); }
            catch (InvalidOperationException) { rejectedInput = true; }
            if (!rejectedInput) throw new InvalidOperationException("Unsafe M6 library fallback accepted.");
        }
        Console.WriteLine("[TEST OK] M6 exact installed profile selection; five absent/ambiguous/fallback cases rejected.");
    }
}

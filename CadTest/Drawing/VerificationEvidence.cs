using System.Text.Json;

namespace CadTest.Drawing;

public static class VerificationEvidence
{
    public sealed record Result(double VolumeMm3, bool Focused, bool CorrectedFlange19);
    public static Result Read(JsonElement root)
    {
        if (!root.GetProperty("FreshDiskCopyLoaded").GetBoolean()
            || !root.GetProperty("SavedBytesUnchanged").GetBoolean()
            || root.GetProperty("OpenWarnings").GetInt32() != 0)
            throw new InvalidOperationException("Successful fresh-disk evidence required.");
        bool focused = root.TryGetProperty("FullInheritedGeometryRecheckedAfterReload", out var full) && !full.GetBoolean();
        bool flange19 = root.TryGetProperty("Flange19", out var flange) && flange.ValueKind == JsonValueKind.Object
            && flange.GetProperty("Count").GetInt32() == 12 && flange.GetProperty("DiameterMm").GetDouble() == 19;
        double volume;
        if (focused)
        {
            if (!flange19 || root.TryGetProperty("Inherited", out _))
                throw new InvalidOperationException("Ambiguous focused verification scope.");
            volume = root.GetProperty("VolumeMm3").GetDouble();
            double expected = root.GetProperty("ExpectedVolumeMm3").GetDouble();
            if (!double.IsFinite(expected) || expected <= 0 || Math.Abs(volume - expected) > .1)
                throw new InvalidOperationException("Focused volume mismatch.");
        }
        else
        {
            var inherited = root.GetProperty("Inherited");
            if (!inherited.GetProperty("BaseGeometryChecked").GetBoolean()
                || !inherited.GetProperty("SplineSurfacesChecked").GetBoolean())
                throw new InvalidOperationException("Incomplete inherited verification.");
            volume = inherited.GetProperty("VolumeMm3").GetDouble();
        }
        if (!double.IsFinite(volume) || volume <= 0) throw new InvalidOperationException("Invalid verified volume.");
        return new(volume, focused, flange19);
    }

    public static void RunChecks()
    {
        string Fixture(bool fresh = true, int warnings = 0, bool unchanged = true, int count = 12,
            double volume = 1000, double expected = 1000, bool full = false) => JsonSerializer.Serialize(new {
                FreshDiskCopyLoaded = fresh, SavedBytesUnchanged = unchanged, OpenWarnings = warnings,
                FullInheritedGeometryRecheckedAfterReload = full,
                Flange19 = new { Count = count, DiameterMm = 19 }, VolumeMm3 = volume, ExpectedVolumeMm3 = expected });
        Result Parse(string json) { using var doc = JsonDocument.Parse(json); return Read(doc.RootElement); }
        if (!Parse(Fixture()).Focused) throw new InvalidOperationException("Focused evidence promoted to full.");
        int rejected = 0;
        foreach (var invalid in new[] { Fixture(fresh: false), Fixture(warnings: 128), Fixture(unchanged: false),
            Fixture(count: 11), Fixture(volume: -1), Fixture(expected: 999), Fixture(full: true) })
        {
            try { Parse(invalid); }
            catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { rejected++; }
        }
        if (rejected != 7) throw new InvalidOperationException("Verification evidence rejection tests failed.");
        Console.WriteLine("[TEST OK] Verification evidence: focused scope retained; 7 malformed/incomplete reports rejected.");
    }
}

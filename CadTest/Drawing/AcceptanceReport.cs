using System.Security.Cryptography;
using System.Text.Json;

namespace CadTest.Drawing;

public static class AcceptanceReport
{
    public static string Write(string modelPath, string outputDirectory, double actualVolume, double expectedVolume, string mode)
    {
        modelPath = Path.GetFullPath(modelPath);
        if (!File.Exists(modelPath)) throw new FileNotFoundException("Verified model file is missing.", modelPath);
        double tolerance = Math.Max(1, expectedVolume * 1e-6);
        if (!double.IsFinite(actualVolume) || !double.IsFinite(expectedVolume) || expectedVolume <= 0 ||
            actualVolume <= 0 || Math.Abs(actualVolume - expectedVolume) > tolerance)
            throw new InvalidOperationException("Cannot issue acceptance report for an invalid volume.");
        var before = new FileInfo(modelPath);
        long size = before.Length;
        DateTime modified = before.LastWriteTimeUtc;
        // SolidWorks holds the document open for writing; this stream only reads it.
        using var file = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        string hash = Convert.ToHexString(SHA256.HashData(file));
        before.Refresh();
        if (before.Length != size || before.LastWriteTimeUtc != modified)
            throw new IOException("Model file changed while computing its hash; retry verification.");
        var path = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(modelPath)}_acceptance_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json");
        var report = new
        {
            SchemaVersion = 1,
            VerifiedAtUtc = DateTime.UtcNow,
            Mode = mode,
            Revision = DrawingRevision.Revision,
            ModelFile = modelPath,
            FileSha256 = hash,
            FileSizeBytes = size,
            FileLastWriteTimeUtc = modified,
            VerificationScope = "Current revision parameters and in-memory CAD body; not full drawing conformity. File hash identifies saved bytes, not a separate disk reload check.",
            IsManufacturingReady = false,
            ParameterRegressionChecksPassed = true,
            CadChecksPassed = true,
            ActualVolumeMm3 = actualVolume,
            ExpectedVolumeMm3 = expectedVolume,
            AbsoluteVolumeDifferenceMm3 = Math.Abs(actualVolume - expectedVolume),
            VolumeToleranceMm3 = tolerance,
            TemporaryAssumption = new { RightStationMm = DrawingRevision.AssumedRightStation,
                UserApproved = true, ConfirmedDrawingDimension = false },
            Limitations = DrawingRevision.Limitations
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }
}

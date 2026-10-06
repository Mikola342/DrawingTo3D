namespace CadTest.Drawing;

// Technological reference only, NOT the full OST thread profile or a drawing datum.
public static class KgPreparationReference
{
    public static object CreateReport() => new
    {
        Source = "https://www.mashin.ru/files/gz307_s_pril.pdf",
        Pages = "30-31", Table = 6, Figure = "1b", ThreadSizeInch = "1/8",
        TaperDiameterRatio = "1:16",
        ConeEntryDiameterMm = new { BrittleMaterial = 8.707, DuctileMaterial = 8.825, UpperDeviation = .09 },
        ExitPilotDiameterMm = new { Basic = 8.36, UpperDeviation = .20 },
        ChamferMaximumDiameterMm = 10.4, MinimumPreparationLengthMm = 10,
        CurrentTrial = new { EntryDiameterAtT0Mm = 2 * KgPrepStudy.EntryRadius,
            ConeEndT = KgPrepStudy.End, TransitionEndT = KgPrepStudy.TransitionEnd, TransitionDiameterMm = 8 },
        ComparisonAccepted = false, FullOstProfileAvailable = false, CadChangeAuthorizedByThisReference = false,
        Scope = "Preparation guidance, not thread geometry. Trial t0 on curved flange is NOT established as reference figure entry plane; material category/process and drawing depth unresolved. Table7 describes rods for rolling and must not be used for internal thread holes."
    };
}

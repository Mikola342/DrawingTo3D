using System.Text.Json;

namespace CadTest.Drawing;

// Proposed design departure, deliberately separate from drawing acceptance and CAD builders.
public static class ChannelCorrectionProposal
{
    public static object CreateReport()
    {
        const double rear = 265, shoulderOffset = 50, holeOffset = 107;
        const double drawingLength = 110, radialRadius = 3, axialAllowance = .25;
        double angle = 4 * Math.PI / 180;
        double radialX = rear - shoulderOffset - holeOffset;
        double endX = radialX - radialRadius - axialAllowance;
        double proposedLength = (rear - endX) / Math.Cos(angle);
        double movedHoleX = rear - drawingLength * Math.Cos(angle) + radialRadius + axialAllowance;
        return new
        {
            Status = "ON_HOLD_NEW_TIFF_SEPARATES_CHANNEL_SECTIONS",
            LengthCorrectionRecommendationOnHold = true,
            SourceReconciliation = ChannelSourceReconciliation.CreateReport(),
            ProposalPreparationAuthorized = true, CadImplementationAuthorized = false,
            CountsAsOriginalDrawingConformance = false, IsManufacturingReady = false,
            GeometricMargins = ChannelProposalMargins.CreateReport(),
            Recommended = new {
                Id = "A_KEEP_RADIAL_DATUM", RearConstructionPlaneX = rear,
                ShoulderX = rear - shoulderOffset, RadialHoleX = radialX,
                BlindEndCentreX = endX, AngleDegrees = 4,
                OriginalInterpretedLengthMm = drawingLength,
                ProposedConstructionCutLengthMm = proposedLength,
                LengthIncreaseMm = proposedLength - drawingLength,
                AxialAllowanceBeyondRadialCylinderMm = axialAllowance,
                NominalEndCapClearanceMm = axialAllowance - 3.15 * Math.Sin(angle),
                OtherConstructionDatums = new[] { 254.0, 260.0, 265.0 }.Select(x => new {
                    ConstructionPlaneX = x, LengthToSameEndCentreMm = (x - endX) / Math.Cos(angle)
                }).ToArray(),
                MatchesExistingTrialParameters = Math.Abs(proposedLength - ChannelTrialStudy.Length) < 1e-9,
                Scope = "Dimension the existing experimental construction explicitly. X265 is a construction datum, not an established physical drill-entry surface; these lengths are not shop drilling depths. No new CAD geometry needed for this nominal proposal."
            },
            AlternativeKeep110 = new {
                Id = "B_MOVE_RADIAL_HOLE", RadialHoleX = movedHoleX,
                ProposedHoleToShoulderMm = rear - shoulderOffset - movedHoleX,
                HoleDisplacementMm = movedHoleX - radialX,
                Scope = "Algebraic alternative only, not CAD-tested or recommended. Changes 107 and the radial port position; connection to other features and function unverified."
            },
            AlternativeMoveEntry = new {
                Id = "C_MOVE_ENTRY", EntryConstructionX = endX + drawingLength * Math.Cos(angle),
                Scope = "Algebraic alternative only. Abandons rear entry and needs a new access/connection layout; not a drop-in correction."
            },
            OpenDecisions = new[] {
                "Confirm physical entry datum and whether 110 denotes full drill depth, working length or another segment.",
                "Select blind-end tool geometry, depth tolerance and required intersection allowance; 0.25 is a nominal modelling assumption only.",
                "Check walls, thread seats, sealing, manufacturing access and flow; CAD connectivity alone is insufficient.",
                "Retain original PDF unmodified; release any revised drawing only as a separately approved design revision."
            }
        };
    }

    public static void RunChecks()
    {
        ChannelProposalMargins.RunChecks();
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(CreateReport()));
        var root = doc.RootElement;
        var a = root.GetProperty("Recommended");
        double length = a.GetProperty("ProposedConstructionCutLengthMm").GetDouble();
        if (Math.Abs(length - 160.64131418072033) > .000001
            || !root.GetProperty("LengthCorrectionRecommendationOnHold").GetBoolean()
            || !a.GetProperty("MatchesExistingTrialParameters").GetBoolean()
            || a.GetProperty("NominalEndCapClearanceMm").GetDouble() <= 0
            || root.GetProperty("CadImplementationAuthorized").GetBoolean()
            || root.GetProperty("CountsAsOriginalDrawingConformance").GetBoolean())
            throw new InvalidOperationException("Channel proposal regression or scope failure.");
        double movedX = root.GetProperty("AlternativeKeep110").GetProperty("RadialHoleX").GetDouble();
        if (Math.Abs((265 - (movedX - 3 - .25)) / Math.Cos(4 * Math.PI / 180) - 110) > 1e-9)
            throw new InvalidOperationException("Alternative channel chain does not close.");
        Console.WriteLine("[TEST OK] Channel proposal: chain closure, existing trial match, positive nominal cap clearance; acceptance flags remain false.");
    }

    public static void WriteReport(string destination)
    {
        RunChecks();
        File.WriteAllText(destination, JsonSerializer.Serialize(CreateReport(), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[PROPOSAL ONLY] {Path.GetFullPath(destination)}");
    }
}

using CadTest.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CadTest.Drawing;

// Stage 16: separate P-P flange hole; temporary right station retained.
public static class DrawingRevision
{
    public const string Revision = "stage16";
    public const double SeparateHoleDiameter = 22;
    public const double SeparateHoleOffset = 111.5;
    public static double SeparateHoleVolumeMm3 => Math.PI * 121 * FlangeThickness;
    public const double SplineLeadStart = 42;
    // Drawing angle is measured from the radial line, not the shaft axis.
    public static readonly double SplineLeadEnd = SplineLeadStart + (50 - 45) * Math.Tan(Math.PI / 12);
    public const double BoreEntryLength = 3;
    public static readonly double BoreEntryRadius = 31 + BoreEntryLength * Math.Tan(Math.PI / 6);
    public const double AssumedRightStation = 245; // approved by user, NOT a drawing dimension
    public static double LocalHoleRemovedVolumeMm3 => ApprovedRightTransition.LocalHoleIntersectionVolumeMm3
        + Math.PI * 36 * (RearRecessStart - FlangeFront);
    // Historical stage16 baseline only. Source recheck 2026-09-27 confirms D19;
    // corrected saved branch uses CorrectFlangeDiameter19 and explicit D19 inspection.
    public const double FlangeHoleDiameter = 15;
    public const double FlangeHoleCircle = 226;
    public const double FlangeHoleStartAngle = 15;
    public const double LocalHoleDiameter = 12;
    public const double LocalHoleOffset = 84;
    // Axial span only, NOT full circular area times length: X=241..245 is partial intersection.
    public const double LocalHoleDraftLength = RearRecessStart - RightStepPlane;
    public const double FlangeRear = 265;
    public const double FlangeThickness = 18;
    public const double FlangeFront = FlangeRear - FlangeThickness;
    // TIFF A-A: the 8 mm projection starts at the 265 mm flange rear datum.
    // The separate 26 mm dimension runs LEFT from that datum to ShoulderRight;
    // FlangeFront + 26 happened to give 273 but encoded the wrong dimension chain.
    public const double BossProjection = 8;
    public const double BossEnd = FlangeRear + BossProjection;
    public const double BossOuterRadius = 100;
    public const double BossInnerRadius = 95;
    public const double BossChamfer = 1.6;
    public const double BossChamferStart = BossEnd - BossChamfer;
    public const double RearRecessStart = BossEnd - 13; // X=260
    public const double CollarEnd = FlangeRear; // chosen nominal zero inset; drawing allows 1.6 max
    public const double CollarGrooveStart = CollarEnd - 3;
    public const double CollarGrooveEnd = CollarEnd - 2;
    public const double CollarRadius = 48.5; // Ø97 outside the collar, NOT a bore
    public const double CollarGrooveRadius = 47.5; // Ø95 external groove root

    private static List<AnnularCutDescription> NominalAnnularCuts() =>
    [
        new() { Name = "Rear_recess_left", StartX = RearRecessStart, EndX = CollarGrooveStart,
            InnerRadius = CollarRadius, OuterRadius = BossInnerRadius },
        new() { Name = "Collar_groove_nominal", StartX = CollarGrooveStart, EndX = CollarGrooveEnd,
            InnerRadius = CollarGrooveRadius, OuterRadius = BossInnerRadius, RootFilletRadius = 0.5 },
        new() { Name = "Rear_recess_lip", StartX = CollarGrooveEnd, EndX = CollarEnd,
            InnerRadius = CollarRadius, OuterRadius = BossInnerRadius }
    ];
    public const double Bore68Start = FlangeRear - 61.5;
    public const double Bore90Start = BossEnd - 19;
    public const double Bore78Start = Bore90Start - 14;
    public const double Shoulder125 = FlangeRear - 50; // datum Б: X=215
    public const double ShoulderFillet = 2;
    public const double ShoulderChamferLength = 3;
    public static readonly double ShoulderChamferRadius = 62.5 - ShoulderChamferLength * Math.Tan(Math.PI / 6);
    public const double ShoulderRight = FlangeRear - 26; // X=239
    public const double RightStepPlane = ShoulderRight + 2; // drawing 2±0.2
    public const double RightStepStartRadius = 131.0 / 2;
    // 45-degree segment followed by tangent R2 at the next radial face.
    public static readonly double RightStepArcCentreRadius = RightStepStartRadius + 2 + 2 * Math.Tan(Math.PI / 8);
    public static readonly double RightStepArcStartX = ShoulderRight + Math.Sqrt(2);
    public static readonly double RightStepArcStartRadius = RightStepArcCentreRadius - Math.Sqrt(2);
    public const double Outer105Start = ShoulderRight - 144; // X=95
    public const double SplineShoulderFillet = 3;
    public static readonly double SplineSharpCorner = Outer105Start - 2.5 / Math.Tan(Math.PI / 6);
    public static readonly double SplineTangentStart = SplineSharpCorner - SplineShoulderFillet * Math.Tan(Math.PI / 12);
    public static readonly double SplineTangentEnd = SplineTangentStart + SplineShoulderFillet / 2;
    public static readonly double SplineTangentRadius = 50 + SplineShoulderFillet * (1 - Math.Cos(Math.PI / 6));
    public const double Outer110Start = ShoulderRight - 56; // X=183
    public static readonly double Outer105End = Outer110Start - (55 - 52.5) / Math.Tan(Math.PI / 6);
    public const double ConeRootRadius = 2; // chosen minimum permitted by R2 min
    public static readonly double ConeTangentStart = Outer105End - ConeRootRadius * Math.Tan(Math.PI / 12);
    public static readonly double ConeTangentEnd = ConeTangentStart + ConeRootRadius * 0.5;
    public static readonly double ConeTangentRadius = 52.5 + ConeRootRadius * (1 - Math.Cos(Math.PI / 6));
    private static readonly RightTransitionStudy.Candidate ApprovedRightTransition = RightTransitionStudy.Evaluate(AssumedRightStation);

    // Unfinished regions are deliberately exposed alongside the CAD parameters.
    public static readonly string[] Limitations =
    [
        "Left end Ø82 x 3, external 2x45 chamfer, flat 84.5 over 25 mm with R35 runout, bottom end cut 20 from Ø90 generatrix built. Bore entry 3 mm / 30 degrees retained. M90 thread itself is still absent.",
        "Spline blank lead-in: 15 degrees from radial direction, X=42 to 42+5*tan(15 degrees). Teeth and R50 tool runout pending. Outer Ø100/105 transition has tangent R3 and 30-degree cone; R35 left flat is built.",
        "Ø105/110 transition: tangent R2 selected at the allowed R2 min; nominal cone slope remains 30 degrees.",
        "X=215 and X=239 roots: nominal R2 modelled; 3 mm axial / 30-degree transition to Ø125 retained. R2 at other unresolved steps is pending.",
        "X=239..241: Ø131 / 2 mm / R2 built. Ø164/188 transition uses user-approved TEMPORARY intermediate station X=245, NOT a confirmed drawing dimension; 45-degree transitions and R2 selected as studied.",
        "Ø12 removal uses partial circle overlap on X=241..247 plus full intersection to X=260; axial span 19 mm is NOT a full cylinder length.",
        "Ø12 hole at end-view offset 84 is present; Д/К shaped recess R16/Ø33 is pending. Through-all length depends on provisional exterior.",
        "Ц: external groove has R0.5 roots, meeting tangentially at nominal width 1 mm. Collar end X=265 selects zero inset within 1.6 max, not an exact drawing dimension.",
        "Bore steps remain nominal/sharp; detail Л, grooves, splines and side channels pending."
    ];

    private static List<RevolveProfileElement> NominalOuterProfile() =>
    [
        Outer(0, 3, 41, 41),
        Outer(3, 3, 41, 43),
        Outer(3, 5, 43, 45),
        Outer(5, SplineLeadStart, 45, 45),
        Outer(SplineLeadStart, SplineLeadEnd, 45, 50),
        Outer(SplineLeadEnd, SplineTangentStart, 50, 50),
        new() { Type = RevolveProfileElementType.Arc, StartX = SplineTangentStart, EndX = SplineTangentEnd,
            StartRadius = 50, EndRadius = SplineTangentRadius, CenterX = SplineTangentStart, CenterRadius = 53,
            Radius = SplineShoulderFillet, Clockwise = false },
        Outer(SplineTangentEnd, Outer105Start, SplineTangentRadius, 52.5),
        Outer(Outer105Start, ConeTangentStart, 52.5, 52.5),
        new() { Type = RevolveProfileElementType.Arc, StartX = ConeTangentStart, EndX = ConeTangentEnd,
            StartRadius = 52.5, EndRadius = ConeTangentRadius, CenterX = ConeTangentStart, CenterRadius = 54.5,
            Radius = ConeRootRadius, Clockwise = false },
        Outer(ConeTangentEnd, Outer110Start, ConeTangentRadius, 55),
        Outer(Outer110Start, Shoulder125 - ShoulderFillet, 55, 55),
        new() { Type = RevolveProfileElementType.Arc, StartX = Shoulder125 - ShoulderFillet, EndX = Shoulder125,
            StartRadius = 55, EndRadius = 57, CenterX = Shoulder125 - ShoulderFillet, CenterRadius = 57,
            Radius = ShoulderFillet, Clockwise = false },
        Outer(Shoulder125, Shoulder125, 57, ShoulderChamferRadius),
        Outer(Shoulder125, Shoulder125 + ShoulderChamferLength, ShoulderChamferRadius, 62.5),
        Outer(Shoulder125 + ShoulderChamferLength, ShoulderRight - ShoulderFillet, 62.5, 62.5),
        new() { Type = RevolveProfileElementType.Arc, StartX = ShoulderRight - ShoulderFillet, EndX = ShoulderRight,
            StartRadius = 62.5, EndRadius = 64.5, CenterX = ShoulderRight - ShoulderFillet, CenterRadius = 64.5,
            Radius = ShoulderFillet, Clockwise = false },
        Outer(ShoulderRight, ShoulderRight, 64.5, RightStepStartRadius),
        Outer(ShoulderRight, RightStepArcStartX, RightStepStartRadius, RightStepArcStartRadius),
        new() { Type = RevolveProfileElementType.Arc, StartX = RightStepArcStartX, EndX = RightStepPlane,
            StartRadius = RightStepArcStartRadius, EndRadius = RightStepArcCentreRadius,
            CenterX = ShoulderRight, CenterRadius = RightStepArcCentreRadius, Radius = 2, Clockwise = false },
        .. RightTransitionStudy.Profile(AssumedRightStation).Select(e => new RevolveProfileElement
        {
            Type = e.Kind == "arc" ? RevolveProfileElementType.Arc : RevolveProfileElementType.Line,
            StartX = e.X0, StartRadius = e.R0, EndX = e.X1, EndRadius = e.R1,
            CenterX = e.CentreX ?? 0, CenterRadius = e.CentreR ?? 0, Radius = e.FilletRadius ?? 0,
            Clockwise = false
        }),
        Outer(FlangeFront, FlangeRear, 127.5, 127.5),
        Outer(FlangeRear, FlangeRear, 127.5, BossOuterRadius),
        Outer(FlangeRear, BossChamferStart, BossOuterRadius, BossOuterRadius),
        Outer(BossChamferStart, BossEnd, BossOuterRadius, BossOuterRadius - BossChamfer)
    ];

    public static PartDescription Create()
    {
        var part = new PartDescription
        {
            TotalLength = BossEnd,
            BoreEndWallThickness = 0,
            RevolveProfile = NominalOuterProfile(),
            AnnularCuts = NominalAnnularCuts(),
            LeftEnd = new LeftEndDescription(),
            HolePatterns =
            [
                new HolePatternDescription
                {
                    Count = 12, BoltCircleDiameter = FlangeHoleCircle, StartAngle = FlangeHoleStartAngle, EqualSpacing = true,
                    Hole = new HoleDescription { Type = HoleType.Simple, Diameter = FlangeHoleDiameter, ThroughAll = true, Depth = FlangeThickness }
                }
            ]
        };
        part.BoreRevolveProfile =
        [
            Bore(0, BoreEntryLength, BoreEntryRadius, 31, "Entry: 3 mm axial, 30 degrees to axis"),
            Bore(BoreEntryLength, Bore68Start, 31, 31, "Ø62 nominal"),
            Bore(Bore68Start, Bore68Start, 31, 34, "Shoulder; detail Л pending"),
            Bore(Bore68Start, Bore78Start, 34, 34, "Ø68 nominal"),
            Bore(Bore78Start, Bore78Start, 34, 39, "Shoulder; local fillet pending"),
            Bore(Bore78Start, Bore90Start, 39, 39, "Ø78; end chamfer pending"),
            Bore(Bore90Start, Bore90Start, 39, 45, "Shoulder; detail Ц pending"),
            Bore(Bore90Start, FlangeRear, 45, 45, "Ø90; groove/exit detail Ц pending"),
            Bore(FlangeRear, FlangeRear, 45, BossInnerRadius, "Rear flange face; local cuts Д/К pending"),
            Bore(FlangeRear, BossEnd, BossInnerRadius, BossInnerRadius, "Ø190 inside right annular boss")
        ];

        part.ExpandHolePatterns();
        part.Holes.Add(new HoleDescription { Type = HoleType.Simple, Diameter = SeparateHoleDiameter,
            X = 0, Y = SeparateHoleOffset, ThroughAll = true, Depth = FlangeThickness });
        part.Holes.Add(new HoleDescription { Type = HoleType.Simple, Diameter = LocalHoleDiameter,
            X = 0, Y = -LocalHoleOffset, ThroughAll = true, Depth = LocalHoleDraftLength });
        return part;
    }

    private static BoreProfileElement Bore(double x0, double x1, double r0, double r1, string note) => new()
    {
        Type = BoreProfileElementType.Line, StartX = x0, EndX = x1,
        StartRadius = r0, EndRadius = r1, Description = note
    };

    private static RevolveProfileElement Outer(double x0, double x1, double r0, double r1) => new()
    {
        Type = RevolveProfileElementType.Line, StartX = x0, EndX = x1,
        StartRadius = r0, EndRadius = r1
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Validate(PartDescription part)
    {
        Require(part.LeftEnd != null, "Missing local left-end cuts.");
        part.LeftEnd!.Validate();
        part.Validate();
        part.ValidateRevolveProfiles();
        var expectedCuts = NominalAnnularCuts();
        Require(part.AnnularCuts.Count == expectedCuts.Count, "Missing rear recess/collar cuts.");
        for (int i = 0; i < expectedCuts.Count; i++)
        {
            var cut = part.AnnularCuts[i];
            var expected = expectedCuts[i];
            cut.Validate();
            Require(cut.StartX == expected.StartX && cut.EndX == expected.EndX &&
                cut.InnerRadius == expected.InnerRadius && cut.OuterRadius == expected.OuterRadius && cut.RootFilletRadius == expected.RootFilletRadius,
                $"Incorrect rear annular cut #{i + 1}.");
        }
        Require(part.TotalLength == BossEnd, "Current draft must reach the end of the boss, X=273.");
        Require(BossEnd - FlangeRear == 8 && FlangeRear - FlangeFront == 18 &&
                FlangeRear - ShoulderRight == 26 && ShoulderRight == 239,
            "TIFF rear datum chains must stay distinct: projection 8, flange 18, left shoulder offset 26.");
        Require(part.Threads.Count == 0, "Unverified thread length must not enter this draft.");
        Require(part.BoreEndWallThickness == 0, "Drawing shows an open bore, not an 8 mm end wall.");
        var entry = part.BoreRevolveProfile[0];
        Require(entry.Type == BoreProfileElementType.Line && entry.StartX == 0 && entry.EndX == BoreEntryLength &&
            entry.StartRadius == BoreEntryRadius && entry.EndRadius == 31, "Incorrect 3 mm / 30-degree bore entry.");
        var bore = part.BoreRevolveProfile.Where(e => e.EndX > e.StartX && e.StartRadius == e.EndRadius).ToArray();
        Require(part.BoreRevolveProfile.Count == 10, "Unexpected bore segment count.");
        double[] x = [BoreEntryLength, Bore68Start, Bore78Start, Bore90Start, FlangeRear, BossEnd];
        double[] r = [31, 34, 39, 45, BossInnerRadius];
        Require(bore.Length == 5, "Expected four bore cylinders and the Ø190 boss interior.");
        for (var i = 0; i < bore.Length; i++)
            Require(bore[i].Type == BoreProfileElementType.Line &&
                bore[i].StartX == x[i] && bore[i].EndX == x[i + 1] &&
                bore[i].StartRadius == r[i] && bore[i].EndRadius == r[i],
                $"Incorrect bore cylinder #{i + 1}.");

        var flange = part.RevolveProfile.Single(e => e.EndX > e.StartX && e.StartRadius == 127.5);
        Require(flange.StartX == FlangeFront && flange.EndX == FlangeRear && flange.EndRadius == 127.5,
            "Flange Ø255 must have nominal thickness 18 mm (X=247..265).");
        ValidateChain(part.RevolveProfile.Select(e => (e.StartX, e.EndX, e.StartRadius, e.EndRadius)));
        ValidateExteriorTangencies(part.RevolveProfile);
        ValidateChain(part.BoreRevolveProfile.Select(e => (e.StartX, e.EndX, e.StartRadius, e.EndRadius)));
        var expectedOuter = NominalOuterProfile();
        Require(part.RevolveProfile.Count == expectedOuter.Count, "Unexpected exterior segment count.");
        for (int i = 0; i < expectedOuter.Count; i++)
        {
            var actual = part.RevolveProfile[i];
            var expected = expectedOuter[i];
            Require(actual.Type == expected.Type && Math.Abs(actual.StartX - expected.StartX) < 1e-8 &&
                Math.Abs(actual.EndX - expected.EndX) < 1e-8 && Math.Abs(actual.StartRadius - expected.StartRadius) < 1e-8 &&
                Math.Abs(actual.EndRadius - expected.EndRadius) < 1e-8 && actual.Radius == expected.Radius &&
                actual.CenterX == expected.CenterX && actual.CenterRadius == expected.CenterRadius && actual.Clockwise == expected.Clockwise,
                $"Incorrect exterior segment #{i + 1}.");
        }

        var holes = part.Holes;
        Require(holes.Count == 14, "Expected 12 flange holes, separate P-P Ø22 and local Ø12.");
        Require(holes.Count(h => h.Type == HoleType.Simple && h.ThroughAll && h.Diameter == SeparateHoleDiameter &&
            h.X == 0 && h.Y == SeparateHoleOffset && h.Depth == FlangeThickness) == 1,
            "Incorrect separate P-P Ø22 hole position/type/length.");
        Require(holes.Count(h => h.Type == HoleType.Simple && h.ThroughAll && h.Diameter == LocalHoleDiameter &&
            h.X == 0 && h.Y == -LocalHoleOffset && h.Depth == LocalHoleDraftLength) == 1, "Incorrect local Ø12 hole position/type/draft length.");
        for (var index = 0; index < 12; index++)
        {
            double angle = (FlangeHoleStartAngle + index * 30) * Math.PI / 180;
            Require(holes.Count(h => h.Type == HoleType.Simple && h.ThroughAll && h.Diameter == FlangeHoleDiameter &&
                Math.Abs(h.X - 113 * Math.Cos(angle)) < 1e-6 &&
                Math.Abs(h.Y - 113 * Math.Sin(angle)) < 1e-6) == 1,
                $"Missing/duplicate hole at {FlangeHoleStartAngle + index * 30} degrees.");
        }
    }

    private static void ValidateChain(IEnumerable<(double X0, double X1, double R0, double R1)> chain)
    {
        (double X, double R)? previous = null;
        foreach (var e in chain)
        {
            Require(new[] { e.X0, e.X1, e.R0, e.R1 }.All(double.IsFinite), "Non-finite profile value.");
            Require(e.X1 > e.X0 || e.R1 != e.R0, "Zero-length profile segment.");
            if (previous is { } p)
                Require(Math.Abs(p.X - e.X0) < 1e-6 && Math.Abs(p.R - e.R0) < 1e-6, "Disconnected profile.");
            previous = (e.X1, e.R1);
        }
    }

    private static void ValidateExteriorTangencies(List<RevolveProfileElement> profile)
    {
        for (int i = 0; i < profile.Count; i++)
        {
            var arc = profile[i];
            if (arc.Type != RevolveProfileElementType.Arc) continue;
            Require(i > 0 && i + 1 < profile.Count && arc.Radius > 0 &&
                double.IsFinite(arc.CenterX) && double.IsFinite(arc.CenterRadius), "Invalid exterior arc.");
            foreach (var (x, r, line) in new[] { (arc.StartX, arc.StartRadius, profile[i - 1]),
                         (arc.EndX, arc.EndRadius, profile[i + 1]) })
            {
                double dx = x - arc.CenterX, dr = r - arc.CenterRadius;
                Require(Math.Abs(dx * dx + dr * dr - arc.Radius * arc.Radius) < 1e-8,
                    "Arc endpoint is not on its circle.");
                double lx = line.EndX - line.StartX, lr = line.EndRadius - line.StartRadius;
                double length = Math.Sqrt(lx * lx + lr * lr);
                Require(length > 0 && Math.Abs((dx * lx + dr * lr) / length / arc.Radius) < 1e-8,
                    "Exterior arc is not tangent to the adjacent line.");
                double direction = arc.Clockwise ? -1 : 1;
                Require(direction * (-dr * lx + dx * lr) > 0, "Reversed exterior arc tangent.");
            }
        }
    }

    public static void RunRegressionChecks(PartDescription part)
    {
        int rejected = 0;
        void Reject(string name, Action<PartDescription> mutate)
        {
            var copy = JsonSerializer.Deserialize<PartDescription>(JsonSerializer.Serialize(part))!;
            mutate(copy);
            try { Validate(copy); }
            catch (Exception) { rejected++; Console.WriteLine($"[TEST OK] Rejected: {name}"); return; }
            throw new Exception($"Regression was not detected: {name}");
        }
        Validate(part);
        double ringVolume = Math.PI * ((8 - 1.6) * 100 * 100
            + 1.6 * (100 * 100 + 100 * 98.4 + 98.4 * 98.4) / 3 - 8 * 95 * 95);
        double recessVolume = Math.PI * (4 * (95 * 95 - 48.5 * 48.5) + (95 * 95 - 47.5 * 47.5));
        double retained = Math.PI * (48 * 48 + 1.0 / 6 - 12 * Math.PI - 47.5 * 47.5);
        double rootDelta = Math.PI * (2 * 57 * 57 - 57 * 4 * Math.PI / 2 + 16.0 / 3 - 2 * 55 * 55);
        double chamferBottom = 62.5 - 3 / Math.Sqrt(3);
        double chamferDelta = Math.PI * (chamferBottom * chamferBottom + chamferBottom * 62.5 + 62.5 * 62.5 - 3 * 62.5 * 62.5);
        double rightRootDelta = Math.PI * (2 * 64.5 * 64.5 - 64.5 * 4 * Math.PI / 2 + 16.0 / 3 - 2 * 62.5 * 62.5);
        // Independent midpoint integration in X, not the angular primitive used by ArcVolume.
        double coneDelta = 0;
        const int samples = 100000;
        double step = (ConeTangentEnd - ConeTangentStart) / samples;
        for (int i = 0; i < samples; i++)
        {
            double x = ConeTangentStart + (i + 0.5) * step;
            double r = 54.5 - Math.Sqrt(4 - Math.Pow(x - ConeTangentStart, 2));
            double oldR = x <= Outer105End ? 52.5 : 52.5 + (x - Outer105End) / Math.Sqrt(3);
            coneDelta += Math.PI * (r * r - oldR * oldR) * step;
        }
        double correctedHolesDelta = 12 * 18 * Math.PI * (9.5 * 9.5 - 7.5 * 7.5);
        double rightStepDelta = 2 * Math.PI * 36; // Ø12 intersection shortened by 2 mm
        for (int i = 0; i < samples; i++)
        {
            double x = 2.0 * (i + 0.5) / samples;
            double r = x <= Math.Sqrt(2) ? 65.5 + x : 67.5 + 2 * (Math.Sqrt(2) - 1) - Math.Sqrt(4 - x * x);
            rightStepDelta += 2.0 / samples * Math.PI * (r * r - 10000);
        }
        double entryRemoval = Math.PI * (93 * Math.Sqrt(3) + 3);
        double leftEnvelopeDelta = Math.PI * (3 * (41 * 41 - 45 * 45) + 2.0 / 3 * (43 * 43 + 43 * 45 + 45 * 45) - 2 * 45 * 45);
        // Frustum minus the former Ø100 cylinder, before the intersecting R35 cut.
        leftEnvelopeDelta += Math.PI * (SplineLeadEnd - SplineLeadStart) * ((45 * 45 + 45 * 50 + 50 * 50) / 3.0 - 50 * 50);
        double leftRemoval = part.LeftEnd!.RemovedVolumeMm3(part);
        double splineShoulderDelta = 0;
        double splineStep = (Outer105Start - SplineTangentStart) / samples;
        for (int i = 0; i < samples; i++)
        {
            double x = SplineTangentStart + (i + 0.5) * splineStep;
            double r = x < SplineTangentEnd
                ? 53 - Math.Sqrt(9 - Math.Pow(x - SplineTangentStart, 2))
                : 52.5 - (Outer105Start - x) / Math.Sqrt(3);
            splineShoulderDelta += Math.PI * (r * r - 2500) * splineStep;
        }
        Require(Math.Abs(leftRemoval - part.LeftEnd.RemovedVolumeMm3(part, 40000)) < 0.001, "Left-end integral did not converge.");
        Require(Math.Abs(LeftEndDescription.CircularCap(2, 0) - 2 * Math.PI) < 1e-12 && LeftEndDescription.CircularCap(2, 2) == 0,
            "Incorrect circular segment area.");
        Require(Math.Abs(ExpectedDraftVolumeMm3(part) - (2271432.279208951 + ringVolume - recessVolume + retained - 21 * Math.PI * 36 + rootDelta + chamferDelta + rightRootDelta + coneDelta + correctedHolesDelta + rightStepDelta + ApprovedRightTransition.VolumeDeltaFromStage10Mm3 - entryRemoval + leftEnvelopeDelta - leftRemoval + splineShoulderDelta - 18 * Math.PI * 11 * 11)) < 0.001,
            "Draft volume differs from independent transition integrals.");
        Require(part.AnnularCuts[1].InnerRadiusAt(262.5) == 47.5 && part.AnnularCuts[1].InnerRadiusAt(262) == 48,
            "Incorrect root tangencies.");
        Require(Math.Abs((Outer110Start - Outer105End) * Math.Tan(Math.PI / 6) - 2.5) < 1e-10,
            "The Ø105/110 transition must have a nominal 30-degree slope.");
        Reject("old blind bore", p => p.BoreEndWallThickness = 8);
        Reject("outer Ø105 used as bore", p => p.BoreRevolveProfile[0].EndRadius = 52.5);
        Reject("26 mm flange", p => p.RevolveProfile.Single(e => e.EndX > e.StartX && e.StartRadius == 127.5).StartX = 239);
        Reject("duplicate hole", p => { p.Holes[1].X = p.Holes[0].X; p.Holes[1].Y = p.Holes[0].Y; });
        Reject("NaN", p => p.RevolveProfile[0].StartRadius = double.NaN);
        Reject("unverified M90", p => p.Threads.Add(new ThreadDescription()));
        Reject("old Ø131 middle cylinder", p => { var e = p.RevolveProfile.Single(e => e.StartX == Outer105Start && e.EndX > e.StartX); e.StartRadius = e.EndRadius = 65.5; });
        Reject("wrong Ø110 shoulder station", p => p.RevolveProfile.Single(e => e.StartX == Outer110Start).StartX = 184);
        Reject("old Ø188 middle cylinder", p => { var e = p.RevolveProfile.Single(e => e.StartRadius == 62.5 && e.EndRadius == 62.5 && e.EndX > e.StartX); e.StartRadius = e.EndRadius = 94; });
        Reject("wrong cone slope", p => p.RevolveProfile.Single(e => e.EndX == Outer110Start).StartX += 1);
        Reject("missing boss extent", p => p.TotalLength = FlangeRear);
        Reject("solid boss instead of annular", p => p.BoreRevolveProfile[^1].StartRadius = p.BoreRevolveProfile[^1].EndRadius = 45);
        Reject("missing boss chamfer", p => p.RevolveProfile[^1].EndRadius = 100);
        Reject("wrong boss chamfer length", p => p.RevolveProfile[^1].StartX = 272);
        Reject("missing external collar recess", p => p.AnnularCuts.Clear());
        Reject("groove 3 mm instead of 3-2", p => p.AnnularCuts[1].StartX -= 2);
        Reject("inverted groove radii", p => p.AnnularCuts[1].InnerRadius = 48.5);
        Reject("groove cut through Ø90 collar", p => p.AnnularCuts[1].InnerRadius = 44);
        Reject("missing R0.5", p => p.AnnularCuts[1].RootFilletRadius = 0);
        Reject("overlapping root fillets", p => p.AnnularCuts[1].RootFilletRadius = 0.6);
        Reject("negative root fillet", p => p.AnnularCuts[1].RootFilletRadius = -0.5);
        Reject("missing Ø12", p => p.Holes.RemoveAt(p.Holes.Count - 1));
        Reject("missing separate P-P hole", p => p.Holes.RemoveAt(12));
        Reject("P-P mistaken for D15 pattern", p => p.Holes[12].Diameter = 15);
        Reject("P-P placed on D226", p => p.Holes[12].Y = 113);
        Reject("P-P on wrong side", p => p.Holes[12].Y = -111.5);
        Reject("blind P-P", p => p.Holes[12].ThroughAll = false);
        Reject("P-P wrong thickness", p => p.Holes[12].Depth = 26);
        Require(SeparateHoleOffset - SeparateHoleDiameter / 2 > BossOuterRadius &&
            SeparateHoleOffset + SeparateHoleDiameter / 2 < 127.5,
            "P-P must lie wholly in the flange, outside the boss.");
        Require(part.Holes.Take(12).All(h => Math.Sqrt(h.X * h.X + Math.Pow(h.Y - SeparateHoleOffset, 2)) >
            (h.Diameter + SeparateHoleDiameter) / 2), "P-P intersects the flange pattern.");
        Reject("Ø12 on wrong side", p => p.Holes[^1].Y = 84);
        Reject("Ø12 on flange bolt circle", p => p.Holes[^1].Y = -113);
        Reject("blind Ø12", p => p.Holes[^1].ThroughAll = false);
        Reject("wrong shoulder R2", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == Shoulder125).Radius = 3);
        Reject("wrong shoulder arc direction", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == Shoulder125).Clockwise = true);
        Reject("wrong shoulder 30 degrees", p => p.RevolveProfile.Single(e => e.StartX == 215 && e.EndX == 218).StartRadius = 60);
        Reject("cone root below R2 min", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == ConeTangentEnd).Radius = 1.9);
        Reject("non-tangent cone root", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == ConeTangentEnd).CenterX += 0.1);
        Reject("non-finite arc center", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == ConeTangentEnd).CenterRadius = double.NaN);
        Reject("reversed cone root", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == ConeTangentEnd).Clockwise = true);
        Reject("wrong spline shoulder R3", p => p.RevolveProfile.Single(e => e.EndX == SplineTangentEnd).Radius = 2);
        Reject("non-tangent spline shoulder", p => p.RevolveProfile.Single(e => e.EndX == SplineTangentEnd).CenterRadius += 0.1);
        Reject("reversed spline shoulder arc", p => p.RevolveProfile.Single(e => e.EndX == SplineTangentEnd).Clockwise = true);
        Reject("wrong spline shoulder cone", p => p.RevolveProfile.Single(e => e.StartX == SplineTangentEnd).EndRadius = 53);
        Require(Math.Abs((SplineLeadEnd - SplineLeadStart) / 5 - Math.Tan(Math.PI / 12)) < 1e-12,
            "Spline lead-in must be 15 degrees from the radial direction.");
        Reject("lead-in angle measured from axis", p => p.RevolveProfile.Single(e => e.StartX == SplineLeadStart).EndX = SplineLeadStart + 5 / Math.Tan(Math.PI / 12));
        Reject("lead-in replaced by radial step", p => p.RevolveProfile.Single(e => e.StartX == SplineLeadStart).EndX = SplineLeadStart);
        Reject("wrong lead-in start station", p => p.RevolveProfile.Single(e => e.StartX == SplineLeadStart).StartX = 38);
        Reject("lead-in stops at root diameter", p => p.RevolveProfile.Single(e => e.StartX == SplineLeadStart).EndRadius = 47);
        Reject("missing left cuts", p => p.LeftEnd = null);
        Reject("84.5 treated as diameter", p => p.LeftEnd!.FlatHeight = 84.5 / 2);
        Reject("wrong flat length", p => p.LeftEnd!.FlatLength = 24);
        Reject("wrong R35 runout", p => p.LeftEnd!.RunoutRadius = 50);
        Reject("wrong bottom cut", p => p.LeftEnd!.BottomHeight = -20);
        Reject("bottom cut extends into thread blank", p => p.LeftEnd!.BottomLength = 25);
        Reject("wrong Ø82 end", p => p.RevolveProfile[0].StartRadius = 45);
        Reject("missing external chamfer", p => p.RevolveProfile[2].StartRadius = 45);
        Reject("wrong right root", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == ShoulderRight).Radius = 2.2);
        Reject("wrong Ø131 start", p => p.RevolveProfile.Single(e => e.StartX == ShoulderRight && e.EndX > e.StartX).StartRadius = 65);
        Reject("wrong 2 mm right step", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == RightStepPlane).EndX += 0.2);
        Reject("wrong right-step R2", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == RightStepPlane).Radius = 1);
        Reject("stale Ø12 intersection length", p => p.Holes[^1].Depth = 21);
        Reject("missing bore entry chamfer", p => p.BoreRevolveProfile[0].StartRadius = 31);
        Reject("wrong bore entry axial length", p => p.BoreRevolveProfile[0].EndX = 2);
        Reject("entry 30 degrees to face instead of axis", p => p.BoreRevolveProfile[0].StartRadius = 31 + 3 * Math.Sqrt(3));
        Reject("changed approved temporary station", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == AssumedRightStation).EndX = 244);
        Reject("wrong Ø164 theoretical corner", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == AssumedRightStation).CenterRadius -= 1);
        Reject("wrong flange-root R2", p => p.RevolveProfile.Single(e => e.Type == RevolveProfileElementType.Arc && e.EndX == FlangeFront).Radius = 1);
        Require(LocalHoleRemovedVolumeMm3 < Math.PI * 36 * LocalHoleDraftLength,
            "Partial Ø12 intersection must not be treated as a full cylinder.");
        Reject("historical Ø19 flange holes", p => { foreach (var h in p.Holes.Take(12)) h.Diameter = 19; });
        Reject("historical 90-degree flange phase", p =>
        {
            for (int i = 0; i < 12; i++)
            {
                double a = (90 + i * 30) * Math.PI / 180;
                p.Holes[i].X = 113 * Math.Cos(a); p.Holes[i].Y = 113 * Math.Sin(a);
            }
        });
        Console.WriteLine($"[TEST OK] Valid {Revision} draft + {rejected} regression cases passed without SolidWorks.");
        Console.WriteLine($"[TEST OK] Analytical volume: {ExpectedDraftVolumeMm3(part):F6} mm³.");
    }

    public static double ExpectedDraftVolumeMm3(PartDescription part)
    {
        Validate(part);
        static double Segment(double x0, double x1, double r0, double r1) =>
            Math.PI * (x1 - x0) * (r0 * r0 + r0 * r1 + r1 * r1) / 3;
        return part.RevolveProfile.Sum(e => e.Type == RevolveProfileElementType.Arc ? ArcVolume(e) : Segment(e.StartX, e.EndX, e.StartRadius, e.EndRadius))
            - part.BoreRevolveProfile.Sum(e => Segment(e.StartX, e.EndX, e.StartRadius, e.EndRadius))
            - 12 * Math.PI * Math.Pow(FlangeHoleDiameter / 2, 2) * FlangeThickness
            - LocalHoleRemovedVolumeMm3
            - SeparateHoleVolumeMm3
            - part.LeftEnd!.RemovedVolumeMm3(part)
            - part.AnnularCuts.Sum(c => c.VolumeMm3);
    }

    private static (double Start, double Delta) ArcAngles(RevolveProfileElement e)
    {
        double start = Math.Atan2(e.StartRadius - e.CenterRadius, e.StartX - e.CenterX);
        double delta = Math.Atan2(e.EndRadius - e.CenterRadius, e.EndX - e.CenterX) - start;
        if (e.Clockwise) { while (delta >= 0) delta -= 2 * Math.PI; }
        else { while (delta <= 0) delta += 2 * Math.PI; }
        return (start, delta);
    }

    private static double ArcVolume(RevolveProfileElement e)
    {
        var (start, delta) = ArcAngles(e);
        double a = e.Radius, c = e.CenterRadius;
        double Primitive(double t) => a * c * c * Math.Cos(t)
            - c * a * a * (t - Math.Sin(t) * Math.Cos(t))
            + a * a * a * (Math.Cos(t) - Math.Pow(Math.Cos(t), 3) / 3);
        return Math.PI * (Primitive(start + delta) - Primitive(start));
    }

    private static IEnumerable<(double X, double R)> OuterPoints(RevolveProfileElement e)
    {
        if (e.Type != RevolveProfileElementType.Arc) return new[] { (e.StartX, e.StartRadius), (e.EndX, e.EndRadius) };
        var (start, delta) = ArcAngles(e);
        return Enumerable.Range(0, 65).Select(i => (e.CenterX + e.Radius * Math.Cos(start + delta * i / 64),
            e.CenterRadius + e.Radius * Math.Sin(start + delta * i / 64)));
    }

    public static void WriteSectionSvg(PartDescription part, string path)
    {
        static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        string Points(IEnumerable<(double X, double R)> values, int sign) => string.Join(" ",
            values.Select(p => $"{F(40 + p.X * 3)},{F(445 - sign * p.R * 3)}"));
        var exterior = part.RevolveProfile.SelectMany(OuterPoints);
        var bore = part.BoreRevolveProfile.SelectMany(e => new[] { (e.StartX, e.StartRadius), (e.EndX, e.EndRadius) });
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='1000' height='930' viewBox='0 0 1000 930'><rect width='1000' height='930' fill='white'/><g font-family='Arial' font-size='16'><text x='30' y='24'>{Revision.ToUpperInvariant()} DRAFT: right station X=245 is a USER-APPROVED TEMPORARY ASSUMPTION.</text><text x='30' y='48'>Orange: unfinished / assumed geometry; blue: bore/recess. Local D12 not shown.</text></g>");
        foreach (int sign in new[] { 1, -1 })
        {
            svg.Append($"<polygon points='{Points(exterior.Concat(bore.Reverse()), sign)}' fill='#e4e7eb' stroke='#555' stroke-width='1'/>");
            svg.Append($"<polyline points='{Points(bore, sign)}' fill='none' stroke='#1368ba' stroke-width='3'/>");
            foreach (var e in part.RevolveProfile)
            {
                bool mainDiameter = (e.StartX >= Outer105Start && e.EndX <= RightStepPlane && e.EndRadius <= RightStepArcCentreRadius)
                    || e.StartX >= FlangeFront;
                string color = mainDiameter ? "#137a45" : "#bd6b15";
                svg.Append($"<polyline points='{Points(OuterPoints(e), sign)}' fill='none' stroke='{color}' stroke-width='3'/>");
            }
            if (part.AnnularCuts.Count > 0)
            {
                var lower = part.AnnularCuts.SelectMany(c => c.BoundaryPoints()).ToArray();
                var outline = new[] { (RearRecessStart, BossInnerRadius) }.Concat(lower).ToArray();
                var cavity = outline.Append((CollarEnd, BossInnerRadius));
                svg.Append($"<polygon points='{Points(cavity, sign)}' fill='white' stroke='white' stroke-width='4'/>");
                svg.Append($"<polyline points='{Points(outline, sign)}' fill='none' stroke='#1368ba' stroke-width='2'/>");
                svg.Append($"<polyline points='{Points(new[] { (RearRecessStart, BossInnerRadius), (BossEnd, BossInnerRadius) }, sign)}' fill='none' stroke='#1368ba' stroke-width='2'/>");
            }
        }
        // Overlay the local left cuts in the meridional section; not axisymmetric.
        var left = part.LeftEnd!;
        var topCut = Enumerable.Range(0, 301).Select(i =>
        {
            double x = 60.0 * i / 300;
            return (x, left.TopBoundary(x));
        }).ToArray();
        svg.Append($"<polygon points='{Points(topCut.Concat(new[] { (60.0, 80.0), (0.0, 80.0) }), 1)}' fill='white'/>");
        var visibleTop = topCut.Where(p => p.Item2 <= 50).ToArray();
        svg.Append($"<polyline points='{Points(visibleTop, 1)}' fill='none' stroke='#137a45' stroke-width='2'/>");
        svg.Append($"<polygon points='{Points(new[] { (0.0, -25.0), (3.0, -25.0), (3.0, -60.0), (0.0, -60.0) }, 1)}' fill='white'/>");
        svg.Append("<g font-family='Arial' font-size='13' fill='#137a45'><text x='40' y='320'>Flat 84.5 / R35</text><text x='40' y='550'>End cut</text></g>");
        svg.Append("<g font-family='Arial' font-size='14' fill='#137a45'>");
        foreach (var (x, r, label) in new[] { (130.0, 52.5, "D105"), (199.0, 55.0, "D110"), (228.0, 62.5, "D125") })
            svg.Append($"<text x='{F(40 + x * 3)}' y='{F(445 - r * 3 - 12)}' text-anchor='middle'>{label}</text>");
        svg.Append("</g><g font-family='Arial' font-size='13' fill='#bd6b15'><text x='90' y='290'>D90 blank</text><text x='210' y='274'>D100 spline blank</text></g>");
        svg.Append("<path d='M20 445H950' stroke='#888' stroke-dasharray='12 5'/><g font-family='Arial' font-size='14' fill='#1368ba'>");
        foreach (var e in part.BoreRevolveProfile.Where(e => e.EndX > e.StartX && e.StartX < FlangeRear && e.StartRadius == e.EndRadius))
            svg.Append($"<text x='{F(40 + (e.StartX + e.EndX) * 1.5)}' y='430' text-anchor='middle'>D{F(e.StartRadius * 2)}</text>");
        svg.Append("</g><g font-family='Arial' font-size='13'>");
        foreach (double station in new[] { 0.0, 42, Outer105Start, Outer110Start, Shoulder125, ShoulderRight, FlangeFront, FlangeRear, BossEnd })
            svg.Append($"<text x='{F(40 + station * 3)}' y='875' transform='rotate(45 {F(40 + station * 3)} 875)'>X={F(station)}</text>");
        svg.Append("</g><g font-family='Arial' font-size='14'><text x='867' y='136'>D200 / D190</text><text x='867' y='156'>1.6 x 45 deg</text><text x='867' y='176'>X=265..273</text><path d='M861 158H851' stroke='#137a45'/></g></svg>");
        File.WriteAllText(path, svg.ToString());
    }

    public static void WriteEndViewSvg(PartDescription part, string path)
    {
        Validate(part);
        static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='850' height='850'><rect width='850' height='850' fill='white'/><g font-family='Arial' font-size='17'><text x='30' y='30'>{Revision.ToUpperInvariant()} DRAFT: flange hole layout, not a CAD export</text><text x='30' y='56'>12 x D15 on D226, phase 15 deg; D22 at +111.5; D12 at -84</text><text x='30' y='82'>Threads, channels and shaped recess omitted / unfinished.</text></g>");
        svg.Append("<circle cx='420' cy='440' r='318.75' fill='#e4e7eb' stroke='#333' stroke-width='2'/><circle cx='420' cy='440' r='237.5' fill='white' stroke='#777'/><circle cx='420' cy='440' r='282.5' fill='none' stroke='#888' stroke-dasharray='9 5'/><path d='M70 440H770 M420 100V780' stroke='#aaa' stroke-dasharray='10 5'/>");
        foreach (var h in part.Holes)
            svg.Append($"<circle cx='{F(420 + h.X * 2.5)}' cy='{F(440 - h.Y * 2.5)}' r='{F(h.Diameter * 1.25)}' fill='white' stroke='#1368ba' stroke-width='2'/>");
        svg.Append("<g font-family='Arial' font-size='16'><text x='30' y='818'>Only the flange outline and hole centres are represented; inner geometry is schematic.</text></g></svg>");
        File.WriteAllText(path, svg.ToString());
    }

    public static void WriteCollarDetailSvg(string path)
    {
        // Magnified meridional section, not a CAD face export. The inner bore is Ø90.
        static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        string P(IEnumerable<(double X, double R)> points) => string.Join(" ", points.Select(p =>
            $"{F(80 + (p.X - 258) * 70)},{F(450 - (p.R - 44) * 65)}"));
        var boundary = new[] { (258.0, 45.0), (265.0, 45.0) }
            .Concat(NominalAnnularCuts().SelectMany(c => c.BoundaryPoints(80)).Reverse())
            .Concat(new[] { (260.0, 50.0), (258.0, 50.0) });
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='800' height='620'><rect width='800' height='620' fill='white'/><g font-family='Arial' font-size='17'><text x='30' y='28'>{Revision.ToUpperInvariant()}: collar unchanged; tangent R0.5 root fillets</text><text x='30' y='55'>X=265 collar end remains a chosen zero-inset nominal.</text></g>");
        svg.Append($"<polygon points='{P(boundary)}' fill='#e4e7eb' stroke='#1368ba' stroke-width='3'/>");
        svg.Append("<g font-family='Arial' font-size='16'><text x='600' y='165'>D97 collar</text><text x='383' y='260'>D95 root</text><text x='295' y='420'>D90 bore</text><text x='70' y='110'>Material</text><text x='390' y='120'>Rear recess (air)</text>");
        foreach (double x in new[] { 260.0, 262, 263, 265 })
            svg.Append($"<text x='{F(80 + (x - 258) * 70)}' y='490' text-anchor='middle'>X={F(x)}</text>");
        svg.Append("<text x='270' y='535'>Groove width = 3 - 2 = 1 mm (not 3 mm)</text><text x='270' y='565'>Root wall = (95 - 90) / 2 = 2.5 mm</text></g></svg>");
        File.WriteAllText(path, svg.ToString());
    }
}

using CadTest.Models;

namespace CadTest.Drawing;

// Historical API fixture. Dimensions below are NOT an approved drawing interpretation.
internal static class LegacyControlPartFactory
{
    internal static PartDescription Create()
    {
        return new PartDescription
        {
            TotalLength = 265,
        
            // Ø68 является глухой расточкой: X=239..257 (18 мм),
            // после неё до правого торца остаётся 8 мм материала.
            BoreEndWallThickness = 8,
        
            // ---------------------------------------------------------
            // Основной ступенчатый профиль детали
            // ---------------------------------------------------------
        
            ProfileStations = new List<ProfileStation>
            {
                new ProfileStation
                {
                    Position = 0,
                    Diameter = 40
                },
        
                new ProfileStation
                {
                    Position = 10,
                    Diameter = 255
                },
        
                new ProfileStation
                {
                    Position = 40,
                    Diameter = 30
                },
        
                new ProfileStation
                {
                    Position = 265,
                    Diameter = 30
                }
            },
            // ---------------------------------------------------------
            // Одиночные отверстия
            // ---------------------------------------------------------
        
            Holes = new List<HoleDescription>(),
        
            // ---------------------------------------------------------
            // Группы отверстий
            // ---------------------------------------------------------
        
            HolePatterns = new List<HolePatternDescription>
            {
                new HolePatternDescription
                {
                    Count = 12,
                    BoltCircleDiameter = 226,
                    StartAngle = 90,
                    EqualSpacing = true,
        
                    Hole = new HoleDescription
                    {
                        Type = HoleType.Simple,
                        Diameter = 19,
                        Depth = 10,
                        ThroughAll = true
                    }
                }
            },
        
            // Наружная резьба на левом торце; цилиндр Ø90 × 3 входит
            // в RevolveProfile, а Thread Feature создаётся после Revolve.
            Threads = new List<ThreadDescription>
            {
                new ThreadDescription
                {
                    Type = ThreadType.External,
                    Designation = "M90x2",
                    ToleranceClass = "6g",
                    NominalDiameter = 90,
                    Pitch = 2,
                    StartPosition = 0,
                    Length = 3,
                    ThroughAll = false
                }
            },
        
            RevolveProfile = new List<RevolveProfileElement>
            {
                // ------------------------------------------------------------
                // Левая резьбовая часть M90x2
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 0,
                    EndX = 3,
                    StartRadius = 45,
                    EndRadius = 45
                },
        
                // Переход с резьбовой части на Ø82
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 3,
                    EndX = 3,
                    StartRadius = 45,
                    EndRadius = 41
                },
        
                // Ø82
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 3,
                    EndX = 25,
                    StartRadius = 41,
                    EndRadius = 41
                },
        
                // ------------------------------------------------------------
                // Переходная часть корпуса
                // R35 начинается от подтверждённой точки X=25, Ø82.
                // До восстановления второй точки касания участок остаётся
                // контрольным конусом, а не окончательной геометрией.
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Cone,
                    StartX = 25,
                    EndX = 70,
                    StartRadius = 41,
                    EndRadius = 65.5
                },
        
                // ------------------------------------------------------------
                // Основной участок Ø131
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 70,
                    EndX = 144,
                    StartRadius = 65.5,
                    EndRadius = 65.5
                },
        
                // ------------------------------------------------------------
                // Переход к большой части корпуса
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Cone,
                    StartX = 144,
                    EndX = 200,
                    StartRadius = 65.5,
                    EndRadius = 94
                },
        
                // ------------------------------------------------------------
                // Участок Ø188
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 200,
                    EndX = 220,
                    StartRadius = 94,
                    EndRadius = 94
                },
        
                // ------------------------------------------------------------
                // Участок Ø190
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 220,
                    EndX = 239,
                    StartRadius = 95,
                    EndRadius = 95
                },
        
                // Переход Ø190 → Ø200
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 239,
                    EndX = 239,
                    StartRadius = 95,
                    EndRadius = 100
                },
        
                // ------------------------------------------------------------
                // Участок Ø200
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 239,
                    EndX = 239,
                    StartRadius = 100,
                    EndRadius = 100
                },
        
                // ------------------------------------------------------------
                // Фланец Ø255
                // ------------------------------------------------------------
                new RevolveProfileElement
                {
                    Type = RevolveProfileElementType.Line,
                    StartX = 239,
                    EndX = 265,
                    StartRadius = 127.5,
                    EndRadius = 127.5
                }
            },
        
            BoreProfile = new List<BoreStep>
            {
                new BoreStep
                {
                    StartPosition = 0,
                    Length = 38,
                    Diameter = 62
                }
            },
        
           BoreRevolveProfile = new List<BoreProfileElement>
            {
                // ============================================================
                // Ø62
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Line,
        
                    StartX = 0,
                    EndX = 42,
        
                    StartRadius = 31,
                    EndRadius = 31,
        
                    Description = "Ø62 / 42 +1"
                },
        
                // ============================================================
                // 15°
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Cone,
        
                    StartX = 42,
                    EndX = 70,
        
                    StartRadius = 31,
                    EndRadius = 38.502577,
        
                    Description = "15°"
                },
        
                // ============================================================
                // R50
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Arc,
        
                    StartX = 70,
                    EndX = 90,
        
                    StartRadius = 38.502577,
                    EndRadius = 52.5,
        
                    Radius = 50.0,
        
                    CenterX = 107.802204,
                    CenterRadius = 5.776542,
        
                    Clockwise = true,
        
                    Description = "R50"
                },
        
                // ============================================================
                // Ø105
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Line,
        
                    StartX = 90,
                    EndX = 142,
        
                    StartRadius = 52.5,
                    EndRadius = 52.5,
        
                    Description = "Ø105"
                },
        
                // ============================================================
                // R3
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Arc,
        
                    StartX = 142.0,
                    EndX = 143.5,
        
                    StartRadius = 52.5,
                    EndRadius = 52.901924,
        
                    Radius = 3.0,
        
                    CenterX = 142.0,
                    CenterRadius = 55.5,
        
                    Clockwise = false,
        
                    Description = "R3"
                },
        
                // ============================================================
                // 30°
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Cone,
        
                    StartX = 143.5,
                    EndX = 146.669872,
        
                    StartRadius = 52.901924,
                    EndRadius = 54.732051,
        
                    Description = "30°"
                },
        
                // ============================================================
                // R2 min
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Arc,
        
                    StartX = 146.669872,
                    EndX = 147.669872,
        
                    StartRadius = 54.732051,
                    EndRadius = 55.0,
        
                    Radius = 2.0,
        
                    CenterX = 147.669872,
                    CenterRadius = 53.0,
        
                    Clockwise = true,
        
                    Description = "R2 min"
                },
        
                // ============================================================
                // Ø110
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Line,
        
                    StartX = 147.669872,
                    EndX = 203.5,
        
                    StartRadius = 55.0,
                    EndRadius = 55.0,
        
                    Description = "Ø110"
                },
        
                // ============================================================
                // Ступень Ø110 → Ø94
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Line,
        
                    StartX = 203.5,
                    EndX = 203.5,
        
                    StartRadius = 55.0,
                    EndRadius = 47.0,
        
                    Description = "Ступень Ø110 → Ø94"
                },
        
                // ============================================================
                // Ø94
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Line,
        
                    StartX = 203.5,
                    EndX = 226.0,
        
                    StartRadius = 47.0,
                    EndRadius = 47.0,
        
                    Description = "Ø94"
                },
        
                // ============================================================
                // 45° : Ø94 → Ø68
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Cone,
        
                    StartX = 226.0,
                    EndX = 239.0,
        
                    StartRadius = 47.0,
                    EndRadius = 34.0,
        
                    Description = "45° / Ø94 → Ø68"
                },
        
                // ============================================================
                // Ø68
                // ============================================================
        
                new BoreProfileElement
                {
                    Type = BoreProfileElementType.Line,
        
                    StartX = 239.0,
                    EndX = 257.0,
        
                    StartRadius = 34.0,
                    EndRadius = 34.0,
        
                    Description = "Ø68 / 18 мм; глухая расточка, стенка 8 мм"
                }
            }
        };
    }
}


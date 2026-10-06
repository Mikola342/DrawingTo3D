using System;
using System.Collections.Generic;

namespace CadTest.Models;

public class PartDescription
{
    // Общая длина детали, мм
    public double TotalLength { get; set; }

    // Ступени наружного профиля
    public List<ProfileStep> Profile { get; set; } = new();

    public List<ProfileStation> ProfileStations { get; set; } = new();

    // Универсальный профиль для построения Revolve
    public List<RevolveProfileElement> RevolveProfile { get; set; } = new();

    // Ступенчатый внутренний профиль / расточка
    public List<BoreStep> BoreProfile { get; set; } = new();

    public List<BoreProfileElement> BoreRevolveProfile { get; set; } = new();
    public List<AnnularCutDescription> AnnularCuts { get; set; } = new();
    public LeftEndDescription? LeftEnd { get; set; }

    // Толщина материала от конца глухой расточки до правого торца, мм.
    // 0 означает выход на торец; null отключает проверку толщины.
    public double? BoreEndWallThickness { get; set; }

    // Отверстия
    public List<HoleDescription> Holes { get; set; } = new();

    // Группы отверстий
    public List<HolePatternDescription> HolePatterns { get; set; } = new();

    // Резьбы
    public List<ThreadDescription> Threads { get; set; } = new();

    // Фаски
    public List<ChamferDescription> Chamfers { get; set; } = new();

    // Скругления
    public List<FilletDescription> Fillets { get; set; } = new();

    public void Validate()
    {
        if (TotalLength <= 0)
            throw new Exception(
                $"Некорректная общая длина детали: {TotalLength} мм."
            );

        if ((Profile == null || Profile.Count == 0) && RevolveProfile.Count == 0)
            throw new Exception(
                "Профиль детали не содержит участков."
            );

        double expectedStart = 0.0;

        foreach (ProfileStep step in Profile ?? new())
        {
            if (step.StartPosition < 0)
                throw new Exception(
                    $"Некорректная начальная позиция: {step.StartPosition} мм."
                );

            if (step.Length <= 0)
                throw new Exception(
                    $"Некорректная длина участка: {step.Length} мм."
                );

            if (step.Diameter <= 0)
                throw new Exception(
                    $"Некорректный диаметр участка: {step.Diameter} мм."
                );

            if (Math.Abs(step.StartPosition - expectedStart) > 0.0001)
                throw new Exception(
                    $"Разрыв профиля: ожидалось начало " +
                    $"{expectedStart} мм, получено {step.StartPosition} мм."
                );

            expectedStart =
                step.StartPosition + step.Length;
        }

        if (Profile is { Count: > 0 } && Math.Abs(expectedStart - TotalLength) > 0.0001)
            throw new Exception(
                $"Профиль заканчивается на {expectedStart} мм, " +
                $"но TotalLength = {TotalLength} мм."
            );

        // -----------------------------------------------------
        // Проверка внутреннего профиля / расточки
        // -----------------------------------------------------

        if (BoreProfile == null)
        {
            throw new Exception(
                "Внутренний профиль детали не задан."
            );
        }

        if (BoreProfile.Count > 0)
        {
            double expectedBoreStart = 0.0;

            foreach (BoreStep step in BoreProfile)
            {
                if (step.StartPosition < 0)
                {
                    throw new Exception(
                        $"Некорректная начальная позиция внутреннего профиля: " +
                        $"{step.StartPosition} мм."
                    );
                }

                if (step.Length <= 0)
                {
                    throw new Exception(
                        $"Некорректная длина внутреннего участка: " +
                        $"{step.Length} мм."
                    );
                }

                if (step.Diameter <= 0)
                {
                    throw new Exception(
                        $"Некорректный внутренний диаметр: " +
                        $"{step.Diameter} мм."
                    );
                }

                if (Math.Abs(
                        step.StartPosition - expectedBoreStart
                    ) > 0.0001)
                {
                    throw new Exception(
                        $"Разрыв внутреннего профиля: " +
                        $"ожидалось начало {expectedBoreStart} мм, " +
                        $"получено {step.StartPosition} мм."
                    );
                }

                if (step.StartPosition + step.Length > TotalLength)
                {
                    throw new Exception(
                        $"Внутренний профиль выходит за пределы детали: " +
                        $"{step.StartPosition + step.Length} мм."
                    );
                }

                expectedBoreStart =
                    step.StartPosition + step.Length;
            }
        }

        // -----------------------------------------------------
        // Проверка одиночных отверстий
        // -----------------------------------------------------

        foreach (HoleDescription hole in Holes)
        {
            if (double.IsNaN(hole.Diameter) ||
                double.IsInfinity(hole.Diameter) ||
                hole.Diameter <= 0)
            {
                throw new Exception(
                    $"Некорректный диаметр отверстия: " +
                    $"{hole.Diameter} мм."
                );
            }

            if (double.IsNaN(hole.X) ||
                double.IsInfinity(hole.X))
            {
                throw new Exception(
                    $"Некорректная координата X отверстия: " +
                    $"{hole.X} мм."
                );
            }

            if (double.IsNaN(hole.Y) ||
                double.IsInfinity(hole.Y))
            {
                throw new Exception(
                    $"Некорректная координата Y отверстия: " +
                    $"{hole.Y} мм."
                );
            }

            if (!hole.ThroughAll)
            {
                if (double.IsNaN(hole.Depth) ||
                    double.IsInfinity(hole.Depth) ||
                    hole.Depth <= 0)
                {
                    throw new Exception(
                        $"Некорректная глубина отверстия: " +
                        $"{hole.Depth} мм."
                    );
                }
            }

            switch (hole.Type)
            {
                case HoleType.Simple:
                    break;

                case HoleType.Counterbore:
                case HoleType.Countersink:

                    if (double.IsNaN(hole.HeadDiameter) ||
                        double.IsInfinity(hole.HeadDiameter) ||
                        hole.HeadDiameter <= 0)
                    {
                        throw new Exception(
                            $"Некорректный HeadDiameter: " +
                            $"{hole.HeadDiameter} мм."
                        );
                    }

                    if (hole.HeadDiameter <= hole.Diameter)
                    {
                        throw new Exception(
                            $"Для отверстия типа {hole.Type} " +
                            $"HeadDiameter должен быть больше Diameter."
                        );
                    }

                    if (double.IsNaN(hole.HeadDepth) ||
                        double.IsInfinity(hole.HeadDepth) ||
                        hole.HeadDepth <= 0)
                    {
                        throw new Exception(
                            $"Некорректный HeadDepth: " +
                            $"{hole.HeadDepth} мм."
                        );
                    }

                    break;

                case HoleType.Threaded:

                    if (hole.Thread == null)
                    {
                        throw new Exception(
                            "Для резьбового отверстия " +
                            "не указано описание резьбы."
                        );
                    }

                    if (string.IsNullOrWhiteSpace(
                        hole.Thread.Designation))
                    {
                        throw new Exception(
                            "Для резьбы не указано обозначение."
                        );
                    }

                    if (double.IsNaN(
                            hole.Thread.NominalDiameter) ||
                        double.IsInfinity(
                            hole.Thread.NominalDiameter) ||
                        hole.Thread.NominalDiameter <= 0)
                    {
                        throw new Exception(
                            $"Некорректный номинальный диаметр резьбы: " +
                            $"{hole.Thread.NominalDiameter} мм."
                        );
                    }

                    if (double.IsNaN(hole.Thread.Pitch) ||
                        double.IsInfinity(hole.Thread.Pitch) ||
                        hole.Thread.Pitch <= 0)
                    {
                        throw new Exception(
                            $"Некорректный шаг резьбы: " +
                            $"{hole.Thread.Pitch} мм."
                        );
                    }

                    break;

                default:

                    throw new Exception(
                        $"Неизвестный тип отверстия: {hole.Type}"
                    );
            }
        }

        // -----------------------------------------------------
        // Проверка групп отверстий
        // -----------------------------------------------------

        foreach (HolePatternDescription pattern in HolePatterns)
        {
            if (pattern.Count < 2)
            {
                throw new Exception(
                    $"Количество отверстий в группе должно быть >= 2: " +
                    $"{pattern.Count}."
                );
            }

            if (double.IsNaN(pattern.BoltCircleDiameter) ||
                double.IsInfinity(pattern.BoltCircleDiameter) ||
                pattern.BoltCircleDiameter <= 0)
            {
                throw new Exception(
                    $"Некорректный диаметр окружности отверстий: " +
                    $"{pattern.BoltCircleDiameter} мм."
                );
            }

            if (double.IsNaN(pattern.StartAngle) ||
                double.IsInfinity(pattern.StartAngle))
            {
                throw new Exception(
                    $"Некорректный начальный угол: " +
                    $"{pattern.StartAngle}°."
                );
            }

            if (pattern.Hole == null)
            {
                throw new Exception(
                    "Группа отверстий не содержит описания отверстия."
                );
            }
        }

        // -----------------------------------------------------
        // Проверка резьб
        // -----------------------------------------------------

        foreach (ThreadDescription thread in Threads)
        {
            if (string.IsNullOrWhiteSpace(thread.Designation))
            {
                throw new Exception(
                    "Для резьбы не указано обозначение."
                );
            }

            if (double.IsNaN(thread.NominalDiameter) ||
                double.IsInfinity(thread.NominalDiameter) ||
                thread.NominalDiameter <= 0)
            {
                throw new Exception(
                    $"Некорректный номинальный диаметр резьбы: " +
                    $"{thread.NominalDiameter} мм."
                );
            }

            if (double.IsNaN(thread.Pitch) ||
                double.IsInfinity(thread.Pitch) ||
                thread.Pitch <= 0)
            {
                throw new Exception(
                    $"Некорректный шаг резьбы: " +
                    $"{thread.Pitch} мм."
                );
            }

            if (thread.StartPosition < 0 ||
                thread.StartPosition > TotalLength)
            {
                throw new Exception(
                    $"Некорректная начальная позиция резьбы: " +
                    $"{thread.StartPosition} мм."
                );
            }

            if (thread.Length <= 0)
            {
                throw new Exception(
                    $"Некорректная длина резьбы: " +
                    $"{thread.Length} мм."
                );
            }

            if (!thread.ThroughAll &&
                thread.StartPosition + thread.Length > TotalLength)
            {
                throw new Exception(
                    $"Резьба выходит за пределы детали: " +
                    $"конец на " +
                    $"{thread.StartPosition + thread.Length} мм."
                );
            }

            if (thread.Starts < 1)
            {
                throw new Exception(
                    $"Количество заходов резьбы должно быть >= 1: " +
                    $"{thread.Starts}."
                );
            }
        }
    }

    public void ValidateRevolveProfiles()
    {
        ValidateProfileChain(
            RevolveProfile,
            "Наружный профиль"
        );

        ValidateBoreProfileChain(
            BoreRevolveProfile,
            "Внутренний профиль"
        );

        if (BoreEndWallThickness is double expectedWallThickness)
        {
            if (expectedWallThickness < 0)
            {
                throw new Exception(
                    "Толщина стенки в конце расточки не может быть отрицательной."
                );
            }

            double actualWallThickness = TotalLength -
                BoreRevolveProfile[^1].EndX;

            if (Math.Abs(actualWallThickness - expectedWallThickness) > 0.0001)
            {
                throw new Exception(
                    "Конец внутреннего профиля не соответствует заданной " +
                    $"толщине стенки: ожидалось {expectedWallThickness} мм, " +
                    $"получено {actualWallThickness} мм."
                );
            }
        }
    }

    // Drawing-specific checks live in DrawingRevision. The former checks
    // incorrectly treated the historical API fixture as drawing evidence.

    private static void ValidateProfileChain(
        List<RevolveProfileElement> profile,
        string name)
    {
        if (profile == null || profile.Count == 0)
            throw new Exception(
                $"{name}: профиль пуст."
            );

        double currentX = profile[0].StartX;

        for (int i = 0; i < profile.Count; i++)
        {
            RevolveProfileElement element = profile[i];

            if (element.EndX < element.StartX)
            {
                throw new Exception(
                    $"{name}: элемент #{i + 1} имеет EndX < StartX."
                );
            }

            if (i > 0 &&
                Math.Abs(element.StartX - currentX) > 0.0001)
            {
                throw new Exception(
                    $"{name}: разрыв между элементами " +
                    $"#{i} и #{i + 1}. " +
                    $"Ожидалось X={currentX}, " +
                    $"получено X={element.StartX}."
                );
            }

            if (element.StartRadius <= 0 ||
                element.EndRadius <= 0)
            {
                throw new Exception(
                    $"{name}: элемент #{i + 1} " +
                    $"имеет недопустимый радиус."
                );
            }

            if (element.Type == RevolveProfileElementType.Arc &&
                element.Radius <= 0)
            {
                throw new Exception(
                    $"{name}: дуга #{i + 1} " +
                    "имеет недопустимый Radius."
                );
            }

            if (element.Type == RevolveProfileElementType.Arc)
            {
                double startDx = element.StartX - element.CenterX;
                double startDy = element.StartRadius - element.CenterRadius;
                double startDistance = Math.Sqrt(
                    startDx * startDx + startDy * startDy
                );

                double endDx = element.EndX - element.CenterX;
                double endDy = element.EndRadius - element.CenterRadius;
                double endDistance = Math.Sqrt(
                    endDx * endDx + endDy * endDy
                );

                const double arcToleranceMm = 0.001;

                if (Math.Abs(startDistance - element.Radius) > arcToleranceMm ||
                    Math.Abs(endDistance - element.Radius) > arcToleranceMm)
                {
                    throw new Exception(
                        $"{name}: концы дуги #{i + 1} " +
                        "не лежат на заданном радиусе."
                    );
                }
            }

            currentX = element.EndX;
        }
    }

    private static void ValidateBoreProfileChain(
        List<BoreProfileElement> profile,
        string name)
    {
        if (profile == null || profile.Count == 0)
            throw new Exception(
                $"{name}: профиль пуст."
            );

        double currentX = profile[0].StartX;

        for (int i = 0; i < profile.Count; i++)
        {
            BoreProfileElement element = profile[i];

            if (element.EndX < element.StartX)
            {
                throw new Exception(
                    $"{name}: элемент #{i + 1} " +
                    $"имеет EndX < StartX."
                );
            }

            if (i > 0 &&
                Math.Abs(element.StartX - currentX) > 0.0001)
            {
                throw new Exception(
                    $"{name}: разрыв между элементами " +
                    $"#{i} и #{i + 1}. " +
                    $"Ожидалось X={currentX}, " +
                    $"получено X={element.StartX}."
                );
            }

            if (element.StartRadius <= 0 ||
                element.EndRadius <= 0)
            {
                throw new Exception(
                    $"{name}: элемент #{i + 1} " +
                    $"имеет недопустимый радиус."
                );
            }

            if (element.Type == BoreProfileElementType.Arc &&
                element.Radius <= 0)
            {
                throw new Exception(
                    $"{name}: дуга #{i + 1} " +
                    $"имеет недопустимый Radius."
                );
            }

            currentX = element.EndX;
        }
    }

    // ---------------------------------------------------------
    // Разворачивает группы отверстий в обычный список Holes.
    // ---------------------------------------------------------
    public void ExpandHolePatterns()
    {
        if (HolePatterns == null ||
            HolePatterns.Count == 0)
        {
            return;
        }

        foreach (HolePatternDescription pattern in HolePatterns)
        {
            List<HoleDescription> generated =
                pattern.GenerateHoles();

            Holes.AddRange(generated);
        }

        HolePatterns.Clear();
    }

    public void BuildProfileFromStations()
    {
        if (ProfileStations == null ||
            ProfileStations.Count < 2)
        {
            return;
        }

        List<ProfileStation> stations =
            new(ProfileStations);

        stations.Sort(
            (a, b) => a.Position.CompareTo(b.Position)
        );

        List<ProfileStep> generatedProfile = new();

        for (int i = 0; i < stations.Count - 1; i++)
        {
            ProfileStation current = stations[i];
            ProfileStation next = stations[i + 1];

            if (next.Position <= current.Position)
            {
                throw new Exception(
                    $"Некорректные позиции ProfileStations: " +
                    $"{current.Position} → {next.Position} мм."
                );
            }

            generatedProfile.Add(
                new ProfileStep
                {
                    StartPosition = current.Position,
                    Length = next.Position - current.Position,
                    Diameter = current.Diameter
                }
            );
        }

        Profile = generatedProfile;
    }
}

public class ProfileStep
{
    public double StartPosition { get; set; }

    public double Length { get; set; }

    public double Diameter { get; set; }
}

public class ProfileStation
{
    // Осевое положение, мм
    public double Position { get; set; }

    // Наружный диаметр в этой точке, мм
    public double Diameter { get; set; }
}

public enum RevolveProfileElementType
{
    Line,
    Cone,
    Arc
}

public class RevolveProfileElement
{
    // Тип геометрического элемента
    public RevolveProfileElementType Type { get; set; }

    // Начальная координата по оси X, мм
    public double StartX { get; set; }

    // Конечная координата по оси X, мм
    public double EndX { get; set; }

    // Начальный радиус, мм
    public double StartRadius { get; set; }

    // Конечный радиус, мм
    public double EndRadius { get; set; }

    // ---------------------------------------------------------
    // Параметры дуги
    // ---------------------------------------------------------

    // Радиус дуги, мм. Используется только для Type = Arc.
    public double Radius { get; set; }

    // Координата центра дуги по X, мм
    public double CenterX { get; set; }

    // Радиус центра дуги относительно оси вращения, мм
    public double CenterRadius { get; set; }

    // Направление обхода дуги
    public bool Clockwise { get; set; }
}

public class BoreStep
{
    // Начальная координата участка, мм
    public double StartPosition { get; set; }

    // Длина участка, мм
    public double Length { get; set; }

    // Внутренний диаметр участка, мм
    public double Diameter { get; set; }
}

public enum BoreProfileElementType
{
    Line,
    Cone,
    Arc
}

public class BoreProfileElement
{
    public BoreProfileElementType Type { get; set; }

    public double StartX { get; set; }
    public double EndX { get; set; }

    public double StartRadius { get; set; }
    public double EndRadius { get; set; }

    // Радиус дуги
    public double Radius { get; set; }

    // Для дуги:
    // центр вычисляется автоматически.
    public double CenterX { get; set; }
    public double CenterRadius { get; set; }

    public bool Clockwise { get; set; }

    public string Description { get; set; } = "";
}

public enum HoleType
{
    Simple,
    Counterbore,
    Countersink,
    Threaded
}

public class HoleDescription
{
    public HoleType Type { get; set; } = HoleType.Simple;

    public double Diameter { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Depth { get; set; }

    public bool ThroughAll { get; set; }

    public double HeadDiameter { get; set; }

    public double HeadDepth { get; set; }

    public ThreadDescription? Thread { get; set; }
}

public class HolePatternDescription
{
    // Количество отверстий
    public int Count { get; set; }

    // Диаметр окружности расположения центров, мм
    public double BoltCircleDiameter { get; set; }

    // Начальный угол первого отверстия, градусы
    public double StartAngle { get; set; }

    // Равномерное распределение
    public bool EqualSpacing { get; set; } = true;

    // Описание одного отверстия
    public HoleDescription Hole { get; set; } = new();

    public List<HoleDescription> GenerateHoles()
    {
        if (Count < 2)
            throw new Exception(
                $"Количество отверстий должно быть >= 2: {Count}."
            );

        if (BoltCircleDiameter <= 0)
            throw new Exception(
                $"Некорректный диаметр окружности отверстий: " +
                $"{BoltCircleDiameter} мм."
            );

        if (Hole == null)
            throw new Exception(
                "Группа отверстий не содержит описания отверстия."
            );

        if (!EqualSpacing)
            throw new Exception(
                "Пока поддерживается только равномерное распределение."
            );

        List<HoleDescription> holes = new();

        double radius =
            BoltCircleDiameter / 2.0;

        double angleStep =
            360.0 / Count;

        for (int i = 0; i < Count; i++)
        {
            double angleDeg =
                StartAngle + i * angleStep;

            double angleRad =
                angleDeg * Math.PI / 180.0;

            double x =
                radius * Math.Cos(angleRad);

            double y =
                radius * Math.Sin(angleRad);

            holes.Add(
                new HoleDescription
                {
                    Type = Hole.Type,
                    Diameter = Hole.Diameter,
                    X = x,
                    Y = y,
                    Depth = Hole.Depth,
                    ThroughAll = Hole.ThroughAll,
                    HeadDiameter = Hole.HeadDiameter,
                    HeadDepth = Hole.HeadDepth,
                    Thread = Hole.Thread
                }
            );
        }

        return holes;
    }
}

public enum ThreadType
{
    Internal,
    External
}

public class ThreadDescription
{
    public ThreadType Type { get; set; } = ThreadType.Internal;

    public string Designation { get; set; } = "";

    // Поле допуска, например 6g или 7H. Не входит в размер
    // профиля SolidWorks, но сохраняет требование чертежа.
    public string ToleranceClass { get; set; } = "";

    public double NominalDiameter { get; set; }

    public double Pitch { get; set; }

    public double StartPosition { get; set; }

    public double Length { get; set; }

    public bool ThroughAll { get; set; }

    public int Starts { get; set; } = 1;
}

public class ChamferDescription
{
    public double Size { get; set; }

    public double Position { get; set; }
}

public class FilletDescription
{
    public double Radius { get; set; }

    public double Position { get; set; }
}

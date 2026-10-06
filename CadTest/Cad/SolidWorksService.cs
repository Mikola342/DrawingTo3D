using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using CadTest.Models;

namespace CadTest.Cad
{
    public partial class SolidWorksService
    {
        private SldWorks? _swApp;
        private ModelDoc2? _model;

        // Prevent UI snapping/inferred relations from changing supplied coordinates.
        private sealed class ExactSketchCoordinates : IDisposable
        {
            private readonly SketchManager _manager;
            private readonly bool _previous;
            public ExactSketchCoordinates(SketchManager manager)
            {
                _manager = manager;
                _previous = manager.AddToDB;
                manager.AddToDB = true;
            }
            public void Dispose() => _manager.AddToDB = _previous;
        }

        public void Connect()
        {
            Type? swType = Type.GetTypeFromProgID("SldWorks.Application");

            if (swType == null)
                throw new Exception("SolidWorks COM объект не найден.");

            object? swObject = Activator.CreateInstance(swType);

            if (swObject == null)
                throw new Exception("Не удалось создать SolidWorks COM объект.");

            _swApp = (SldWorks)swObject;

            _swApp.Visible = true;

            Console.WriteLine("[OK] Подключение к SolidWorks выполнено.");
            Console.WriteLine($"[OK] Revision: {_swApp.RevisionNumber()}");
        }

        public void CreateTestPart()
        {
            if (_swApp == null)
                throw new Exception("Сначала необходимо вызвать Connect().");

            Console.WriteLine("[INFO] Создание новой детали...");

            _model = (ModelDoc2)_swApp.NewPart();

            if (_model == null)
                throw new Exception("Не удалось создать новую деталь.");

            Console.WriteLine("[OK] Новая деталь создана.");
        }

        public void CreateTestSketch(double widthMm, double heightMm)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            Console.WriteLine(
                $"[INFO] Создание Sketch: {widthMm} x {heightMm} мм..."
            );

            Feature? frontPlane = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.Name == "Спереди" &&
                    feature.GetTypeName2() == "RefPlane")
                {
                    frontPlane = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (frontPlane == null)
                throw new Exception("Не удалось найти плоскость «Спереди».");

            bool selected = frontPlane.Select2(false, 0);

            if (!selected)
                throw new Exception("Не удалось выбрать плоскость «Спереди».");

            SketchManager sketchManager = _model.SketchManager;

            sketchManager.InsertSketch(true);

            // SolidWorks API использует метры.
            double width = widthMm / 1000.0;
            double height = heightMm / 1000.0;

            double halfWidth = width / 2.0;
            double halfHeight = height / 2.0;

            sketchManager.CreateCornerRectangle(
                -halfWidth,
                -halfHeight,
                0,
                halfWidth,
                halfHeight,
                0
            );

            sketchManager.InsertSketch(true);

            Console.WriteLine("[OK] Sketch создан.");
        }

        public void CreateTestRevolveSketch(
            double totalLength,
            double leftLength,
            double rightLength,
            double smallDiameter,
            double largeDiameter)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            Console.WriteLine("[INFO] Создание профиля для Revolve...");

            Feature? frontPlane = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.Name == "Спереди" &&
                    feature.GetTypeName2() == "RefPlane")
                {
                    frontPlane = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (frontPlane == null)
                throw new Exception("Не удалось найти плоскость «Спереди».");

            if (!frontPlane.Select2(false, 0))
                throw new Exception("Не удалось выбрать плоскость «Спереди».");

            SketchManager sketchManager = _model.SketchManager;

            sketchManager.InsertSketch(true);

            // SolidWorks API использует метры.
            double x0 = 0.0 / 1000.0;

            double x1 = leftLength / 1000.0;

            double x3 = totalLength / 1000.0;

            double x2 = (totalLength - rightLength) / 1000.0;

            double r1 = (smallDiameter / 2.0) / 1000.0;

            double r2 = (largeDiameter / 2.0) / 1000.0;

            // Нижняя линия профиля — ось вращения.
            sketchManager.CreateLine(
                x0, 0, 0,
                x3, 0, 0
            );

            // Левая торцевая линия.
            sketchManager.CreateLine(
                x0, 0, 0,
                x0, r1, 0
            );

            // Первый участок Ø30.
            sketchManager.CreateLine(
                x0, r1, 0,
                x1, r1, 0
            );

            // Переход на Ø50.
            sketchManager.CreateLine(
                x1, r1, 0,
                x1, r2, 0
            );

            // Центральный участок Ø50.
            sketchManager.CreateLine(
                x1, r2, 0,
                x2, r2, 0
            );

            // Переход обратно на Ø30.
            sketchManager.CreateLine(
                x2, r2, 0,
                x2, r1, 0
            );

            // Правый участок Ø30.
            sketchManager.CreateLine(
                x2, r1, 0,
                x3, r1, 0
            );

            // Правая торцевая линия.
            sketchManager.CreateLine(
                x3, r1, 0,
                x3, 0, 0
            );

            // Закрываем Sketch.
            sketchManager.InsertSketch(true);

            Console.WriteLine("[OK] Профиль для Revolve создан.");
        }

        public void CreateRevolveSketch(PartDescription part)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (part.Profile == null || part.Profile.Count == 0)
                throw new Exception("Описание детали не содержит профиля.");

            Console.WriteLine("[INFO] Создание профиля из PartDescription...");

            Feature? frontPlane = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.Name == "Спереди" &&
                    feature.GetTypeName2() == "RefPlane")
                {
                    frontPlane = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (frontPlane == null)
                throw new Exception("Не удалось найти плоскость «Спереди».");

            if (!frontPlane.Select2(false, 0))
                throw new Exception("Не удалось выбрать плоскость «Спереди».");

            SketchManager sketchManager = _model.SketchManager;

            sketchManager.InsertSketch(true);

            // Все координаты SolidWorks API — метры.
            // Осевая линия.
            sketchManager.CreateLine(
                0,
                0,
                0,
                part.TotalLength / 1000.0,
                0,
                0
            );

            double previousRadius = 0.0;

            foreach (ProfileStep step in part.Profile)
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

                double startX = step.StartPosition / 1000.0;
                double endX =
                    (step.StartPosition + step.Length) / 1000.0;

                double radius =
                    step.Diameter / 2.0 / 1000.0;

                // Вертикальная стенка в начале участка.
                sketchManager.CreateLine(
                    startX,
                    previousRadius,
                    0,
                    startX,
                    radius,
                    0
                );

                // Горизонтальный участок профиля.
                sketchManager.CreateLine(
                    startX,
                    radius,
                    0,
                    endX,
                    radius,
                    0
                );

                previousRadius = radius;
            }
        }

        public void CreateRevolveSketchFromElements(PartDescription part)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (part.RevolveProfile == null || part.RevolveProfile.Count == 0)
                throw new Exception("RevolveProfile пуст.");

            Console.WriteLine("[INFO] Создание Sketch из RevolveProfile...");

            // ------------------------------------------------------------
            // 1. Находим плоскость «Спереди»
            // ------------------------------------------------------------
            Feature? frontPlane = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.Name == "Спереди" &&
                    feature.GetTypeName2() == "RefPlane")
                {
                    frontPlane = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (frontPlane == null)
                throw new Exception("Не удалось найти плоскость «Спереди».");

            _model.ClearSelection2(true);

            if (!frontPlane.Select2(false, 0))
                throw new Exception("Не удалось выбрать плоскость «Спереди».");

            SketchManager sketchManager = _model.SketchManager;

            sketchManager.InsertSketch(true);

            // SolidWorks API использует метры.
            using var exactCoordinates = new ExactSketchCoordinates(sketchManager);
            double totalLength = part.TotalLength / 1000.0;

            // ------------------------------------------------------------
            // 2. Осевая линия
            //    Она создаётся ПЕРВОЙ, поэтому это Line1.
            // ------------------------------------------------------------
            sketchManager.CreateLine(
                0,
                0,
                0,
                totalLength,
                0,
                0
            );

            // ------------------------------------------------------------
            // 3. Проверяем начало профиля
            // ------------------------------------------------------------
            RevolveProfileElement first = part.RevolveProfile[0];

            if (Math.Abs(first.StartX) > 0.0001)
            {
                throw new Exception(
                    $"Профиль должен начинаться с X=0 мм. Сейчас X={first.StartX} мм."
                );
            }

            // Левая стенка профиля.
            sketchManager.CreateLine(
                first.StartX / 1000.0,
                0,
                0,
                first.StartX / 1000.0,
                first.StartRadius / 1000.0,
                0
            );

            double currentX = first.StartX;
            double currentRadius = first.StartRadius;

            // ------------------------------------------------------------
            // 4. Строим все элементы профиля
            // ------------------------------------------------------------
            foreach (RevolveProfileElement element in part.RevolveProfile)
            {
                double startX = element.StartX;
                double endX = element.EndX;

                double startRadius = element.StartRadius;
                double endRadius = element.EndRadius;

                Console.WriteLine(
                    $"[INFO] Profile: {element.Type}, " +
                    $"X={startX}..{endX}, " +
                    $"R={startRadius}..{endRadius}"
                );

                // Проверяем непрерывность по X.
                if (Math.Abs(startX - currentX) > 0.0001)
                {
                    throw new Exception(
                        $"Разрыв профиля по X: ожидалось X={currentX} мм, " +
                        $"получено X={startX} мм."
                    );
                }

                // --------------------------------------------------------
                // Если радиус изменился между элементами,
                // создаём вертикальный переход.
                // Например:
                // X=10: R20 -> R127.5
                // --------------------------------------------------------
                if (Math.Abs(startRadius - currentRadius) > 0.0001)
                {
                    sketchManager.CreateLine(
                        startX / 1000.0,
                        currentRadius / 1000.0,
                        0,
                        startX / 1000.0,
                        startRadius / 1000.0,
                        0
                    );
                }

                // --------------------------------------------------------
                // Сам элемент
                // --------------------------------------------------------
                switch (element.Type)
                {
                    case RevolveProfileElementType.Line:
                    case RevolveProfileElementType.Cone:

                        sketchManager.CreateLine(
                            startX / 1000.0,
                            startRadius / 1000.0,
                            0,
                            endX / 1000.0,
                            endRadius / 1000.0,
                            0
                        );

                        break;

                    case RevolveProfileElementType.Arc:
                        {
                            // CreateArc proved unreliable for the small bore
                            // transitions. Build the same three-point arc here
                            // so the external R35/R50 transitions can be
                            // described parametrically when their endpoints
                            // are confirmed from the drawing.
                            double startAngle = Math.Atan2(
                                element.StartRadius - element.CenterRadius,
                                element.StartX - element.CenterX
                            );

                            double endAngle = Math.Atan2(
                                element.EndRadius - element.CenterRadius,
                                element.EndX - element.CenterX
                            );

                            double delta = endAngle - startAngle;

                            if (element.Clockwise)
                            {
                                while (delta >= 0)
                                    delta -= 2.0 * Math.PI;
                            }
                            else
                            {
                                while (delta <= 0)
                                    delta += 2.0 * Math.PI;
                            }

                            double middleAngle = startAngle + delta / 2.0;

                            double middleX = element.CenterX +
                                element.Radius * Math.Cos(middleAngle);

                            double middleRadius = element.CenterRadius +
                                element.Radius * Math.Sin(middleAngle);

                            SketchSegment? arc = sketchManager.Create3PointArc(
                                startX / 1000.0,
                                startRadius / 1000.0,
                                0,

                                endX / 1000.0,
                                endRadius / 1000.0,
                                0,

                                middleX / 1000.0,
                                middleRadius / 1000.0,
                                0
                            );

                            if (arc == null)
                            {
                                throw new Exception(
                                    "Не удалось создать дугу наружного профиля."
                                );
                            }

                            break;
                        }

                    default:

                        throw new Exception(
                            $"Неизвестный тип элемента профиля: {element.Type}"
                        );
                }

                currentX = endX;
                currentRadius = endRadius;
            }

            // ------------------------------------------------------------
            // 5. Последний элемент должен доходить до TotalLength
            // ------------------------------------------------------------
            if (Math.Abs(currentX - part.TotalLength) > 0.0001)
            {
                throw new Exception(
                    $"Профиль не доходит до общей длины детали: " +
                    $"последний X={currentX} мм, TotalLength={part.TotalLength} мм."
                );
            }

            // Правая стенка.
            sketchManager.CreateLine(
                currentX / 1000.0,
                currentRadius / 1000.0,
                0,
                currentX / 1000.0,
                0,
                0
            );

            // ------------------------------------------------------------
            // 6. Закрываем Sketch
            // ------------------------------------------------------------
            sketchManager.InsertSketch(true);

            Console.WriteLine(
                "[OK] Замкнутый Sketch из RevolveProfile создан."
            );
        }

        public void CreateTestRevolve()
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            Console.WriteLine("[INFO] Создание Revolve 360°...");

            // Очищаем текущий выбор.
            _model.ClearSelection2(true);

            // Находим эскиз профиля.
            Feature? sketchFeature = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.GetTypeName2() == "ProfileFeature")
                {
                    sketchFeature = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (sketchFeature == null)
                throw new Exception("Не удалось найти эскиз профиля.");

            Console.WriteLine($"[INFO] Найден эскиз: {sketchFeature.Name}");

            // Выбираем эскиз как профиль Revolve.
            if (!sketchFeature.Select2(false, 0))
                throw new Exception("Не удалось выбрать эскиз профиля.");

            Console.WriteLine("[OK] Профиль выбран.");

            // Первая созданная нами линия — это осевая линия.
            // Она называется Line1 внутри эскиза.
            string axisName = $"Line1@{sketchFeature.Name}";

            bool axisSelected = _model.Extension.SelectByID2(
                axisName,
                "EXTSKETCHSEGMENT",
                0,
                0,
                0,
                true,
                4,
                null,
                0
            );

            if (!axisSelected)
                throw new Exception(
                    $"Не удалось выбрать осевую линию Revolve: {axisName}"
                );

            Console.WriteLine($"[OK] Осевая линия выбрана: {axisName}");

            // 360 градусов.
            double angle = 2.0 * Math.PI;

            Feature? revolve = (Feature?)_model.FeatureManager.FeatureRevolve(
                angle,
                false,
                0,
                (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees,
                0,
                false,
                false,
                true
            );

            if (revolve == null)
                throw new Exception("Не удалось создать Revolve.");

            Console.WriteLine("[OK] Revolve 360° создан.");

            _model.ForceRebuild3(false);
        }

        public void CreateRevolveFromDescription(PartDescription part)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            if (part == null)
                throw new ArgumentNullException(nameof(part));

            Console.WriteLine("[INFO] Построение Revolve из PartDescription...");

            // Эскиз уже создан из PartDescription.
            // Выбираем первый профильный эскиз.
            Feature? sketchFeature = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.GetTypeName2() == "ProfileFeature")
                {
                    sketchFeature = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (sketchFeature == null)
                throw new Exception("Не удалось найти эскиз профиля.");

            _model.ClearSelection2(true);

            if (!sketchFeature.Select2(false, 0))
                throw new Exception("Не удалось выбрать профиль.");

            string axisName = $"Line1@{sketchFeature.Name}";

            bool axisSelected = _model.Extension.SelectByID2(
                axisName,
                "EXTSKETCHSEGMENT",
                0,
                0,
                0,
                true,
                4,
                null,
                0
            );

            if (!axisSelected)
                throw new Exception(
                    $"Не удалось выбрать ось вращения: {axisName}"
                );

            double angle = 2.0 * Math.PI;

            Feature? revolve = (Feature?)_model.FeatureManager.FeatureRevolve(
                angle,
                false,
                0,
                (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees,
                0,
                false,
                false,
                true
            );

            if (revolve == null)
                throw new Exception("Не удалось создать Revolve.");

            Console.WriteLine("[OK] Revolve из PartDescription создан.");

            _model.ForceRebuild3(false);
        }

        public void CreateTestExtrude()
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            Console.WriteLine("[INFO] Создание Extrude 20 мм...");

            // SolidWorks API использует метры.
            double depth = 20.0 / 1000.0;

            Feature? extrude = (Feature?)_model.FeatureManager.FeatureExtrusion3(
                true,
                false,
                false,
                (int)swEndConditions_e.swEndCondBlind,
                (int)swEndConditions_e.swEndCondBlind,
                depth,
                0,
                false,
                false,
                false,
                false,
                0,
                0,
                false,
                false,
                false,
                false,
                true,
                true,
                true,
                (int)swStartConditions_e.swStartSketchPlane,
                0,
                false
            );

            if (extrude == null)
                throw new Exception("Не удалось создать Extrude.");

            Console.WriteLine("[OK] Extrude 20 мм создан.");
        }

        public void CreateHolesFromDescription(PartDescription part)
        {
            if (part.Holes == null || part.Holes.Count == 0)
            {
                Console.WriteLine("[INFO] Отверстий для построения нет.");
                return;
            }

            if (_model == null)
                throw new Exception("Модель SolidWorks не создана.");

            Console.WriteLine(
                $"[INFO] Построение отверстий: {part.Holes.Count}"
            );

            // ---------------------------------------------------------
            // Находим базовую плоскость "Справа".
            // Она находится на X = 0.
            // ---------------------------------------------------------

            Feature? rightPlane = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.Name == "Справа" &&
                    feature.GetTypeName2() == "RefPlane")
                {
                    rightPlane = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (rightPlane == null)
                throw new Exception(
                    "Не удалось найти плоскость «Справа»."
                );

            Console.WriteLine(
                "[OK] Найдена базовая плоскость «Справа»."
            );

            // ---------------------------------------------------------
            // Вспомогательная функция создания Sketch с окружностью.
            // ---------------------------------------------------------

            Feature CreateHoleSketch(
                double xMm,
                double yMm,
                double diameterMm)
            {
                _model.ClearSelection2(true);

                if (!rightPlane!.Select2(false, 0))
                    throw new Exception(
                        "Не удалось выбрать плоскость «Справа»."
                    );

                SketchManager sketchManager = _model.SketchManager;

                sketchManager.InsertSketch(true);

                _model.ClearSelection2(true);

                double x = xMm / 1000.0;
                using var exactCoordinates = new ExactSketchCoordinates(sketchManager);
                double y = yMm / 1000.0;
                double radius = diameterMm / 2.0 / 1000.0;

                SketchSegment? circle =
                    sketchManager.CreateCircle(
                        x,
                        y,
                        0,
                        x + radius,
                        y,
                        0
                    );

                if (circle == null)
                    throw new Exception(
                        $"Не удалось создать окружность Ø{diameterMm} мм."
                    );

                Console.WriteLine(
                    $"[OK] Окружность Ø{diameterMm} мм создана."
                );

                sketchManager.InsertSketch(true);

                _model.ClearSelection2(true);

                // Ищем последний Sketch.
                Feature? result = null;

                Feature? current =
                    (Feature?)_model.FirstFeature();

                while (current != null)
                {
                    if (current.GetTypeName2() == "ProfileFeature")
                    {
                        result = current;
                    }

                    current =
                        (Feature?)current.GetNextFeature();
                }

                if (result == null)
                    throw new Exception(
                        "Не удалось найти созданный Sketch."
                    );

                Console.WriteLine(
                    $"[OK] Найден Sketch: {result.Name}"
                );

                return result;
            }

            // ---------------------------------------------------------
            // Вспомогательная функция Cut-Extrude.
            // ---------------------------------------------------------

            Feature CreateBlindCut(
                Feature sketch,
                double depthMm)
            {
                _model!.ClearSelection2(true);

                if (!sketch.Select2(false, 0))
                    throw new Exception(
                        "Не удалось выбрать Sketch для Cut-Extrude."
                    );

                double depth =
                    depthMm / 1000.0;

                Feature? cut =
                    (Feature?)_model.FeatureManager.FeatureCut3(
                        true,
                        false,
                        true,
                        (int)swEndConditions_e.swEndCondBlind,
                        (int)swEndConditions_e.swEndCondBlind,
                        depth,
                        0,
                        false,
                        false,
                        false,
                        false,
                        0,
                        0,
                        false,
                        false,
                        false,
                        false,
                        false,
                        true,
                        true,
                        false,
                        false,
                        false,
                        (int)swStartConditions_e.swStartSketchPlane,
                        0,
                        false
                    );

                if (cut == null)
                    throw new Exception(
                        $"Не удалось создать Blind Cut глубиной {depthMm} мм."
                    );

                _model.ForceRebuild3(false);

                return cut;
            }

            // ---------------------------------------------------------
            // Вспомогательная функция Through All.
            // ---------------------------------------------------------

            Feature CreateThroughAllCut(
                Feature sketch)
            {
                _model!.ClearSelection2(true);

                if (!sketch.Select2(false, 0))
                    throw new Exception(
                        "Не удалось выбрать Sketch для Through All."
                    );

                Feature? cut =
                    (Feature?)_model.FeatureManager.FeatureCut3(
                        true,
                        false,
                        true,
                        (int)swEndConditions_e.swEndCondThroughAll,
                        (int)swEndConditions_e.swEndCondBlind,
                        0,
                        0,
                        false,
                        false,
                        false,
                        false,
                        0,
                        0,
                        false,
                        false,
                        false,
                        false,
                        false,
                        true,
                        true,
                        false,
                        false,
                        false,
                        (int)swStartConditions_e.swStartSketchPlane,
                        0,
                        false
                    );

                if (cut == null)
                    throw new Exception(
                        "Не удалось создать Through All Cut."
                    );

                _model.ForceRebuild3(false);

                return cut;
            }

            // ---------------------------------------------------------
            // Строим отверстия.
            // ---------------------------------------------------------

            foreach (HoleDescription hole in part.Holes)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"[INFO] Отверстие: Type={hole.Type}, " +
                    $"Ø{hole.Diameter} мм, " +
                    $"X={hole.X} мм, " +
                    $"Y={hole.Y} мм, " +
                    $"Depth={hole.Depth} мм"
                );

                // =====================================================
                // SIMPLE / THREADED
                // =====================================================

                if (hole.Type == HoleType.Simple)
{
    Feature sketch =
        CreateHoleSketch(
            hole.X,
            hole.Y,
            hole.Diameter
        );

    if (hole.ThroughAll)
    {
        CreateThroughAllCut(sketch);

        Console.WriteLine(
            "[OK] Отверстие Through All создано."
        );
    }
    else
    {
        CreateBlindCut(
            sketch,
            hole.Depth
        );

        Console.WriteLine(
            $"[OK] Отверстие глубиной " +
            $"{hole.Depth} мм создано."
        );
    }

    continue;
}

                if (hole.Type == HoleType.Threaded)
                {
                    Feature sketch =
                        CreateHoleSketch(
                            hole.X,
                            hole.Y,
                            hole.Diameter
                        );

                    if (hole.ThroughAll)
                    {
                        CreateThroughAllCut(sketch);

                        Console.WriteLine(
                            "[OK] Базовое отверстие Through All создано."
                        );
                    }
                    else
                    {
                        CreateBlindCut(
                            sketch,
                            hole.Depth
                        );

                        Console.WriteLine(
                            $"[OK] Базовое отверстие Ø{hole.Diameter} × " +
                            $"{hole.Depth} мм создано."
                        );
                    }

                    // Создаём настоящую резьбу для этого конкретного отверстия.
                    CreateThreadFromDescription(hole);

                    Console.WriteLine(
                        $"[OK] Резьба {hole.Thread?.Designation} " +
                        "добавлена к отверстию."
                    );

                    continue;
                }

                // =====================================================
                // COUNTERBORE
                // =====================================================

                if (hole.Type == HoleType.Counterbore)
                {
                    Console.WriteLine(
                        "[INFO] Тип отверстия: Counterbore"
                    );

                    // -------------------------------------------------
                    // 1. Основное отверстие
                    // -------------------------------------------------

                    Feature mainSketch =
                        CreateHoleSketch(
                            hole.X,
                            hole.Y,
                            hole.Diameter
                        );

                    if (hole.ThroughAll)
                    {
                        CreateThroughAllCut(mainSketch);

                        Console.WriteLine(
                            "[OK] Основное отверстие Through All создано."
                        );
                    }
                    else
                    {
                        CreateBlindCut(
                            mainSketch,
                            hole.Depth
                        );

                        Console.WriteLine(
                            $"[OK] Основное отверстие Ø{hole.Diameter} × {hole.Depth} мм создано."
                        );
                    }

                    // -------------------------------------------------
                    // 2. Контрборт
                    // -------------------------------------------------

                    Console.WriteLine(
                        $"[INFO] Создание контрборта Ø{hole.HeadDiameter} × {hole.HeadDepth} мм..."
                    );

                    Feature headSketch =
                        CreateHoleSketch(
                            hole.X,
                            hole.Y,
                            hole.HeadDiameter
                        );

                    CreateBlindCut(
                        headSketch,
                        hole.HeadDepth
                    );

                    Console.WriteLine(
                        "[OK] Контрборт создан."
                    );

                    continue;
                }

                // =====================================================
                // COUNTERSINK
                // =====================================================

                if (hole.Type == HoleType.Countersink)
                {
                    Console.WriteLine(
                        "[INFO] Тип отверстия: Countersink"
                    );

                    // -----------------------------------------------------
                    // 1. Сначала создаём основное отверстие Ø10
                    // -----------------------------------------------------

                    Feature mainSketch =
                        CreateHoleSketch(
                            hole.X,
                            hole.Y,
                            hole.Diameter
                        );

                    if (hole.ThroughAll)
                    {
                        CreateThroughAllCut(mainSketch);

                        Console.WriteLine(
                            "[OK] Основное отверстие Through All создано."
                        );
                    }
                    else
                    {
                        CreateBlindCut(
                            mainSketch,
                            hole.Depth
                        );

                        Console.WriteLine(
                            $"[OK] Основное отверстие Ø{hole.Diameter} × {hole.Depth} мм создано."
                        );
                    }

                    // -----------------------------------------------------
                    // 2. Создаём Sketch большого диаметра
                    // -----------------------------------------------------

                    Console.WriteLine(
                        $"[INFO] Создание зенковки Ø{hole.HeadDiameter} мм..."
                    );

                    Feature headSketch =
                        CreateHoleSketch(
                            hole.X,
                            hole.Y,
                            hole.HeadDiameter
                        );

                    // -----------------------------------------------------
                    // 3. Рассчитываем угол конуса
                    //
                    // Радиальное уменьшение:
                    // (HeadDiameter - Diameter) / 2
                    //
                    // Осевое расстояние:
                    // HeadDepth
                    // -----------------------------------------------------

                    double radialDifference =
                        (hole.HeadDiameter - hole.Diameter) / 2.0;

                    double draftAngle =
                        Math.Atan(
                            radialDifference / hole.HeadDepth
                        );

                    Console.WriteLine(
                        $"[INFO] Угол конуса: " +
                        $"{draftAngle * 180.0 / Math.PI:F3}°"
                    );

                    // -----------------------------------------------------
                    // 4. Выбираем Sketch
                    // -----------------------------------------------------

                    _model.ClearSelection2(true);

                    if (!headSketch.Select2(false, 0))
                        throw new Exception(
                            "Не удалось выбрать Sketch зенковки."
                        );

                    // -----------------------------------------------------
                    // 5. Создаём конический Cut-Extrude
                    // -----------------------------------------------------

                    double headDepth =
                        hole.HeadDepth / 1000.0;

                    Feature? countersinkCut =
                        (Feature?)_model.FeatureManager.FeatureCut3(
                            true,
                            false,
                            true,

                            (int)swEndConditions_e.swEndCondBlind,
                            (int)swEndConditions_e.swEndCondBlind,

                            headDepth,
                            0,

                            // Разрешаем Draft в направлении 1.
                            true,

                            // Draft во втором направлении не используется.
                            false,

                            // Направление Draft 1.
                            false,

                            // Направление Draft 2.
                            false,

                            draftAngle,
                            0,

                            false,
                            false,

                            false,
                            false,
                            false,

                            true,
                            true,

                            false,
                            false,
                            false,

                            (int)swStartConditions_e.swStartSketchPlane,

                            0,
                            false
                        );

                    if (countersinkCut == null)
                        throw new Exception(
                            "Не удалось создать коническую зенковку."
                        );

                    Console.WriteLine(
                        $"[OK] Зенковка Ø{hole.HeadDiameter} × " +
                        $"{hole.HeadDepth} мм создана."
                    );

                    _model.ForceRebuild3(false);

                    continue;
                }

                throw new Exception(
                    $"Неподдерживаемый тип отверстия: {hole.Type}"
                );
            }

            _model.ForceRebuild3(false);
        }

        public void TestThreadEdgeSelection()
        {
            if (_model == null)
                throw new Exception("Модель SolidWorks не создана.");

            Console.WriteLine(
                "[INFO] Проверка выбора стартовой кромки резьбы..."
            );

            _model.ClearSelection2(true);

            // Наше тестовое отверстие:
            // диаметр 10 мм, центр X=0, Y=0.
            // Круговая кромка находится на входе отверстия.
            double radius = 0.00425;

            bool selected = _model.Extension.SelectByRay(
                -0.010,
                radius,
                0,
                1,
                0,
                0,
                0.001,
                1,
                false,
                1,
                0
            );

            if (!selected)
            {
                throw new Exception(
                    "Не удалось выбрать стартовую кромку отверстия."
                );
            }

            ISelectionMgr selectionManager =
                _model.ISelectionManager;

            object selectedObject =
                selectionManager.GetSelectedObject6(1, -1);

            if (selectedObject is not Edge edge)
            {
                throw new Exception(
                    "Выбранный объект не является Edge."
                );
            }

            Console.WriteLine(
                "[OK] Стартовая кромка отверстия выбрана."
            );

            Console.WriteLine(
                $"[INFO] Объект: {edge.GetType().Name}"
            );

            _model.ClearSelection2(true);

            Console.WriteLine(
                "[OK] Проверка выбора кромки завершена."
            );
        }

        public void CreateThreadFromDescription(HoleDescription threadedHole)
        {
            if (_model == null)
                throw new Exception(
                    "Модель SolidWorks не создана."
                );

            if (threadedHole == null)
                throw new ArgumentNullException(
                    nameof(threadedHole)
                );

            if (threadedHole.Type != HoleType.Threaded)
                throw new Exception(
                    "Переданное отверстие не является резьбовым."
                );

            if (threadedHole.Thread == null)
                throw new Exception(
                    "У резьбового отверстия отсутствует ThreadDescription."
                );

            ThreadDescription thread =
                threadedHole.Thread;

            if (thread.Type != ThreadType.Internal)
            {
                throw new Exception(
                    "CreateThreadFromDescription поддерживает только " +
                    "внутреннюю резьбу. Для наружной резьбы используйте " +
                    "отдельный метод выбора наружной кромки."
                );
            }

            Console.WriteLine();
            Console.WriteLine(
                "[INFO] Создание настоящей Thread Feature..."
            );

            Console.WriteLine(
                $"[INFO] Резьба: {thread.Designation}"
            );

            Console.WriteLine(
                $"[INFO] Диаметр отверстия: " +
                $"{threadedHole.Diameter} мм"
            );

            Console.WriteLine(
                $"[INFO] Длина резьбы: " +
                $"{thread.Length} мм"
            );

            // ---------------------------------------------------------
            // 1. Создаём ThreadFeatureData
            // ---------------------------------------------------------

            object? definitionObject =
                _model.FeatureManager.CreateDefinition(
                    (int)swFeatureNameID_e.swFmSweepThread
                );

            if (definitionObject == null)
                throw new Exception(
                    "Не удалось создать ThreadFeatureData."
                );

            IThreadFeatureData threadData =
                (IThreadFeatureData)definitionObject;

            threadData.InitializeThreadData();

            Console.WriteLine(
                "[OK] ThreadFeatureData создан."
            );

            // ---------------------------------------------------------
            // 2. Выбираем стартовую круговую кромку отверстия
            // ---------------------------------------------------------

            _model.ClearSelection2(true);

            double radius =
                threadedHole.Diameter / 2.0 / 1000.0;

            bool edgeSelected =
                _model.Extension.SelectByRay(
                    -0.010,
                    radius,
                    0,
                    1,
                    0,
                    0,
                    0.001,
                    1,
                    false,
                    1,
                    0
                );

            if (!edgeSelected)
                throw new Exception(
                    "Не удалось выбрать стартовую кромку резьбы."
                );

            ISelectionMgr selectionManager =
                _model.ISelectionManager;

            object selectedObject =
                selectionManager.GetSelectedObject6(1, -1);

            if (selectedObject is not Edge startEdge)
                throw new Exception(
                    "Выбранный объект не является Edge."
                );

            Console.WriteLine(
                "[OK] Стартовая кромка резьбы выбрана."
            );

            // ---------------------------------------------------------
            // 3. Передаём кромку Thread Feature
            // ---------------------------------------------------------

            threadData.Edge = startEdge;

            _model.ClearSelection2(true);

            // ---------------------------------------------------------
            // 4. Профиль и размер резьбы
            // ---------------------------------------------------------

            threadData.Type =
                "Metric Tap.SLDLFP";

            threadData.Size =
                thread.Designation;

            Console.WriteLine(
                $"[INFO] Thread profile: {threadData.Type}"
            );

            Console.WriteLine(
                $"[INFO] Thread size: {threadData.Size}"
            );

            // ---------------------------------------------------------
            // 5. Длина резьбы
            // ---------------------------------------------------------

            threadData.EndCondition =
                (int)swThreadEndCondition_e.swThreadEndCondition_Blind;

            threadData.BlindDepth =
                thread.Length / 1000.0;

            Console.WriteLine(
                $"[INFO] Thread depth: {thread.Length} мм"
            );

            // ---------------------------------------------------------
            // 6. Создаём настоящую Thread Feature
            // ---------------------------------------------------------

            Feature? threadFeature =
                (Feature?)_model.FeatureManager.CreateFeature(
                    threadData
                );

            if (threadFeature == null)
                throw new Exception(
                    "SolidWorks не смог создать Thread Feature."
                );

            Console.WriteLine(
                $"[OK] Настоящая Thread Feature создана: " +
                $"{threadFeature.Name}"
            );

            _model.ForceRebuild3(false);
        }

        public void CreateLeftExternalThread(ThreadDescription thread)
        {
            if (_model == null)
                throw new Exception("Модель SolidWorks не создана.");

            if (thread == null)
                throw new ArgumentNullException(nameof(thread));

            if (thread.Type != ThreadType.External)
            {
                throw new Exception(
                    "CreateLeftExternalThread поддерживает только наружную резьбу."
                );
            }

            Console.WriteLine();
            Console.WriteLine(
                $"[INFO] Создание наружной резьбы {thread.Designation}" +
                $"-{thread.ToleranceClass}..."
            );

            object? definitionObject = _model.FeatureManager.CreateDefinition(
                (int)swFeatureNameID_e.swFmSweepThread
            );

            if (definitionObject is not IThreadFeatureData threadData)
            {
                throw new Exception(
                    "Не удалось создать ThreadFeatureData для наружной резьбы."
                );
            }

            threadData.InitializeThreadData();
            _model.ClearSelection2(true);

            // M90×2 расположен на левом торце: луч идёт к торцу снаружи
            // детали вдоль оси X и выбирает круговую кромку Ø90.
            double outerRadius = thread.NominalDiameter / 2.0 / 1000.0;
            bool edgeSelected = _model.Extension.SelectByRay(
                -0.010,
                outerRadius,
                0,
                1,
                0,
                0,
                0.001,
                1,
                false,
                1,
                0
            );

            if (!edgeSelected ||
                _model.ISelectionManager.GetSelectedObject6(1, -1) is not Edge startEdge)
            {
                throw new Exception(
                    "Не удалось выбрать наружную стартовую кромку резьбы."
                );
            }

            threadData.Edge = startEdge;
            _model.ClearSelection2(true);

            threadData.Type = "Metric Die.SLDLFP";
            threadData.Size = thread.Designation;
            threadData.EndCondition =
                (int)swThreadEndCondition_e.swThreadEndCondition_Blind;
            threadData.BlindDepth = thread.Length / 1000.0;

            Feature? threadFeature =
                (Feature?)_model.FeatureManager.CreateFeature(threadData);

            if (threadFeature == null)
            {
                throw new Exception(
                    $"SolidWorks не смог создать наружную резьбу {thread.Designation}."
                );
            }

            _model.ForceRebuild3(false);

            Console.WriteLine(
                $"[OK] Наружная резьба создана: {threadFeature.Name}"
            );
        }

        private SketchSegment CreateSketchArc(
    SketchManager sketchManager,
    double centerX,
    double centerR,
    double startX,
    double startR,
    double endX,
    double endR,
    bool clockwise)
        {
            short direction =
                clockwise
                    ? (short)-1
                    : (short)1;

            SketchSegment? arc =
                sketchManager.CreateArc(
                    centerX / 1000.0,
                    centerR / 1000.0,
                    0,

                    startX / 1000.0,
                    startR / 1000.0,
                    0,

                    endX / 1000.0,
                    endR / 1000.0,
                    0,

                    direction
                );

            if (arc == null)
            {
                throw new Exception(
                    $"Не удалось создать дугу " +
                    $"({startX},{startR}) → ({endX},{endR})."
                );
            }

            return arc;
        }

        public void CreateBoreRevolveProfile(PartDescription part)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (part.BoreRevolveProfile == null ||
                part.BoreRevolveProfile.Count == 0)
            {
                Console.WriteLine(
                    "[INFO] Полноценный BoreRevolveProfile пока пуст."
                );

                return;
            }

            Console.WriteLine(
                $"[INFO] Построение сложной внутренней расточки: " +
                $"{part.BoreRevolveProfile.Count} элементов."
            );

            // ------------------------------------------------------------
            // Находим плоскость «Спереди»
            // ------------------------------------------------------------

            Feature? frontPlane = null;

            Feature? feature =
                (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.Name == "Спереди" &&
                    feature.GetTypeName2() == "RefPlane")
                {
                    frontPlane = feature;
                    break;
                }

                feature =
                    (Feature?)feature.GetNextFeature();
            }

            if (frontPlane == null)
                throw new Exception(
                    "Не удалось найти плоскость «Спереди»."
                );

            _model.ClearSelection2(true);

            if (!frontPlane.Select2(false, 0))
                throw new Exception(
                    "Не удалось выбрать плоскость «Спереди»."
                );

            SketchManager sketchManager =
                _model.SketchManager;

            sketchManager.InsertSketch(true);

            // ------------------------------------------------------------
            // Ось вращения.
            // Она создаётся первой → Line1.
            // ------------------------------------------------------------

            double profileEndX =
                part.BoreRevolveProfile.Max(
                    e => e.EndX
                );
            using var exactCoordinates = new ExactSketchCoordinates(sketchManager);

            sketchManager.CreateLine(
                0,
                0,
                0,
                profileEndX / 1000.0,
                0,
                0
            );

            // ------------------------------------------------------------
            // Первый элемент
            // ------------------------------------------------------------

            BoreProfileElement first =
                part.BoreRevolveProfile[0];

            if (Math.Abs(first.StartX) > 0.0001)
            {
                throw new Exception(
                    "Первый элемент BoreRevolveProfile " +
                    "должен начинаться с X=0."
                );
            }

            // Левая торцевая стенка.
            sketchManager.CreateLine(
                0,
                0,
                0,
                0,
                first.StartRadius / 1000.0,
                0
            );

            double currentX = first.StartX;
            double currentRadius = first.StartRadius;

            // ------------------------------------------------------------
            // Элементы профиля
            // ------------------------------------------------------------

            foreach (
                BoreProfileElement element
                in part.BoreRevolveProfile)
            {
                Console.WriteLine(
                    $"[INFO] Bore: {element.Type}, " +
                    $"{element.Description}, " +
                    $"X={element.StartX}..{element.EndX}, " +
                    $"R={element.StartRadius}..{element.EndRadius}"
                );

                // Проверка непрерывности
                if (Math.Abs(
                        element.StartX - currentX) > 0.0001)
                {
                    throw new Exception(
                        $"Разрыв BoreRevolveProfile по X: " +
                        $"ожидалось {currentX} мм, " +
                        $"получено {element.StartX} мм."
                    );
                }

                // --------------------------------------------------------
                // Вертикальный переход по радиусу
                // --------------------------------------------------------

                if (Math.Abs(
                        element.StartRadius -
                        currentRadius) > 0.0001)
                {
                    sketchManager.CreateLine(
                        element.StartX / 1000.0,
                        currentRadius / 1000.0,
                        0,
                        element.StartX / 1000.0,
                        element.StartRadius / 1000.0,
                        0
                    );
                }

                // --------------------------------------------------------
                // Геометрия элемента
                // --------------------------------------------------------

                switch (element.Type)
                {
                    case BoreProfileElementType.Line:

                        sketchManager.CreateLine(
                            element.StartX / 1000.0,
                            element.StartRadius / 1000.0,
                            0,
                            element.EndX / 1000.0,
                            element.EndRadius / 1000.0,
                            0
                        );

                        break;

                    case BoreProfileElementType.Cone:

                        sketchManager.CreateLine(
                            element.StartX / 1000.0,
                            element.StartRadius / 1000.0,
                            0,
                            element.EndX / 1000.0,
                            element.EndRadius / 1000.0,
                            0
                        );

                        break;

                    case BoreProfileElementType.Arc:
                        {
                            double startX =
                                element.StartX / 1000.0;

                            double startRadius =
                                element.StartRadius / 1000.0;

                            double endX =
                                element.EndX / 1000.0;

                            double endRadius =
                                element.EndRadius / 1000.0;

                            // --------------------------------------------------------
                            // Для дуги используем середину между начальной
                            // и конечной угловыми позициями относительно центра.
                            //
                            // Это особенно важно для малых радиусов R2/R3.
                            // --------------------------------------------------------

                            double centerX =
                                element.CenterX;

                            double centerRadius =
                                element.CenterRadius;

                            double startAngle =
                                Math.Atan2(
                                    element.StartRadius - centerRadius,
                                    element.StartX - centerX
                                );

                            double endAngle =
                                Math.Atan2(
                                    element.EndRadius - centerRadius,
                                    element.EndX - centerX
                                );

                            double delta;

                            if (element.Clockwise)
                            {
                                delta = endAngle - startAngle;

                                while (delta >= 0)
                                    delta -= 2.0 * Math.PI;
                            }
                            else
                            {
                                delta = endAngle - startAngle;

                                while (delta <= 0)
                                    delta += 2.0 * Math.PI;
                            }

                            double middleAngle =
                                startAngle + delta / 2.0;

                            double middleX =
                                centerX +
                                element.Radius * Math.Cos(middleAngle);

                            double middleRadius =
                                centerRadius +
                                element.Radius * Math.Sin(middleAngle);

                            SketchSegment? arc =
                                sketchManager.Create3PointArc(
                                    startX,
                                    startRadius,
                                    0,

                                    endX,
                                    endRadius,
                                    0,

                                    middleX / 1000.0,
                                    middleRadius / 1000.0,
                                    0
                                );

                            if (arc == null)
                            {
                                throw new Exception(
                                    $"Не удалось создать дугу: {element.Description}"
                                );
                            }

                            break;
                        }

                    default:

                        throw new Exception(
                            $"Неизвестный тип BoreProfileElement: " +
                            $"{element.Type}"
                        );
                }

                currentX =
                    element.EndX;

                currentRadius =
                    element.EndRadius;
            }

            // ------------------------------------------------------------
            // Закрываем профиль у последней точки
            // ------------------------------------------------------------

            sketchManager.CreateLine(
                currentX / 1000.0,
                currentRadius / 1000.0,
                0,
                currentX / 1000.0,
                0,
                0
            );

            // Закрываем Sketch.
            sketchManager.InsertSketch(true);

            Console.WriteLine(
                "[OK] Sketch сложного внутреннего профиля создан."
            );

            // ------------------------------------------------------------
            // Находим именно созданный Sketch
            // ------------------------------------------------------------

            Feature? boreSketch = null;

            feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.GetTypeName2() == "ProfileFeature")
                {
                    // Последний найденный ProfileFeature после создания
                    // этого Sketch считаем Sketch расточки.
                    boreSketch = feature;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (boreSketch == null)
            {
                throw new Exception(
                    "Не удалось найти Sketch сложного внутреннего профиля."
                );
            }

            Console.WriteLine(
                $"[OK] Найден Sketch: {boreSketch.Name}"
            );

            // ------------------------------------------------------------
            // Выбираем Sketch как профиль выреза
            // ------------------------------------------------------------

            _model.ClearSelection2(true);

            if (!boreSketch.Select2(false, 0))
            {
                throw new Exception(
                    $"Не удалось выбрать Sketch расточки: {boreSketch.Name}"
                );
            }

            // ------------------------------------------------------------
            // Выбираем ось вращения.
            // Line1 — первая линия Sketch, наша осевая линия.
            // Mark = 4.
            // ------------------------------------------------------------

            string axisName =
                $"Line1@{boreSketch.Name}";

            bool axisSelected =
                _model.Extension.SelectByID2(
                    axisName,
                    "EXTSKETCHSEGMENT",
                    0,
                    0,
                    0,
                    true,
                    4,
                    null,
                    0
                );

            if (!axisSelected)
            {
                throw new Exception(
                    $"Не удалось выбрать ось расточки: {axisName}"
                );
            }

            Console.WriteLine(
                $"[OK] Ось расточки выбрана: {axisName}"
            );

            // ------------------------------------------------------------
            // Revolve Cut 360°
            // ------------------------------------------------------------

            Feature? boreCut =
                (Feature?)_model.FeatureManager.FeatureRevolveCut(
                    2.0 * Math.PI,
                    false,
                    0,
                    (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees,
                    0,
                    false,
                    true
                );

            if (boreCut == null)
            {
                throw new Exception(
                    "Не удалось создать Revolve Cut сложной внутренней расточки."
                );
            }

            Console.WriteLine(
                "[OK] Revolve Cut сложной внутренней расточки создан."
            );

            _model.ForceRebuild3(false);
        }

        public void CreateBoreFromDescription(PartDescription part)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            if (part == null)
                throw new ArgumentNullException(nameof(part));

            if (part.BoreProfile == null || part.BoreProfile.Count == 0)
            {
                Console.WriteLine("[INFO] Внутренней расточки для построения нет.");
                return;
            }

            Console.WriteLine(
                $"[INFO] Построение внутренней расточки: {part.BoreProfile.Count} участков."
            );

            // ------------------------------------------------------------
            // Находим плоскость «Спереди»
            // ------------------------------------------------------------
            Feature? frontPlane = null;

            Feature? feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.Name == "Спереди" &&
                    feature.GetTypeName2() == "RefPlane")
                {
                    frontPlane = feature;
                    break;
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (frontPlane == null)
                throw new Exception("Не удалось найти плоскость «Спереди».");

            _model.ClearSelection2(true);

            if (!frontPlane.Select2(false, 0))
                throw new Exception("Не удалось выбрать плоскость «Спереди».");

            SketchManager sketchManager = _model.SketchManager;

            sketchManager.InsertSketch(true);

            // ------------------------------------------------------------
            // Осевая линия — первая линия Sketch.
            // Поэтому она будет Line1.
            // ------------------------------------------------------------
            double lastEndX =
                part.BoreProfile.Max(b => b.StartPosition + b.Length);

            sketchManager.CreateLine(
                0,
                0,
                0,
                lastEndX / 1000.0,
                0,
                0
            );

            // ------------------------------------------------------------
            // Начинаем профиль расточки.
            // ------------------------------------------------------------
            BoreStep firstStep = part.BoreProfile[0];

            if (Math.Abs(firstStep.StartPosition) > 0.0001)
                throw new Exception(
                    "Для первого участка расточки StartPosition должен быть 0 мм."
                );

            double firstRadius =
                firstStep.Diameter / 2.0 / 1000.0;

            double firstEndX =
                (firstStep.StartPosition + firstStep.Length) / 1000.0;

            // Левая стенка отверстия.
            sketchManager.CreateLine(
                0,
                0,
                0,
                0,
                firstRadius,
                0
            );

            // Основной цилиндрический участок Ø62.
            sketchManager.CreateLine(
                0,
                firstRadius,
                0,
                firstEndX,
                firstRadius,
                0
            );

            // Закрываем профиль обратно к оси.
            sketchManager.CreateLine(
                firstEndX,
                firstRadius,
                0,
                firstEndX,
                0,
                0
            );

            sketchManager.InsertSketch(true);

            Console.WriteLine(
                $"[OK] Sketch расточки Ø{firstStep.Diameter} × {firstStep.Length} мм создан."
            );

            // ------------------------------------------------------------
            // Находим созданный Sketch
            // ------------------------------------------------------------
            Feature? sketchFeature = null;

            feature = (Feature?)_model.FirstFeature();

            while (feature != null)
            {
                if (feature.GetTypeName2() == "ProfileFeature")
                {
                    string name = feature.Name;

                    if (name.StartsWith("Эскиз", StringComparison.OrdinalIgnoreCase))
                    {
                        sketchFeature = feature;
                    }
                }

                feature = (Feature?)feature.GetNextFeature();
            }

            if (sketchFeature == null)
                throw new Exception("Не удалось найти Sketch расточки.");

            Console.WriteLine(
                $"[OK] Найден Sketch расточки: {sketchFeature.Name}"
            );

            _model.ClearSelection2(true);

            if (!sketchFeature.Select2(false, 0))
                throw new Exception("Не удалось выбрать Sketch расточки.");

            string axisName =
                $"Line1@{sketchFeature.Name}";

            bool axisSelected = _model.Extension.SelectByID2(
                axisName,
                "EXTSKETCHSEGMENT",
                0,
                0,
                0,
                true,
                4,
                null,
                0
            );

            if (!axisSelected)
                throw new Exception(
                    $"Не удалось выбрать ось расточки: {axisName}"
                );

            Console.WriteLine(
                $"[OK] Ось расточки выбрана: {axisName}"
            );

            // ------------------------------------------------------------
            // Revolve Cut 360°
            // ------------------------------------------------------------
            Feature? cutRevolve =
                (Feature?)_model.FeatureManager.FeatureRevolveCut(
                    2.0 * Math.PI,
                    false,
                    0,
                    (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees,
                    0,
                    true,
                    true
                );

            if (cutRevolve == null)
                throw new Exception(
                    "Не удалось создать Revolve Cut внутренней расточки."
                );

            Console.WriteLine(
                "[OK] Revolve Cut расточки создан."
            );

            _model.ForceRebuild3(false);
        }

        public void OpenPartForInspection(string path, bool requireFresh = false)
        {
            if (_swApp == null) throw new InvalidOperationException("Нет подключения к SolidWorks.");
            path = System.IO.Path.GetFullPath(path);
            if (!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException("Деталь не найдена.", path);
            int errors = 0, warnings = 0;
            _model = _swApp.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                "", ref errors, ref warnings);
            if (_model == null || errors != 0 || (requireFresh && warnings != 0))
                throw new InvalidOperationException($"Открытие детали: errors={errors}, warnings={warnings}.");
              Console.WriteLine($"[INFO] Проверка без сохранения: {path}; warnings={warnings}.");
              Console.WriteLine($"[AUDIT] Opened document: {_model.GetPathName()}; active document: {((ModelDoc2?)_swApp.ActiveDoc)?.GetPathName()}.");
        }

        public void CreateAnnularCuts(PartDescription part)
        {
            if (_model == null) throw new InvalidOperationException("Нет модели.");
            foreach (var cut in part.AnnularCuts)
            {
                cut.Validate();
                Feature? plane = null;
                for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                    if (f.GetTypeName2() == "RefPlane" && f.Name == "Спереди") { plane = f; break; }
                _model.ClearSelection2(true);
                if (plane == null || !plane.Select2(false, 0))
                    throw new InvalidOperationException("Не найдена плоскость Спереди.");
                var manager = _model.SketchManager;
                manager.InsertSketch(true);
                using (var exact = new ExactSketchCoordinates(manager))
                {
                    var axis = manager.CreateLine(0, 0, 0, part.TotalLength / 1000, 0, 0);
                    if (axis == null) throw new InvalidOperationException("Не создана ось кольцевого выреза.");
                    axis.ConstructionGeometry = true;
                    double fillet = cut.RootFilletRadius;
                    if (fillet > 0)
                    {
                        double s = cut.StartX, e = cut.EndX, r = cut.InnerRadius;
                        double diagonal = fillet / Math.Sqrt(2);
                        if (manager.Create3PointArc(s / 1000, (r + fillet) / 1000, 0,
                            (s + fillet) / 1000, r / 1000, 0,
                            (s + fillet - diagonal) / 1000, (r + fillet - diagonal) / 1000, 0) == null)
                            throw new InvalidOperationException("Не создано левое скругление канавки.");
                        if (e - s > 2 * fillet + 1e-8)
                            manager.CreateLine((s + fillet) / 1000, r / 1000, 0, (e - fillet) / 1000, r / 1000, 0);
                        if (manager.Create3PointArc((e - fillet) / 1000, r / 1000, 0,
                            e / 1000, (r + fillet) / 1000, 0,
                            (e - fillet + diagonal) / 1000, (r + fillet - diagonal) / 1000, 0) == null)
                            throw new InvalidOperationException("Не создано правое скругление канавки.");
                    }
                    var points = new[]
                    {
                        (cut.StartX, cut.InnerRadius + fillet), (cut.EndX, cut.InnerRadius + fillet),
                        (cut.EndX, cut.OuterRadius), (cut.StartX, cut.OuterRadius)
                    };
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (i == 0 && fillet > 0) continue; // replaced by the tangent root arcs
                        var a = points[i]; var b = points[(i + 1) % points.Length];
                        if (manager.CreateLine(a.Item1 / 1000, a.Item2 / 1000, 0,
                            b.Item1 / 1000, b.Item2 / 1000, 0) == null)
                            throw new InvalidOperationException("Не создан отрезок кольцевого выреза.");
                    }
                }
                manager.InsertSketch(true);
                Feature? sketch = null;
                for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                    if (f.GetTypeName2() == "ProfileFeature") sketch = f;
                _model.ClearSelection2(true);
                if (sketch == null || !sketch.Select2(false, 0) ||
                    !_model.Extension.SelectByID2($"Line1@{sketch.Name}", "EXTSKETCHSEGMENT",
                        0, 0, 0, true, 4, null, 0))
                    throw new InvalidOperationException("Не выбран профиль/ось кольцевого выреза.");
                var feature = (Feature?)_model.FeatureManager.FeatureRevolveCut(2 * Math.PI, false, 0,
                    (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees, 0, false, true);
                if (feature == null) throw new InvalidOperationException($"Не создан {cut.Name}.");
                feature.Name = cut.Name;
                Console.WriteLine($"[OK] {cut.Name}: X={cut.StartX}..{cut.EndX}, R={cut.InnerRadius}..{cut.OuterRadius}.");
            }
        }

        public void CreateLeftEndCuts(PartDescription part)
        {
            if (_model == null || part.LeftEnd == null) throw new InvalidOperationException("Missing left-end model.");
            var left = part.LeftEnd;
            left.Validate();
            for (int kind = 0; kind < 2; kind++)
            {
                _model.ClearSelection2(true);
                if (!_model.Extension.SelectByID2("Спереди", "PLANE", 0, 0, 0, false, 0, null, 0))
                    throw new InvalidOperationException("Front plane not selected for left-end cut.");
                var sm = _model.SketchManager;
                sm.InsertSketch(true);
                using (var exact = new ExactSketchCoordinates(sm))
                {
                    void Line(double x0, double y0, double x1, double y1)
                    {
                        if (sm.CreateLine(x0 / 1000, y0 / 1000, 0, x1 / 1000, y1 / 1000, 0) == null)
                            throw new InvalidOperationException("Left-end cut segment failed.");
                    }
                    if (kind == 0)
                    {
                        double h = left.FlatHeight, x = left.FlatLength, r = left.RunoutRadius;
                        Line(-1, h, x, h);
                        if (sm.Create3PointArc(x / 1000, h / 1000, 0, (x + r) / 1000, (h + r) / 1000, 0,
                            (x + r / Math.Sqrt(2)) / 1000, (h + r - r / Math.Sqrt(2)) / 1000, 0) == null)
                            throw new InvalidOperationException("R35 cut arc failed.");
                        Line(x + r, h + r, x + r, 150);
                        Line(x + r, 150, -1, 150);
                        Line(-1, 150, -1, h);
                    }
                    else
                    {
                        Line(-1, left.BottomHeight, left.BottomLength, left.BottomHeight);
                        Line(left.BottomLength, left.BottomHeight, left.BottomLength, -100);
                        Line(left.BottomLength, -100, -1, -100);
                        Line(-1, -100, -1, left.BottomHeight);
                    }
                }
                sm.InsertSketch(true);
                Feature? sketch = null;
                for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
                    if (f.GetTypeName2() == "ProfileFeature") sketch = f;
                _model.ClearSelection2(true);
                if (sketch == null || !sketch.Select2(false, 0)) throw new InvalidOperationException("Left-end cut sketch missing.");
                var cut = (Feature?)_model.FeatureManager.FeatureCut3(false, false, false,
                    (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondThroughAll,
                    0, 0, false, false, false, false, 0, 0, false, false, false, false, false,
                    true, true, false, false, false, (int)swStartConditions_e.swStartSketchPlane, 0, false);
                if (cut == null) throw new InvalidOperationException("Left-end bidirectional cut failed.");
                cut.Name = kind == 0 ? "Left_flat_84_5_R35" : "Left_end_bottom_20";
                Console.WriteLine($"[OK] {cut.Name}: exact local cut, both directions.");
            }
        }

        public double VerifyDraftBody(PartDescription part, double? expectedVolumeOverride = null, bool checkVolume = true, bool approximateDk = false, bool correctedFlange19 = false)
        {
            if (_model == null) throw new InvalidOperationException("Нет модели.");
            if (!_model.ForceRebuild3(false)) throw new InvalidOperationException("Перестроение не выполнено.");
            var bodies = (object[]?)((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false);
            if (bodies == null || bodies.Length != 1)
                throw new InvalidOperationException($"Ожидалось одно твёрдое тело, получено {bodies?.Length ?? 0}.");
            _model.ClearSelection2(true);
            var mass = (MassProperty2?)_model.Extension.CreateMassProperty2();
            if (mass == null) throw new InvalidOperationException("Нет объекта массовых свойств.");
            mass.UseSystemUnits = true;
            mass.AccuracyLevel = (int)swMassPropertyAccuracyLevel_e.swMassPropertyAccuracyLevel_Higher;
            mass.Recalculate();
            if (mass.Volume <= 0)
                throw new InvalidOperationException("Нулевой объём модели.");
            Console.WriteLine($"[OK] Одно твёрдое тело; объём {mass.Volume * 1e9:F3} мм³.");
            double expected = expectedVolumeOverride ?? Drawing.DrawingRevision.ExpectedDraftVolumeMm3(part);
            double actual = mass.Volume * 1e9;

            var cylinders = new List<double[]>();
            var tori = new List<double[]>();
            var planes = new List<double[]>();
            var axialEdges = new List<(double X, double R)>();
            var localHoleEndStations = new List<double>();
            var separateHoleEndStations = new List<double>();
            foreach (Edge edge in (object[])((Body2)bodies[0]).GetEdges())
            {
                var curve = (Curve)edge.GetCurve();
                if (!curve.IsCircle()) continue;
                var circle = (double[])curve.CircleParams;
                if (Math.Abs(circle[1] - 0.1115) < 1e-7 && Math.Abs(circle[2]) < 1e-7 &&
                    Math.Abs(circle[6] - 0.011) < 1e-7)
                    separateHoleEndStations.Add(circle[0] * 1000);
                if (Math.Abs(circle[1] + Drawing.DrawingRevision.LocalHoleOffset / 1000) < 1e-7 &&
                    Math.Abs(circle[2]) < 1e-7 && Math.Abs(circle[6] - Drawing.DrawingRevision.LocalHoleDiameter / 2000) < 1e-7)
                    localHoleEndStations.Add(circle[0] * 1000);
                if (Math.Abs(circle[1]) < 1e-7 && Math.Abs(circle[2]) < 1e-7)
                    axialEdges.Add((circle[0] * 1000, circle[6] * 1000));
            }
            var expectedEdges = part.RevolveProfile.SelectMany(e => new[] { (e.StartX, e.StartRadius), (e.EndX, e.EndRadius) })
                .Concat(part.BoreRevolveProfile.SelectMany(e => new[] { (e.StartX, e.StartRadius), (e.EndX, e.EndRadius) })).Distinct();
            if (part.AnnularCuts.Count > 0)
            {
                // The recess merges the Ø190 face across X=265: that former edge disappears.
                expectedEdges = expectedEdges.Where(e => !(e.Item1 == Drawing.DrawingRevision.FlangeRear &&
                    e.Item2 == Drawing.DrawingRevision.BossInnerRadius))
                    .Concat(part.AnnularCuts.SelectMany(c => new[] { (c.StartX, c.InnerRadius + c.RootFilletRadius), (c.EndX, c.InnerRadius + c.RootFilletRadius) }))
                    .Append((Drawing.DrawingRevision.RearRecessStart, Drawing.DrawingRevision.BossInnerRadius));
            }
            foreach (var (x, r) in expectedEdges)
                if (!axialEdges.Any(e => Math.Abs(e.X - x) < 1e-4 && Math.Abs(e.R - r) < 1e-4))
                    throw new InvalidOperationException($"Нет кругового ребра X={x}, R={r}; возможна автопривязка эскиза.");
            Console.WriteLine("[OK] Координаты всех уступов наружного профиля и канала совпадают с параметрами.");
            foreach (Face2 face in (object[])((Body2)bodies[0]).GetFaces())
            {
                var surface = (Surface)face.GetSurface();
                if (surface.IsPlane()) planes.Add((double[])surface.PlaneParams);
                if (surface.IsTorus()) tori.Add((double[])surface.TorusParams);
                if (surface.IsCylinder())
                {
                    var parameters = (double[])surface.CylinderParams;
                    cylinders.Add(parameters);
                }
            }
            const double tolerance = 1e-7; // metres
            bool Axial(double[] c) => Math.Abs(Math.Abs(c[3]) - 1) < tolerance
                && Math.Abs(c[4]) < tolerance && Math.Abs(c[5]) < tolerance;
            foreach (double y in new[] { part.LeftEnd!.FlatHeight, part.LeftEnd.BottomHeight })
                if (!planes.Any(p => Math.Abs(p[0]) < tolerance && Math.Abs(Math.Abs(p[1]) - 1) < tolerance &&
                    Math.Abs(p[2]) < tolerance && Math.Abs(p[4] * 1000 - y) < 1e-4))
                    throw new InvalidOperationException($"Missing left flat at Y={y}.");
            if (!cylinders.Any(c => Math.Abs(c[3]) < tolerance && Math.Abs(c[4]) < tolerance &&
                Math.Abs(Math.Abs(c[5]) - 1) < tolerance && Math.Abs(c[0] - 0.025) < tolerance &&
                Math.Abs(c[1] - 0.0745) < tolerance && Math.Abs(c[6] - 0.035) < tolerance))
                throw new InvalidOperationException("Missing transverse R35 runout cylinder.");
            Console.WriteLine("[OK] Left end: planes Y=39.5/-25 and transverse R35 at X=25/Y=74.5.");
            foreach (var arc in part.RevolveProfile.Where(e => e.Type == RevolveProfileElementType.Arc))
            {
                if (!tori.Any(t => Axial(t) && Math.Abs(t[0] * 1000 - arc.CenterX) < 1e-5 &&
                    Math.Abs(t[1]) < tolerance && Math.Abs(t[2]) < tolerance &&
                    Math.Abs(t[6] * 1000 - arc.CenterRadius) < 1e-5 && Math.Abs(t[7] * 1000 - arc.Radius) < 1e-5))
                    throw new InvalidOperationException($"Нет наружной тороидальной поверхности R{arc.Radius}, X={arc.CenterX}.");
                Console.WriteLine($"[OK] Наружное скругление R{arc.Radius}: центр X={arc.CenterX}, радиус центра={arc.CenterRadius} мм.");
            }
            foreach (var cut in part.AnnularCuts.Where(c => c.RootFilletRadius > 0))
            {
                foreach (double centre in new[] { cut.StartX + cut.RootFilletRadius, cut.EndX - cut.RootFilletRadius }.Distinct())
                    if (!tori.Any(t => Axial(t) && Math.Abs(t[0] * 1000 - centre) < 1e-5 &&
                        Math.Abs(t[1]) < tolerance && Math.Abs(t[2]) < tolerance &&
                        Math.Abs(t[6] * 1000 - cut.InnerRadius - cut.RootFilletRadius) < 1e-5 &&
                        Math.Abs(t[7] * 1000 - cut.RootFilletRadius) < 1e-5))
                        throw new InvalidOperationException($"Не найдена тороидальная поверхность R{cut.RootFilletRadius} при X={centre}.");
                Console.WriteLine($"[OK] CAD: тороидальное дно {cut.Name}, малый радиус R{cut.RootFilletRadius}.");
            }
            var outerRadii = part.RevolveProfile.Where(e => e.EndX > e.StartX && e.StartRadius == e.EndRadius)
                .Select(e => e.StartRadius / 1000).Distinct().ToArray();
            var boreRadii = part.BoreRevolveProfile.Where(e => e.EndX > e.StartX && e.StartRadius == e.EndRadius)
                .Select(e => e.StartRadius / 1000);
            foreach (double radius in outerRadii.Concat(boreRadii).Concat(part.AnnularCuts
                .Where(c => c.EndX - c.StartX > 2 * c.RootFilletRadius).Select(c => c.InnerRadius / 1000)).Distinct())
            {
                if (!cylinders.Any(c => Axial(c) && Math.Abs(c[1]) < tolerance && Math.Abs(c[2]) < tolerance
                    && Math.Abs(c[6] - radius) < tolerance))
                    throw new InvalidOperationException($"Нет соосной цилиндрической грани Ø{radius * 2000}.");
            }
            Console.WriteLine("[OK] CAD-грани наружных цилиндров: Ø" +
                string.Join("/", outerRadii.Select(r => (r * 2000).ToString("G"))) + " мм (включая заготовочные участки).");
            if (correctedFlange19 && checkVolume) throw new InvalidOperationException("D19 correction requires its own volume baseline.");
            double flangeDiameter = correctedFlange19 ? 19 : Drawing.DrawingRevision.FlangeHoleDiameter;
            var holeFaces = cylinders.Where(c => Axial(c) && Math.Abs(c[6] - flangeDiameter / 2000) < tolerance
                && Math.Abs(Math.Sqrt(c[1] * c[1] + c[2] * c[2]) - 0.113) < tolerance).ToArray();
            var centres = new List<(double Y, double Z)>();
            foreach (var c in holeFaces)
                if (!centres.Any(p => Math.Abs(p.Y - c[1]) < tolerance && Math.Abs(p.Z - c[2]) < tolerance))
                    centres.Add((c[1], c[2]));
            if (centres.Count != 12)
                throw new InvalidOperationException($"На Ø226 обнаружено {centres.Count} отверстий Ø{flangeDiameter} вместо 12.");
            // Equal angular gaps alone cannot detect a globally rotated pattern.
            for (int i = 0; i < 12; i++)
            {
                double a = (Drawing.DrawingRevision.FlangeHoleStartAngle + 30 * i) * Math.PI / 180;
                double y = 0.113 * Math.Sin(a), z = 0.113 * Math.Cos(a);
                if (!centres.Any(p => Math.Abs(p.Y - y) < tolerance && Math.Abs(p.Z - z) < tolerance))
                    throw new InvalidOperationException($"Неверная фаза фланцевого ряда: нет центра Y={y * 1000}/Z={z * 1000}.");
            }
            var angles = centres.Select(p => Math.Atan2(p.Z, p.Y)).OrderBy(a => a).ToArray();
            for (int i = 0; i < angles.Length; i++)
            {
                double gap = (i == angles.Length - 1 ? angles[0] + 2 * Math.PI : angles[i + 1]) - angles[i];
                if (Math.Abs(gap - Math.PI / 6) > 1e-6)
                    throw new InvalidOperationException("Угловой шаг отверстий не равен 30°.");
            }
            Console.WriteLine($"[OK] CAD-грани: канал Ø62/68/78/90, 12 отверстий Ø{flangeDiameter} на Ø226; шаг 30°, фаза 15° проверена по координатам.");
            var localFaces = cylinders.Where(c => Axial(c) && Math.Abs(c[6] - 0.006) < tolerance &&
                Math.Abs(c[1] + 0.084) < tolerance && Math.Abs(c[2]) < tolerance).ToArray();
            if (localFaces.Length != 1)
                throw new InvalidOperationException($"Ожидалась одна грань отверстия Ø12 при Y=-84/Z=0; найдено {localFaces.Length}.");
            Console.WriteLine("[OK] Местное отверстие Ø12: ось X, центр Y=-84/Z=0 мм.");
            if (cylinders.Count(c => Axial(c) && Math.Abs(c[6] - 0.011) < tolerance &&
                Math.Abs(c[1] - 0.1115) < tolerance && Math.Abs(c[2]) < tolerance) != 1 ||
                separateHoleEndStations.Count != 2 ||
                !separateHoleEndStations.Any(x => Math.Abs(x - 247) < 1e-4) ||
                !separateHoleEndStations.Any(x => Math.Abs(x - 265) < 1e-4))
                throw new InvalidOperationException("Incorrect P-P Ø22 cylinder or end edges at X=247/265.");
            Console.WriteLine("[OK] Separate P-P hole: D22, Y=111.5/Z=0, end edges X=247/265.");
            // The entrance on the cone is not a planar circular edge. At X=245 a circular
            // edge portion remains on the radial step; the exit at X=260 is circular.
            if (approximateDk)
            {
                if (checkVolume) throw new InvalidOperationException("DK trial requires its own removed-volume check.");
                InspectDkTrial(); // replaces the removed D12 exit circle check with the actual bowl intersection
            }
            foreach (double station in approximateDk ? new[] { Drawing.DrawingRevision.AssumedRightStation } : new[] { Drawing.DrawingRevision.AssumedRightStation, Drawing.DrawingRevision.RearRecessStart })
                if (!localHoleEndStations.Any(x => Math.Abs(x - station) < 1e-4))
                    throw new InvalidOperationException($"Нет торцевого ребра местного Ø12 на X={station}.");
            Console.WriteLine(approximateDk ? "[OK] D12 entry X245 retained; exit replaced by verified APPROX DK intersection." : $"[OK] Ø12: частичный вход через конус, рёбра X=245/260; удалённый объём {Drawing.DrawingRevision.LocalHoleRemovedVolumeMm3:F6} мм³ учтён в проверке общего объёма.");
            if (checkVolume && Math.Abs(actual - expected) > Math.Max(1, expected * 1e-6))
                throw new InvalidOperationException($"Объём не совпадает: CAD={actual:F6}, расчёт={expected:F6} мм³.");
            if (checkVolume) Console.WriteLine($"[OK] Объём совпадает с независимым расчётом: {expected:F3} мм³.");
            else Console.WriteLine("[INFO] Base geometry checked; exact total-volume comparison explicitly skipped for nominal thread TRIAL.");
            for (Feature? feature = (Feature?)_model.FirstFeature(); feature != null;
                feature = (Feature?)feature.GetNextFeature())
            {
                int code = feature.GetErrorCode2(out bool isWarning);
                if (code != 0 && !isWarning)
                    throw new InvalidOperationException($"Ошибка операции {feature.Name}: {code}.");
                if (code != 0) Console.WriteLine($"[WARNING] {feature.Name}: {code}.");
            }
            Console.WriteLine("[OK] Ошибки операций верхнего уровня не обнаружены. Это проверка DRAFT, не всего чертежа.");
            return actual;
        }

        public double CreateSplineTrial()
        {
            if (_model == null) throw new InvalidOperationException("No model.");
            _model.ClearSelection2(true);
            Feature? plane = (Feature?)_model.FirstFeature();
            while (plane != null && !(plane.GetTypeName2() == "RefPlane" && (plane.Name == "Спереди" || plane.Name == "Front Plane")))
                plane = (Feature?)plane.GetNextFeature();
            if (plane == null || !plane.Select2(false, 0)) throw new InvalidOperationException("Front plane missing.");
            var sm = _model.SketchManager;
            sm.InsertSketch(true);
            using (new ExactSketchCoordinates(sm))
            {
                foreach (var segment in Drawing.SplineTrialProfile.Segments())
                {
                    var p = segment.Points;
                    SketchSegment? entity;
                    if (segment.Arc)
                    {
                        var a = p[0]; var b = p[^1]; var m = p[p.Length / 2];
                        entity = sm.Create3PointArc(a.X / 1000, a.Y / 1000, 0, b.X / 1000, b.Y / 1000, 0, m.X / 1000, m.Y / 1000, 0);
                    }
                    else entity = sm.CreateSpline2(p.SelectMany(v => new[] { v.X / 1000, v.Y / 1000, 0.0 }).ToArray(), false);
                    if (entity == null) throw new InvalidOperationException("Trial sketch segment failed.");
                }
                if (sm.CreateCircleByRadius(0, 0, 0, Drawing.SplineTrialProfile.BoreRadius / 1000) == null)
                    throw new InvalidOperationException("Trial bore circle failed.");
            }
            sm.InsertSketch(true);
            CreateTestExtrude();
            _model.ForceRebuild3(false);
            var bodies = (object[]?)((PartDoc)_model).GetBodies2((int)swBodyType_e.swSolidBody, false);
            if (bodies == null || bodies.Length != 1) throw new InvalidOperationException("Trial must have one solid body.");
            var mass = (MassProperty2)_model.Extension.CreateMassProperty2();
            mass.UseSystemUnits = true;
            mass.AccuracyLevel = (int)swMassPropertyAccuracyLevel_e.swMassPropertyAccuracyLevel_Higher;
            mass.Recalculate();
            double volume = mass.Volume * 1e9;
            double expected = Drawing.SplineTrialProfile.Volume();
            if (Math.Abs(volume - expected) > 0.1) throw new InvalidOperationException($"Trial volume mismatch: {volume} vs {expected}.");
            var radii = new List<double>();
            foreach (Face2 face in (object[])((Body2)bodies[0]).GetFaces())
            {
                var surface = (Surface)face.GetSurface();
                if (surface.IsCylinder()) radii.Add(((double[])surface.CylinderParams)[6] * 1000);
            }
            foreach (var (r, count) in new[] { (47.0, 38), (50.0, 38), (Drawing.SplineTrialProfile.Root.FilletRadius, 76), (31.0, 1) })
                if (radii.Count(v => Math.Abs(v - r) < 1e-5) != count)
                    throw new InvalidOperationException($"Trial cylinder count mismatch for R{r}.");
            _model.ViewZoomtofit2();
            Console.WriteLine($"[OK] Trial CAD: one body; 38 crests, 38 roots, 76 root fillets, bore; volume {volume:F6} vs {expected:F6} mm3.");
            return volume;
        }

        public void SaveTestPart(string filePath)
        {
            if (_model == null)
                throw new Exception("Сначала необходимо создать деталь.");

            Console.WriteLine($"[INFO] Сохранение детали: {filePath}");

            int errors = 0;
            int warnings = 0;

            bool result = _model.Extension.SaveAs(
                filePath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings
            );

            if (!result)
            {
                throw new Exception(
                    $"Не удалось сохранить деталь. Errors={errors}, Warnings={warnings}"
                );
            }

            Console.WriteLine("[OK] Деталь сохранена.");
            Console.WriteLine($"[INFO] Файл: {filePath}");
        }

    }
}

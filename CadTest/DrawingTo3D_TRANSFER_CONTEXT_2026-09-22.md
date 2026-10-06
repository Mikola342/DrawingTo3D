# DrawingTo3D — контекст для перехода в другой чат

Дата: 22.09.2026

## 1. Проект

Проект: `DrawingTo3D`

Рабочая папка:
`F:\Project\DrawingTo3D\CadTest`

Цель проекта:
автоматически превращать 2D инженерный чертёж в параметрическую 3D CAD-модель, используя C# + SolidWorks API, с последующим расширением до AI-разбора чертежей.

Среда:
- Windows
- VS Code
- C#
- .NET SDK 10.0.401
- SolidWorks 2025 SP05
- SolidWorks Interop/API
- Revision SolidWorks: `33.5.0`

Структура:
```text
F:\Project\DrawingTo3D\CadTest\
├── Program.cs
├── Cad\
│   └── SolidWorksService.cs
├── Models\
│   └── PartDescription.cs
└── CadTest.csproj
```

## 2. Главный принцип работы

Работать пошагово:
1. изменить небольшой участок;
2. `dotnet run`;
3. проверить полный лог;
4. только после успешного запуска переходить дальше.

Не делать большие неподтверждённые изменения сразу.

Важно:
не придумывать размеры чертежа, если они не подтверждены.

## 3. Что уже доказано рабочим

Работает полностью:
- C# → SolidWorks COM;
- `SldWorks.Application`;
- `_swApp.NewPart()`;
- поиск стандартной плоскости `Спереди`;
- создание Sketch;
- построение профиля;
- Revolve 360°;
- Revolve Cut;
- создание отверстий;
- шаблон 12 отверстий;
- Through All;
- SaveAs в `.SLDPRT`.

`CA1416` для `Type.GetTypeFromProgID(string)` — ожидаемое Windows-only предупреждение, не ошибка.

## 4. Последняя рабочая контрольная точка

Последний успешный файл:
`F:\Project\DrawingTo3D\CadTest\Test_093644.SLDPRT`

Также в текущем чате был загружен этот `.SLDPRT`:
`Test_093644.SLDPRT`

Последний успешный `dotnet run`:
```text
[INFO] Построение сложной внутренней расточки: 12 элементов.
[OK] Sketch сложного внутреннего профиля создан.
[OK] Найден Sketch: Эскиз2
[OK] Ось расточки выбрана: Line1@Эскиз2
[OK] Revolve Cut сложной внутренней расточки создан.
...
[INFO] Построение отверстий: 12
...
[OK] Отверстие Through All создано.
...
[INFO] Сохранение детали: F:\Project\DrawingTo3D\CadTest\Test_093644.SLDPRT
[OK] Деталь сохранена.
[SUCCESS] Деталь успешно создана.
```

## 5. Текущий Program.cs — важные параметры

### Общая длина

```csharp
TotalLength = 265,
```

### Текущий временный наружный профиль

`RevolveProfile` сейчас контрольный и НЕ является окончательным профилем чертежа:

```text
X=0..3      R45
X=3         R45→R41
X=3..25     R41
X=25..70    R41→R65.5
X=70..144   R65.5
X=144..200  R65.5→R94
X=200..220  R94
X=220..239  R95
X=239       R95→R100
X=239       R100
X=239..265  R127.5
```

Это сделано для того, чтобы гарантировать наличие материала для 12 отверстий и проверить API.

### 12 отверстий

Подтверждённый рисунком шаблон:
- Count = 12
- Hole diameter = 19 мм
- Bolt circle diameter = 226 мм
- StartAngle = 90°
- EqualSpacing = true
- ThroughAll = true

```csharp
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
```

Это успешно создаётся как 12 отдельных Cut-Extrude.

## 6. Текущий внутренний профиль

Используется:

```csharp
solidWorks.CreateBoreRevolveProfile(part);
```

Старый:
```csharp
solidWorks.CreateBoreFromDescription(part);
```
сейчас НЕ вызывается.

Текущий `BoreRevolveProfile` содержит 12 элементов:

```text
1.  Ø62 / 42 +1
2.  15°
3.  R50
4.  Ø105
5.  R3
6.  30°
7.  R2 min
8.  Ø110
9.  ступень Ø110 → Ø94
10. Ø94
11. 45° / Ø94 → Ø68
12. Ø68 / 18 мм
```

Текущие контрольные координаты:

```text
Ø62:
X=0..42
R=31

15°:
X=42..70
R=31..38.502577

R50:
X=70..90
R=38.502577..52.5
CenterX=107.802204
CenterRadius=5.776542
Clockwise=true

Ø105:
X=90..142
R=52.5

R3:
X=142..143.5
R=52.5..52.901924
Radius=3
CenterX=142
CenterRadius=55.5
Clockwise=false

30°:
X=143.5..146.669872
R=52.901924..54.732051

R2 min:
X=146.669872..147.669872
R=54.732051..55
Radius=2
CenterX=147.669872
CenterRadius=53
Clockwise=true

Ø110:
X=147.669872..203.5
R=55

ступень Ø110→Ø94:
X=203.5
R=55→47

Ø94:
X=203.5..226
R=47

45°:
X=226..239
R=47→34

Ø68:
X=239..257
R=34
```

Это контрольная геометрия. НЕ считать все эти X окончательными размерами чертежа.

## 7. Важные исправления, уже сделанные

### Arc

Первоначальный `CreateArc()` для R3 не сработал.

Рабочее решение:
использовать `Create3PointArc()`.

В `CreateBoreRevolveProfile()` для `BoreProfileElementType.Arc` сейчас вычисляется средняя точка дуги через центр/углы, затем вызывается `Create3PointArc()`.

Это позволило успешно создать:
- R50;
- R3;
- R2.

### Цепочка профиля

Добавлен метод:
```csharp
part.ValidateRevolveProfiles();
```

Он проверяет непрерывность `RevolveProfile` и `BoreRevolveProfile` до запуска SolidWorks.

В `Program.cs`:
```csharp
part.Validate();

part.ValidateRevolveProfiles();

Console.WriteLine(
    "[OK] Цепочки RevolveProfile и BoreRevolveProfile корректны."
);
```

В последнем логе этот контроль прошёл.

## 8. Чертёж — подтверждённые данные

Используемый PDF чертежа:
`666191e6-c362-4ce4-a312-50d40235ef67.pdf`

Также был найден/использован PDF:
`chertezh_corrected_final.pdf`

Подтверждено из текущего чертежа:
- общая длина 265±0,16;
- M90×2-6g;
- 12 отверстий Ø19;
- Ø226;
- шаг 11×30°=330°;
- Ø62;
- M12-7H, 2 отверстия;
- M8×1,25-6H;
- Ø105 с допуском −0,072/−0,126;
- Ø110 с допуском −0,036/−0,071;
- Ø200 −0,072;
- Ø255;
- Ø190;
- Ø131;
- Ø94 −0,23/−0,54;
- Ø68 −0,010/−0,029;
- R50;
- R35;
- R3;
- R2±0,2;
- R2 min;
- 15°;
- 30°;
- 45°;
- 1,6×45°;
- R16−1,1;
- 61,5+0,19;
- 18+0,27;
- 19+0,52;
- 26−0,52;
- 56±0,37;
- 38 min;
- 42+1;
- 82 min;
- 10;
- 84 и другие локальные размеры.

ВАЖНО:
не считать автоматически все эти размеры осевыми X-координатами.
Чертёж содержит размерные цепи, допуски, локальные сечения и выносные элементы.

## 9. Последний реальный этап работы

Сейчас построена рабочая контрольная модель примерно такой структуры:

```text
Наружный корпус
   ↓
Revolve 360°
   ↓
Внутренний сложный BoreRevolveProfile
   ↓
Revolve Cut
   ↓
12 × Ø19 на Ø226
   ↓
SLDPRT
```

Внутренняя геометрия уже дошла до:
```text
Ø62 → 15° → R50 → Ø105 → R3 → 30° → R2 → Ø110 → Ø94 → 45° → Ø68
```

## 10. Что сейчас НЕ закончено

### Наружный профиль

Он пока контрольный.
Нужно заменить его на настоящий профиль чертежа.

На реальном чертеже наружный контур содержит:
- M90×2;
- R35;
- Ø82;
- Ø125;
- Ø164;
- Ø188;
- Ø190;
- Ø200;
- Ø255;
- различные R/фаски;
- локальные переходы.

Не восстанавливать этот контур только по догадке.

### Правая внутренняя часть

Сейчас Ø68 заканчивается на X=257.
Участок X=257..265 пока не восстановлен окончательно.

В этой зоне на чертеже видны:
- R16−1,1;
- 45°;
- 1,6×45°;
- Ø94;
- Ø68;
- 18;
- 19;
- 26;
- 61,5.

Нужна привязка по самому разрезу перед окончательным вводом координат.

### Резьбы

На чертеже подтверждены:
```text
M90×2-6g
M8×1,25-6H
M18×1,25-6H
M12-7H
```

У SolidWorks уже ПРОТЕСТИРОВАНО реальное создание Thread Feature:
- внутреннее резьбовое отверстие;
- Metric Tap.SLDLFP;
- пример M10×1.5;
- базовое отверстие Ø8.5;
- длина резьбы 20 мм.

M10×1.5 — только синтетический API-тест.
НЕ считать его размером реального чертежа.

Путь профилей SolidWorks:
```text
C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2025\thread profiles
```

## 11. Важные особенности SolidWorks API

Используется typed Interop, не dynamic.

Подтверждённые рабочие моменты:
- `NewPart()`:
  ```csharp
  _model = (ModelDoc2)_swApp.NewPart();
  ```
- `ModelDoc2.FeatureByName()` в используемом Interop отсутствует.
- Feature искать через:
  ```csharp
  _model.FirstFeature()
  feature.GetNextFeature()
  ```
- Front Plane:
  ```text
  Спереди
  ```
- Axis Revolve:
  ```text
  Line1@<SketchName>
  ```
  с `mark = 4`.
- `SketchManager.InsertSketch(true)` возвращает void.
- SolidWorks API использует метры для геометрических координат.

## 12. Текущая последовательность Program.cs

Сейчас активна такая логика:

```csharp
part.ExpandHolePatterns();
part.BuildProfileFromStations();

part.Validate();

part.ValidateRevolveProfiles();

SolidWorksService solidWorks = new SolidWorksService();

try
{
    solidWorks.Connect();

    solidWorks.CreateTestPart();

    solidWorks.CreateRevolveSketchFromElements(part);

    solidWorks.CreateRevolveFromDescription(part);

    solidWorks.CreateBoreRevolveProfile(part);

    solidWorks.CreateHolesFromDescription(part);

    solidWorks.SaveTestPart(
        $@"F:\Project\DrawingTo3D\CadTest\Test_{DateTime.Now:HHmmss}.SLDPRT"
    );
}
catch (Exception ex)
{
    ...
}
```

## 13. Последняя загруженная версия Program.cs

Пользователь отдельно загрузил текущий `Program.cs`:
`Вставленный код(1).cs`

На момент загрузки:
- `TotalLength=265`;
- 12×Ø19 / Ø226;
- контрольный наружный `RevolveProfile`;
- `BoreProfile` Ø62×38;
- `BoreRevolveProfile` до Ø68;
- вызов `CreateBoreRevolveProfile(part)`.

## 14. Последняя команда и результат

Команда:
```powershell
cd F:\Project\DrawingTo3D\CadTest
dotnet run
```

Последний подтверждённый результат:
```text
[OK] Цепочки RevolveProfile и BoreRevolveProfile корректны.
[OK] PartDescription корректен.
[OK] Подключение к SolidWorks выполнено.
[OK] Revision: 33.5.0
[OK] Новая деталь создана.
[OK] Замкнутый Sketch из RevolveProfile создан.
[OK] Revolve из PartDescription создан.
[OK] Sketch сложного внутреннего профиля создан.
[OK] Revolve Cut сложной внутренней расточки создан.
...
[OK] Отверстие Through All создано.
...
[OK] Деталь сохранена.
[SUCCESS] Деталь успешно создана.
```

## 15. Следующая рекомендуемая задача

Не продолжать добавление случайных X-координат.

Следующий чат должен:
1. изучить этот файл;
2. принять текущую модель как контрольную;
3. посмотреть продольный разрез текущей модели в SolidWorks;
4. сравнить его с текущим PDF;
5. затем точно восстановить правый внутренний участок X=257..265;
6. затем восстановить наружный профиль;
7. затем добавить реальные резьбы/отверстия;
8. затем добавить финальную геометрию R/фасок;
9. затем сделать проверку модели по размерам чертежа.

## 16. Как продолжать

Пользователь предпочитает:
- один конкретный шаг;
- точный файл;
- точный блок кода;
- точную команду `dotnet run`;
- после этого пользователь присылает лог;
- после успешного лога переходим дальше.

Не начинать проект заново.
Не удалять рабочий код без необходимости.
Не использовать M10×1.5 как реальный размер чертежа.
Не считать контрольные X-координаты окончательными без проверки.

## 17. Состояние на 22.09.2026

Контрольная точка:
`Test_093644.SLDPRT`

Рабочая база:
- SolidWorks API — OK
- наружный Revolve — OK
- внутренний Revolve Cut — OK
- Arc R50 — OK
- Arc R3 — OK
- Arc R2 — OK
- 12 отверстий Ø19 на Ø226 — OK
- Save SLDPRT — OK
- профильная валидация — OK

Главная задача дальше:
**довести модель от контрольной геометрии до фактического чертежа без угадывания размеров.**

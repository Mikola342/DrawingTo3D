using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object ExportVariantStepRoundTrip(string stepPath)
    {
        if (_model == null || _swApp == null) throw new InvalidOperationException("No model.");
        if (File.Exists(stepPath)) throw new IOException("Refusing to overwrite STEP.");
        var sourceModel = _model;
        var source = CopyPilotBody();
        double sourceVolume = CurrentTrialVolume();
        var sourceBox = (double[])source.GetBodyBox();
        _model.ClearSelection2(true);
        int errors = 0, warnings = 0;
        bool saved = _model.Extension.SaveAs(stepPath, 0,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
        if (!saved || errors != 0 || warnings != 0 || !File.Exists(stepPath))
            throw new InvalidOperationException($"STEP export failed: {errors}/{warnings}.");
        Console.WriteLine($"[STEP EXPORTED] {stepPath}");
        var importData = _swApp.GetImportFileData(stepPath);
        var imported = (ModelDoc2?)_swApp.LoadFile4(stepPath, "r", importData, ref errors);
        if (imported == null || errors != 0) throw new InvalidOperationException($"STEP import failed: {errors}.");
        try
        {
            _model = imported;
            var bodies = (object[]?)((PartDoc)imported).GetBodies2((int)swBodyType_e.swSolidBody, false);
            var sheets = (object[]?)((PartDoc)imported).GetBodies2((int)swBodyType_e.swSheetBody, false);
            if (bodies?.Length != 1 || (sheets?.Length ?? 0) != 0)
                throw new InvalidOperationException("STEP requires one solid and no sheets.");
            var body = (Body2)bodies[0];
            double volume = CurrentTrialVolume();
            var box = (double[])body.GetBodyBox();
            Console.WriteLine($"[STEP IMPORTED] volume={volume:R};source={sourceVolume:R}.");
            object Difference(Body2 left, Body2 right)
            {
                var result = (object[]?)((Body2)left.Copy()).Operations2(
                    (int)swBodyOperationType_e.SWBODYCUT, (Body2)right.Copy(), out int code);
                return new { Code = code, RemainingBodies = result?.Length ?? 0,
                    Empty = code == 0 && (result?.Length ?? 0) == 0,
                    Regions = (result ?? []).Cast<Body2>().Select(b => new {
                        ApproximateBoxMetres=(double[])b.GetBodyBox(),
                        VolumeMm3=((double[])b.GetMassProperties(1))[3]*1e9,
                        Faces=((object[])b.GetFaces()).Length
                    }).ToArray() };
            }
            // Full-body comparison includes internal channels and small thread/spline
            // geometry. It is a same-kernel diagnostic, not independent certification.
            var missing = Difference(source, body);
            Console.WriteLine("[STEP DIFFERENCE] source minus import completed.");
            var added = Difference(body, source);
            Console.WriteLine("[STEP DIFFERENCE] import minus source completed.");
            return new { SourceVolumeMm3=sourceVolume, ImportedVolumeMm3=volume,
                VolumeDifferenceMm3=volume-sourceVolume, SourceBoxMetres=sourceBox, ImportedBoxMetres=box,
                BoundingBoxIsApproximate=true, SolidBodies=1, SheetBodies=0,
                Missing=missing, Added=added, SameKernelComparison=true,
                DrawingConformanceAccepted=false, ManufacturingAccepted=false };
        }
        finally { _model = sourceModel; }
    }
}

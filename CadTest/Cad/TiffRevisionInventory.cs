using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectTiffRevisionBaseline()
    {
        if (_model == null) throw new InvalidOperationException("No model loaded.");
        var features = new List<object>();
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            int error = f.GetErrorCode2(out bool warning);
            features.Add(new { Name = f.Name, Type = f.GetTypeName2(), Error = error, Warning = warning });
        }
        return new {
            Features = features, VolumeMm3 = CurrentTrialVolume(),
            Scope = "Read-only top-level feature inventory of the inherited PDF-based geometry. Not TIFF acceptance; no operations suppressed, deleted, rebuilt or saved by this inventory."
        };
    }
}

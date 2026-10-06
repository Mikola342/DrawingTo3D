using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object DiagnoseFeatureErrors()
    {
        if(_model==null)throw new InvalidOperationException("No model.");
        var rows=new List<object>();
        for(Feature? f=(Feature?)_model.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())
        {
            int code=f.GetErrorCode2(out bool warning);
            rows.Add(new{Name=f.Name,Type=f.GetTypeName2(),Suppressed=TiffSuppressed(f),Error=code,Warning=warning});
            if(code!=0||warning)Console.WriteLine($"[FEATURE ISSUE] {f.Name}: code={code},warning={warning}");
        }
        double? volume=null;string? volumeError=null;
        try{volume=CurrentTrialVolume();}catch(Exception e){volumeError=e.Message;}
        return new{Features=rows,VolumeMm3=volume,VolumeError=volumeError,Accepted=false};
    }
    public object InspectTiffFeatureHealth()
    {
        if (_model == null) throw new InvalidOperationException("No model.");
        int active = 0, suppressed = 0;
        for (Feature? f = (Feature?)_model.FirstFeature(); f != null; f = (Feature?)f.GetNextFeature())
        {
            if (TiffSuppressed(f)) { suppressed++; continue; }
            active++;
            int error = f.GetErrorCode2(out bool warning);
            if (error != 0 || warning)
                throw new InvalidOperationException($"TIFF active feature issue: {f.Name}/{error}/{warning}");
        }
        if (active == 0) throw new InvalidOperationException("No active features.");
        // CurrentTrialVolume rejects zero/multiple solid bodies and invalid mass properties.
        double volume = CurrentTrialVolume();
        Console.WriteLine($"[TIFF HEALTH OK] active={active}, suppressed={suppressed}, one solid body.");
        return new { ActiveFeatures = active, SuppressedFeatures = suppressed, ActiveErrors = 0,
            ActiveWarnings = 0, SolidBodies = 1, VolumeMm3 = volume };
    }
}

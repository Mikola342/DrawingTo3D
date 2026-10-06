using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object PrepareNativeVariant(string destination)
    {
        if(File.Exists(destination))throw new IOException("Refusing to overwrite prepared SLDPRT.");
        object CheckSolid()
        {
            var bodies=(object[])((PartDoc)_model!).GetBodies2((int)swBodyType_e.swSolidBody,false);
            if(bodies.Length!=1)throw new InvalidOperationException("Expected one solid.");
            var fault=((Body2)bodies[0]).Check3;
            int count=fault?.Count??0;
            var codes=Enumerable.Range(0,count).Select(i=>fault!.get_ErrorCode(i)).ToArray();
            Console.WriteLine($"[NATIVE BODY CHECK] faults={count};codes={string.Join(',',codes)}.");
            if(count!=0)throw new InvalidOperationException("Native body contains kernel faults: "+string.Join(',',codes));
            return new{FaultCount=count,FaultCodes=codes,Passed=count==0};
        }
        object beforeHealth,beforeSolid;double beforeVolume;
        using(var batch=BeginApiBatch())
        {
            _model!.ForceRebuild3(false);
            beforeHealth=InspectTiffFeatureHealth();
            beforeVolume=CurrentTrialVolume();beforeSolid=CheckSolid();
            SaveTestPart(destination);
        }
        string title=_model!.GetTitle();
        _swApp!.CloseDoc(title);_model=null;
        OpenPartForInspection(destination,requireFresh:true);
        object afterHealth,afterSolid;double afterVolume;
        using(var batch=BeginApiBatch())
        {
            afterHealth=InspectTiffFeatureHealth();afterSolid=CheckSolid();
            afterVolume=CurrentTrialVolume();
        }
        if(Math.Abs(afterVolume-beforeVolume)>.001)throw new InvalidOperationException("Saved native volume changed.");
        return new{BeforeHealth=beforeHealth,AfterHealth=afterHealth,BeforeSolid=beforeSolid,AfterSolid=afterSolid,
            BeforeVolumeMm3=beforeVolume,AfterVolumeMm3=afterVolume,FreshSavedFileOpened=true,
            FullDrawingConformanceAccepted=false,UnresolvedValidationRetained=true};
    }
}

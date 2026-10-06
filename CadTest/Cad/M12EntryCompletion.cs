using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object CompareCompletedM12Entries(string originalCopy,string revisedCopy)
    {
        using var batch=BeginApiBatch();
        OpenPartForInspection(originalCopy,requireFresh:true);var before=CopyPilotBody();double volume=CurrentTrialVolume();
        OpenPartForInspection(revisedCopy,requireFresh:true);
        return CompareM12EntryBodies(before,CopyPilotBody(),volume);
    }
    public object TrimCompletedM12Starts()
    {
        using var batch=BeginApiBatch();
        double before=CurrentTrialVolume();
        for(int i=1;i<=2;i++)
        {
            var feature=TiffFeature($"TRIAL_M12x1_75_7H_nominal_{i}");
            var data=(IThreadFeatureData)feature.GetDefinition();
            if(!data.Offset||Math.Abs(data.OffsetDistance-.00175)>1e-9||Math.Abs(data.BlindDepth-.01975)>1e-9||data.TrimStartFace)
                throw new InvalidOperationException("Expected untrimmed extended M12 start.");
            if(!data.AccessSelections((ModelDoc)_model!,null))throw new InvalidOperationException("M12 start trim unavailable.");
            data.TrimStartFace=true;
            if(!feature.ModifyDefinition(data,_model,null))throw new InvalidOperationException("M12 start trim edit failed.");
            _model!.ForceRebuild3(false);_=InspectTiffFeatureHealth();
        }
        return new{BeforeMm3=before,AfterMm3=CurrentTrialVolume(),Scope="Restore start-face trimming with helix still extended outside flange; nominal dimensions unchanged"};
    }
    public object CompleteM12Entries()
    {
        using var batch=BeginApiBatch();
        var before=CopyPilotBody();double beforeVolume=CurrentTrialVolume();
        for(int i=1;i<=2;i++)
        {
            var f=TiffFeature($"TRIAL_M12x1_75_7H_nominal_{i}");
            var prior=(IThreadFeatureData)f.GetDefinition();
            if(prior.Offset||Math.Abs(prior.BlindDepth-.018)>1e-9||Math.Abs(prior.Pitch-.00175)>1e-9||!prior.RightHanded||prior.ReverseDirection)
                throw new InvalidOperationException("Unexpected M12 predecessor parameters.");
            if(!_model!.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToAfterFeature,f.Name))throw new InvalidOperationException("M12 rollback failed.");
            try
            {
                var d=(IThreadFeatureData)f.GetDefinition();
                if(!d.AccessSelections((ModelDoc)_model,null))throw new InvalidOperationException("M12 definition unavailable.");
                d.Offset=true;d.OffsetDistance=.00175;d.ReverseOffset=true;d.MaintainThreadLength=true;
                d.BlindDepth=.01975;d.TrimStartFace=false;
                if(!f.ModifyDefinition(d,_model,null))throw new InvalidOperationException("M12 entry modification failed.");
            }
            finally
            {
                if(!_model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToEnd,""))throw new InvalidOperationException("M12 tree restoration failed.");
            }
            _model.ForceRebuild3(false);_=InspectTiffFeatureHealth();
            Console.WriteLine($"[M12 ENTRY] {i}/2: start helix X245.25, front face remains X247.");
        }
        var after=CopyPilotBody();
        return CompareM12EntryBodies(before,after,beforeVolume);
    }
    private object CompareM12EntryBodies(Body2 before,Body2 after,double beforeVolume)
    {
        var added=(object[]?)((Body2)after.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)before.Copy(),out int addError);
        if((added?.Length??0)!=0||(addError!=0&&addError!=(int)swBodyOperationError_e.swBodyOperationEmptyBody))throw new InvalidOperationException("M12 unexpectedly added material.");
        var removed=(object[]?)((Body2)before.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)after.Copy(),out int cutError);
        if(cutError!=0||removed?.Length!=2)throw new InvalidOperationException("Expected two M12 entry removals.");
        var rows=new List<object>();var seen=new HashSet<int>();
        foreach(Body2 piece in removed)
        {
            var points=new List<double[]>();
            foreach(Edge e in (object[])piece.GetEdges())
            {
                var b=e.GetCurveParams3();
                for(int k=0;k<=128;k++)
                {
                    var p=(double[])e.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*k/128,0);
                    if(p.Length!=4||!p.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)throw new InvalidOperationException("M12 difference point invalid.");
                    points.Add(p.Take(3).Select(v=>v*1000).ToArray());
                }
            }
            int sign=points.Average(p=>p[1])>0?1:-1;
            if(!seen.Add(sign))throw new InvalidOperationException("Duplicate M12 removal region.");
            var radii=points.Select(p=>Math.Sqrt(Math.Pow(p[1]-sign*56.5,2)+Math.Pow(p[2]+sign*113*Math.Cos(Math.PI/6),2))).ToArray();
            double lo=points.Min(p=>p[0]),hi=points.Max(p=>p[0]);
            if(lo<246.9999||hi>248.3126||radii.Min()<M12BasicMinorDiameter/2-.0001||radii.Max()>6.0001)throw new InvalidOperationException("M12 change outside intended front entry.");
            rows.Add(new{Hole=sign==1?1:2,MinXmm=lo,MaxXmm=hi,MinRadiusMm=radii.Min(),MaxRadiusMm=radii.Max(),Samples=points.Count});
        }
        var reconstruction=CheckM6BooleanReconstruction(before,after,removed,"M12 ENTRY");
        return new{BeforeMm3=beforeVolume,AfterMm3=CurrentTrialVolume(),Changes=rows,Reconstruction=reconstruction,
            Accepted=false,Scope="Full nominal through-thread entry trial; no added chamfer, front datum and rear ending retained; full-profile diagnostics separate"};
    }
}

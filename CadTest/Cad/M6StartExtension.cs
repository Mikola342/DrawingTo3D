using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object CompareM6StartExtension(string originalCopy,string revisedCopy)
    {
        using var batch=BeginApiBatch();
        OpenPartForInspection(originalCopy,requireFresh:true);
        var before=CopyPilotBody();
        OpenPartForInspection(revisedCopy,requireFresh:true);
        var after=CopyPilotBody();
        var added=(object[]?)((Body2)after.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)before.Copy(),out int addError);
        if((added?.Length??0)!=0||(addError!=0&&addError!=(int)swBodyOperationError_e.swBodyOperationEmptyBody))
            throw new InvalidOperationException("M6 extension unexpectedly added material.");
        var removed=(object[]?)((Body2)before.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)after.Copy(),out int cutError);
        if(cutError!=0||removed?.Length!=4)throw new InvalidOperationException("Expected four isolated M6 extensions.");
        var rows=new List<object>();var indices=new HashSet<int>();
        foreach(Body2 piece in removed)
        {
            var samples=new List<double[]>();
            foreach(Edge e in (object[])piece.GetEdges())
            {
                var b=e.GetCurveParams3();
                for(int j=0;j<=128;j++)
                {
                    var p=(double[])e.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*j/128,0);
                    if(p.Length!=4||!p.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)
                        throw new InvalidOperationException("Extension edge unavailable.");
                    samples.Add(p.Take(3).Select(x=>x*1000).ToArray());
                }
            }
            if(samples.Count==0)throw new InvalidOperationException("No extension edges.");
            int index=Array.FindIndex(TiffM6Centres,c=>samples.All(p=>Math.Sqrt(Math.Pow(p[1]-c.Y,2)+Math.Pow(p[2]-c.Z,2))<3.001));
            if(index<0||!indices.Add(index))throw new InvalidOperationException("Extension not confined to one distinct M6 hole.");
            var centre=TiffM6Centres[index];
            double minX=samples.Min(p=>p[0]),maxX=samples.Max(p=>p[0]);
            var radii=samples.Select(p=>Math.Sqrt(Math.Pow(p[1]-centre.Y,2)+Math.Pow(p[2]-centre.Z,2))).ToArray();
            if(minX<248.999||maxX>250.751||radii.Min()<TiffM6PilotRadius-.001)
                throw new InvalidOperationException($"M6 extension outside intended start region: {minX:R}..{maxX:R}");
            rows.Add(new{Hole=index+1,MinXmm=minX,MaxXmm=maxX,MinRadiusMm=radii.Min(),MaxRadiusMm=radii.Max(),EdgeSamples=samples.Count});
        }
        var reconstruction=CheckM6BooleanReconstruction(before,after,removed,"M6 START EXTENSION");
        return new{AddedMaterial=false,RemovedComponents=rows,Reconstruction=reconstruction,Health=InspectTiffFeatureHealth(),
            LocalityBudgetMm=.001,FullThreadProfileAccepted=false,Scope="Exact body differences plus sampled boundaries; not continuous full-profile certification"};
    }
    // Separate agreed-variant experiment: extend one pitch into the existing drill
    // reserve, preserving the phase and the exit at X265. Never changes the source.
    public object ExtendM6Starts()
    {
        using var batch=BeginApiBatch();
        double before=CurrentTrialVolume();
        var rows=new List<object>();
        for(int i=1;i<=4;i++)
        {
            var f=TiffFeature($"TIFF_M6_{i}_THREAD");
            _=CheckTiffM6Thread(f);
            if(!_model!.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToAfterFeature,f.Name))
                throw new InvalidOperationException("Cannot expose M6 thread.");
            try
            {
                var d=(IThreadFeatureData)f.GetDefinition();
                if(!d.AccessSelections((ModelDoc)_model,null))throw new InvalidOperationException("M6 selections unavailable.");
                d.Offset=true;d.OffsetDistance=.001;d.ReverseOffset=true;
                d.MaintainThreadLength=true;d.BlindDepth=.016;d.TrimStartFace=false;
                if(!f.ModifyDefinition(d,_model,null))throw new InvalidOperationException("M6 start modification failed.");
            }
            finally
            {
                if(!_model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToEnd,""))
                    throw new InvalidOperationException("M6 full history restoration failed.");
            }
            _model.ForceRebuild3(false);
            _=InspectTiffFeatureHealth();
            var actual=(IThreadFeatureData)f.GetDefinition();
            rows.Add(new{Feature=f.Name,actual.Offset,actual.OffsetDistance,actual.ReverseOffset,actual.MaintainThreadLength,actual.BlindDepth,actual.TrimStartFace,actual.TrimEndFace});
            Console.WriteLine($"[M6 EXTENDED] {i}/4; volume={CurrentTrialVolume():R}");
        }
        double after=CurrentTrialVolume();
        if(before-after<=0||before-after>4*Math.PI*(9-TiffM6PilotRadius*TiffM6PilotRadius)*2)
            throw new InvalidOperationException("M6 extension removal outside conservative envelope.");
        return new{BeforeMm3=before,FinalMm3=after,RemovedMm3=before-after,Threads=rows,
            SourceConformanceAccepted=false,RunoutChoice="One-pitch start extension into existing drill reserve; full profile requires measurement"};
    }
}

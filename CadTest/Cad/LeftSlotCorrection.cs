using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private static void ValidateLeftSlotDepths(bool forwardBlind,bool reverseBlind,double forwardMetres,double reverseMetres)
    {
        if(!forwardBlind||!reverseBlind||!double.IsFinite(forwardMetres)||!double.IsFinite(reverseMetres)||
            Math.Abs(forwardMetres-.008)>1e-10||Math.Abs(reverseMetres-.008)>1e-10)
            throw new InvalidOperationException("Left slot must be blind +/-8mm, not through-all or one-sided16mm.");
    }
    public static void TestLeftSlotDepths()
    {
        ValidateLeftSlotDepths(true,true,.008,.008);
        foreach(var v in new[]{(false,true,.008,.008),(true,false,.008,.008),(true,true,.016,0.0),
            (true,true,0.0,.016),(true,true,.007,.009),(true,true,-.008,.008),
            (true,true,double.NaN,.008),(true,true,.008,double.PositiveInfinity)})
        {
            bool rejected=false;try{ValidateLeftSlotDepths(v.Item1,v.Item2,v.Item3,v.Item4);}catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Invalid left-slot depths accepted.");
        }
        Console.WriteLine("[TEST OK] Left slot: symmetric16mm,8 invalid end-condition/depth pairs rejected.");
    }
    public object CorrectLeftSlotWidth()
    {
        double before=CurrentTrialVolume();
        var feature=TiffFeature("Left_flat_84_5_R35");
        if(TiffSuppressed(feature))throw new InvalidOperationException("Left cut is suppressed.");
        var data=(IExtrudeFeatureData2)feature.GetDefinition();
        if(data.GetEndCondition(true)!=(int)swEndConditions_e.swEndCondThroughAll ||
            data.GetEndCondition(false)!=(int)swEndConditions_e.swEndCondThroughAll)
            throw new InvalidOperationException("Expected inherited bidirectional through-all cut.");
        if(!data.AccessSelections(_model,null))throw new InvalidOperationException("Cannot access left cut.");
        try
        {
            foreach(bool forward in new[]{true,false})
            {data.SetEndCondition(forward,(int)swEndConditions_e.swEndCondBlind);data.SetDepth(forward,.008);}
            if(!feature.ModifyDefinition(data,_model,null))throw new InvalidOperationException("Left slot width modification failed.");
        }
        finally{data.ReleaseSelectionAccess();}
        if(!_model!.ForceRebuild3(false))throw new InvalidOperationException("Left slot rebuild failed.");
        var geometry=InspectLeftSlotWidth();
        double after=CurrentTrialVolume();
        if(after<=before||after-before>10000)throw new InvalidOperationException("Unexpected restored left shoulder volume.");
        Console.WriteLine($"[LEFT SLOT] width16; restored shoulder volume {after-before:R} mm3.");
        return new{BeforeMm3=before,FinalMm3=after,RestoredShouldersMm3=after-before,LeftSlotWidthCorrected=true,
            Geometry=geometry,Health=InspectTiffFeatureHealth(),FullLeftEndAccepted=false};
    }

    public object InspectLeftSlotWidth()
    {
        var f=TiffFeature("Left_flat_84_5_R35");
        if(TiffSuppressed(f))throw new InvalidOperationException("Left slot suppressed.");
        var data=(IExtrudeFeatureData2)f.GetDefinition();
        ValidateLeftSlotDepths(data.GetEndCondition(true)==(int)swEndConditions_e.swEndCondBlind,
            data.GetEndCondition(false)==(int)swEndConditions_e.swEndCondBlind,data.GetDepth(true),data.GetDepth(false));
        int floor=0,negative=0,positive=0,runout=0,samples=0;double minZ=double.PositiveInfinity,maxZ=double.NegativeInfinity;
        foreach(Face2 face in (object[])f.GetFaces())
        {
            var s=(Surface)face.GetSurface();
            if(s.IsPlane())
            {
                var p=(double[])s.PlaneParams;
                if(Math.Abs(Math.Abs(p[1])-1)<1e-8 && Math.Abs(p[4]*1000-39.5)<1e-5)floor++;
                if(Math.Abs(Math.Abs(p[2])-1)<1e-8)
                {if(Math.Abs(p[5]*1000-8)<1e-5)positive++;if(Math.Abs(p[5]*1000+8)<1e-5)negative++;}
            }
            if(s.IsCylinder())
            {
                var p=(double[])s.CylinderParams;
                if(Math.Abs(p[0]*1000-25)<1e-5&&Math.Abs(p[1]*1000-74.5)<1e-5&&
                    Math.Abs(Math.Abs(p[5])-1)<1e-8&&Math.Abs(p[6]*1000-35)<1e-5)runout++;
            }
            foreach(Edge edge in (object[])face.GetEdges())
            {
                var t=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(t.UMinValue,t.UMaxValue);
                for(int i=0;i<=32;i++)
                {
                    var p=(double[])edge.Evaluate2(t.UMinValue+(t.UMaxValue-t.UMinValue)*i/32,0);
                    if(p.Length!=4||!p.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)
                        throw new InvalidOperationException("Left slot edge evaluation failed.");
                    double z=p[2]*1000;minZ=Math.Min(minZ,z);maxZ=Math.Max(maxZ,z);samples++;
                    if(Math.Abs(z)>8.00001)throw new InvalidOperationException("Left slot face extends outside width16.");
                }
            }
        }
        if(floor<1||negative<1||positive<1||runout<1||samples==0||Math.Abs(minZ+8)>1e-5||Math.Abs(maxZ-8)>1e-5)
            throw new InvalidOperationException($"Left slot boundary missing: floor{floor},side-{negative},side+{positive},R35:{runout}.");
        Console.WriteLine($"[LEFT SLOT CHECK] floor39.5,R35 center25/74.5,width{maxZ-minZ:R};samples{samples}.");
        return new{WidthMm=maxZ-minZ,MinimumZMm=minZ,MaximumZMm=maxZ,FloorFaces=floor,NegativeSideFaces=negative,
            PositiveSideFaces=positive,RunoutR35Faces=runout,EdgeSamples=samples,FullLeftEndAccepted=false};
    }
}

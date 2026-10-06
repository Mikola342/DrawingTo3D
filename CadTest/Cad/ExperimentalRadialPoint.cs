using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private static readonly double RadialEndR=51+(132-265)*Math.Tan(4*Math.PI/180);
    private static double[] RadialPointAt(double r)=>[132,r*Math.Sin(2*Math.PI/3),-r*Math.Cos(2*Math.PI/3)];
    private static double RadialPointAddedVolume(int n)
    {
        var main=ChannelPoint(true);double a=2*Math.PI/3,h=3/Math.Tan(Math.PI/3),sum=0;
        for(int i=0;i<n;i++)
        {
            double s=h*(i+.5)/n,inner=s*Math.Tan(Math.PI/3),weight=Math.PI*(9-inner*inner)*h/(4.0*n*n*n);
            for(int j=0;j<n;j++)
            {
                double r=Math.Sqrt(inner*inner+(9-inner*inner)*(j+.5)/n);
                for(int k=0;k<4*n;k++)
                {
                    double t=2*Math.PI*(k+.5)/(4*n),x=132+r*Math.Cos(t),side=r*Math.Sin(t);
                    double y=(RadialEndR+s)*Math.Sin(a)+side*Math.Cos(a),z=-(RadialEndR+s)*Math.Cos(a)+side*Math.Sin(a);
                    double dx=x-main.Tip[0],dy=y-main.Tip[1],dz=z-main.Tip[2];
                    double along=dx*main.Axis[0]+dy*main.Axis[1]+dz*main.Axis[2];
                    double allowed=Math.Min(3.15,Math.Max(0,along)*Math.Tan(Math.PI/3));
                    double distance2=Math.Pow(dx-along*main.Axis[0],2)+Math.Pow(dy-along*main.Axis[1],2)+Math.Pow(dz-along*main.Axis[2],2);
                    if(along<0||distance2>allowed*allowed)sum+=weight;
                }
            }
        }
        return sum;
    }
    public static void TestExperimentalRadialPoint()
    {
        double length=6/Math.Tan(Math.PI/3);
        ValidateChannelCutExtent(length,3,true,Math.PI/3,true);
        ValidateChannelCutExtent(length+1e-12,3,true,Math.PI/3,true);
        int rejected=0;
        foreach(var test in new[]{(length+1e-5,true,true),(length-1e-5,true,true),(length,false,true),(0.0,true,true),(double.NaN,true,true),(length+1e-5,true,false)})
        {
            try{ValidateChannelCutExtent(test.Item1,3,test.Item2,Math.PI/3,test.Item3);}
            catch(ArgumentException){rejected++;}
        }
        if(rejected!=6)throw new InvalidOperationException("Exact-apex guard regression.");
        double coarse=RadialPointAddedVolume(80),fine=RadialPointAddedVolume(160);
        if(!double.IsFinite(fine)||fine<=0||fine>=2*Math.PI*9*(3/Math.Tan(Math.PI/3))/3||Math.Abs(coarse-fine)>.01)
            throw new InvalidOperationException("Radial end integral failed.");
        Console.WriteLine($"[TEST OK] Radial end including finite main cone:added={fine:R}, refinement={Math.Abs(coarse-fine):R}; assumed120deg.");
    }
    public object InspectExperimentalRadialPoint()
    {
        if(!TiffSuppressed(TiffFeature("TIFF_REAR_AA_D6_X132")))throw new InvalidOperationException("Old radial end active.");
        var feature=TiffFeature("EXPERIMENT_AA_RADIAL_POINT120");
        if(TiffSuppressed(feature))throw new InvalidOperationException("Radial point suppressed.");
        double a=2*Math.PI/3,h=3/Math.Tan(Math.PI/3),max=double.NegativeInfinity;
        double[] tip=RadialPointAt(RadialEndR),axis=[0,Math.Sin(a),-Math.Cos(a)];int cones=0,samples=0;
        foreach(Face2 face in (object[])feature.GetFaces())
        {
            var surface=(Surface)face.GetSurface();if(!surface.IsCone())continue;
            cones++;var c=(double[])surface.ConeParams2;
            if(Math.Abs(Math.Abs(c[7])-Math.PI/3)>1e-8||Math.Abs(Math.Abs(axis.Select((u,i)=>u*c[i+3]).Sum())-1)>1e-8)throw new InvalidOperationException("Radial cone angle/axis mismatch.");
            foreach(Edge edge in (object[])face.GetEdges())
            {
                var b=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(b.UMinValue,b.UMaxValue);
                for(int j=0;j<=64;j++)
                {
                    var q=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*j/64,0);
                    if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)throw new InvalidOperationException("Radial edge evaluation failed.");
                    var d=tip.Select((v,i)=>q[i]*1000-v).ToArray();double s=d.Zip(axis).Sum(t=>t.First*t.Second);
                    double r=Math.Sqrt(d.Select((v,i)=>Math.Pow(v-s*axis[i],2)).Sum());
                    if(s<-.0001||s>h+.0001||Math.Abs(r-s*Math.Tan(Math.PI/3))>.0001)throw new InvalidOperationException("Radial cone envelope mismatch.");
                    max=Math.Max(max,s);samples++;
                }
            }
        }
        if(cones==0||samples==0||Math.Abs(max-h)>.0001)throw new InvalidOperationException("Radial cone base missing.");
        var cap=InspectRadialAaEnd(requireNoFlatCap:true);
        var connection=InspectTiffAaChannel(true,true,true,true);
        return new{TipInsideMainChannel=tip,HeightMm=h,ConicalFaces=cones,EdgeSamples=samples,Cap=cap,Connection=connection,IncludedAngleDegrees=120,DrawingAngleAndDepthAccepted=false};
    }
    public object BuildExperimentalRadialPoint()
    {
        var prior=new Dictionary<string,bool>();
        for(Feature? f=(Feature?)_model!.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())
        {if(f.Name.StartsWith("EXPERIMENT_AA_RADIAL"))throw new InvalidOperationException("Radial trial exists.");prior.Add(f.Name,TiffSuppressed(f));}
        const string old="TIFF_REAR_AA_D6_X132";
        if(!prior.TryGetValue(old,out bool suppressed)||suppressed)throw new InvalidOperationException("Missing radial drill.");
        double before=CurrentTrialVolume(),coarse=RadialPointAddedVolume(80),fine=RadialPointAddedVolume(160),h=3/Math.Tan(Math.PI/3);
        if(Math.Abs(coarse-fine)>.01)throw new InvalidOperationException("Radial integral unconverged.");
        if(!TiffFeature(old).SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))throw new InvalidOperationException("Radial suppression failed.");
        _model.ForceRebuild3(false);double a=2*Math.PI/3;double[] tangent=[0,Math.Cos(a),Math.Sin(a)];
        CutChannelCylinder("EXPERIMENT_AA_RADIAL_CYLINDER",RadialPointAt(55),RadialPointAt(RadialEndR+h),3,tangent);
        CutChannelCylinder("EXPERIMENT_AA_RADIAL_POINT120",RadialPointAt(RadialEndR+2*h),RadialPointAt(RadialEndR),3,tangent,true,Math.PI/3,exactApex:true);
        double final=CurrentTrialVolume();
        if(Math.Abs(final-before-fine)>.05)throw new InvalidOperationException($"Radial volume mismatch:{final-before:R}/{fine:R}");
        CheckTiffSuppressionStates(prior,[old]);var point=InspectExperimentalRadialPoint();var health=InspectTiffFeatureHealth();
        return new{BeforeMm3=before,FinalMm3=final,AddedMm3=final-before,IndependentIntegralMm3=fine,CoarseIntegralMm3=coarse,VolumeBudgetMm3=.05,Point=point,Health=health,
            ExperimentalRadialPointBuilt=true,ExperimentalFeedPointsBuilt=true,ExperimentalChannelPointsBuilt=true,ExperimentalM6PointsBuilt=true,ExperimentalLProfileCompleted=true,ExperimentalLExitBuilt=true,
            PpChamferBuilt=true,M6PatternBuilt=true,RearCollarRebuilt=true,OuterProfileRebuilt=true,AaChannelRebuilt=true,
            RightM8Rebuilt=true,LeftM8Rebuilt=true,Bore68StartRebuilt=true,Bore80PartialRebuilt=true,Bore80Restored=true,DetailLCoreBuilt=true,
            IsManufacturingReady=false,DrawingInterpretationAccepted=false};
    }
}

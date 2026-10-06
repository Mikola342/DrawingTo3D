using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public static void TestExperimentalChannelPoints()
    {
        // Invalid datum combinations must fail before touching COM.
        var service=new SolidWorksService();
        int rejected=0;
        try {service.InspectTiffAaChannel(false,true);} catch(ArgumentException){rejected++;}
        try {service.InspectTiffIiChannel(false,true);} catch(ArgumentException){rejected++;}
        if(rejected!=2)throw new InvalidOperationException("Invalid point datum was accepted.");
        foreach(bool aa in new[]{false,true})
        {
            var p=ChannelPoint(aa);double h=3.15/Math.Tan(Math.PI/3);
            if(Math.Abs(p.Axis.Sum(v=>v*v)-1)>1e-12 || Math.Abs(p.Axis.Zip(p.Tangent).Sum(t=>t.First*t.Second))>1e-12)
                throw new InvalidOperationException("Non-orthonormal channel frame.");
            if(Math.Abs(p.Base.Select((v,i)=>Math.Pow(v-p.Tip[i],2)).Sum()-h*h)>1e-10)
                throw new InvalidOperationException("Channel point height mismatch.");
            double length=(265-p.Tip[0])/p.Axis[0];
            if(aa ? Math.Abs(p.Tip[0]-128.75)>1e-10 : Math.Abs(length-110)>1e-10)
                throw new InvalidOperationException("Channel depth changed.");
            double coarse=ChannelPointAddedVolume(aa,80),fine=ChannelPointAddedVolume(aa,160);
            if(!double.IsFinite(fine) || fine<=0 || fine>=2*Math.PI*3.15*3.15*h/3 || Math.Abs(coarse-fine)>.01)
                throw new InvalidOperationException("Clipped channel-point integral invalid.");
            Console.WriteLine($"[TEST OK] Experimental channel point {(aa?"AA":"II")}: depth={length:R}, added={fine:R}, refinement={Math.Abs(fine-coarse):R}. Assumed120deg, not TIFF acceptance.");
        }
    }
    private static (double[] Tip, double[] Axis, double[] Tangent, double[] Base) ChannelPoint(bool aa)
    {
        double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180;
        double x=aa?128.75:265-110*Math.Cos(b),r=51+(x-265)*Math.Tan(b),h=3.15/Math.Tan(Math.PI/3);
        double[] tip=[x,r*Math.Sin(a),-r*Math.Cos(a)],axis=[Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)];
        return (tip,axis,[0,Math.Cos(a),Math.Sin(a)],tip.Select((v,i)=>v+h*axis[i]).ToArray());
    }

    // Independent midpoint integration of the material restored between a flat
    // end and a cone, excluding the pre-existing D62 bore / finite AA radial drill.
    private static double ChannelPointAddedVolume(bool aa,int slices)
    {
        var p=ChannelPoint(aa); double h=3.15/Math.Tan(Math.PI/3),sum=0;
        double[] v=[-p.Axis[1]*p.Tangent[2]+p.Axis[2]*p.Tangent[1],
            -p.Axis[2]*p.Tangent[0]+p.Axis[0]*p.Tangent[2],-p.Axis[0]*p.Tangent[1]+p.Axis[1]*p.Tangent[0]];
        double a=(aa?120:60)*Math.PI/180,radialEnd=51+(132-265)*Math.Tan(4*Math.PI/180);
        int angles=4*slices;
        for(int i=0;i<slices;i++)
        {
            double s=h*(i+.5)/slices,inner=s*Math.Tan(Math.PI/3),area=Math.PI*(3.15*3.15-inner*inner);
            for(int j=0;j<slices;j++)
            {
                double r=Math.Sqrt(inner*inner+(3.15*3.15-inner*inner)*(j+.5)/slices);
                for(int k=0;k<angles;k++)
                {
                    double t=2*Math.PI*(k+.5)/angles,co=Math.Cos(t),si=Math.Sin(t);
                    double x=p.Tip[0]+s*p.Axis[0]+r*(co*p.Tangent[0]+si*v[0]);
                    double y=p.Tip[1]+s*p.Axis[1]+r*(co*p.Tangent[1]+si*v[1]);
                    double z=p.Tip[2]+s*p.Axis[2]+r*(co*p.Tangent[2]+si*v[2]);
                    if(y*y+z*z<31*31)continue;
                    double along=y*Math.Sin(a)-z*Math.Cos(a),side=y*Math.Cos(a)+z*Math.Sin(a);
                    if(aa && along>=radialEnd && along<=55 && (x-132)*(x-132)+side*side<9)continue;
                    sum+=area*h/(slices*(double)slices*angles);
                }
            }
        }
        return sum;
    }

    public object InspectExperimentalChannelPoints(bool feedPoints = false, bool radialPoint = false)
    {
        var results=new List<object>();
        foreach(bool aa in new[]{false,true})
        {
            var p=ChannelPoint(aa);double h=3.15/Math.Tan(Math.PI/3);string key=aa?"AA":"II";
            if(!TiffSuppressed(TiffFeature(aa?"TIFF_REAR_AA_D63_4deg":"TIFF_REAR_II_D63_10deg")))
                throw new InvalidOperationException("Old flat channel end is active.");
            var f=TiffFeature($"EXPERIMENT_{key}_POINT120");
            if(TiffSuppressed(f))throw new InvalidOperationException("Channel point suppressed.");
            int cones=0,samples=0;double max=double.NegativeInfinity;bool apex=false;
            foreach(Face2 face in (object[])f.GetFaces())
            {
                var surface=(Surface)face.GetSurface();if(!surface.IsCone())continue;
                cones++;var c=(double[])surface.ConeParams2;
                if(Math.Abs(Math.Abs(c[7])-Math.PI/3)>1e-8 || Math.Abs(Math.Abs(p.Axis.Select((u,i)=>u*c[i+3]).Sum())-1)>1e-8)
                    throw new InvalidOperationException("Channel point angle/axis mismatch.");
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var b=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(b.UMinValue,b.UMaxValue);
                    for(int j=0;j<=64;j++)
                    {
                        var q=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*j/64,0);
                        if(q.Length!=4 || !q.Take(3).All(double.IsFinite) || (BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                            throw new InvalidOperationException("Channel point edge evaluation failed.");
                        var d=p.Tip.Select((v,i)=>q[i]*1000-v).ToArray();double s=d.Zip(p.Axis).Sum(t=>t.First*t.Second);
                        double r=Math.Sqrt(d.Select((v,i)=>Math.Pow(v-s*p.Axis[i],2)).Sum());
                        if(s<-.0001 || s>h+.0001 || Math.Abs(r-s*Math.Tan(Math.PI/3))>.0001)
                            throw new InvalidOperationException("Channel point envelope mismatch.");
                        max=Math.Max(max,s);samples++;
                    }
                }
            }
            var body=CopyPilotBody();
            foreach(Vertex vertex in ((object[]?)body.GetVertices()??[]).OfType<Vertex>())
            {
                var q=(double[])vertex.GetPoint();
                if(p.Tip.Select((v,i)=>Math.Pow(q[i]*1000-v,2)).Sum()<1e-8)apex=true;
            }
            GC.KeepAlive(body);
            if(cones!=1 || samples==0 || !apex || Math.Abs(max-h)>.0001)throw new InvalidOperationException($"Channel point extent mismatch:{key}/{cones}/{samples}/{apex}/{max}");
            results.Add(new {Channel=key,Tip=p.Tip,Base=p.Base,HeightMm=h,EdgeSamples=samples,ApexChecked=apex});
        }
        var ii=InspectTiffIiChannel(true,true);var aaCheck=InspectTiffAaChannel(true,true,feedPoints,radialPoint);
        Console.WriteLine("[EXPERIMENT CHANNEL POINTS OK]120deg; old tip stations retained; both connections retained.");
        return new {Points=results,II=ii,AA=aaCheck,ExperimentalChannelPointsBuilt=true,DrawingEndStationsAccepted=false,IncludedAngleDegrees=120};
    }

    public object BuildExperimentalChannelPoints()
    {
        var prior=new Dictionary<string,bool>();
        for(Feature? f=(Feature?)_model!.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())
        {if(f.Name.StartsWith("EXPERIMENT_II_POINT")||f.Name.StartsWith("EXPERIMENT_AA_POINT"))throw new InvalidOperationException("Channel trial already exists.");prior.Add(f.Name,TiffSuppressed(f));}
        string[] replaced=["TIFF_REAR_II_D63_10deg","TIFF_REAR_AA_D63_4deg"];
        double before=CurrentTrialVolume();var steps=new List<object>();
        foreach(bool aa in new[]{false,true})
        {
            string old=aa?replaced[1]:replaced[0],key=aa?"AA":"II";var p=ChannelPoint(aa);
            if(!prior.TryGetValue(old,out bool suppressed)||suppressed)throw new InvalidOperationException("Missing old channel.");
            double start=CurrentTrialVolume(),coarse=ChannelPointAddedVolume(aa,80),fine=ChannelPointAddedVolume(aa,160);
            if(Math.Abs(coarse-fine)>.01)throw new InvalidOperationException("Channel integral not converged.");
            if(!TiffFeature(old).SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))throw new InvalidOperationException("Channel suppression failed.");
            _model.ForceRebuild3(false);
            double t=(267-p.Tip[0])/p.Axis[0];double[] from=p.Tip.Select((v,i)=>v+t*p.Axis[i]).ToArray();
            CutChannelCylinder($"EXPERIMENT_{key}_POINT_CYLINDER",from,p.Base,3.15,p.Tangent);
            double h=3.15/Math.Tan(Math.PI/3);
            CutChannelCylinder($"EXPERIMENT_{key}_POINT120",p.Base.Select((v,i)=>v+h*p.Axis[i]).ToArray(),p.Tip,3.15,p.Tangent,true,Math.PI/3,exactApex:true);
            double actual=CurrentTrialVolume()-start;
            if(Math.Abs(actual-fine)>.05)throw new InvalidOperationException($"Channel volume mismatch:{key}/{actual:R}/{fine:R}");
            steps.Add(new {Channel=key,AddedMm3=actual,IndependentIntegralMm3=fine,CoarseIntegralMm3=coarse,VolumeBudgetMm3=.05});
            Console.WriteLine($"[CHANNEL POINT BUILT]{key} added={actual:R},expected={fine:R}");
        }
        CheckTiffSuppressionStates(prior,replaced);var points=InspectExperimentalChannelPoints();var health=InspectTiffFeatureHealth();
        return new {BeforeMm3=before,FinalMm3=CurrentTrialVolume(),Steps=steps,Points=points,Health=health,
            ExperimentalChannelPointsBuilt=true,ExperimentalM6PointsBuilt=true,ExperimentalLProfileCompleted=true,ExperimentalLExitBuilt=true,
            PpChamferBuilt=true,M6PatternBuilt=true,RearCollarRebuilt=true,OuterProfileRebuilt=true,AaChannelRebuilt=true,
            RightM8Rebuilt=true,LeftM8Rebuilt=true,Bore68StartRebuilt=true,Bore80PartialRebuilt=true,Bore80Restored=true,DetailLCoreBuilt=true,
            IsManufacturingReady=false,DrawingInterpretationAccepted=false};
    }
}

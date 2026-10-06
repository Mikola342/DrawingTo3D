using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private static (double[] Tip,double[] Axis,double[] Tangent,double[] MainAxis,double[] MainOrigin) FeedPoint(bool aa)
    {
        double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180,f=7*Math.PI/180;
        double crossing=(51+(257.5-127*Math.Tan(f)-265)*Math.Tan(b))/(1-Math.Tan(f)*Math.Tan(b));
        double r=crossing-1,x=257.5+(r-127)*Math.Tan(f);
        return ([x,r*Math.Sin(a),-r*Math.Cos(a)], [Math.Sin(f),Math.Cos(f)*Math.Sin(a),-Math.Cos(f)*Math.Cos(a)],
            [0,Math.Cos(a),Math.Sin(a)],[Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)],
            [265,51*Math.Sin(a),-51*Math.Cos(a)]);
    }
    private static double FeedPointAddedVolume(bool aa,int n)
    {
        var p=FeedPoint(aa);double h=3.4/Math.Tan(Math.PI/3),sum=0;
        double[] v=[-Math.Cos(7*Math.PI/180),p.Axis[0]*Math.Sin((aa?120:60)*Math.PI/180),-p.Axis[0]*Math.Cos((aa?120:60)*Math.PI/180)];
        for(int i=0;i<n;i++)
        {
            double s=h*(i+.5)/n,inner=s*Math.Tan(Math.PI/3),weight=Math.PI*(3.4*3.4-inner*inner)*h/(n*(double)n*4*n);
            for(int j=0;j<n;j++)
            {
                double r=Math.Sqrt(inner*inner+(3.4*3.4-inner*inner)*(j+.5)/n);
                for(int k=0;k<4*n;k++)
                {
                    double t=2*Math.PI*(k+.5)/(4*n);
                    double dx=p.Tip[0]+s*p.Axis[0]+r*(Math.Cos(t)*p.Tangent[0]+Math.Sin(t)*v[0])-p.MainOrigin[0];
                    double dy=p.Tip[1]+s*p.Axis[1]+r*(Math.Cos(t)*p.Tangent[1]+Math.Sin(t)*v[1])-p.MainOrigin[1];
                    double dz=p.Tip[2]+s*p.Axis[2]+r*(Math.Cos(t)*p.Tangent[2]+Math.Sin(t)*v[2])-p.MainOrigin[2];
                    double along=dx*p.MainAxis[0]+dy*p.MainAxis[1]+dz*p.MainAxis[2];
                    double distance2=Math.Pow(dx-along*p.MainAxis[0],2)+Math.Pow(dy-along*p.MainAxis[1],2)+Math.Pow(dz-along*p.MainAxis[2],2);
                    if(distance2>3.15*3.15)sum+=weight;
                }
            }
        }
        return sum;
    }
    public static void TestExperimentalFeedPoints()
    {
        // Infinite-cylinder containment alone is insufficient: the new AA cone
        // starts atX130.56 whereas the radial disk reachesX129. Keep D6 pending.
        double radialBound=3+3/Math.Tan(Math.PI/3)*Math.Sin(4*Math.PI/180);
        if(radialBound>=3.15)throw new InvalidOperationException("Unexpected infinite-cylinder bound.");
        foreach(bool aa in new[]{false,true})
        {
            double coarse=FeedPointAddedVolume(aa,80),fine=FeedPointAddedVolume(aa,160);
            if(!double.IsFinite(fine)||fine<=0||Math.Abs(fine-coarse)>.01)throw new InvalidOperationException("Feed point integral failed.");
            Console.WriteLine($"[TEST OK] Feed point {(aa?"AA":"II")}: added={fine:R}, refinement={Math.Abs(fine-coarse):R}.");
        }
    }

    public object InspectExperimentalFeedPoints(bool radialPoint = false)
    {
        var results=new List<object>();double h=3.4/Math.Tan(Math.PI/3);
        foreach(bool aa in new[]{false,true})
        {
            var p=FeedPoint(aa);string key=aa?"AA":"II",old=aa?"TIFF_REAR_AA_D68_7deg":"TIFF_REAR_II_D68_feed_7deg";
            if(!TiffSuppressed(TiffFeature(old)))throw new InvalidOperationException("Old feed active.");
            var feature=TiffFeature($"EXPERIMENT_{key}_FEED_POINT120");
            if(TiffSuppressed(feature))throw new InvalidOperationException("Feed point suppressed.");
            int cones=0,samples=0;double max=double.NegativeInfinity,min=double.PositiveInfinity;
            foreach(Face2 face in (object[])feature.GetFaces())
            {
                var surface=(Surface)face.GetSurface();if(!surface.IsCone())continue;
                cones++;var c=(double[])surface.ConeParams2;
                if(Math.Abs(Math.Abs(c[7])-Math.PI/3)>1e-8||Math.Abs(Math.Abs(p.Axis.Select((u,i)=>u*c[i+3]).Sum())-1)>1e-8)throw new InvalidOperationException("Feed cone axis/angle mismatch.");
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var b=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(b.UMinValue,b.UMaxValue);
                    for(int j=0;j<=64;j++)
                    {
                        var q=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*j/64,0);
                        if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)throw new InvalidOperationException("Feed edge evaluation failed.");
                        var d=p.Tip.Select((v,i)=>q[i]*1000-v).ToArray();double s=d.Zip(p.Axis).Sum(t=>t.First*t.Second);
                        double r=Math.Sqrt(d.Select((v,i)=>Math.Pow(v-s*p.Axis[i],2)).Sum());
                        if(s<-.0001||s>h+.0001||Math.Abs(r-s*Math.Tan(Math.PI/3))>.0001)throw new InvalidOperationException("Feed cone envelope mismatch.");
                        max=Math.Max(max,s);min=Math.Min(min,s);samples++;
                    }
                }
            }
            if(cones==0||samples==0||Math.Abs(max-h)>.0001)throw new InvalidOperationException("Feed cone boundary missing.");
            results.Add(new {Channel=key,TipInsideMainChannel=p.Tip,HeightMm=h,MinimumVisibleStationMm=min,MaximumVisibleStationMm=max,EdgeSamples=samples});
        }
        var ii=InspectTiffIiChannel(true,true);var aaResult=InspectTiffAaChannel(true,true,true,radialPoint);
        return new {Points=results,II=ii,AA=aaResult,RadialEnd=InspectRadialAaEnd(radialPoint),IncludedAngleDegrees=120,DrawingAngleAndDepthAccepted=false};
    }
    private object InspectRadialAaEnd(bool requireNoFlatCap = false)
    {
        double a=2*Math.PI/3,r=51+(132-265)*Math.Tan(4*Math.PI/180);
        double[] centre=[132,r*Math.Sin(a),-r*Math.Cos(a)],axis=[0,Math.Sin(a),-Math.Cos(a)];
        int caps=0,samples=0;var body=CopyPilotBody();
        foreach(Face2 face in (object[])body.GetFaces())
        {
            var surface=(Surface)face.GetSurface();if(!surface.IsPlane())continue;
            var p=(double[])surface.PlaneParams;
            double dot=axis.Select((v,i)=>v*p[i]).Sum(),distance=axis.Select((v,i)=>v*(p[i+3]*1000-centre[i])).Sum();
            if(Math.Abs(Math.Abs(dot)-1)>1e-8||Math.Abs(distance)>.0001)continue;
            int faceSamples=0;bool inside=true;
            foreach(Edge edge in (object[])face.GetEdges())
            {
                var b=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(b.UMinValue,b.UMaxValue);
                for(int j=0;j<=32;j++)
                {
                    var q=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*j/32,0);
                    if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)throw new InvalidOperationException("Radial cap evaluation failed.");
                    double norm2=centre.Select((v,i)=>Math.Pow(q[i]*1000-v,2)).Sum();
                    if(norm2>9.001)inside=false;faceSamples++;
                }
            }
            if(inside&&faceSamples>0){caps++;samples+=faceSamples;}
        }
        GC.KeepAlive(body);
        if(requireNoFlatCap && caps!=0)throw new InvalidOperationException("Residual radial flat end remains.");
        Console.WriteLine($"[RADIAL END AUDIT] AA D6: residual flat caps={caps},samples={samples}; experimental point={requireNoFlatCap}.");
        return new{ResidualFlatCapFaces=caps,EdgeSamples=samples,EndCentre=centre,ExperimentalRadialPointBuilt=requireNoFlatCap,RadialEndAccepted=false};
    }
    public object BuildExperimentalFeedPoints()
    {
        var prior=new Dictionary<string,bool>();
        for(Feature? f=(Feature?)_model!.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())
        {if(f.Name.StartsWith("EXPERIMENT_AA_FEED")||f.Name.StartsWith("EXPERIMENT_II_FEED"))throw new InvalidOperationException("Feed trial exists.");prior.Add(f.Name,TiffSuppressed(f));}
        string[] replaced=["TIFF_REAR_II_D68_feed_7deg","TIFF_REAR_AA_D68_7deg"];
        double before=CurrentTrialVolume(),h=3.4/Math.Tan(Math.PI/3);var steps=new List<object>();
        foreach(bool aa in new[]{false,true})
        {
            var p=FeedPoint(aa);string key=aa?"AA":"II",old=aa?replaced[1]:replaced[0];
            if(!prior.TryGetValue(old,out bool suppressed)||suppressed)throw new InvalidOperationException("Missing feed.");
            double coarse=FeedPointAddedVolume(aa,80),fine=FeedPointAddedVolume(aa,160),start=CurrentTrialVolume();
            if(Math.Abs(coarse-fine)>.01)throw new InvalidOperationException("Feed integration unconverged.");
            if(!TiffFeature(old).SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))throw new InvalidOperationException("Feed suppression failed.");
            _model.ForceRebuild3(false);
            double tipR=Math.Sqrt(p.Tip[1]*p.Tip[1]+p.Tip[2]*p.Tip[2]),length=(130-tipR)/Math.Cos(7*Math.PI/180);
            double[] startPoint=p.Tip.Select((v,i)=>v+length*p.Axis[i]).ToArray(),basePoint=p.Tip.Select((v,i)=>v+h*p.Axis[i]).ToArray();
            CutChannelCylinder($"EXPERIMENT_{key}_FEED_CYLINDER",startPoint,basePoint,3.4,p.Tangent);
            CutChannelCylinder($"EXPERIMENT_{key}_FEED_POINT120",p.Tip.Select((v,i)=>v+2*h*p.Axis[i]).ToArray(),p.Tip,3.4,p.Tangent,true,Math.PI/3,exactApex:true);
            double added=CurrentTrialVolume()-start;
            if(Math.Abs(added-fine)>.05)throw new InvalidOperationException($"Feed volume mismatch:{key}/{added:R}/{fine:R}");
            steps.Add(new{Channel=key,AddedMm3=added,IndependentIntegralMm3=fine,CoarseIntegralMm3=coarse,VolumeBudgetMm3=.05});
            Console.WriteLine($"[FEED POINT BUILT]{key}:added={added:R},expected={fine:R}");
        }
        CheckTiffSuppressionStates(prior,replaced);var points=InspectExperimentalFeedPoints();var health=InspectTiffFeatureHealth();
        return new{BeforeMm3=before,FinalMm3=CurrentTrialVolume(),Steps=steps,Points=points,Health=health,
            ExperimentalFeedPointsBuilt=true,ExperimentalChannelPointsBuilt=true,ExperimentalM6PointsBuilt=true,ExperimentalLProfileCompleted=true,ExperimentalLExitBuilt=true,
            PpChamferBuilt=true,M6PatternBuilt=true,RearCollarRebuilt=true,OuterProfileRebuilt=true,AaChannelRebuilt=true,
            RightM8Rebuilt=true,LeftM8Rebuilt=true,Bore68StartRebuilt=true,Bore80PartialRebuilt=true,Bore80Restored=true,DetailLCoreBuilt=true,
            IsManufacturingReady=false,DrawingInterpretationAccepted=false};
    }
}

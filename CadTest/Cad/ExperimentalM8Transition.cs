using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private static double M8TransitionHeight => (Drawing.M8PilotStudy.Radius-3.15)/Math.Tan(Math.PI/3);
    private static double M8TransitionVolume => Math.PI*M8TransitionHeight*
        (Math.Pow(Drawing.M8PilotStudy.Radius,2)+Drawing.M8PilotStudy.Radius*3.15-2*3.15*3.15)/3;
    public static void TestM8Transition()
    {
        double sum=0,h=M8TransitionHeight,r=Drawing.M8PilotStudy.Radius;
        for(int i=0;i<10000;i++)
        {double s=h*(i+.5)/10000,q=r-s*Math.Tan(Math.PI/3);sum+=Math.PI*(q*q-3.15*3.15)*h/10000;}
        if(h<=0||Math.Abs(sum-M8TransitionVolume)>1e-9)throw new InvalidOperationException("M8 transition integral mismatch.");
        foreach(double degrees in new[]{4.0,10.0})
        {
            var e=Drawing.CurrentM8EnvelopeStudy.Evaluate(degrees);
            if(e.CurrentFeedClearanceMm<h)throw new InvalidOperationException("M8 transition reaches feed.");
        }
        Console.WriteLine($"[TEST OK] M8 experimental120deg pilot transition:h={h:R},removed each={M8TransitionVolume:R}; no thread-length change.");
    }
    private static (double[] Base,double[] Axis,double[] Tangent) M8TransitionFrame(bool aa)
    {
        double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180,r=51-6*Math.Tan(b);
        return ([259,r*Math.Sin(a),-r*Math.Cos(a)],[Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)],
            [0,Math.Cos(a),Math.Sin(a)]);
    }
    public object InspectExperimentalM8Transitions()
    {
        var results=new List<object>();double h=M8TransitionHeight,r=Drawing.M8PilotStudy.Radius;
        foreach(bool aa in new[]{false,true})
        {
            var p=M8TransitionFrame(aa);string key=aa?"AA":"II";
            var feature=TiffFeature($"EXPERIMENT_{key}_M8_TO_D63_120");
            if(TiffSuppressed(feature))throw new InvalidOperationException("M8 transition suppressed.");
            int cones=0,samples=0;double min=double.PositiveInfinity,max=double.NegativeInfinity;
            foreach(Face2 face in (object[])feature.GetFaces())
            {
                var surface=(Surface)face.GetSurface();if(!surface.IsCone())continue;
                cones++;var c=(double[])surface.ConeParams2;
                if(Math.Abs(Math.Abs(c[7])-Math.PI/3)>1e-8||Math.Abs(Math.Abs(p.Axis.Select((u,i)=>u*c[i+3]).Sum())-1)>1e-8)
                    throw new InvalidOperationException("M8 transition axis/angle mismatch.");
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var b=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(b.UMinValue,b.UMaxValue);
                    for(int j=0;j<=64;j++)
                    {
                        var q=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*j/64,0);
                        if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                            throw new InvalidOperationException("M8 transition edge evaluation failed.");
                        var d=p.Base.Select((v,i)=>q[i]*1000-v).ToArray();double s=-d.Zip(p.Axis).Sum(t=>t.First*t.Second);
                        double radius=Math.Sqrt(d.Select((v,i)=>Math.Pow(v+s*p.Axis[i],2)).Sum());
                        if(s<-.0001||s>h+.0001||Math.Abs(radius-(r-s*Math.Tan(Math.PI/3)))>.0001)
                            throw new InvalidOperationException("M8 transition envelope mismatch.");
                        min=Math.Min(min,s);max=Math.Max(max,s);samples++;
                    }
                }
            }
            if(cones!=1||samples==0||Math.Abs(min)>.0001||Math.Abs(max-h)>.0001)
                throw new InvalidOperationException($"M8 transition extent mismatch:{key}/{cones}/{min}/{max}");
            results.Add(new{Channel=key,Base=p.Base,HeightMm=h,MinimumStationMm=min,MaximumStationMm=max,EdgeSamples=samples});
        }
        Console.WriteLine("[M8 TRANSITIONS OK] both pilot/D6.3 cones; temporary thread length unchanged.");
        return new{Transitions=results,IncludedAngleDegrees=120,ThreadLengthChanged=false,ThreadRunoutAccepted=false,DrawingAngleAccepted=false};
    }
    public object BuildExperimentalM8Transitions()
    {
        TestM8Transition();var prior=new Dictionary<string,bool>();
        for(Feature? f=(Feature?)_model!.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())
        {if(f.Name.Contains("_M8_TO_D63_120"))throw new InvalidOperationException("M8 transition exists.");prior.Add(f.Name,TiffSuppressed(f));}
        double before=CurrentTrialVolume(),h=M8TransitionHeight;var steps=new List<object>();
        foreach(bool aa in new[]{false,true})
        {
            var p=M8TransitionFrame(aa);double start=CurrentTrialVolume();
            CutChannelCylinder($"EXPERIMENT_{(aa?"AA":"II")}_M8_TO_D63_120",
                p.Base.Select((v,i)=>v+h*p.Axis[i]).ToArray(),p.Base.Select((v,i)=>v-h*p.Axis[i]).ToArray(),
                Drawing.M8PilotStudy.Radius,p.Tangent,true,Math.PI/3);
            double removed=start-CurrentTrialVolume();
            if(Math.Abs(removed-M8TransitionVolume)>.003)throw new InvalidOperationException($"M8 transition volume mismatch:{removed:R}/{M8TransitionVolume:R}");
            steps.Add(new{Channel=aa?"AA":"II",RemovedMm3=removed,ExpectedMm3=M8TransitionVolume,VolumeBudgetMm3=.003});
        }
        CheckTiffSuppressionStates(prior,[]);var transitions=InspectExperimentalM8Transitions();var health=InspectTiffFeatureHealth();
        return new{BeforeMm3=before,FinalMm3=CurrentTrialVolume(),Steps=steps,Transitions=transitions,Health=health,
            ExperimentalM8TransitionsBuilt=true,ExperimentalRadialPointBuilt=true,ExperimentalFeedPointsBuilt=true,ExperimentalChannelPointsBuilt=true,ExperimentalM6PointsBuilt=true,ExperimentalLProfileCompleted=true,ExperimentalLExitBuilt=true,
            PpChamferBuilt=true,M6PatternBuilt=true,RearCollarRebuilt=true,OuterProfileRebuilt=true,AaChannelRebuilt=true,
            RightM8Rebuilt=true,LeftM8Rebuilt=true,Bore68StartRebuilt=true,Bore80PartialRebuilt=true,Bore80Restored=true,DetailLCoreBuilt=true,
            IsManufacturingReady=false,DrawingInterpretationAccepted=false};
    }
}

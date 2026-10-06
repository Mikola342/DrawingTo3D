using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    // Experimental axial datum t=0 at R127, inherited from the existing pilot.
    private const double KgChamferEnd=1.6,KgChamferIntercept=5.95;
    private static double KgChamferIntegral(int n)
    {
        if(n<100||n>3200)throw new ArgumentOutOfRangeException(nameof(n));
        double result=0,b=7*Math.PI/180;
        for(int i=0;i<n;i++)
        {
            double t=-2+3.6*(i+.5)/n,outer=KgChamferIntercept-t,inner=4.4-t/32,y=127-t*Math.Cos(b);
            for(int j=0;j<n;j++)
            {
                double phi=2*Math.PI*(j+.5)/n,k=-Math.Sin(b)*Math.Cos(phi),z=Math.Sin(phi);
                double aa=k*k+z*z,bb=2*y*k,cc=y*y-127.5*127.5,disc=bb*bb-4*aa*cc;
                if(disc<=0)continue;
                double lo=Math.Max(inner,(-bb-Math.Sqrt(disc))/(2*aa)),hi=Math.Min(outer,(-bb+Math.Sqrt(disc))/(2*aa));
                if(hi>lo)result+=(hi*hi-lo*lo)/2*3.6/n*2*Math.PI/n;
            }
        }
        return result;
    }
    public static void TestKgEntryChamfer()
    {
        double a=KgChamferIntegral(800),b=KgChamferIntegral(1600);
        if(!double.IsFinite(b)||b<=0||Math.Abs(a-b)>.02||
            Math.Abs(KgChamferIntercept-KgChamferEnd-(4.4-KgChamferEnd/32))>1e-12)
            throw new InvalidOperationException("KG entry integral/join test failed.");
        foreach(int n in new[]{-1,0,99,3201})
        {bool rejected=false;try{KgChamferIntegral(n);}catch(ArgumentOutOfRangeException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Invalid KG integration count accepted.");}
        Console.WriteLine($"[TEST OK] Experimental KG entries:integral={b:R},refinement={Math.Abs(a-b):R};4 invalid counts rejected;datum unaccepted.");
    }
    public object BuildKgEntryChamfers()
    {
        _=InspectKgPrepTrial();double before=CurrentTrialVolume();var steps=new List<object>();
        double coarse=KgChamferIntegral(800),fine=KgChamferIntegral(1600);
        if(Math.Abs(coarse-fine)>.02)throw new InvalidOperationException("KG chamfer integral not converged.");
        foreach(double degrees in new[]{60.0,120.0})
        {
            double initial=CurrentTrialVolume(),a=degrees*Math.PI/180;
            CutChannelCylinder($"EXPERIMENT_KG_ENTRY_16X45_{degrees}",Drawing.KgPrepStudy.Point(-5.6,degrees),
                Drawing.KgPrepStudy.Point(KgChamferEnd,degrees),KgChamferIntercept+2,[0,Math.Cos(a),Math.Sin(a)],true);
            double removed=initial-CurrentTrialVolume();
            if(Math.Abs(removed-fine)>.05)throw new InvalidOperationException($"KG entry volume mismatch:{removed:R}/{fine:R}.");
            steps.Add(new{BranchDegrees=degrees,RemovedMm3=removed,IndependentIntegralMm3=fine,CoarseIntegralMm3=coarse,VolumeBudgetMm3=.05});
            Console.WriteLine($"[KG ENTRY BUILT] {degrees}:removed={removed:R},integral={fine:R}.");
        }
        return new{BeforeMm3=before,FinalMm3=CurrentTrialVolume(),Steps=steps,Geometry=InspectKgEntryChamfers(),Health=InspectTiffFeatureHealth(),
            ExperimentalKgEntriesBuilt=true,KgThreadBuilt=false,EntryDatumAccepted=false,DrawingInterpretationAccepted=false};
    }
    public object InspectKgEntryChamfers()
    {
        var result=new List<object>();
        foreach(double degrees in new[]{60.0,120.0})
        {
            var f=TiffFeature($"EXPERIMENT_KG_ENTRY_16X45_{degrees}");
            if(TiffSuppressed(f))throw new InvalidOperationException("KG entry suppressed.");
            double a=degrees*Math.PI/180,b=7*Math.PI/180;var origin=Drawing.KgPrepStudy.Point(0,degrees);
            double[] axis=[-Math.Sin(b),-Math.Cos(b)*Math.Sin(a),Math.Cos(b)*Math.Cos(a)];
            int cones=0,samples=0;double maxT=double.NegativeInfinity,residual=0;
            foreach(Face2 face in (object[])f.GetFaces())
            {
                var s=(Surface)face.GetSurface();if(!s.IsCone())continue;
                var c=(double[])s.ConeParams2;
                if(Math.Abs(Math.Abs(c[7])-Math.PI/4)>1e-8)throw new InvalidOperationException("KG entry angle mismatch.");
                cones++;
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var e=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(e.UMinValue,e.UMaxValue);
                    for(int j=0;j<=64;j++)
                    {
                        var q=(double[])edge.Evaluate2(e.UMinValue+(e.UMaxValue-e.UMinValue)*j/64,0);
                        if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)throw new InvalidOperationException("KG entry evaluation failed.");
                        var d=origin.Select((v,i)=>q[i]*1000-v).ToArray();double t=d.Zip(axis).Sum(p=>p.First*p.Second);
                        double r=Math.Sqrt(d.Select((v,i)=>Math.Pow(v-t*axis[i],2)).Sum());
                        residual=Math.Max(residual,Math.Abs(r-(KgChamferIntercept-t)));maxT=Math.Max(maxT,t);samples++;
                    }
                }
            }
            if(cones!=1||samples==0||residual>.0001||Math.Abs(maxT-1.6)>.0001)throw new InvalidOperationException($"KG entry bounds:{cones}/{samples}/{residual}/{maxT}.");
            result.Add(new{BranchDegrees=degrees,EdgeSamples=samples,MaximumTmm=maxT,MaximumResidualMm=residual});
        }
        return new{Entries=result,Pilot=InspectKgPrepTrial(),EntryDatumAccepted=false,KgThreadBuilt=false};
    }
}

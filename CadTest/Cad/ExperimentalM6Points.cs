using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private static double M6PointBaseX => 243 + Drawing.M6PointStudy.Height(TiffM6PilotRadius,120);
    public object InspectExperimentalM6Points()
    {
        var results = new List<object>();
        for (int i=0;i<4;i++)
        {
            var centre=TiffM6Centres[i];
            if (!TiffSuppressed(TiffFeature($"TIFF_M6_{i+1}_DEEP_PILOT_PENDING_POINT")))
                throw new InvalidOperationException("Old flat M6 bottom active.");
            var f=TiffFeature($"EXPERIMENT_M6_{i+1}_POINT120");
            if (TiffSuppressed(f)) throw new InvalidOperationException("M6 point suppressed.");
            int cones=0,n=0; double min=double.PositiveInfinity,max=double.NegativeInfinity;
            foreach(Face2 face in (object[])f.GetFaces())
            {
                var s=(Surface)face.GetSurface(); if(!s.IsCone()) continue;
                cones++; var c=(double[])s.ConeParams2;
                if(Math.Abs(Math.Abs(c[7])-Math.PI/3)>1e-8 || Math.Abs(c[1]*1000-centre.Y)>1e-5 ||
                    Math.Abs(c[2]*1000-centre.Z)>1e-5 || Math.Abs(Math.Abs(c[3])-1)>1e-8)
                    throw new InvalidOperationException("M6 point axis/angle mismatch.");
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var b=edge.GetCurveParams3(); Drawing.M12Study.ValidateEdgeInterval(b.UMinValue,b.UMaxValue);
                    for(int j=0;j<=32;j++)
                    {
                        var p=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*j/32,0);
                        if(p.Length!=4 || !p.Take(3).All(double.IsFinite) || (BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)
                            throw new InvalidOperationException("M6 point edge evaluation failed.");
                        double x=p[0]*1000,r=Math.Sqrt(Math.Pow(p[1]*1000-centre.Y,2)+Math.Pow(p[2]*1000-centre.Z,2));
                        if(Math.Abs(r-(x-243)*Math.Tan(Math.PI/3))>.0001 || x<242.9999 || x>M6PointBaseX+.0001)
                            throw new InvalidOperationException("M6 point envelope mismatch.");
                        min=Math.Min(min,x);max=Math.Max(max,x);n++;
                    }
                }
                // Cone apex may be a singular vertex rather than an evaluable edge.
                var pointBody=CopyPilotBody();
                foreach(Vertex v in ((object[]?)pointBody.GetVertices() ?? []).OfType<Vertex>())
                {
                    var p=(double[])v.GetPoint();
                    if(Math.Abs(p[1]*1000-centre.Y)<1e-5 && Math.Abs(p[2]*1000-centre.Z)<1e-5 && Math.Abs(p[0]*1000-243)<.0001)
                        min=Math.Min(min,p[0]*1000);
                }
                GC.KeepAlive(pointBody);
            }
            if(cones!=1 || n==0 || Math.Abs(min-243)>.0001 || Math.Abs(max-M6PointBaseX)>.0001)
                throw new InvalidOperationException($"M6 point extents mismatch:{i}/{min}/{max}");
            results.Add(new { Hole=i+1,TipX=min,BaseX=max,EdgeSamples=n });
        }
        Console.WriteLine("[EXPERIMENT M6 POINTS OK] four120deg tips;total depth22;angle is assumption.");
        return new { Points=results,IncludedAngleDegrees=120,TotalDepthMm=22,CylinderDepthMm=265-M6PointBaseX,
            ExperimentalPointsBuilt=true,DrawingPointAngleAccepted=false,FullProfileRunoutAccepted=false };
    }

    public object BuildExperimentalM6Points()
    {
        Drawing.M6PointStudy.RunChecks();
        var prior=new Dictionary<string,bool>();
        for(Feature? f=(Feature?)_model!.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())
        { if(f.Name.StartsWith("EXPERIMENT_M6_"))throw new InvalidOperationException("M6 point trial exists.");prior.Add(f.Name,TiffSuppressed(f)); }
        var replaced=Enumerable.Range(1,4).Select(i=>$"TIFF_M6_{i}_DEEP_PILOT_PENDING_POINT").ToArray();
        double before=CurrentTrialVolume(),r=TiffM6PilotRadius,h=r/Math.Tan(Math.PI/3);
        foreach(string name in replaced)
        {
            if(!prior.TryGetValue(name,out bool suppressed)||suppressed)throw new InvalidOperationException("M6 old pilot missing.");
            if(!TiffFeature(name).SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))
                throw new InvalidOperationException("Cannot suppress M6 flat extension.");
        }
        _model.ForceRebuild3(false);double restored=CurrentTrialVolume();
        if(Math.Abs(restored-before-4*Math.PI*r*r*7)>.02)throw new InvalidOperationException("M6 restoration volume mismatch.");
        for(int i=0;i<4;i++)
        {
            var c=TiffM6Centres[i];string prefix=$"EXPERIMENT_M6_{i+1}_";
            CutChannelCylinder(prefix+"CYLINDER",[250,c.Y,c.Z],[M6PointBaseX,c.Y,c.Z],r,[0,1,0]);
            CutChannelCylinder(prefix+"POINT120",[M6PointBaseX+h,c.Y,c.Z],[243,c.Y,c.Z],r,[0,1,0],true,Math.PI/3,exactApex:true);
        }
        double final=CurrentTrialVolume(),expectedAdded=4*2*Math.PI*r*r*h/3;
        if(Math.Abs(final-before-expectedAdded)>.02)throw new InvalidOperationException($"M6 point net volume mismatch:{final-before:R}/{expectedAdded:R}");
        CheckTiffSuppressionStates(prior,replaced);
        var points=InspectExperimentalM6Points();var geometry=InspectTiffM6Pattern(true);var health=InspectTiffFeatureHealth();
        return new { BeforeMm3=before,FinalMm3=final,AddedMm3=final-before,ExpectedAddedMm3=expectedAdded,Points=points,Geometry=geometry,Health=health,
            ExperimentalM6PointsBuilt=true,ExperimentalLProfileCompleted=true,ExperimentalLExitBuilt=true,
            PpChamferBuilt=true,M6PatternBuilt=true,RearCollarRebuilt=true,OuterProfileRebuilt=true,AaChannelRebuilt=true,
            RightM8Rebuilt=true,LeftM8Rebuilt=true,Bore68StartRebuilt=true,Bore80PartialRebuilt=true,Bore80Restored=true,DetailLCoreBuilt=true,
            IsManufacturingReady=false,DrawingInterpretationAccepted=false,Scope="Experimental120deg M6 tips within22 total depth;thread runout remains open." };
    }
}

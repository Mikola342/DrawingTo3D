using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    // Measures surviving body edges, not merely the thread feature's requested depth.
    public object InspectAxialThreadEdges(bool shortM90 = false)
    {
        var targets=new List<(string Name,double Y,double Z,double Pitch)>();
        for(int i=0;i<4;i++) targets.Add(($"TIFF_M6_{i+1}_THREAD",TiffM6Centres[i].Y,TiffM6Centres[i].Z,1));
        foreach(int sign in new[]{1,-1})targets.Add(($"TRIAL_M12x1_75_7H_nominal_{(sign==1?1:2)}",sign*56.5,-sign*113*Math.Cos(Math.PI/6),1.75));
        targets.Add(("TRIAL_M90x2_X5_to_X38",0,0,2));
        if(shortM90)targets=targets.Where(t=>t.Pitch==2).ToList();
        var reports=new List<object>();
        foreach(var target in targets)
        {
            var f=TiffFeature(target.Name);
            if(TiffSuppressed(f))throw new InvalidOperationException("Suppressed thread: "+target.Name);
            var rows=new List<object>();int faces=0,longEdges=0;
            foreach(Face2 face in (object[])f.GetFaces())
            {
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    if(shortM90&&((Curve)edge.GetCurve()).IsLine())continue;
                    var e=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(e.UMinValue,e.UMaxValue);
                    var trace=new List<(double T,double Phase)>();var radii=new List<double>();
                    int intervals=shortM90?64:512;
                    for(int j=0;j<=intervals;j++)
                    {
                        var p=(double[])edge.Evaluate2(e.UMinValue+(e.UMaxValue-e.UMinValue)*j/intervals,0);
                        if(p.Length!=4||!p.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)
                            throw new InvalidOperationException("Thread edge evaluation failed.");
                        double y=p[1]*1000-target.Y,z=p[2]*1000-target.Z;
                        trace.Add((p[0]*1000,Math.Atan2(z,y)));radii.Add(Math.Sqrt(y*y+z*z));
                    }
                    double lo=trace.Min(p=>p.T),hi=trace.Max(p=>p.T);
                    if(hi-lo<(shortM90?.05:2)*target.Pitch)continue;
                    Drawing.HelicalEdgeFit? fit=null;
                    try{fit=Drawing.HelicalEdgeFit.Measure(trace);}catch(ArgumentException){}
                    // Long axial lines also occur at slot/flat interruptions. Do not
                    // assume every long edge is a helix, or infer full length from it.
                    bool pitchMatches=fit!=null&&Math.Abs(Math.Abs(fit.SignedPitchMm)-target.Pitch)<.0001;
                    if(pitchMatches)longEdges++;
                    var normalChecks=new List<object>();
                    if(shortM90&&pitchMatches)
                    {
                        foreach(double fraction in new[]{.2,.5,.8})
                        {
                            var q=(double[])edge.Evaluate2(e.UMinValue+(e.UMaxValue-e.UMinValue)*fraction,0);
                            var normal=(double[]?)((Surface)face.GetSurface()).EvaluateAtPoint(q[0],q[1],q[2]);
                            if(normal==null){normalChecks.Add(new{Available=false});continue;}
                            double y=q[1]*1000-target.Y,z=q[2]*1000-target.Z,r=Math.Sqrt(y*y+z*z);
                            double radial=(normal[1]*y+normal[2]*z)/r;
                            double angle=Math.Atan2(Math.Abs(radial),Math.Abs(normal[0]))*180/Math.PI;
                            normalChecks.Add(new{Available=true,AxialSectionNormalAngleDegrees=angle,Flank30ResidualDegrees=Math.Abs(angle-30)});
                        }
                    }
                    rows.Add(new{Face=faces,MinXmm=lo,MaxXmm=hi,MinRadiusMm=radii.Min(),MaxRadiusMm=radii.Max(),Fit=fit,
                        MeanPitchMatches=pitchMatches,LocalResidualWithinBudget=fit!=null&&fit.MaximumAxialFitResidualMm<.0001,NormalChecks=normalChecks});
                }
                faces++;
            }
            Console.WriteLine($"[AXIAL THREAD EDGES] {target.Name}:faces={faces},longEdges={rows.Count},pitchMatches={longEdges}.");
            reports.Add(new{target.Name,ExpectedPitchMm=target.Pitch,Edges=rows,DuplicateSharedEdgesIncluded=true,FullProfileAccepted=false});
        }
        return new{Threads=reports,SamplesPerEdge=shortM90?65:513,ShortM90=shortM90,DiagnosticBudgetMm=.0001,BudgetIsDrawingTolerance=false,GeometryChanged=false,Accepted=false};
    }
}

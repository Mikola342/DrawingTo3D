using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectM8HelicalExtents()
    {
        var branches=new List<object>();
        foreach(bool aa in new[]{false,true})
        {
            string branch=aa?"AA":"II";
            var feature=TiffFeature($"TIFF_REAR_{branch}_M8_NOMINAL_PENDING_LENGTH");
            if(TiffSuppressed(feature))throw new InvalidOperationException("M8 thread suppressed.");
            double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180;
            double[] origin=[265,51*Math.Sin(a),-51*Math.Cos(a)];
            double[] axis=[Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)];
            double[] u=[0,Math.Cos(a),Math.Sin(a)],v=[axis[1]*u[2]-axis[2]*u[1],axis[2]*u[0]-axis[0]*u[2],axis[0]*u[1]-axis[1]*u[0]];
            var edgeReports=new List<object>();int faceIndex=0,helicalEdges=0;
            foreach(Face2 face in (object[])feature.GetFaces())
            {
                int edgeIndex=0;
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var bounds=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(bounds.UMinValue,bounds.UMaxValue);
                    var trace=new List<(double T,double Phase)>();var radii=new List<double>();
                    for(int j=0;j<=256;j++)
                    {
                        var q=(double[])edge.Evaluate2(bounds.UMinValue+(bounds.UMaxValue-bounds.UMinValue)*j/256,0);
                        if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                            throw new InvalidOperationException("M8 extent edge evaluation failed.");
                        var d=origin.Select((x,k)=>q[k]*1000-x).ToArray();double s=d.Zip(axis).Sum(x=>x.First*x.Second);
                        double r=Math.Sqrt(d.Select((x,k)=>Math.Pow(x-s*axis[k],2)).Sum());
                        double phase=Math.Atan2(d.Zip(v).Sum(x=>x.First*x.Second),d.Zip(u).Sum(x=>x.First*x.Second));
                        trace.Add((s,phase));radii.Add(r);
                    }
                    double min=trace.Min(x=>x.T),max=trace.Max(x=>x.T),minR=radii.Min(),maxR=radii.Max();
                    Drawing.HelicalEdgeFit? fit=null;
                    // Long edges only; short trim boundaries are still reported without assuming helicity.
                    if(max-min>2.5)
                    {
                        try{fit=Drawing.HelicalEdgeFit.Measure(trace);}catch(ArgumentException){}
                    }
                    bool helical=fit!=null&&Math.Abs(Math.Abs(fit.SignedPitchMm)-1.25)<.0001&&fit.MaximumAxialFitResidualMm<.0001;
                    if(helical)helicalEdges++;
                    edgeReports.Add(new{FaceIndex=faceIndex,EdgeIndex=edgeIndex++,Samples=trace.Count,MinimumOutwardSmm=min,MaximumOutwardSmm=max,
                        AxialSpanMm=max-min,MinimumRadiusMm=minR,MaximumRadiusMm=maxR,PitchFit=fit,MatchesExperimentalPitch=helical});
                    Console.WriteLine($"[M8 EXTENT] {branch}:face={faceIndex},s={min:R}..{max:R},r={minR:R}..{maxR:R},helical={helical},pitch={fit?.SignedPitchMm:R},residual={fit?.MaximumAxialFitResidualMm:R}.");
                }
                faceIndex++;
            }
            branches.Add(new{Branch=branch,Edges=edgeReports,HelicalEdgeOccurrences=helicalEdges,DuplicateSharedEdgesIncluded=true,
                LongHelicalEdgesVerified=helicalEdges>0,
                OperationAxialLengthMm=6/Math.Cos(b),FullProfileLengthVerified=false});
        }
        return new{Branches=branches,ExpectedPitchMm=1.25,PitchDifferenceBudgetMm=.0001,AxialFitResidualBudgetMm=.0001,
            BudgetIsDrawingTolerance=false,Definitions=InspectCurrentM8Definitions(expectedEndTrim:false),Accepted=false,FullProfileLengthVerified=false};
    }
}

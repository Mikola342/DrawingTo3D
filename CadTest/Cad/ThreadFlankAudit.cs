using SolidWorks.Interop.sldworks;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectThreadFlankNormals()
    {
        var targets=new List<(string Name,double[] Origin,double[] Axis)>();
        for(int i=0;i<4;i++)targets.Add(($"TIFF_M6_{i+1}_THREAD",[0,TiffM6Centres[i].Y,TiffM6Centres[i].Z],[1,0,0]));
        foreach(int sign in new[]{1,-1})targets.Add(($"TRIAL_M12x1_75_7H_nominal_{(sign==1?1:2)}",[0,sign*56.5,-sign*113*Math.Cos(Math.PI/6)],[1,0,0]));
        targets.Add(("TRIAL_M90x2_X5_to_X38",[0,0,0],[1,0,0]));
        foreach(bool aa in new[]{false,true})
        {
            double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180;
            targets.Add(($"TIFF_REAR_{(aa?"AA":"II")}_M8_NOMINAL_PENDING_LENGTH",[265,51*Math.Sin(a),-51*Math.Cos(a)],
                [Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)]));
        }
        var results=new List<object>();
        foreach(var target in targets)
        {
            var feature=TiffFeature(target.Name);int samples=0,unavailable=0,positive=0,negative=0,other=0;
            double maxAngleError=0;var faceRows=new List<object>();int faceIndex=0;
            foreach(Face2 face in (object[])feature.GetFaces())
            {
                var surface=(Surface)face.GetSurface();
                if(surface.IsPlane()||surface.IsCylinder()||surface.IsCone()){faceIndex++;continue;}
                var angles=new List<double>();
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var bound=edge.GetCurveParams3();
                    foreach(double fraction in new[]{.2,.5,.8})
                    {
                        var p=(double[])edge.Evaluate2(bound.UMinValue+(bound.UMaxValue-bound.UMinValue)*fraction,0);
                        if(p.Length!=4||!p.Take(3).All(double.IsFinite))throw new InvalidOperationException("Flank edge evaluation failed.");
                        var n=(double[]?)surface.EvaluateAtPoint(p[0],p[1],p[2]);
                        if(n==null){unavailable++;continue;}
                        var d=target.Origin.Select((x,k)=>p[k]*1000-x).ToArray();
                        double axial=d.Zip(target.Axis).Sum(v=>v.First*v.Second);
                        var radial=d.Select((x,k)=>x-axial*target.Axis[k]).ToArray();double r=Math.Sqrt(radial.Sum(x=>x*x));
                        if(r<1e-9)throw new InvalidOperationException("Flank point on axis.");
                        double na=n.Take(3).Zip(target.Axis).Sum(v=>v.First*v.Second),nr=n.Take(3).Zip(radial).Sum(v=>v.First*v.Second)/r;
                        double angle=Math.Atan2(Math.Abs(nr),Math.Abs(na))*180/Math.PI;
                        angles.Add(angle);samples++;
                        if(Math.Abs(angle-30)<.01)
                        {
                            maxAngleError=Math.Max(maxAngleError,Math.Abs(angle-30));
                            if(na*nr>0)positive++;else negative++;
                        }
                        else other++;
                    }
                }
                faceRows.Add(new{Face=faceIndex++,Samples=angles.Count,MinAngleDegrees=angles.DefaultIfEmpty(double.NaN).Min(),MaxAngleDegrees=angles.DefaultIfEmpty(double.NaN).Max()});
            }
            bool both=positive>0&&negative>0;
            Console.WriteLine($"[THREAD FLANKS] {target.Name}: +/-={positive}/{negative},other={other},unavailable={unavailable},max30Error={maxAngleError:R}.");
            results.Add(new{target.Name,Samples=samples,PositiveFlankSamples=positive,NegativeFlankSamples=negative,OtherSurfaceSamples=other,Unavailable=unavailable,
                Both60DegreeFlanksObserved=both,Max30DegreeResidual=maxAngleError,Faces=faceRows,ContinuousProfileVerified=false});
        }
        return new{Threads=results,AngleClassificationBudgetDegrees=.01,Scope="Sampled surface normals at trimmed boundaries; axial profile angle, not thread tolerance/continuous interior coverage",GeometryChanged=false};
    }
}

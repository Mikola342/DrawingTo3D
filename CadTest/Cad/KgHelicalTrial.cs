using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectKgHelicalTrial(bool truncated = false)
    {
        var reports=new List<object>();
        foreach(double degrees in new[]{60.0,120.0})
        {
            var feature=TiffFeature($"{(truncated ? "DESIGN_KG_TRUNCATED_CUT" : "EXPERIMENT_KG_V60_HELICAL_CUT")}_{degrees}");
            if(TiffSuppressed(feature))throw new InvalidOperationException("KG helical cut suppressed.");
            double a=degrees*Math.PI/180,b=7*Math.PI/180;var origin=Drawing.KgPrepStudy.Point(0,degrees);
            double[] axis=[-Math.Sin(b),-Math.Cos(b)*Math.Sin(a),Math.Cos(b)*Math.Cos(a)];
            double[] u=[0,Math.Cos(a),Math.Sin(a)],v=[axis[1]*Math.Sin(a)-axis[2]*Math.Cos(a),-axis[0]*Math.Sin(a),axis[0]*Math.Cos(a)];
            object? customSketch = null;
            if (truncated)
            {
                Sketch? sketch = null;
                for (Feature? sub=(Feature?)feature.GetFirstSubFeature();sub!=null;sub=(Feature?)sub.GetNextSubFeature())
                    if(sub.Name==$"DESIGN_KG_TRUNCATED_PROFILE_{degrees}") sketch=(Sketch)sub.GetSpecificFeature2();
                if(sketch==null)throw new InvalidOperationException("Custom KG profile sketch missing.");
                double r0=4.4-2.0/32-.03,w=.45/Math.Sqrt(3);
                (double T,double R)[] expected=[(2-w-.05,r0),(2-.05,r0+.45),(2+.05,r0+.45),(2+w+.05,r0)];
                var seen=new HashSet<(int,int)>();var hits=new int[4];double maxError=0;
                foreach(SketchSegment segment in (object[])sketch.GetSketchSegments())
                {
                    if(segment.GetType()!=(int)swSketchSegments_e.swSketchLINE)throw new InvalidOperationException("Custom profile must have four straight sides.");
                    // Use trimmed sketch endpoints, not the underlying line curve's
                    // parameter interval (which need not delimit this sketch segment).
                    var line=(SketchLine)segment;
                    int[] ids=new int[2];int n=0;
                    foreach(SketchPoint p in new[]{(SketchPoint)line.GetStartPoint2(),(SketchPoint)line.GetEndPoint2()})
                    {
                        double[] q=[p.X,p.Y,p.Z];
                        var d=origin.Select((value,i)=>q[i]*1000-value).ToArray();
                        double t=d.Zip(axis).Sum(z=>z.First*z.Second),r=d.Zip(u).Sum(z=>z.First*z.Second),off=d.Zip(v).Sum(z=>z.First*z.Second);
                        var best=expected.Select((pt,i)=>(Index:i,Error:Math.Sqrt(Math.Pow(t-pt.T,2)+Math.Pow(r-pt.R,2)+off*off))).MinBy(z=>z.Error);
                        if(!double.IsFinite(best.Error)||best.Error>1e-6)throw new InvalidOperationException($"Custom profile vertex differs: branch={degrees},t={t:R},r={r:R},off={off:R},error={best.Error:R}.");
                        maxError=Math.Max(maxError,best.Error);ids[n++]=best.Index;hits[best.Index]++;
                    }
                    if(ids[0]==ids[1]||!seen.Add((Math.Min(ids[0],ids[1]),Math.Max(ids[0],ids[1]))))
                        throw new InvalidOperationException("Custom profile duplicate or zero edge.");
                }
                if(hits.Any(h=>h!=2)||!seen.SetEquals(new[]{(0,1),(1,2),(2,3),(0,3)}))
                    throw new InvalidOperationException("Custom profile is not the expected closed trapezoid.");
                customSketch=new{Edges=4,AxialBottomFlatMm=.1,RadialDepthMm=.45,MaximumVertexErrorMm=maxError,StandardProfileAccepted=false};
                Console.WriteLine($"[CUSTOM KG PROFILE OK] {degrees}: four sides, flat0.10/depth0.45; max vertex error={maxError:R}.");
            }
            var helices=new List<object>();
            int samples=0,faces=0;double minT=double.PositiveInfinity,maxT=double.NegativeInfinity,minR=double.PositiveInfinity,maxR=0;
            foreach(Face2 face in (object[])feature.GetFaces())
            {
                faces++;
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var e=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(e.UMinValue,e.UMaxValue);
                    var trace=new List<(double T,double Phase)>();
                    var radialTrace=new List<(double T,double R)>();
                    for(int j=0;j<=256;j++)
                    {
                        var q=(double[])edge.Evaluate2(e.UMinValue+(e.UMaxValue-e.UMinValue)*j/256,0);
                        if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)throw new InvalidOperationException("KG helical edge evaluation failed.");
                        var d=origin.Select((v,i)=>q[i]*1000-v).ToArray();double t=d.Zip(axis).Sum(v=>v.First*v.Second);
                        double r=Math.Sqrt(d.Select((v,i)=>Math.Pow(v-t*axis[i],2)).Sum());
                        double phase=Math.Atan2(d.Zip(v).Sum(p=>p.First*p.Second),d.Zip(u).Sum(p=>p.First*p.Second));
                        trace.Add((t,phase));
                        radialTrace.Add((t,r));
                        minT=Math.Min(minT,t);maxT=Math.Max(maxT,t);minR=Math.Min(minR,r);maxR=Math.Max(maxR,r);samples++;
                    }
                    double span=trace.Max(p=>p.T)-trace.Min(p=>p.T);
                    if(span>3.5)
                    {
                        var fit=Drawing.HelicalEdgeFit.Measure(trace);
                        double pitch=fit.SignedPitchMm,residual=fit.MaximumAxialFitResidualMm;
                        double meanT=radialTrace.Average(p=>p.T),meanR=radialTrace.Average(p=>p.R);
                        double radialSlope=radialTrace.Sum(p=>(p.T-meanT)*(p.R-meanR))/radialTrace.Sum(p=>Math.Pow(p.T-meanT,2));
                        double radialResidual=radialTrace.Max(p=>Math.Abs(p.R-meanR-radialSlope*(p.T-meanT)));
                        helices.Add(new{Samples=trace.Count,AxialSpanMm=span,SignedPitchMm=pitch,MaximumAxialFitResidualMm=residual,
                            RadiusAtT2Mm=meanR+radialSlope*(2-meanT),RadiusChangePerAxialMm=radialSlope,MaximumRadialFitResidualMm=radialResidual,
                            PositivePhaseWithIncreasingInwardT=pitch>0,NominalPitchDifferenceMm=pitch-25.4/27});
                        Console.WriteLine($"[KG HELICAL PITCH] {degrees}:span={span:R},signedPitch={pitch:R},fitResidual={residual:R}.");
                    }
                }
            }
            if(samples==0||minT<1||maxT>7||minR<4||maxR>5||maxT-minT<3.5)throw new InvalidOperationException($"KG helical sanity bounds failed:{degrees}/{minT}/{maxT}/{minR}/{maxR}.");
            if(helices.Count==0)throw new InvalidOperationException("No long helical edges available for pitch audit.");
            reports.Add(new{BranchDegrees=degrees,CustomProfileSketch=customSketch,Faces=faces,EdgeSamples=samples,MinimumTmm=minT,MaximumTmm=maxT,MinimumRadiusMm=minR,MaximumRadiusMm=maxR,HelicalEdgeFits=helices});
            Console.WriteLine($"[KG HELICAL BOUNDS] {degrees}:N={samples},t={minT:R}..{maxT:R},r={minR:R}..{maxR:R}.");
        }
        return new{Operations=reports,FullProfileVerified=false,PitchAndHandednessVerified=false,Accepted=false};
    }
    public object TrialKgHelicalCut(bool truncated = false)
    {
        // Geometry experiment, NOT an OST profile: short sharp V groove, no sealing claim.
        const double pitch=25.4/27,start=2,end=6,depth=.45;
        double before=CurrentTrialVolume();var reports=new List<object>();
        foreach(double degrees in new[]{60.0,120.0})
        {
            double a=degrees*Math.PI/180,b=7*Math.PI/180;
            double[] axis=[-Math.Sin(b),-Math.Cos(b)*Math.Sin(a),Math.Cos(b)*Math.Cos(a)];
            double[] u=[0,Math.Cos(a),Math.Sin(a)];
            double[] v=[axis[1]*u[2]-axis[2]*u[1],axis[2]*u[0]-axis[0]*u[2],axis[0]*u[1]-axis[1]*u[0]];
            double[] At(double t,double r,double phase)
            {var p=Drawing.KgPrepStudy.Point(t,degrees);return p.Select((x,i)=>(x+r*(Math.Cos(phase)*u[i]+Math.Sin(phase)*v[i]))/1000).ToArray();}
            Feature LastSketch()
            {Feature? last=null;for(Feature? f=(Feature?)_model!.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())if(f.GetTypeName2()=="3DProfileFeature")last=f;
                return last??throw new InvalidOperationException("3D sketch missing.");}
            var sm=_model!.SketchManager;_model.ClearSelection2(true);sm.Insert3DSketch(true);
            double[] points=Enumerable.Range(0,321).SelectMany(i=>{double t=start+(end-start)*i/320;return At(t,4.4-t/32-.03,2*Math.PI*(t-start)/pitch);}).ToArray();
            using(var exact=new ExactSketchCoordinates(sm))
                if(sm.CreateSpline2(points,false)==null)throw new InvalidOperationException("KG trial path spline failed.");
            sm.Insert3DSketch(true);var path=LastSketch();path.Name=$"EXPERIMENT_KG_HELIX_PATH_{degrees}";
            _model.ClearSelection2(true);sm.Insert3DSketch(true);
            double r0=4.4-start/32-.03,w=depth/Math.Sqrt(3);
            // Agreed custom variant: 0.10 mm axial flat at the groove bottom.
            // This is deliberately NOT labelled as an OST/NPT standard profile.
            const double halfFlat=.05;
            var vertices=truncated
                ? new[]{At(start-w-halfFlat,r0,0),At(start-halfFlat,r0+depth,0),
                    At(start+halfFlat,r0+depth,0),At(start+w+halfFlat,r0,0)}
                : new[]{At(start-w,r0,0),At(start,r0+depth,0),At(start+w,r0,0)};
            using(var exact=new ExactSketchCoordinates(sm))
                for(int i=0;i<vertices.Length;i++){var p=vertices[i];var q=vertices[(i+1)%vertices.Length];if(sm.CreateLine(p[0],p[1],p[2],q[0],q[1],q[2])==null)throw new InvalidOperationException("KG profile failed.");}
            sm.Insert3DSketch(true);var profile=LastSketch();profile.Name=$"{(truncated ? "DESIGN_KG_TRUNCATED_PROFILE" : "EXPERIMENT_KG_V60_PROFILE")}_{degrees}";
            _model.ClearSelection2(true);
            if(!profile.Select2(false,1)||!path.Select2(true,4))throw new InvalidOperationException("KG sweep selection failed.");
            var data=(ISweepFeatureData)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweepCut);
            data.TwistControlType=(short)swTwistControlType_e.swTwistControlFollowPath;
            data.AlignWithEndFaces=false;data.ThinFeature=false;
            Console.WriteLine($"[KG HELIX TRIAL] {degrees}:321 spline points;truncated={truncated};P={pitch:R};t2..6;NOT OST.");
            var cut=(Feature?)_model.FeatureManager.CreateFeature(data);
            if(cut==null)throw new InvalidOperationException("KG trial sweep returned null; no substitute thread accepted.");
            cut.Name=$"{(truncated ? "DESIGN_KG_TRUNCATED_CUT" : "EXPERIMENT_KG_V60_HELICAL_CUT")}_{degrees}";
            if(!_model.ForceRebuild3(false)||cut.GetErrorCode2(out bool warning)!=0||warning)throw new InvalidOperationException("KG sweep rebuild failed.");
            reports.Add(new{BranchDegrees=degrees,VolumeMm3=CurrentTrialVolume(),Feature=cut.Name});
        }
        double final=CurrentTrialVolume();if(before-final<=0||before-final>500)throw new InvalidOperationException("KG helical removal outside sanity bounds.");
        return new{BeforeMm3=before,FinalMm3=final,Operations=reports,Health=InspectTiffFeatureHealth(),
            PitchMm=pitch,StartTmm=start,EndTmm=end,RadialToolDepthMm=depth,
            CustomTruncatedProfile=truncated,AxialBottomFlatMm=truncated?.1:0,
            Profile=truncated?"Agreed custom truncated60, NOT OST; axial bottom flat0.10":"Sharp experimental V60, no OST truncations",
            Accepted=false,FullProfileVerified=false,RequiresFreshVerification=true,DrawingInterpretationAccepted=false};
    }
}

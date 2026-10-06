using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Text.Json;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    // Independent rolling sphere construction between X265 and the oblique pilot cylinder.
    // Thread cuts trim this ideal blend; missing ideal points are reported, not silently accepted.
    public object InspectM8BlendSurfaces(string? onlyBranch = null, bool interstitial = false)
    {
        var reports=new List<object>();
        foreach(bool aa in new[]{false,true})
        {
            string branch=aa?"AA":"II";
            if(onlyBranch!=null&&onlyBranch!=branch)continue;
            var pointReports=new List<object>();
            var feature=TiffFeature($"EXPERIMENT_{branch}_M8_MOUTH_R05");
            if(TiffSuppressed(feature))throw new InvalidOperationException("M8 blend suppressed.");
            var faces=((object[])feature.GetFaces()).Cast<Face2>().ToArray();
            if(faces.Length==0)throw new InvalidOperationException("No surviving M8 blend surface.");
            double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180;
            double[] origin=[265,51*Math.Sin(a),-51*Math.Cos(a)];
            double[] axis=[Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)];
            double[] u=[0,Math.Cos(a),Math.Sin(a)];
            double[] v=[axis[1]*u[2]-axis[2]*u[1],axis[2]*u[0]-axis[0]*u[2],axis[0]*u[1]-axis[1]*u[0]];
            const double radius=.5,budget=.0001;
            double rc=Drawing.M8PilotStudy.Radius+radius;
            int idealPresent=0,total=0,projectionFailures=0,curvatureFailures=0,trimmedQueries=0;double maxDistance=0,maxCenterResidual=0,maxRadiusResidual=0;
            int angles=interstitial?64:32,arcCount=interstitial?8:5;
            for(int i=0;i<angles;i++)for(int j=0;j<arcCount;j++)
            {
                double angle=2*Math.PI*(i+(interstitial?.83:.37))/angles,f=interstitial?(j+.5)/8.0:j/4.0;
                var w=u.Select((x,k)=>Math.Cos(angle)*x+Math.Sin(angle)*v[k]).ToArray();
                double s=(-radius-rc*w[0])/axis[0];
                var c=origin.Select((x,k)=>x+s*axis[k]+rc*w[k]).ToArray();
                var n=w.Select((x,k)=>(k==0?1-f:0)-f*x).ToArray();
                double norm=Math.Sqrt(n.Sum(x=>x*x));n=n.Select(x=>x/norm).ToArray();
                var p=c.Select((x,k)=>(x+radius*n[k])/1000).ToArray();
                Face2? chosen=null;double[]? q=null;double best=double.PositiveInfinity;
                foreach(var face in faces)
                {
                    var closest=(double[]?)face.GetClosestPointOn(p[0],p[1],p[2]);
                    if(closest==null)continue;
                    if(closest.Length!=5||!closest.All(double.IsFinite))throw new InvalidOperationException("Invalid blend projection.");
                    double distance=Math.Sqrt(Enumerable.Range(0,3).Sum(k=>Math.Pow(closest[k]-p[k],2)))*1000;
                    if(distance<best){best=distance;q=closest;chosen=face;}
                }
                total++;
                if(chosen==null||q==null){projectionFailures++;pointReports.Add(new{AngularIndex=i,ArcIndex=j,ProjectionFailed=true,IdealPointMetres=p});continue;}
                maxDistance=Math.Max(maxDistance,best);if(best<=budget)idealPresent++;
                if(interstitial&&best>budget)
                {
                    trimmedQueries++;
                    pointReports.Add(new{AngularIndex=i,ArcIndex=j,ProjectionFailed=false,IdealPointMetres=p,ProjectedPointMetres=q.Take(3).ToArray(),ProjectionDistanceMm=best,OutsideSurvivingBlend=true,CurvatureNotTested=true});
                    continue;
                }
                var surface=(Surface)chosen.GetSurface();var eval=(double[]?)surface.EvaluateAtPoint(q[0],q[1],q[2]);
                pointReports.Add(new{AngularIndex=i,ArcIndex=j,ProjectionFailed=false,IdealPointMetres=p,ProjectedPointMetres=q.Take(3).ToArray(),ProjectionDistanceMm=best,CurvatureAvailable=eval!=null});
                if(eval==null){curvatureFailures++;continue;}
                if(eval.Length!=11||!eval.All(double.IsFinite))throw new InvalidOperationException("Invalid blend curvature.");
                double normalLength=Math.Sqrt(eval.Take(3).Sum(x=>x*x));
                if(Math.Abs(normalLength-1)>1e-6)throw new InvalidOperationException("Invalid blend normal length.");
                double residual=double.PositiveInfinity;
                foreach(int sign in new[]{-1,1})
                {
                    var center=Enumerable.Range(0,3).Select(k=>q[k]*1000+sign*radius*eval[k]).ToArray();
                    var d=center.Select((x,k)=>x-origin[k]).ToArray();double axial=d.Zip(axis).Sum(x=>x.First*x.Second);
                    double radial=Math.Sqrt(d.Select((x,k)=>Math.Pow(x-axial*axis[k],2)).Sum());
                    residual=Math.Min(residual,Math.Max(Math.Abs(center[0]-264.5),Math.Abs(radial-rc)));
                }
                maxCenterResidual=Math.Max(maxCenterResidual,residual);
                double radiusResidual=new[]{eval[9],eval[10]}.Where(k=>Math.Abs(k)>1e-12).Select(k=>Math.Abs(1000/Math.Abs(k)-radius)).DefaultIfEmpty(double.PositiveInfinity).Min();
                maxRadiusResidual=Math.Max(maxRadiusResidual,radiusResidual);
            }
            reports.Add(new{Branch=branch,QuerySamples=total,IdealPointsOnSurvivingFace=idealPresent,MaximumIdealProjectionDistanceMm=maxDistance,
                Points=pointReports,
                ProjectionFailures=projectionFailures,CurvatureFailures=curvatureFailures,
                InterstitialGrid=interstitial,AngularStations=angles,ArcStations=arcCount,TrimmedQueries=trimmedQueries,
                MaximumRollingCenterResidualMm=maxCenterResidual,MaximumPrincipalRadiusResidualMm=maxRadiusResidual,
                SamplingBudgetMm=budget,SampledRollingGeometryPass=idealPresent>0&&projectionFailures==0&&curvatureFailures==0&&maxCenterResidual<=budget&&maxRadiusResidual<=budget});
            Console.WriteLine($"[M8 BLEND] {branch}:present={idealPresent}/{total},projectionFailures={projectionFailures},curvatureFailures={curvatureFailures},centerResidual={maxCenterResidual:R},radiusResidual={maxRadiusResidual:R},trimDistance={maxDistance:R}.");
        }
        return new{Operations=reports,FullBlendSurfaceVerified=false,ThreadTrimCoverageAccepted=false,Accepted=false};
    }

    public object CompareM8BlendBeforeThreads()
    {
        double originalVolume=CurrentTrialVolume();
        var final=JsonSerializer.SerializeToElement(InspectM8BlendSurfaces());
        var comparisons=new List<object>();
        foreach(string branch in new[]{"II","AA"})
        {
            string thread=$"TIFF_REAR_{branch}_M8_NOMINAL_PENDING_LENGTH";
            JsonElement before,afterThread;
            try
            {
                if(!_model!.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToBeforeFeature,thread))
                    throw new InvalidOperationException("Cannot isolate pre-thread blend.");
                before=JsonSerializer.SerializeToElement(InspectM8BlendSurfaces(branch));
                if(!_model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToAfterFeature,thread))
                    throw new InvalidOperationException("Cannot isolate post-thread blend.");
                afterThread=JsonSerializer.SerializeToElement(InspectM8BlendSurfaces(branch));
            }
            finally
            {
                if(!_model!.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToEnd,""))
                    throw new InvalidOperationException("Cannot restore full model after blend audit.");
                if(!_model.ForceRebuild3(false))throw new InvalidOperationException("Full model failed to rebuild after blend audit.");
            }
            var b=before.GetProperty("Operations")[0];
            var t=afterThread.GetProperty("Operations")[0];
            var f=final.GetProperty("Operations").EnumerateArray().Single(x=>x.GetProperty("Branch").GetString()==branch);
            int newlyAbsent=0,unexplained=0,threadOnlyAbsent=0,postThreadCoverageChanges=0;
            var bp=b.GetProperty("Points").EnumerateArray().ToArray();
            var fp=f.GetProperty("Points").EnumerateArray().ToArray();
            var tp=t.GetProperty("Points").EnumerateArray().ToArray();
            if(bp.Length!=fp.Length||tp.Length!=fp.Length)throw new InvalidOperationException("Blend sample grids differ.");
            for(int i=0;i<bp.Length;i++)
            {
                bool wasPresent=!bp[i].GetProperty("ProjectionFailed").GetBoolean()&&bp[i].GetProperty("ProjectionDistanceMm").GetDouble()<=.0001;
                bool present=!fp[i].GetProperty("ProjectionFailed").GetBoolean()&&fp[i].GetProperty("ProjectionDistanceMm").GetDouble()<=.0001;
                bool threadPresent=!tp[i].GetProperty("ProjectionFailed").GetBoolean()&&tp[i].GetProperty("ProjectionDistanceMm").GetDouble()<=.0001;
                if(wasPresent&&!threadPresent)threadOnlyAbsent++;
                if(threadPresent!=present)postThreadCoverageChanges++;
                if(!present){if(wasPresent)newlyAbsent++;else unexplained++;}
            }
            comparisons.Add(new{Branch=branch,BeforeThread=b,AfterThread=t,Final=f,AbsentOnlyAfterLaterFeatures=newlyAbsent,AbsentAlreadyBeforeThread=unexplained,
                AbsentAfterThreadAlone=threadOnlyAbsent,PostThreadCoverageChanges=postThreadCoverageChanges,
                SampledCoverageExplainedByThread=unexplained==0&&threadOnlyAbsent==newlyAbsent&&postThreadCoverageChanges==0,
                FullContinuousTrimCoverageVerified=false});
        }
        double restored=CurrentTrialVolume();
        if(Math.Abs(restored-originalVolume)>.001)throw new InvalidOperationException("Blend audit changed restored volume.");
        return new{Comparisons=comparisons,OriginalVolumeMm3=originalVolume,RestoredVolumeMm3=restored,Health=InspectTiffFeatureHealth(),
            Accepted=false,FullBlendSurfaceVerified=false};
    }
}

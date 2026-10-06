using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectM8MouthFillets(bool noEndTrim = false)
    {
        var results=new List<object>();
        foreach(string branch in new[]{"II","AA"})
        {
            var feature=TiffFeature($"EXPERIMENT_{branch}_M8_MOUTH_R05");
            if(TiffSuppressed(feature)||feature.GetErrorCode2(out bool warning)!=0||warning)
                throw new InvalidOperationException("M8 mouth feature suppressed or failed.");
            var definition=(ISimpleFilletFeatureData2)feature.GetDefinition();
            if(Math.Abs(definition.DefaultRadius-.0005)>1e-10)throw new InvalidOperationException("M8 mouth radius definition changed.");
            var faces=(object[]?)feature.GetFaces();
            if(faces==null||faces.Length==0)throw new InvalidOperationException("M8 mouth has no surviving faces.");
            var thread=TiffFeature($"TIFF_REAR_{branch}_M8_NOMINAL_PENDING_LENGTH");
            double a=(branch=="II"?60:120)*Math.PI/180,b=(branch=="II"?10:4)*Math.PI/180;
            double[] origin=[265,51*Math.Sin(a),-51*Math.Cos(a)],axis=[Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)];
            int samples=0;double minS=double.PositiveInfinity,maxS=double.NegativeInfinity,maxR=0,maxX=double.NegativeInfinity;
            foreach(Face2 face in (object[])thread.GetFaces())
            foreach(Edge edge in (object[])face.GetEdges())
            {
                var e=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(e.UMinValue,e.UMaxValue);
                for(int j=0;j<=64;j++)
                {
                    var q=(double[])edge.Evaluate2(e.UMinValue+(e.UMaxValue-e.UMinValue)*j/64,0);
                    if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                        throw new InvalidOperationException("M8 thread edge evaluation failed.");
                    var d=origin.Select((v,i)=>q[i]*1000-v).ToArray();double s=d.Zip(axis).Sum(p=>p.First*p.Second);
                    double r=Math.Sqrt(d.Select((v,i)=>Math.Pow(v-s*axis[i],2)).Sum());
                    minS=Math.Min(minS,s);maxS=Math.Max(maxS,s);maxR=Math.Max(maxR,r);maxX=Math.Max(maxX,q[0]*1000);samples++;
                }
            }
            if(samples==0||minS< -6/Math.Cos(b)-.0001||maxX>265.0001||maxS>4*Math.Abs(Math.Tan(b))+.0001||Math.Abs(maxR-4)>.0001)
                throw new InvalidOperationException($"M8 mouth thread envelope failed:{branch}/{minS}/{maxS}/{maxR}/{maxX}.");
            results.Add(new{Branch=branch,RadiusDefinitionMm=definition.DefaultRadius*1000,SurvivingFaces=faces.Length,
                ThreadEdgeSamples=samples,MinimumInwardSignedSmm=minS,MaximumOutwardSmm=maxS,MaximumRadiusMm=maxR,MaximumGlobalXmm=maxX});
        }
        return new{Operations=results,Threads=InspectCurrentM8Definitions(expectedEndTrim:!noEndTrim),FullBlendSurfaceVerified=false,Accepted=false};
    }
    public object TrialM8MouthFillets(string diagnosticDirectory, bool beforeThread = false, bool rollback = false, bool noEndTrim = false)
    {
        _=InspectCurrentM8Definitions();
        var reports=new List<object>();double before=CurrentTrialVolume();
        foreach(bool aa in new[]{false,true})
        {
            var thread=TiffFeature($"TIFF_REAR_{(aa?"AA":"II")}_M8_NOMINAL_PENDING_LENGTH");
            bool rolledBack=false;
            if(rollback)
            {
                if(!_model!.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToBeforeFeature,thread.Name))
                    throw new InvalidOperationException("Cannot roll back to M8 pilot.");
                rolledBack=true;
            }
            if(beforeThread && !rollback && !thread.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))
                throw new InvalidOperationException("Cannot isolate pilot for mouth fillet.");
            try
            {
            if(beforeThread)_model!.ForceRebuild3(false);
            double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180;
            double[] origin=[265,51*Math.Sin(a),-51*Math.Cos(a)],axis=[Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)];
            var bodies=(object[])((PartDoc)_model!).GetBodies2((int)swBodyType_e.swSolidBody,false);
            if(bodies.Length!=1)throw new InvalidOperationException("M8 mouth requires one live solid.");
            var edges=new List<Edge>();var evidence=new List<object>();
            foreach(Face2 face in (object[])((Body2)bodies[0]).GetFaces())
            {
                var surface=(Surface)face.GetSurface();if(!surface.IsPlane())continue;
                var plane=(double[])surface.PlaneParams;
                if(Math.Abs(Math.Abs(plane[0])-1)>1e-8||Math.Abs(plane[3]*1000-265)>.0001)continue;
                foreach(Edge edge in (object[])face.GetEdges())
                {
                    var interval=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(interval.UMinValue,interval.UMaxValue);
                    bool inside=true;double min=double.PositiveInfinity,max=0;
                    for(int j=0;j<=64;j++)
                    {
                        var q=(double[])edge.Evaluate2(interval.UMinValue+(interval.UMaxValue-interval.UMinValue)*j/64,0);
                        if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                            throw new InvalidOperationException("M8 mouth edge evaluation failed.");
                        var d=origin.Select((v,i)=>q[i]*1000-v).ToArray();double s=d.Zip(axis).Sum(t=>t.First*t.Second);
                        double r=Math.Sqrt(d.Select((v,i)=>Math.Pow(v-s*axis[i],2)).Sum());
                        if(Math.Abs(q[0]*1000-265)>.0001||r<3.1||r>4.0001)inside=false;
                        min=Math.Min(min,r);max=Math.Max(max,r);
                    }
                    if(inside){edges.Add(edge);evidence.Add(new{MinimumAxisRadiusMm=min,MaximumAxisRadiusMm=max,Samples=65});}
                }
            }
            string branch=aa?"AA":"II";
            System.IO.File.WriteAllText(System.IO.Path.Combine(diagnosticDirectory,$"mouth-{branch}.json"),
                System.Text.Json.JsonSerializer.Serialize(new{Branch=branch,BeforeThread=beforeThread,RollbackBeforeThread=rollback,EdgeCount=edges.Count,Edges=evidence,RequestedRadiusMm=.5,SourceInterpretationAccepted=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"[M8 MOUTH SELECTED] {branch}:edges={edges.Count};R0.5 experiment.");
            if(edges.Count==0||edges.Count>12)throw new InvalidOperationException("Unexpected M8 mouth edge selection.");
            _model.ClearSelection2(true);
            var selection=(SelectData)((SelectionMgr)_model.SelectionManager).CreateSelectData();selection.Mark=1;
            foreach(Edge edge in edges)if(!((Entity)edge).Select4(true,selection))throw new InvalidOperationException("M8 mouth selection failed.");
            var definition=(ISimpleFilletFeatureData2)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmFillet);
            definition.Initialize((int)swSimpleFilletType_e.swConstRadiusFillet);
            definition.DefaultRadius=.0005;
            definition.ConicTypeForCrossSectionProfile=(int)swFeatureFilletProfileType_e.swFeatureFilletCircular;
            var feature=(Feature?)_model.FeatureManager.CreateFeature(definition);
            if(feature==null)throw new InvalidOperationException($"Native R0.5 mouth fillet could not be built:{branch}. No smaller radius substituted.");
            feature.Name=$"EXPERIMENT_{branch}_M8_MOUTH_R05";_model.ForceRebuild3(false);
            if(rollback)
            {
                if(noEndTrim)
                {
                    if(!_model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToAfterFeature,thread.Name))
                        throw new InvalidOperationException("Cannot expose thread for end-trim experiment.");
                    var threadData=(IThreadFeatureData)thread.GetDefinition();
                    if(!threadData.AccessSelections((ModelDoc)_model,null))throw new InvalidOperationException("Cannot access thread trim definition.");
                    threadData.TrimEndFace=false;
                    if(!thread.ModifyDefinition(threadData,_model,null))throw new InvalidOperationException("Cannot disable thread end trimming.");
                    Console.WriteLine($"[M8 TRIM EXPERIMENT] {branch}:TrimEndFace=false; pitch/depth unchanged.");
                }
                if(!_model.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToEnd,""))
                    throw new InvalidOperationException("Cannot restore full history after M8 fillet.");
                rolledBack=false;
                if(!_model.ForceRebuild3(false))
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(diagnosticDirectory,"feature-errors.json"),
                        System.Text.Json.JsonSerializer.Serialize(DiagnoseFeatureErrors(),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
                    throw new InvalidOperationException("Full rebuild after M8 mouth failed.");
                }
            }
            else if(beforeThread)
            {
                if(!_model.Extension.ReorderFeature(feature.Name,thread.Name,(int)swMoveLocation_e.swMoveBefore))
                    throw new InvalidOperationException("Cannot place mouth fillet before thread.");
                if(!thread.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))
                    throw new InvalidOperationException("Cannot restore M8 after mouth fillet.");
                _model.ForceRebuild3(false);
            }
            int error=feature.GetErrorCode2(out bool warning);
            if(error!=0||warning)throw new InvalidOperationException($"M8 mouth fillet failed:{branch}/{error}/{warning}");
            var persisted=(ISimpleFilletFeatureData2)feature.GetDefinition();
            if(Math.Abs(persisted.DefaultRadius-.0005)>1e-10)throw new InvalidOperationException("Persisted fillet radius mismatch.");
            reports.Add(new{Branch=branch,SelectedEdges=edges.Count,RadiusMm=persisted.DefaultRadius*1000,VolumeMm3=CurrentTrialVolume()});
            }
            finally
            {
                if(rolledBack)
                {
                    if(!_model!.FeatureManager.EditRollback((int)swMoveRollbackBarTo_e.swMoveRollbackBarToEnd,""))
                        throw new InvalidOperationException("Failed to restore rollback bar during cleanup.");
                    _model.ForceRebuild3(false);
                }
                if(beforeThread && !rollback && TiffSuppressed(thread))
                {
                    if(!thread.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))
                        throw new InvalidOperationException("Failed to restore M8 during cleanup.");
                    _model!.ForceRebuild3(false);
                }
            }
        }
        return new{BeforeMm3=before,FinalMm3=CurrentTrialVolume(),Operations=reports,Health=InspectTiffFeatureHealth(),
            NoEndTrimExperiment=noEndTrim,Accepted=false,RequiresFreshGeometryAndProfileVerification=true,DrawingInterpretationAccepted=false};
    }
}

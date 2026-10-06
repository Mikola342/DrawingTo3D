using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectPrecisionM6Repair()
    {
        var boundaries=InspectPrecisionM6Boundaries();
        var witnesses=InspectPrecisionM6BoundaryWitnesses();
        var clearance=InspectPrecisionM6Void();
        var profiles=InspectThreadProfilePoints(customM6:true,allCustomM6:true);
        return new{Boundaries=boundaries,Witnesses=witnesses,Clearance=clearance,Profiles=profiles,Accepted=false};
    }
    public object DeepenPrecisionM6ToolOverlap()
    {
        using var batch=BeginApiBatch();var rows=new List<object>();
        // Extend the two tool flanks inside the existing bore. Their equations,
        // major-radius flat and helical path remain unchanged.
        for(int index=1;index<=2;index++)
        {
            var cut=TiffFeature($"PRECISION_M6_{index}_CUT");Sketch? sketch=null;Feature? sketchFeature=null;
            for(Feature? sub=(Feature?)cut.GetFirstSubFeature();sub!=null;sub=(Feature?)sub.GetNextSubFeature())
                if(sub.Name==$"PRECISION_M6_{index}_PROFILE_60"){sketch=(Sketch)sub.GetSpecificFeature2();sketchFeature=sub;}
            if(sketch==null)throw new InvalidOperationException("Precision profile missing.");
            _model!.ClearSelection2(true);
            if(!sketchFeature!.Select2(false,0))throw new InvalidOperationException("Cannot select precision profile.");
            _model.EditSketch();
            if(_model.SketchManager.ActiveSketch==null)throw new InvalidOperationException("Profile edit mode unavailable.");
            try
            {
            var c=TiffM6Centres[index-1];const double start=249.375;
            double oldR=TiffM6PilotRadius-.01,newR=TiffM6PilotRadius-.1;
            double oldW=(3-oldR)/Math.Sqrt(3)+.0625,newW=(3-newR)/Math.Sqrt(3)+.0625;
            var points=new List<SketchPoint>();
            foreach(SketchSegment segment in (object[])sketch.GetSketchSegments())
            {
                if(segment.GetType()!=(int)swSketchSegments_e.swSketchLINE)throw new InvalidOperationException("Unexpected precision profile segment.");
                var line=(SketchLine)segment;
                foreach(SketchPoint p in new[]{(SketchPoint)line.GetStartPoint2(),(SketchPoint)line.GetEndPoint2()})
                    if(!points.Any(q=>_swApp!.IsSame(p,q)==(int)swObjectEquality.swObjectSame))points.Add(p);
            }
            if(points.Count!=4)throw new InvalidOperationException("Expected four unique profile vertices.");
            var bottom=points.Where(p=>Math.Abs(p.Y*1000-c.Y-oldR)<1e-6).ToArray();
            if(bottom.Length!=2)throw new InvalidOperationException("Original tool overlap not identified.");
            foreach(var p in bottom)
            {
                double sign=Math.Sign(p.X*1000-start);
                if(Math.Abs(p.X*1000-start-sign*oldW)>1e-6||Math.Abs(p.Z*1000-c.Z)>1e-6)
                    throw new InvalidOperationException("Unexpected lower profile coordinate.");
                double x=(start+sign*newW)/1000,y=(c.Y+newR)/1000,z=c.Z/1000;
                bool moved=p.SetCoords(x,y,z);
                Console.WriteLine($"[PROFILE MOVE] {index}: accepted={moved},actual={p.X:R},{p.Y:R},{p.Z:R},target={x:R},{y:R},{z:R}");
                if(!moved||Math.Abs(p.X-x)+Math.Abs(p.Y-y)+Math.Abs(p.Z-z)>1e-9)
                    throw new InvalidOperationException("Profile vertex move rejected.");
            }
            foreach(var p in points.Except(bottom))
                if(Math.Abs(Math.Abs(p.X*1000-start)-.0625)>1e-6||Math.Abs(p.Y*1000-c.Y-3)>1e-6||Math.Abs(p.Z*1000-c.Z)>1e-6)
                    throw new InvalidOperationException("Major-radius profile changed unexpectedly.");
            Console.WriteLine($"[TOOL OVERLAP] {index}: 0.01 -> 0.1 mm, nominal flanks unchanged.");
            rows.Add(new{Hole=index,OldOverlapMm=.01,NewOverlapMm=.1});
            }
            finally{_model.SketchManager.Insert3DSketch(true);}
        }
        // Exiting each edited sketch already rebuilds its dependants. The
        // delivery helper performs the single full rebuild before saving.
        return new{Changes=rows,Health=InspectTiffFeatureHealth(),Accepted=false};
    }
    public object InspectPrecisionM6BoundaryWitnesses()
    {
        var rows=new List<object>();
        var finalBody=(Body2)((object[])((PartDoc)_model!).GetBodies2((int)swBodyType_e.swSolidBody,false))[0];
        var finalFaces=((object[])finalBody.GetFaces()).Cast<Face2>().ToArray();
        for(int i=1;i<=2;i++)
        {
            var c=TiffM6Centres[i-1];
            foreach(Face2 face in (object[])TiffFeature($"PRECISION_M6_{i}_CUT").GetFaces())foreach(Edge edge in (object[])face.GetEdges())
            {
                var range=edge.GetCurveParams3();
                foreach(double u in new[]{range.UMinValue,(range.UMinValue+range.UMaxValue)/2,range.UMaxValue})
                {
                    var p=(double[])edge.Evaluate2(u,0);double r=Math.Sqrt(Math.Pow(p[1]*1000-c.Y,2)+Math.Pow(p[2]*1000-c.Z,2));
                    if(r>=TiffM6PilotRadius-.001&&p[0]*1000>=248.999)continue;
                    var q=(double[])edge.GetClosestPointOn(p[0],p[1],p[2]);
                    var f=(double[])face.GetClosestPointOn(p[0],p[1],p[2]);
                    double Distance(double[] a)=>1000*Math.Sqrt(Enumerable.Range(0,3).Sum(n=>Math.Pow(a[n]-p[n],2)));
                    var sv=(Vertex?)edge.GetStartVertex();var ev=(Vertex?)edge.GetEndVertex();
                    int ownerIdentity=_swApp!.IsSame(face.GetBody(),finalBody);
                    bool listed=finalFaces.Any(candidate=>_swApp.IsSame(candidate,face)==(int)swObjectEquality.swObjectSame);
                    bool tolerant=edge.IsTolerant(out double tolerance);
                    Console.WriteLine($"[EDGE TOLERANCE] {i}: tolerant={tolerant},mm={tolerance*1000:R}");
                    rows.Add(new{Hole=i,Parameter=u,EvaluatedXYZ=p.Take(3).Select(v=>v*1000).ToArray(),RadiusMm=r,Tolerant=tolerant,EdgeToleranceMm=tolerance*1000,OwnerBodyIdentity=ownerIdentity,ListedInFinalBody=listed,EdgeClosestXYZ=q.Take(3).Select(v=>v*1000).ToArray(),EdgeDistanceMm=Distance(q),FaceClosestXYZ=f.Take(3).Select(v=>v*1000).ToArray(),FaceDistanceMm=Distance(f),StartVertex=sv==null?null:((double[])sv.GetPoint()).Select(v=>v*1000).ToArray(),EndVertex=ev==null?null:((double[])ev.GetPoint()).Select(v=>v*1000).ToArray()});
                    Console.WriteLine($"[BOUNDARY WITNESS] {i}: X{p[0]*1000:R},R{r:R},edgeDistance={Distance(q):R},faceDistance={Distance(f):R},bodyIdentity={ownerIdentity},listed={listed}");
                }
            }
        }
        return new{Witnesses=rows,Scope="Cross-check out-of-envelope edge evaluations against trimmed edge/face closest points and vertices; not acceptance"};
    }
    public object InspectPrecisionM6Void()
    {
        var body=CopyPilotBody();var modeler=(Modeler)_swApp!.GetModeler();var rows=new List<object>();
        for(int i=0;i<4;i++)
        {
            var c=TiffM6Centres[i];
            foreach(double radius in new[]{TiffM6PilotRadius-.0001,2.8})
            {
                var tool=(Body2)modeler.CreateBodyFromCyl(new double[]{249.0/1000,c.Y/1000,c.Z/1000,1,0,0,radius/1000,15.0/1000});
                var intersection=(object[]?)((Body2)body.Copy()).Operations2((int)swBodyOperationType_e.SWBODYINTERSECT,tool,out int error);
                int count=intersection?.Length??0;bool empty=count==0&&(error==0||error==(int)swBodyOperationError_e.swBodyOperationEmptyBody);
                bool control=radius==2.8;bool passed=control?error==0&&count>0:empty;
                Console.WriteLine($"[PRECISION VOID] {i+1}: R{radius:R},components={count},error={error},control={control},passed={passed}.");
                rows.Add(new{Hole=i+1,RadiusMm=radius,StartXmm=249,EndXmm=264,Components=count,Error=error,PositiveMaterialControl=control,Passed=passed});
            }
        }
        return new{Tests=rows,Scope="Temporary cylinder intersections with saved final body; 0.0001 mm clearance from nominal pilot, positive control R2.8"};
    }
    public object ClearPrecisionM6Pilots()
    {
        using var batch=BeginApiBatch();
        var before=CopyPilotBody();double beforeVolume=CurrentTrialVolume();
        for(int i=1;i<=2;i++)
        {
            var c=TiffM6Centres[i-1];
            CutChannelCylinder($"PRECISION_M6_{i}_PILOT_RECLEAR",[265,c.Y,c.Z],[M6PointBaseX,c.Y,c.Z],TiffM6PilotRadius,[0,1,0]);
            _=InspectTiffFeatureHealth();
        }
        var after=CopyPilotBody();double afterVolume=CurrentTrialVolume();
        var removed=(object[]?)((Body2)before.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)after.Copy(),out int error);
        if(error!=0||removed==null||removed.Length==0)throw new InvalidOperationException("Pilot reclear must remove measurable intrusion.");
        var rows=new List<object>();
        foreach(Body2 piece in removed)
        {
            var points=new List<double[]>();
            foreach(Edge edge in (object[])piece.GetEdges())
            {
                var b=edge.GetCurveParams3();
                for(int k=0;k<=64;k++)
                {
                    var p=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*k/64,0);
                    if(p.Length!=4||!p.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)throw new InvalidOperationException("Reclear boundary unavailable.");
                    points.Add(p.Take(3).Select(v=>v*1000).ToArray());
                }
            }
            if(points.Count==0)throw new InvalidOperationException("Empty reclear boundary.");
            int index=Array.FindIndex(TiffM6Centres,c=>points.All(p=>Math.Sqrt(Math.Pow(p[1]-c.Y,2)+Math.Pow(p[2]-c.Z,2))<=TiffM6PilotRadius+.0001));
            if(index is not (0 or 1)||points.Min(p=>p[0])<M6PointBaseX-.0001||points.Max(p=>p[0])>265.0001)throw new InvalidOperationException("Reclear changed geometry outside nominal pilot.");
            rows.Add(new{Hole=index+1,MinimumXmm=points.Min(p=>p[0]),MaximumXmm=points.Max(p=>p[0]),Samples=points.Count});
        }
        if(beforeVolume-afterVolume<0||beforeVolume-afterVolume>6)throw new InvalidOperationException("Reclear volume outside conservative intrusion envelope.");
        var reconstruction=CheckM6BooleanReconstruction(before,after,removed,"PRECISION PILOT RECLEAR");
        return new{BeforeMm3=beforeVolume,AfterMm3=afterVolume,RemovedMm3=beforeVolume-afterVolume,Components=rows,Reconstruction=reconstruction,Scope="Restore existing nominal cylindrical void, not change drill diameter or tip"};
    }
    public object InspectPrecisionM6Boundaries()
    {
        var rows=new List<object>();bool allPassed=true;
        for(int i=1;i<=4;i++)
        {
            if(!TiffSuppressed(TiffFeature($"TIFF_M6_{i}_THREAD")))throw new InvalidOperationException("Replaced M6 still active.");
            var feature=TiffFeature($"PRECISION_M6_{i}_CUT");
            if(TiffSuppressed(feature))throw new InvalidOperationException("Precision M6 suppressed.");
            var definition=(ISweepFeatureData)feature.GetDefinition();
            int alignment=definition.PathAlignmentType,twist=definition.TwistControlType;bool thin=definition.ThinFeature;
            bool orientationPassed=alignment==(int)swTangencyType_e.swTangencyDirectionVector&&twist==(short)swTwistControlType_e.swTwistControlFollowPath&&!thin;
            Console.WriteLine($"[PRECISION SETTINGS] {i}: alignment={alignment},twist={twist},thin={thin},expectedAlignment={(int)swTangencyType_e.swTangencyDirectionVector},expectedTwist={(short)swTwistControlType_e.swTwistControlFollowPath};passed={orientationPassed}.");
            var c=TiffM6Centres[i-1];var points=new List<(double X,double R)>();
            foreach(Face2 face in (object[])feature.GetFaces())foreach(Edge edge in (object[])face.GetEdges())
            {
                var b=edge.GetCurveParams3();
                for(int k=0;k<=128;k++)
                {
                    var p=(double[])edge.Evaluate2(b.UMinValue+(b.UMaxValue-b.UMinValue)*k/128,0);
                    if(p.Length!=4||!p.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)throw new InvalidOperationException("Precision boundary unavailable.");
                    points.Add((p[0]*1000,Math.Sqrt(Math.Pow(p[1]*1000-c.Y,2)+Math.Pow(p[2]*1000-c.Z,2))));
                }
            }
            if(points.Count==0)throw new InvalidOperationException("No precision boundaries.");
            double lo=points.Min(p=>p.X),hi=points.Max(p=>p.X),rlo=points.Min(p=>p.R),rhi=points.Max(p=>p.R);
            bool passed=Math.Abs(lo-249)<=.0001&&hi>=264.4&&hi<=264.542&&Math.Abs(rlo-TiffM6PilotRadius)<=.0001&&Math.Abs(rhi-3)<=.0001;
            allPassed&=passed&&orientationPassed;
            Console.WriteLine($"[PRECISION BOUNDARY] {i}: X{lo:R}..{hi:R},R{rlo:R}..{rhi:R};passed={passed}.");
            rows.Add(new{Hole=i,MinimumXmm=lo,MaximumXmm=hi,MinimumRadiusMm=rlo,MaximumRadiusMm=rhi,Samples=points.Count,Alignment=alignment,Twist=twist,Thin=thin,DirectionConstrained=orientationPassed,GeometryEnvelopePassed=passed,Passed=passed&&orientationPassed});
        }
        return new{Threads=rows,BoundaryEnvelopePassed=allPassed,Scope="Sampled boundaries, native suppression and sweep orientation; not continuous endpoint certification"};
    }
    public object ComparePrecisionM6Pattern(string originalCopy,string revisedCopy)
    {
        using var batch=BeginApiBatch();
        OpenPartForInspection(originalCopy,requireFresh:true);var before=CopyPilotBody();
        OpenPartForInspection(revisedCopy,requireFresh:true);var after=CopyPilotBody();
        bool allLocal=true;
        object Difference(Body2 left,Body2 right,string label)
        {
            var pieces=(object[]?)((Body2)left.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)right.Copy(),out int error);
            if(error!=0||pieces==null||pieces.Length==0)throw new InvalidOperationException("Precision pattern difference unavailable: "+label);
            var rows=new List<object>();var holes=new HashSet<int>();
            foreach(Body2 piece in pieces)
            {
                var points=new List<double[]>();
                foreach(Edge edge in (object[])piece.GetEdges())
                {
                    var range=edge.GetCurveParams3();
                    for(int k=0;k<=64;k++)
                    {
                        var p=(double[])edge.Evaluate2(range.UMinValue+(range.UMaxValue-range.UMinValue)*k/64,0);
                        if(p.Length!=4||!p.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(p[3])&0xffffffffL)!=1)throw new InvalidOperationException("Invalid precision difference sample.");
                        points.Add(p.Take(3).Select(v=>v*1000).ToArray());
                    }
                }
                if(points.Count==0)throw new InvalidOperationException("Empty difference boundary.");
                int index=Array.FindIndex(TiffM6Centres,c=>points.All(p=>Math.Sqrt(Math.Pow(p[1]-c.Y,2)+Math.Pow(p[2]-c.Z,2))<=3.001));
                if(index<0)throw new InvalidOperationException("Precision change outside M6 holes.");
                var c=TiffM6Centres[index];var radii=points.Select(p=>Math.Sqrt(Math.Pow(p[1]-c.Y,2)+Math.Pow(p[2]-c.Z,2))).ToArray();
                double lo=points.Min(p=>p[0]),hi=points.Max(p=>p[0]);
                bool envelopePassed=lo>=248.999&&hi<=265.001&&radii.Min()>=TiffM6PilotRadius-.001;
                allLocal&=envelopePassed;
                Console.WriteLine($"[PRECISION DIFFERENCE] {label},hole={index+1}: X{lo:R}..{hi:R},R{radii.Min():R}..{radii.Max():R};passed={envelopePassed}.");
                holes.Add(index);rows.Add(new{Hole=index+1,MinXmm=lo,MaxXmm=hi,MinRadiusMm=radii.Min(),MaxRadiusMm=radii.Max(),Samples=points.Count,EnvelopePassed=envelopePassed});
            }
            if(holes.Count!=4)throw new InvalidOperationException("Expected differences at four M6 holes.");
            return new{Kind=label,Components=rows};
        }
        var removed=Difference(before,after,"removed");var added=Difference(after,before,"added");
        return new{Removed=removed,Added=added,LocalityPassed=allLocal,Scope="Exact body differences and sampled boundaries; locality must pass explicitly, phase changes permitted",FullProfileAccepted=false};
    }
    public object AlignPrecisionM6Trial(int index=1)
    {
        using var batch=BeginApiBatch();
        var feature=TiffFeature($"PRECISION_M6_{index}_CUT");
        var definition=(ISweepFeatureData)feature.GetDefinition();
        Console.WriteLine($"[ALIGN BEFORE ACCESS] {index}: {definition.PathAlignmentType}/{definition.TwistControlType}");
        if(!definition.AccessSelections(_model!,null))throw new InvalidOperationException("Precision sweep definition unavailable.");
        Console.WriteLine($"[ALIGN AFTER ACCESS] {index}: {definition.PathAlignmentType}/{definition.TwistControlType}");
        Face2? axisFace=null;
        foreach(Face2 face in (object[])TiffFeature($"EXPERIMENT_M6_{index}_CYLINDER").GetFaces())
        {
            var surface=(Surface)face.GetSurface();
            if(surface.IsCylinder()){axisFace=face;break;}
        }
        if(axisFace==null){definition.ReleaseSelectionAccess();throw new InvalidOperationException("No M6 cylinder for direction reference.");}
        definition.PathAlignmentType=(int)swTangencyType_e.swTangencyDirectionVector;
        definition.SetPathAlignmentDirectionVector(axisFace);
        Console.WriteLine($"[ALIGN BEFORE COMMIT] {index}: {definition.PathAlignmentType}/{definition.TwistControlType}");
        if(definition.PathAlignmentType!=(int)swTangencyType_e.swTangencyDirectionVector)
        {
            definition.ReleaseSelectionAccess();
            throw new InvalidOperationException("SolidWorks rejected direction-vector alignment before commit; no alignment repair accepted.");
        }
        if(!feature.ModifyDefinition(definition,_model,null))throw new InvalidOperationException("Precision sweep direction modification failed.");
        _model!.ForceRebuild3(false);
        var actual=(ISweepFeatureData)feature.GetDefinition();
        Console.WriteLine($"[ALIGN AFTER COMMIT] {index}: {actual.PathAlignmentType}/{actual.TwistControlType}");
        return new{Health=InspectTiffFeatureHealth(),Scope="Direction-constrained precision sweep trial; not accepted until profile verification"};
    }
    public object BuildPrecisionM6Trial(int index=1)
    {
        using var batch=BeginApiBatch();
        if(index<1||index>4)throw new ArgumentOutOfRangeException(nameof(index));
        _=CheckTiffM6Thread(TiffFeature($"TIFF_M6_{index}_THREAD"),true);
        double before=CurrentTrialVolume();
        if(!TiffFeature($"TIFF_M6_{index}_THREAD").SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,(int)swInConfigurationOpts_e.swThisConfiguration,null))
            throw new InvalidOperationException("Cannot suppress library M6 in precision copy.");
        _model!.ForceRebuild3(false);double pilot=CurrentTrialVolume();
        var c=TiffM6Centres[index-1];const double start=249.375,end=265.375;
        double r=TiffM6PilotRadius-.01,w=(3-r)/Math.Sqrt(3)+.0625;
        double[] At(double x,double radius,double phase)=>[x/1000,(c.Y+radius*Math.Cos(phase))/1000,(c.Z+radius*Math.Sin(phase))/1000];
        Feature LastSketch()
        {
            Feature? last=null;for(Feature? f=(Feature?)_model.FirstFeature();f!=null;f=(Feature?)f.GetNextFeature())if(f.GetTypeName2()=="3DProfileFeature")last=f;
            return last??throw new InvalidOperationException("Precision sketch not found.");
        }
        var sm=_model.SketchManager;_model.ClearSelection2(true);sm.Insert3DSketch(true);
        const int segments=2048;
        var coordinates=Enumerable.Range(0,segments+1).SelectMany(i=>{double x=start+(end-start)*i/segments;return At(x,r,2*Math.PI*(x-start));}).ToArray();
        using(var exact=new ExactSketchCoordinates(sm))if(sm.CreateSpline2(coordinates,false)==null)throw new InvalidOperationException("Precision path failed.");
        sm.Insert3DSketch(true);var path=LastSketch();path.Name=$"PRECISION_M6_{index}_PATH_128_PER_TURN";
        _model.ClearSelection2(true);sm.Insert3DSketch(true);
        var vertices=new[]{At(start-w,r,0),At(start-.0625,3,0),At(start+.0625,3,0),At(start+w,r,0)};
        using(var exact=new ExactSketchCoordinates(sm))for(int i=0;i<4;i++)
        {
            var p=vertices[i];var q=vertices[(i+1)%4];
            if(sm.CreateLine(p[0],p[1],p[2],q[0],q[1],q[2])==null)throw new InvalidOperationException("Precision profile failed.");
        }
        sm.Insert3DSketch(true);var profile=LastSketch();profile.Name=$"PRECISION_M6_{index}_PROFILE_60";
        _model.ClearSelection2(true);
        if(!profile.Select2(false,1)||!path.Select2(true,4))throw new InvalidOperationException("Precision sweep selection failed.");
        var definition=(ISweepFeatureData)_model.FeatureManager.CreateDefinition((int)swFeatureNameID_e.swFmSweepCut);
        definition.TwistControlType=(short)swTwistControlType_e.swTwistControlFollowPath;
        definition.AlignWithEndFaces=false;definition.ThinFeature=false;
        var cut=(Feature?)_model.FeatureManager.CreateFeature(definition)??throw new InvalidOperationException("Precision M6 sweep failed.");
        cut.Name=$"PRECISION_M6_{index}_CUT";
        _model.ForceRebuild3(false);var health=InspectTiffFeatureHealth();
        double after=CurrentTrialVolume();
        if(pilot-after<=1||pilot-after>Math.PI*(9-TiffM6PilotRadius*TiffM6PilotRadius)*17)
            throw new InvalidOperationException("Precision M6 removal outside envelope.");
        return new{BeforeMm3=before,PilotMm3=pilot,AfterMm3=after,Health=health,Segments=segments,Accepted=false,
            Scope="One M6 replacement trial, native library feature retained suppressed; sampled final profile must pass before any propagation"};
    }
}

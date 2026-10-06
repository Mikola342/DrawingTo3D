using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private object AuditR35Edges(Body2 body,string stage)
    {
        int faces=0,samples=0,outside=0;double maximum=0;var evidence=new List<object>();
        foreach(Face2 face in (object[])body.GetFaces())
        {
            var surface=(Surface)face.GetSurface();if(!surface.IsCylinder())continue;
            var c=(double[])surface.CylinderParams;
            if(Math.Abs(c[0]*1000-25)>1e-5||Math.Abs(c[1]*1000-74.5)>1e-5||
                Math.Abs(c[6]*1000-35)>1e-5||Math.Abs(Math.Abs(c[5])-1)>1e-8)continue;
            faces++;
            foreach(Edge edge in (object[])face.GetEdges())
            {
                var t=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(t.UMinValue,t.UMaxValue);
                double edgeMaximum=0;int edgeOutside=0;
                for(int i=0;i<=256;i++)
                {
                    var q=(double[])edge.Evaluate2(t.UMinValue+(t.UMaxValue-t.UMinValue)*i/256,0);
                    if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                        throw new InvalidOperationException("R35 edge evaluation failed.");
                    double error=Math.Abs(Math.Sqrt(Math.Pow(q[0]*1000-25,2)+Math.Pow(q[1]*1000-74.5,2))-35);
                    edgeMaximum=Math.Max(edgeMaximum,error);maximum=Math.Max(maximum,error);samples++;
                    if(error>1e-5){outside++;edgeOutside++;}
                }
                evidence.Add(new{MaximumRadialResidualMm=edgeMaximum,OutsideSamples=edgeOutside,Samples=257});
            }
        }
        if(faces==0||samples==0)throw new InvalidOperationException("R35 diagnostic surface missing.");
        Console.WriteLine($"[R35 EDGE AUDIT] {stage}:faces={faces},samples={samples},outside={outside},max={maximum:R}mm.");
        return new{Stage=stage,Faces=faces,EdgeSamples=samples,OutsideSamples=outside,MaximumRadialResidualMm=maximum,
            RadialBudgetMm=1e-5,EdgeResidualPassed=outside==0,Edges=evidence};
    }

    public object VerifyLeftShoulderRestoration(string previousCopy,string currentCopy,bool massOnly=false)
    {
        OpenPartForInspection(previousCopy,requireFresh:true);
        double previousVolume=CurrentTrialVolume();var previous=CopyPilotBody();
        var previousR35=massOnly?null:AuditR35Edges(previous,"previous saved body before Boolean");
        OpenPartForInspection(currentCopy,requireFresh:true);
        double currentVolume=CurrentTrialVolume();var current=CopyPilotBody();
        var currentR35=massOnly?null:AuditR35Edges(current,"current saved body before Boolean");
        var added=(object[]?)((Body2)current.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)previous.Copy(),out int code);
        if(code!=0||added==null||added.Length==0)throw new InvalidOperationException($"No restored shoulders:{code}.");
        var lost=(object[]?)((Body2)previous.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)current.Copy(),out int lostCode);
        if((lost?.Length??0)!=0||(lostCode!=0&&lostCode!=(int)swBodyOperationError_e.swBodyOperationEmptyBody))
            throw new InvalidOperationException("Slot correction removed material outside original body.");
        if(massOnly)
        {
            double oldScratch=MeasureRemovedBodiesInScratchPart([previous]);
            double newScratch=MeasureRemovedBodiesInScratchPart([current]);
            double piecesScratch=MeasureRemovedBodiesInScratchPart(added);
            var reconstructionResult=CheckM6BooleanReconstruction(current,previous,added,"LEFT MASS");
            double scratchDelta=newScratch-oldScratch,documentDelta=currentVolume-previousVolume;
            Console.WriteLine($"[LEFT MASS COMPARISON] previous={oldScratch:R},current={newScratch:R},pieces={piecesScratch:R},scratchDelta={scratchDelta:R},documentDelta={documentDelta:R}.");
            GC.KeepAlive(previous);GC.KeepAlive(current);GC.KeepAlive(added);
            return new{PreviousDocumentMm3=previousVolume,CurrentDocumentMm3=currentVolume,PreviousScratchMm3=oldScratch,
                CurrentScratchMm3=newScratch,IsolatedPiecesMm3=piecesScratch,DocumentDeltaMm3=documentDelta,ScratchDeltaMm3=scratchDelta,
                DocumentResidualMm3=piecesScratch-documentDelta,ScratchResidualMm3=piecesScratch-scratchDelta,
                VolumeBudgetMm3=.02,SameRepresentationVolumePassed=Math.Abs(piecesScratch-scratchDelta)<=.02,
                DocumentVolumePassed=Math.Abs(piecesScratch-documentDelta)<=.02,Reconstruction=reconstructionResult,
                NoMaterialLost=true,Scope="Mass-only comparison; does not repeat or waive prior failed envelope check.",Accepted=false};
        }
        var evidence=new List<object>();bool envelopePassed=true;
        foreach(Body2 piece in added)
        {
            var r35=AuditR35Edges(piece,"Boolean added piece");
            int samples=0,outside=0;var outliers=new List<object>();double minZ=double.PositiveInfinity,maxZ=double.NegativeInfinity,minX=double.PositiveInfinity,maxX=double.NegativeInfinity;
            foreach(Edge edge in (object[])piece.GetEdges())
            {
                var t=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(t.UMinValue,t.UMaxValue);
                for(int i=0;i<=64;i++)
                {
                    var q=(double[])edge.Evaluate2(t.UMinValue+(t.UMaxValue-t.UMinValue)*i/64,0);
                    if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                        throw new InvalidOperationException("Invalid shoulder sample.");
                    double x=q[0]*1000,y=q[1]*1000,z=q[2]*1000;
                    double boundary=x<=25?39.5:74.5-Math.Sqrt(Math.Max(0,35*35-(x-25)*(x-25)));
                    if(x<-.00001||x>60.00001||y<boundary-.00001)
                    {outside++;if(outliers.Count<12)outliers.Add(new{X=x,Y=y,Z=z,BoundaryY=boundary});}
                    minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minZ=Math.Min(minZ,z);maxZ=Math.Max(maxZ,z);samples++;
                }
            }
            bool passed=samples>0&&outside==0&&(minZ>=7.99999||maxZ<=-7.99999);envelopePassed&=passed;
            evidence.Add(new{EdgeSamples=samples,MinimumXmm=minX,MaximumXmm=maxX,MinimumZmm=minZ,MaximumZmm=maxZ,
                EnvelopePassed=passed,OutsideSamples=outside,Outliers=outliers,R35=r35});
            Console.WriteLine($"[SHOULDER PIECE] X={minX:R}..{maxX:R},Z={minZ:R}..{maxZ:R},outside={outside},passed={passed};{System.Text.Json.JsonSerializer.Serialize(outliers)}");
        }
        var reconstruction=CheckM6BooleanReconstruction(current,previous,added,"LEFT SHOULDERS");
        double isolated=MeasureRemovedBodiesInScratchPart(added),delta=currentVolume-previousVolume;
        bool volumePassed=Math.Abs(isolated-delta)<=.02;
        GC.KeepAlive(previous);GC.KeepAlive(current);GC.KeepAlive(added);
        Console.WriteLine($"[LEFT SHOULDERS AUDIT] pieces={added.Length},isolated={isolated:R},delta={delta:R};envelopePassed={envelopePassed};no lost material.");
        return new{PreviousR35=previousR35,CurrentR35=currentR35,PreviousVolumeMm3=previousVolume,CurrentVolumeMm3=currentVolume,RestoredVolumeMm3=isolated,
            DocumentVolumeDifferenceMm3=delta,VolumeBudgetMm3=.02,VolumeComparisonPassed=volumePassed,
            Accepted=envelopePassed&&volumePassed,NoMaterialLost=true,AddedBodies=evidence,Reconstruction=reconstruction,EnvelopePassed=envelopePassed,
            FullThreadProfileAccepted=false,FullLeftEndAccepted=false};
    }

    public object InspectLeftEndNominals()
    {
        var bodies=(object[])((PartDoc)_model!).GetBodies2((int)swBodyType_e.swSolidBody,false);
        if(bodies.Length!=1)throw new InvalidOperationException("Left end requires one solid.");
        var groups=new Dictionary<string,List<Face2>>();
        foreach(string key in new[]{"D82","Bottom20","SlotFloor","Outer45","Bore30"})groups[key]=[];
        foreach(Face2 face in (object[])((Body2)bodies[0]).GetFaces())
        {
            var s=(Surface)face.GetSurface();
            if(s.IsCylinder())
            {
                var p=(double[])s.CylinderParams;
                if(Math.Abs(Math.Abs(p[3])-1)<1e-8&&Math.Abs(p[1])+Math.Abs(p[2])<1e-8&&Math.Abs(p[6]-.041)<1e-9)
                    groups["D82"].Add(face);
            }
            else if(s.IsPlane())
            {
                var p=(double[])s.PlaneParams;
                if(Math.Abs(Math.Abs(p[1])-1)<1e-8)
                {
                    if(Math.Abs(p[4]+.025)<1e-9)groups["Bottom20"].Add(face);
                    if(Math.Abs(p[4]-.0395)<1e-9)groups["SlotFloor"].Add(face);
                }
            }
            else if(s.IsCone())
            {
                // Bounding box only filters candidates; all acceptance below uses evaluated edges.
                var box=(double[])face.GetBox();if(box[0]>.006||box[3]<0)continue;
                var p=(double[])s.ConeParams2;
                if(Math.Abs(Math.Abs(p[3])-1)>1e-8||Math.Abs(p[1])+Math.Abs(p[2])>1e-8)continue;
                if(Math.Abs(Math.Abs(p[7])-Math.PI/4)<1e-8)groups["Outer45"].Add(face);
                if(Math.Abs(Math.Abs(p[7])-Math.PI/6)<1e-8)groups["Bore30"].Add(face);
            }
        }
        var results=new List<object>();
        foreach(var pair in groups)
        {
            double minX=double.PositiveInfinity,maxX=double.NegativeInfinity,minR=double.PositiveInfinity,maxR=0,maxResidual=0;
            int samples=0;
            foreach(var face in pair.Value)
            foreach(Edge edge in (object[])face.GetEdges())
            {
                var t=edge.GetCurveParams3();Drawing.M12Study.ValidateEdgeInterval(t.UMinValue,t.UMaxValue);
                for(int i=0;i<=64;i++)
                {
                    var q=(double[])edge.Evaluate2(t.UMinValue+(t.UMaxValue-t.UMinValue)*i/64,0);
                    if(q.Length!=4||!q.Take(3).All(double.IsFinite)||(BitConverter.DoubleToInt64Bits(q[3])&0xffffffffL)!=1)
                        throw new InvalidOperationException("Invalid left end edge sample.");
                    double x=q[0]*1000,y=q[1]*1000,r=Math.Sqrt(q[1]*q[1]+q[2]*q[2])*1000;
                    double residual=pair.Key switch {"D82"=>r-41,"Bottom20"=>y+25,"SlotFloor"=>y-39.5,
                        "Outer45"=>r-(40+x),"Bore30"=>r-(31+(3-x)*Math.Tan(Math.PI/6)),_=>throw new InvalidOperationException()};
                    minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minR=Math.Min(minR,r);maxR=Math.Max(maxR,r);
                    maxResidual=Math.Max(maxResidual,Math.Abs(residual));samples++;
                }
            }
            double start=pair.Key=="Outer45"?3:0,end=pair.Key switch{"SlotFloor"=>25,"Outer45"=>5,_=>3};
            if(samples==0||maxResidual>1e-5||Math.Abs(minX-start)>1e-5||Math.Abs(maxX-end)>1e-5)
                throw new InvalidOperationException($"Left end mismatch {pair.Key}: faces={pair.Value.Count},N={samples},X={minX:R}..{maxX:R},residual={maxResidual:R}.");
            results.Add(new{Name=pair.Key,Faces=pair.Value.Count,EdgeSamples=samples,MinimumXmm=minX,MaximumXmm=maxX,
                MinimumRadiusMm=minR,MaximumRadiusMm=maxR,MaximumEquationResidualMm=maxResidual});
            Console.WriteLine($"[LEFT END OK] {pair.Key}:X{minX:R}..{maxX:R},N={samples},residual={maxResidual:R}.");
        }
        return new{Measurements=results,Slot=InspectLeftSlotWidth(),Bottom20ReferenceRadiusMm=45,Slot845ReferenceRadiusMm=45,
            BoreEntryAxialLengthSourceAccepted=false,RestoredShoulderIndependentVolumeVerified=false,FullLeftEndAccepted=false};
    }
}

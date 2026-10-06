using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    private sealed record ThreadSectionTarget(string Name,double[] Origin,double[] Axis,double[] U,double[] V,double Pitch,double Major,double Start,double End,bool External=false);
    private sealed record ThreadRaySample(double Station,double Angle,double? Radius,double? NormalAngle,bool Interrupted);

    private List<ThreadSectionTarget> ThreadSectionTargets()
    {
        var targets=new List<ThreadSectionTarget>();
        for(int i=0;i<4;i++)targets.Add(new($"M6_{i+1}",[0,TiffM6Centres[i].Y,TiffM6Centres[i].Z],[1,0,0],[0,1,0],[0,0,1],1,3,250,264));
        foreach(int sign in new[]{1,-1})targets.Add(new($"M12_{(sign==1?1:2)}",[0,sign*56.5,-sign*113*Math.Cos(Math.PI/6)],[1,0,0],[0,1,0],[0,0,1],1.75,6,248.75,262.9));
        targets.Add(new("M90",[0,0,0],[1,0,0],[0,1,0],[0,0,1],2,45,6.75,37.75,true));
        foreach(bool aa in new[]{false,true})
        {
            double a=(aa?120:60)*Math.PI/180,b=(aa?4:10)*Math.PI/180;
            targets.Add(new(aa?"M8_AA":"M8_II",[265,51*Math.Sin(a),-51*Math.Cos(a)],
                [Math.Cos(b),Math.Sin(b)*Math.Sin(a),-Math.Sin(b)*Math.Cos(a)],
                [-Math.Sin(b),Math.Cos(b)*Math.Sin(a),-Math.Cos(b)*Math.Cos(a)],[0,Math.Cos(a),Math.Sin(a)],1.25,4,-4.7,-1.1));
        }
        return targets;
    }

    public object InspectThreadProfilePoints(bool endingsOnly = false, bool customM6 = false, bool allCustomM6=false, bool customEnds=false, bool m8InnerEnds=false, bool m12Ends=false)
    {
        if(customEnds&&!customM6)throw new ArgumentException("Finite M6 ends require the custom M6 profile.");
        Drawing.MetricThreadSectionProfile.RunChecks();
        var body=CopyPilotBody();var reports=new List<object>();
        foreach(var target in ThreadSectionTargets())
        {
            if(m12Ends&&!target.Name.StartsWith("M12_"))continue;
            if(m8InnerEnds&&!target.Name.StartsWith("M8_"))continue;
            if(customM6 && (allCustomM6?!target.Name.StartsWith("M6_"):target.Name!="M6_1"))continue;
            if(endingsOnly && target.Name is not ("M90" or "M8_II" or "M8_AA"))continue;
            string name=target.Name.StartsWith("M6_")?$"TIFF_{target.Name}_THREAD":target.Name.StartsWith("M12_")?$"TRIAL_M12x1_75_7H_nominal_{target.Name[^1]}":target.Name=="M90"?"TRIAL_M90x2_X5_to_X38":$"TIFF_REAR_{target.Name[3..]}_M8_NOMINAL_PENDING_LENGTH";
            if(customM6)name=$"PRECISION_{target.Name}_CUT";
            var faces=((object[])TiffFeature(name).GetFaces()).Cast<Face2>().ToList();
            if(m12Ends)faces.AddRange(((object[])TiffFeature("M12_rear_chamfers_2x45").GetFaces()).Cast<Face2>());
            if(customEnds)faces.AddRange(((object[])TiffFeature($"TIFF_{target.Name}_CHAMFER_1x45").GetFaces()).Cast<Face2>());
            double minor=target.Major-5*Math.Sqrt(3)*target.Pitch/16,crest=target.External?target.Major:minor;
            foreach(Face2 face in (object[])body.GetFaces())
            {
                var surface=(Surface)face.GetSurface();if(!surface.IsCylinder())continue;
                var c=(double[])surface.CylinderParams;
                if(Math.Abs(c[6]*1000-crest)>.00001)continue;
                double dot=Enumerable.Range(0,3).Sum(n=>c[n+3]*target.Axis[n]);
                if(Math.Abs(Math.Abs(dot)-1)>1e-8)continue;
                var offset=target.Origin.Select((v,n)=>c[n]*1000-v).ToArray();double s=offset.Zip(target.Axis).Sum(t=>t.First*t.Second);
                if(Math.Sqrt(offset.Select((v,n)=>Math.Pow(v-s*target.Axis[n],2)).Sum())<.00001)faces.Add(face);
            }
            double[] Point(double s,double a,double phase,int sign)
            {
                double r=Drawing.MetricThreadSectionProfile.Radius(target.Major,target.Pitch,s/target.Pitch+sign*a/(2*Math.PI)+phase,target.External);
                if(m12Ends)r=Math.Max(r,minor+s-263);
                if(customEnds)
                {
                    double delta=Enumerable.Range(0,16).Min(n=>Math.Abs(s-(249.375+a/(2*Math.PI)+n)));
                    double groove=3-Math.Sqrt(3)*Math.Max(0,delta-.0625);
                    r=Math.Max(minor,Math.Max(groove,minor+s-264));
                }
                return target.Origin.Select((v,n)=>(v+s*target.Axis[n]+r*(target.U[n]*Math.Cos(a)+target.V[n]*Math.Sin(a)))/1000).ToArray();
            }
            double Distance(double[] p)
            {
                double best=double.PositiveInfinity;
                foreach(var face in faces)
                {
                    var q=(double[]?)face.GetClosestPointOn(p[0],p[1],p[2]);
                    if(q==null||q.Length<3||!q.Take(3).All(double.IsFinite))continue;
                    best=Math.Min(best,1000*Math.Sqrt(Enumerable.Range(0,3).Sum(n=>Math.Pow(q[n]-p[n],2))));
                }
                if(!double.IsFinite(best))throw new InvalidOperationException("All trimmed-face projections unavailable.");
                return best;
            }
            // Obtain candidate phases from a surviving groove-floor boundary.
            var candidates=new List<(double Phase,int Sign)>();
            if(customM6)
            {
                // The constructed path starts at X249.375 on +Y. Its profile
                // reaches the nominal minor radius at X249: phase is known,
                // not fitted from a potentially distorted sweep boundary.
                candidates.Add((0,-1));
                goto PhaseCandidatesReady;
            }
            foreach(Face2 face in (object[])TiffFeature(name).GetFaces())foreach(Edge edge in (object[])face.GetEdges())
            {
                var bounds=edge.GetCurveParams3();var p=(double[])edge.Evaluate2((bounds.UMinValue+bounds.UMaxValue)/2,0);
                if(p.Length!=4||!p.Take(3).All(double.IsFinite))continue;
                var d=p.Take(3).Select((v,n)=>v*1000-target.Origin[n]).ToArray();
                double s=d.Zip(target.Axis).Sum(t=>t.First*t.Second),u=d.Zip(target.U).Sum(t=>t.First*t.Second),v=d.Zip(target.V).Sum(t=>t.First*t.Second);
                double r=Math.Sqrt(u*u+v*v),angle=Math.Atan2(v,u),floor=target.External?minor:target.Major;
                if(Math.Abs(r-floor)>.00001||s<=target.Start||s>=target.End)continue;
                foreach(int sign in new[]{-1,1})foreach(double boundary in new[]{.3125,target.External?.5625:.4375})
                {
                    double phase=boundary-s/target.Pitch-sign*angle/(2*Math.PI);phase-=Math.Floor(phase);
                    if(!candidates.Any(c=>c.Sign==sign&&Math.Abs(c.Phase-phase)<1e-7))candidates.Add((phase,sign));
                }
                if(candidates.Count>=8)goto PhaseCandidatesReady;
            }
            PhaseCandidatesReady:
            if(candidates.Count==0)throw new InvalidOperationException("No phase reference edge.");
            double middle=(target.Start+target.End)/2;
            bool Interrupted(double s,double[] p)
            {
                if(!target.External)return false;
                double floor=s<=25?39.5:74.5-Math.Sqrt(35*35-Math.Pow(s-25,2));
                return p[1]*1000>floor&&Math.Abs(p[2]*1000)<8;
            }
            double PhaseError(double phase,int sign)=>Enumerable.Range(0,8).Select(k=>Point(middle,2*Math.PI*(k+.173)/8,phase,sign))
                .Where(p=>!Interrupted(middle,p)).Sum(p=>Math.Pow(Distance(p),2));
            var best=candidates.Select(c=>new{c.Phase,c.Sign,Error=PhaseError(c.Phase,c.Sign)}).OrderBy(c=>c.Error).First();
            double phaseLo=best.Phase-.002,phaseHi=best.Phase+.002;
            for(int iteration=0;iteration<(customM6?0:18);iteration++)
            {
                double a=phaseLo+(phaseHi-phaseLo)/3,b=phaseHi-(phaseHi-phaseLo)/3;
                if(PhaseError(a,best.Sign)<PhaseError(b,best.Sign))phaseHi=b;else phaseLo=a;
            }
            double calibratedPhase=customM6?0:(phaseLo+phaseHi)/2;
            var rows=new List<object>();double maximum=0;int skipped=0,count=0;
            int stations=(int)Math.Ceiling((target.End-target.Start)/(target.Pitch/4));
            var sampleStations=customEnds?Enumerable.Range(0,13).Select(j=>248.9+j*.1).Concat(Enumerable.Range(0,12).Select(j=>263.9+j*.1)).ToArray():Enumerable.Range(0,stations+1).Select(j=>target.Start+(target.End-target.Start)*j/stations).ToArray();
            if(m8InnerEnds)
            {
                double first=-6/Math.Cos((target.Name=="M8_AA"?4:10)*Math.PI/180)+.001;
                sampleStations=Enumerable.Range(0,17).Select(j=>first+1.25*j/16).ToArray();
            }
            if(m12Ends)sampleStations=Enumerable.Range(0,17).Select(j=>247.001+1.749*j/16).Concat(Enumerable.Range(0,17).Select(j=>263+1.999*j/16)).ToArray();
            foreach(double s in sampleStations)for(int k=0;k<16;k++)
            {
                double a=2*Math.PI*(k+.173)/16;
                var p=Point(s,a,calibratedPhase,best.Sign);
                if(Interrupted(s,p)){skipped++;continue;}
                double distance=Distance(p);maximum=Math.Max(maximum,distance);count++;
                if(distance>.0001)rows.Add(new{StationMm=s,AngleRadians=a,DistanceMm=distance});
            }
            Console.WriteLine($"[THREAD PROFILE POINTS] {target.Name}: samples={count},slot={skipped},maxDistance={maximum:R},failed={rows.Count}.");
            reports.Add(new{target.Name,Start=sampleStations.Min(),End=sampleStations.Max(),SampleStationsMm=sampleStations,FiniteEnds=customEnds,M8InnerEnd=m8InnerEnds,Phase=calibratedPhase,best.Sign,SampleCount=count,KnownSlotSamplesExcluded=skipped,MaximumDistanceMm=maximum,FailedPoints=rows,SampledProfilePassed=rows.Count==0});
        }
        GC.KeepAlive(body);
        return new{Threads=reports,M12EndInspection=m12Ends,M12EndMethod=m12Ends?"Periodic profile phase fitted only at middle section; max with nominal rear 2x45 chamfer from X263; 17 stations per end and 16 angles; no finite-profile assumption at blind ends":null,DiagnosticBudgetMm=.0001,GeometryChanged=false,ContinuousProfileCertified=false,
            Method=m8InnerEnds?"Nominal periodic profile over first inward M8 pitch, 0.001 mm after existing start plane; phase calibrated only in the previously checked middle section; 17 axial stations by 16 angles. Diagnostic of full profile at blind end, not an assumed runout shape or drawing tolerance":customEnds?"Finite 16-turn helix starting X249.375, 60-degree flanks/P8 flat, max with existing pilot and 1x45 mouth; 16 offset angles at X248.9..250.1 and X263.9..265, 0.1 mm spacing; sampled not continuous":customM6?"Closest point on trimmed final faces at 16 angular phases and <=P/4 axial spacing; prescribed construction phase 0, signed helix phase -1, no phase fitting":
                "Closest point on trimmed final faces at 16 angular phases and <=P/4 axial spacing; phase reference on one edge, chosen by eight middle-station samples"};
    }

    // Retained for diagnosis of RPC_E_SERVERFAULT; not used by the CLI.
    private object InspectThreadRadialSections()
    {
        var targets=ThreadSectionTargets();
        var bodies=(object[])((PartDoc)_model!).GetBodies2((int)swBodyType_e.swSolidBody,false);
        if(bodies.Length!=1)throw new InvalidOperationException("Thread sections require one body.");
        var reports=new List<object>();
        foreach(var target in targets)
        {
            var specs=new List<(double S,double A,double[] Centre,double[] Radial)>();
            // Staggered angular phases avoid repeatedly landing on the same edges.
            int stations=(int)Math.Ceiling((target.End-target.Start)/(target.Pitch/8));
            for(int j=0;j<=stations;j++)for(int k=0;k<24;k++)
            {
                double s=target.Start+(target.End-target.Start)*j/stations,a=2*Math.PI*(k+.173)/24;
                specs.Add((s,a,target.Origin.Select((v,n)=>v+s*target.Axis[n]).ToArray(),target.U.Select((v,n)=>v*Math.Cos(a)+target.V[n]*Math.Sin(a)).ToArray()));
            }
            var rows=new List<ThreadRaySample>();
            for(int first=0;first<specs.Count;first+=256)
            {
                var rays=specs.Skip(first).Take(256).ToArray();
                var starts=rays.SelectMany(r=>r.Centre.Select((v,n)=>(v+(target.External?target.Major+1:0)*r.Radial[n])/1000)).ToArray();
                var directions=rays.SelectMany(r=>r.Radial.Select(v=>target.External?-v:v)).ToArray();
                int hits=_model.Extension.RayIntersections(bodies,starts,directions,(int)swRayPtsOpts_e.swRayPtsOptsNORMALS,1e-9,0,true);
                var hitPoints=(double[]?)_model.GetRayIntersectionsPoints()??[];
                if(hits<0||hitPoints.Length!=hits*9)throw new InvalidOperationException("Unexpected ray result layout.");
                var byRay=new List<double[]>[rays.Length];for(int k=0;k<rays.Length;k++)byRay[k]=new();
                for(int h=0;h<hits;h++)
                {
                    int k=(int)hitPoints[9*h+1];if(k<0||k>=rays.Length)throw new InvalidOperationException("Invalid ray index.");
                    byRay[k].Add(hitPoints.Skip(9*h+3).Take(6).ToArray());
                }
                for(int k=0;k<rays.Length;k++)
                {
                    var ray=rays[k];double? radius=null,angle=null;double nearest=double.PositiveInfinity;
                    foreach(var hit in byRay[k])
                    {
                        double r=Enumerable.Range(0,3).Sum(n=>(hit[n]*1000-ray.Centre[n])*ray.Radial[n]);
                        double distance=target.External?target.Major+1-r:r;
                        if(r<=0||distance<0||distance>=nearest)continue;
                        nearest=distance;radius=r;
                        double na=Enumerable.Range(0,3).Sum(n=>hit[n+3]*target.Axis[n]),nr=Enumerable.Range(0,3).Sum(n=>hit[n+3]*ray.Radial[n]);
                        angle=Math.Atan2(Math.Abs(nr),Math.Abs(na))*180/Math.PI;
                    }
                    bool interrupted=false;
                    if(target.External)
                    {
                        double floor=ray.S<=25?39.5:74.5-Math.Sqrt(35*35-Math.Pow(ray.S-25,2));
                        double low=target.Major-5*Math.Sqrt(3)*target.Pitch/16;
                        interrupted=low*ray.Radial[1]>floor&&Math.Abs(target.Major*ray.Radial[2])<8;
                    }
                    rows.Add(new(ray.S,ray.A,radius,angle,interrupted));
                }
            }
            double Profile(double phase)
            {
                return Drawing.MetricThreadSectionProfile.Radius(target.Major,target.Pitch,phase,target.External);
            }
            var usable=rows.Where(r=>r.Radius.HasValue&&!r.Interrupted).ToArray();
            if(usable.Length<100)throw new InvalidOperationException("Insufficient radial thread intersections.");
            // Calibrate only on the middle station; use remaining stations as holdout.
            double middle=rows.OrderBy(r=>Math.Abs(r.Station-(target.Start+target.End)/2)).First().Station;
            var calibration=usable.Where(r=>r.Station==middle).ToArray();
            if(calibration.Length<16)throw new InvalidOperationException("Thread phase calibration unavailable.");
            double Error(double phase,int sign)=>calibration.Sum(r=>Math.Pow(r.Radius!.Value-Profile(r.Station/target.Pitch+sign*r.Angle/(2*Math.PI)+phase),2));
            int bestSign=1;double bestPhase=0,bestError=double.PositiveInfinity;
            foreach(int sign in new[]{-1,1})for(int k=0;k<512;k++)
            {
                double phase=k/512.0,error=Error(phase,sign);if(error<bestError){bestError=error;bestPhase=phase;bestSign=sign;}
            }
            double lo=bestPhase-1.0/512,hi=bestPhase+1.0/512;
            for(int k=0;k<60;k++){double a=lo+(hi-lo)/3,b=hi-(hi-lo)/3;if(Error(a,bestSign)<Error(b,bestSign))hi=b;else lo=a;}
            bestPhase=(lo+hi)/2;
            var checkedRows=usable.Select(r=>new{r.Station,r.Angle,RadiusMm=r.Radius!.Value,ExpectedRadiusMm=Profile(r.Station/target.Pitch+bestSign*r.Angle/(2*Math.PI)+bestPhase),r.NormalAngle}).ToArray();
            double maximum=checkedRows.Max(r=>Math.Abs(r.RadiusMm-r.ExpectedRadiusMm));
            int missing=rows.Count(r=>!r.Radius.HasValue),interruptions=rows.Count(r=>r.Interrupted);
            bool passed=maximum<=.0001&&missing==0;
            Console.WriteLine($"[RADIAL THREAD] {target.Name}: rays={rows.Count},missing={missing},slot={interruptions},maxResidual={maximum:R},pass={passed}.");
            reports.Add(new{target.Name,target.Pitch,target.Start,target.End,CalibrationStationMm=middle,CalibratedPhase=bestPhase,HelixSign=bestSign,
                Samples=rows.Count,Missing=missing,KnownSlotInterruptions=interruptions,MaximumRadialResidualMm=maximum,SampledProfilePassed=passed,Points=checkedRows});
        }
        return new{Threads=reports,DiagnosticBudgetMm=.0001,GeometryChanged=false,ContinuousProfileCertified=false,
            Scope="Radial intersections with final solid, 24 angular phases and <=P/8 station spacing; phase calibrated on one station, nominal truncated60 profile independently predicted elsewhere"};
    }
}

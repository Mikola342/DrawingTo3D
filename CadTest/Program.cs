using CadTest.Cad;
using CadTest.Drawing;
using System.Text.Json;

try
{
    if(args.Length==2&&args[0] is "--inspect-m8-inner-ends" or "--inspect-m8-blend-interstitial" or "--inspect-m12-end-profiles")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]);const string hash="02ED62B779B538188EA67F4F24E9FCBF703D8571184A5E035B27A393560AA8B8";
        if(Hash(source)!=hash)throw new InvalidOperationException("Pinned working M6 integration required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m8-inner-ends",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"M8_ENDS_"+id+".SLDPRT");File.Copy(source,copy,false);
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        using var batch=cad.BeginApiBatch();var ends=args[0]=="--inspect-m12-end-profiles"?cad.InspectThreadProfilePoints(m12Ends:true):args[0]=="--inspect-m8-blend-interstitial"?cad.InspectM8BlendSurfaces(interstitial:true):cad.InspectThreadProfilePoints(m8InnerEnds:true);var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("M8 check changed saved bytes.");
        File.WriteAllText(Path.Combine(dir,"verification.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,Mode=args[0],Ends=ends,Health=health,SavedBytesUnchanged=true,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("[M8 INNER END REPORT] "+dir);return;
    }
    if(args.Length==2&&args[0] is "--verify-m6-repair-final" or "--verify-m6-integration")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]);const string hash="02ED62B779B538188EA67F4F24E9FCBF703D8571184A5E035B27A393560AA8B8";
        if(Hash(source)!=hash)throw new InvalidOperationException("Pinned overlap repair required.");
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!,"repair.json")));
        bool integration=args[0]=="--verify-m6-integration";
        string previous=manifest.RootElement.GetProperty("SourceFile").GetString()!;
        if(integration)
        {
            using var predecessor=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(previous)!,"native.json")));
            previous=predecessor.RootElement.GetProperty("SourceFile").GetString()!;
        }
        string previousHash=integration?"9A19E8EC04A6F8E15A8570459AAF63A508F2435D35AE588F1B9E859B467D11E7":"BEC09682C1ECDAF1109F9000734AA0AD4FBC5244B4E74C23632CB84FD7698F5A";
        if(Hash(previous)!=previousHash)throw new InvalidOperationException("Repair predecessor mismatch.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m6-repair-final",id));Directory.CreateDirectory(dir);
        string before=Path.Combine(dir,"BEFORE_"+id+".SLDPRT"),after=Path.Combine(dir,"AFTER_"+id+".SLDPRT");File.Copy(previous,before,false);File.Copy(source,after,false);
        var cad=new SolidWorksService();cad.Connect();var locality=cad.InspectPrecisionM6Locality(before,after,integration?4:2);
        File.WriteAllText(Path.Combine(dir,"locality.json"),JsonSerializer.Serialize(locality,new JsonSerializerOptions{WriteIndented=true}));
        using var batch=cad.BeginApiBatch();var ends=integration?null:cad.InspectThreadProfilePoints(customM6:true,allCustomM6:true,customEnds:true);var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(previous)!=previousHash||Hash(before)!=previousHash||Hash(after)!=hash)throw new InvalidOperationException("Saved bytes changed.");
        File.WriteAllText(Path.Combine(dir,"verification.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,Mode=args[0],PredecessorFile=previous,PredecessorSha256=previousHash,Locality=locality,Ends=ends,Health=health,SavedBytesUnchanged=true,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("[M6 FINAL REPAIR REPORT] "+dir);return;
    }
    if(args.Length==2 && args[0] is "--repair-precision-m6-alignment" or "--clear-precision-m6-pilots" or "--repair-precision-m6-overlap")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        if(hash!="BEC09682C1ECDAF1109F9000734AA0AD4FBC5244B4E74C23632CB84FD7698F5A")throw new InvalidOperationException("Pinned precision pattern required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/precision-alignment-repair",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"WORK_"+id+".SLDPRT"),destination=Path.Combine(dir,"M6_REPAIR_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Repair copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        bool clear=args[0]=="--clear-precision-m6-pilots";
        var result=args[0]=="--repair-precision-m6-overlap"?cad.DeepenPrecisionM6ToolOverlap():clear?cad.ClearPrecisionM6Pilots():cad.AlignPrecisionM6Trial(1);var native=cad.PrepareNativeVariant(destination);
        using var batch=cad.BeginApiBatch();var boundaries=cad.InspectPrecisionM6Boundaries();
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("Repair changed source bytes.");
        File.WriteAllText(Path.Combine(dir,"repair.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,ModelFile=destination,ModelSha256=Hash(destination),Result=result,Native=native,Boundaries=boundaries,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("[ALIGNMENT REPAIR REPORT] "+dir);return;
    }
    if(args.Length==2 && args[0] is "--verify-precision-m6-pattern" or "--verify-precision-m6-boundaries" or "--verify-precision-m6-void" or "--verify-precision-m6-profiles" or "--inspect-precision-m6-witnesses" or "--verify-precision-m6-repair")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        string sidecar=Path.Combine(Path.GetDirectoryName(source)!,"native.json");
        bool repairEvidence=!File.Exists(sidecar);
        if(repairEvidence)sidecar=Path.Combine(Path.GetDirectoryName(source)!,"repair.json");
        using var evidence=JsonDocument.Parse(File.ReadAllText(sidecar));
        var root=evidence.RootElement;string predecessor=root.GetProperty("SourceFile").GetString()!;
        string priorHash=repairEvidence?"BEC09682C1ECDAF1109F9000734AA0AD4FBC5244B4E74C23632CB84FD7698F5A":"9A19E8EC04A6F8E15A8570459AAF63A508F2435D35AE588F1B9E859B467D11E7";
        if(repairEvidence&&args[0]=="--verify-precision-m6-pattern")throw new InvalidOperationException("Four-hole replacement comparison is not applicable to a two-hole repair; use targeted checks.");
        if(root.GetProperty("ModelSha256").GetString()!=hash||root.GetProperty("SourceSha256").GetString()!=priorHash||Hash(predecessor)!=priorHash||!string.Equals(root.GetProperty("ModelFile").GetString(),source,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Precision pattern lineage mismatch.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/precision-pattern-verification",id));Directory.CreateDirectory(dir);
        string before=Path.Combine(dir,"BEFORE_"+id+".SLDPRT"),after=Path.Combine(dir,"AFTER_"+id+".SLDPRT");File.Copy(predecessor,before,false);File.Copy(source,after,false);
        if(Hash(before)!=priorHash||Hash(after)!=hash)throw new InvalidOperationException("Pattern verification copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();
        if(args[0] is "--verify-precision-m6-boundaries" or "--verify-precision-m6-void" or "--verify-precision-m6-profiles" or "--inspect-precision-m6-witnesses" or "--verify-precision-m6-repair")
        {
            cad.OpenPartForInspection(after,requireFresh:true);using var boundaryBatch=cad.BeginApiBatch();
            var boundaryResult=args[0]=="--verify-precision-m6-repair"?cad.InspectPrecisionM6Repair():args[0]=="--inspect-precision-m6-witnesses"?cad.InspectPrecisionM6BoundaryWitnesses():args[0]=="--verify-precision-m6-void"?cad.InspectPrecisionM6Void():args[0]=="--verify-precision-m6-profiles"?cad.InspectThreadProfilePoints(customM6:true,allCustomM6:true):cad.InspectPrecisionM6Boundaries();var boundaryHealth=cad.InspectTiffFeatureHealth();
            if(Hash(source)!=hash||Hash(predecessor)!=priorHash||Hash(before)!=priorHash||Hash(after)!=hash)throw new InvalidOperationException("Boundary verification changed saved bytes.");
            File.WriteAllText(Path.Combine(dir,"boundaries.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,Mode=args[0],Boundaries=boundaryResult,Health=boundaryHealth,SavedBytesUnchanged=true},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("[PRECISION BOUNDARIES REPORT] "+dir);return;
        }
        var locality=cad.ComparePrecisionM6Pattern(before,after);
        File.WriteAllText(Path.Combine(dir,"locality.json"),JsonSerializer.Serialize(locality,new JsonSerializerOptions{WriteIndented=true}));
        using var batch=cad.BeginApiBatch();var boundaries=cad.InspectPrecisionM6Boundaries();var profiles=cad.InspectThreadProfilePoints(customM6:true,allCustomM6:true);
        var points=cad.InspectExperimentalM6Points();var m12=cad.InspectM12Trial(true,true);var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(predecessor)!=priorHash||Hash(before)!=priorHash||Hash(after)!=hash)throw new InvalidOperationException("Pattern verification modified saved bytes.");
        File.WriteAllText(Path.Combine(dir,"verification.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,PredecessorSha256=priorHash,Locality=locality,Boundaries=boundaries,Profiles=profiles,Points=points,M12=m12,Health=health,Accepted=false,SavedBytesUnchanged=true},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("[PRECISION PATTERN VERIFICATION REPORT] "+dir);return;
    }
    if(args.Length==2 && args[0]=="--complete-precision-m6-pattern")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        if(hash!="9A19E8EC04A6F8E15A8570459AAF63A508F2435D35AE588F1B9E859B467D11E7")throw new InvalidOperationException("Pinned completed M12 model required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/precision-m6-pattern",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"WORK_"+id+".SLDPRT"),destination=Path.Combine(dir,"Tsapfa_PRECISION_M6_M12.SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Pattern source copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        var changes=new List<object>();
        for(int i=1;i<=4;i++){var build=cad.BuildPrecisionM6Trial(i);var align=cad.AlignPrecisionM6Trial(i);changes.Add(new{Hole=i,Build=build,Align=align});Console.WriteLine("[PRECISION M6 PATTERN] built "+i);}
        var native=cad.PrepareNativeVariant(destination);
        File.WriteAllText(Path.Combine(dir,"native.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,ModelFile=destination,ModelSha256=Hash(destination),Changes=changes,Native=native,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        string comparisonBefore=Path.Combine(dir,"BEFORE_"+id+".SLDPRT"),comparisonAfter=Path.Combine(dir,"AFTER_"+id+".SLDPRT");
        File.Copy(copy,comparisonBefore,false);File.Copy(destination,comparisonAfter,false);
        string modelHash=Hash(destination);
        if(Hash(comparisonBefore)!=hash||Hash(comparisonAfter)!=modelHash)throw new InvalidOperationException("Pattern comparison copy mismatch.");
        var locality=cad.ComparePrecisionM6Pattern(comparisonBefore,comparisonAfter);
        File.WriteAllText(Path.Combine(dir,"locality.json"),JsonSerializer.Serialize(locality,new JsonSerializerOptions{WriteIndented=true}));
        using var batch=cad.BeginApiBatch();var boundaries=cad.InspectPrecisionM6Boundaries();var profiles=cad.InspectThreadProfilePoints(customM6:true,allCustomM6:true);
        var points=cad.InspectExperimentalM6Points();var m12=cad.InspectM12Trial(true,true);var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(copy)!=hash||Hash(destination)!=modelHash||Hash(comparisonBefore)!=hash||Hash(comparisonAfter)!=modelHash)throw new InvalidOperationException("Pattern modified saved bytes.");
        File.WriteAllText(Path.Combine(dir,"verification.json"),JsonSerializer.Serialize(new{SourceSha256=hash,ModelSha256=modelHash,Locality=locality,Boundaries=boundaries,Profiles=profiles,Points=points,M12=m12,Health=health,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("[PRECISION M6 PATTERN REPORT] "+dir);return;
    }
    if(args.Length==2 && args[0]=="--verify-completed-endings-model")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        if(hash!="9A19E8EC04A6F8E15A8570459AAF63A508F2435D35AE588F1B9E859B467D11E7")throw new InvalidOperationException("Pinned completed M6/M12 model required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/completed-endings-verification",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"VERIFY_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Verification copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();
        string predecessor=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m6-start-extension/ea8dbdb5fc6746a09e63c932cabbbcc4/Tsapfa_M6_START_TRIAL.SLDPRT"));
        const string priorHash="9D321AFA4AF759FBCE75D0B06F630625EE327DA35424B706CEDECF9100D39DD9";
        if(Hash(predecessor)!=priorHash)throw new InvalidOperationException("M12 comparison predecessor changed.");
        string beforeCopy=Path.Combine(dir,"BEFORE_"+id+".SLDPRT");File.Copy(predecessor,beforeCopy,false);
        var comparison=cad.CompareCompletedM12Entries(beforeCopy,copy);
        if(Hash(predecessor)!=priorHash||Hash(beforeCopy)!=priorHash)throw new InvalidOperationException("Comparison changed predecessor bytes.");
        using var batch=cad.BeginApiBatch();
        var rows=new Dictionary<string,object>{{"M12BodyComparison",comparison}};
        void Check(string name,Func<object> inspect){rows[name]=inspect();File.WriteAllText(Path.Combine(dir,"progress.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("[COMPLETED ENDINGS CHECK] "+name);}
        Check("M6",()=>cad.InspectTiffM6Pattern(true,false,true));
        Check("M6Points",()=>cad.InspectExperimentalM6Points());
        Check("M12",()=>cad.InspectM12Trial(true,true));
        Check("M90",()=>cad.InspectM90Trial());
        Check("M8Mouths",()=>cad.InspectM8MouthFillets(noEndTrim:true));
        Check("M8Blends",()=>cad.InspectM8BlendSurfaces());
        Check("KG",()=>cad.InspectKgHelicalTrial(true));
        Check("KgEntries",()=>cad.InspectKgEntryChamfers());
        Check("KgPrep",()=>cad.InspectKgPrepTrial());
        Check("DK",()=>cad.InspectDkTrial());
        Check("Splines",()=>cad.VerifyTrialSplineSurfaces());
        Check("Involutes",()=>cad.CheckTrialInvolutes());
        Check("II",()=>cad.InspectTiffIiChannel(true,true));
        Check("AA",()=>cad.InspectTiffAaChannel(true,true,true,true));
        Check("Holes19",()=>cad.InspectFlangeDiameter19());
        Check("Slot",()=>cad.InspectLeftSlotWidth());
        Check("Exterior",()=>cad.InspectTiffExterior());
        Check("RearCollar",()=>cad.InspectTiffRearCollar());
        Check("BoreL",()=>cad.InspectExperimentalLExit(true));
        Check("PP",()=>cad.InspectTiffPpChamfer());
        Check("ChannelPoints",()=>cad.InspectExperimentalChannelPoints(true,true));
        Check("FeedPoints",()=>cad.InspectExperimentalFeedPoints(true));
        Check("RadialPoint",()=>cad.InspectExperimentalRadialPoint());
        Check("M8Transitions",()=>cad.InspectExperimentalM8Transitions());
        Check("LeftEnd",()=>cad.InspectLeftEndNominals());
        Check("Health",()=>cad.InspectTiffFeatureHealth());
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("Verification changed saved bytes.");
        File.WriteAllText(Path.Combine(dir,"verification.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,Checks=rows,SavedBytesUnchanged=true,Accepted=false,Scope="Inherited nominal checks, not complete continuous thread profile acceptance"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("[COMPLETED ENDINGS REPORT] "+dir);return;
    }
    if(args.Length==2 && args[0] is "--complete-m12-entries" or "--repair-m12-start-trim")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        bool trim=args[0]=="--repair-m12-start-trim";
        if(hash!=(trim?"61B15D4625C0DA99571BB80F6517630067776CC98E66F1FC60D4ECADD52DABF4":"9D321AFA4AF759FBCE75D0B06F630625EE327DA35424B706CEDECF9100D39DD9"))throw new InvalidOperationException("Pinned M12 predecessor required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m12-entry-completion",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"WORK_"+id+".SLDPRT"),destination=Path.Combine(dir,"Tsapfa_M6_M12_ENDINGS.SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("M12 copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        var result=trim?cad.TrimCompletedM12Starts():cad.CompleteM12Entries();var native=cad.PrepareNativeVariant(destination);
        using var batch=cad.BeginApiBatch();object m12;bool envelopePassed=false;
        try{m12=cad.InspectM12Trial(completedEntries:true,completedStartTrim:trim);envelopePassed=true;}
        catch(InvalidOperationException e){m12=new{Error=e.Message,Passed=false};Console.WriteLine("[M12 ENVELOPE NOT PASSED] "+e.Message);}
        var m6Points=cad.InspectExperimentalM6Points();var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("M12 completion changed original bytes.");
        File.WriteAllText(Path.Combine(dir,"completion.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,ModelFile=destination,ModelSha256=Hash(destination),Result=result,Native=native,M12=m12,M12EnvelopePassed=envelopePassed,M6Points=m6Points,Health=health,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[M12 COMPLETION REPORT] {dir}");return;
    }
    if(args.Length==2 && args[0] is "--trial-precision-m6" or "--align-precision-m6")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        bool align=args[0]=="--align-precision-m6";
        if(hash!=(align?"01DDBFA86EF57250A101D2AEBBAB10785310966B1F59F2EB6CDBFA99D7FE8593":"9D321AFA4AF759FBCE75D0B06F630625EE327DA35424B706CEDECF9100D39DD9"))throw new InvalidOperationException("Pinned precision predecessor required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/precision-m6",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"WORK_"+id+".SLDPRT"),destination=Path.Combine(dir,"UNVERIFIED_PRECISION_M6.SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Precision copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        var result=align?cad.AlignPrecisionM6Trial():cad.BuildPrecisionM6Trial();var native=cad.PrepareNativeVariant(destination);
        File.WriteAllText(Path.Combine(dir,"native.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,ModelFile=destination,ModelSha256=Hash(destination),Result=result,Native=native,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        using var batch=cad.BeginApiBatch();var sections=cad.InspectThreadProfilePoints(customM6:true);
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("Precision trial changed original bytes.");
        File.WriteAllText(Path.Combine(dir,"trial.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,ModelFile=destination,ModelSha256=Hash(destination),Result=result,Native=native,Sections=sections,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[PRECISION M6 REPORT] {dir}");return;
    }
    if(args.Length==2 && args[0] is "--verify-final-thread-sections" or "--verify-final-thread-endings" or "--verify-precision-m6")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        bool custom=args[0]=="--verify-precision-m6";
        if(custom)
        {
            if(hash!="01DDBFA86EF57250A101D2AEBBAB10785310966B1F59F2EB6CDBFA99D7FE8593")
            {
                using var evidence=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!,"native.json")));
                if(evidence.RootElement.GetProperty("ModelSha256").GetString()!=hash||evidence.RootElement.GetProperty("SourceSha256").GetString()!="01DDBFA86EF57250A101D2AEBBAB10785310966B1F59F2EB6CDBFA99D7FE8593")throw new InvalidOperationException("Precision lineage mismatch.");
            }
        }
        else if(hash!="9D321AFA4AF759FBCE75D0B06F630625EE327DA35424B706CEDECF9100D39DD9")throw new InvalidOperationException("Pinned M6 extension model required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/thread-sections",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"SECTIONS_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Section copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        var sections=cad.InspectThreadProfilePoints(endingsOnly:args[0]=="--verify-final-thread-endings",customM6:custom);var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("Section inspection changed saved bytes.");
        File.WriteAllText(Path.Combine(dir,"sections.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,Sections=sections,Health=health,SavedBytesUnchanged=true},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[THREAD SECTIONS REPORT] {dir}");return;
    }
    if(args.Length==2 && args[0]=="--compare-m6-start-extension")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string revised=Path.GetFullPath(args[1]),buildPath=Path.Combine(Path.GetDirectoryName(revised)!,"trial.json");
        using var build=JsonDocument.Parse(File.ReadAllText(buildPath));var root=build.RootElement;
        string original=root.GetProperty("SourceFile").GetString()!,oldHash=Hash(original),newHash=Hash(revised);
        if(oldHash!="8FC8AF8C31B77A13C6E9E30C0AD5E741F247B7EB4A504A36DF9CAB2F036308E2"||root.GetProperty("ModelSha256").GetString()!=newHash||
            !string.Equals(root.GetProperty("ModelFile").GetString(),revised,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Matching pinned M6 trial required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m6-start-comparison",id));Directory.CreateDirectory(dir);
        string a=Path.Combine(dir,"BEFORE_"+id+".SLDPRT"),b=Path.Combine(dir,"AFTER_"+id+".SLDPRT");File.Copy(original,a,false);File.Copy(revised,b,false);
        if(Hash(a)!=oldHash||Hash(b)!=newHash)throw new InvalidOperationException("Comparison copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();var result=cad.CompareM6StartExtension(a,b);
        if(Hash(original)!=oldHash||Hash(revised)!=newHash||Hash(a)!=oldHash||Hash(b)!=newHash)throw new InvalidOperationException("Comparison changed saved bytes.");
        File.WriteAllText(Path.Combine(dir,"comparison.json"),JsonSerializer.Serialize(new{Original=original,OriginalSha256=oldHash,Revised=revised,RevisedSha256=newHash,Result=result,SavedBytesUnchanged=true},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[M6 START COMPARISON] {dir}");return;
    }
    if(args.Length==2 && args[0]=="--extend-variant-m6-starts")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        if(hash!="8FC8AF8C31B77A13C6E9E30C0AD5E741F247B7EB4A504A36DF9CAB2F036308E2")throw new InvalidOperationException("Pinned native delivery required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m6-start-extension",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"WORK_"+id+".SLDPRT"),destination=Path.Combine(dir,"Tsapfa_M6_START_TRIAL.SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Trial copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        try
        {
            var result=cad.ExtendM6Starts();var native=cad.PrepareNativeVariant(destination);
            var edges=cad.InspectAxialThreadEdges();var points=cad.InspectExperimentalM6Points();
            if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("M6 trial changed source bytes.");
            File.WriteAllText(Path.Combine(dir,"trial.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,ModelFile=destination,ModelSha256=Hash(destination),Result=result,Native=native,Edges=edges,Points=points,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"[M6 START REPORT] {dir}");
        }
        catch
        {
            File.WriteAllText(Path.Combine(dir,"failure.json"),JsonSerializer.Serialize(cad.DiagnoseFeatureErrors(),new JsonSerializerOptions{WriteIndented=true}));
            throw;
        }
        return;
    }
    if(args.Length==2 && args[0]=="--prepare-agreed-sldprt")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        if(hash!="F23826717061925D94F2E85A8DBB4F97A269B50CB697FA3126FCD089BC0D077E")throw new InvalidOperationException("Pinned agreed variant required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/native-delivery",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"WORK_"+id+".SLDPRT"),destination=Path.Combine(dir,"Tsapfa_5309-2304083_AGREED_VARIANT.SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Preparation copy hash mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        var result=cad.PrepareNativeVariant(destination);
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("Preparation changed original bytes.");
        File.WriteAllText(Path.Combine(dir,"native-check.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,ModelFile=destination,ModelSha256=Hash(destination),OriginalBytesUnchanged=true,Result=result,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[PREPARED SLDPRT REPORT] {dir}");return;
    }
    if(args.Length==2 && args[0] is "--audit-variant-m6-mass" or "--audit-variant-thread-edges" or "--audit-variant-m90-short" or "--audit-variant-flanks")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        if(hash!="F23826717061925D94F2E85A8DBB4F97A269B50CB697FA3126FCD089BC0D077E")throw new InvalidOperationException("Pinned agreed variant required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,args[0]=="--audit-variant-thread-edges"?"../../../output/variant-thread-edges":"../../../output/variant-m6-mass",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"M6_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Copy hash mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        var result=args[0]=="--audit-variant-flanks"?cad.InspectThreadFlankNormals():args[0]=="--audit-variant-m6-mass"?cad.InspectTiffM6Pattern(true,true):cad.InspectAxialThreadEdges(args[0]=="--audit-variant-m90-short");var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("M6 audit changed saved bytes.");
        File.WriteAllText(Path.Combine(dir,"audit.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,SavedBytesUnchanged=true,Result=result,Health=health,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[M6 MASS AUDIT REPORT] {dir}");return;
    }
    if(args.Length==2 && args[0]=="--export-agreed-step")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source);
        if(hash!="F23826717061925D94F2E85A8DBB4F97A269B50CB697FA3126FCD089BC0D077E")throw new InvalidOperationException("Pinned agreed variant required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/variant-step",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"VARIANT_"+id+".SLDPRT"),step=Path.Combine(dir,"DESIGN_VARIANT_NOT_FOR_MANUFACTURE.step");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Copy hash mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        var result=cad.ExportVariantStepRoundTrip(step);
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("STEP check changed source bytes.");
        File.WriteAllText(Path.Combine(dir,"roundtrip.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,StepFile=step,StepSha256=Hash(step),SavedBytesUnchanged=true,Result=result,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[STEP ROUNDTRIP REPORT] {dir}");return;
    }
    if(args.Length==2 && args[0] is "--compare-m8-blend-trim" or "--inspect-m8-helical-extents")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source),id=Guid.NewGuid().ToString("N");
        if(hash!="0A8FE538FF34FFD515E7C0C4EF615237E9AE96BF7EB7D87BFF712664EE74D140")throw new InvalidOperationException("Pinned combined trial required.");
        bool extents=args[0]=="--inspect-m8-helical-extents";
        string dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,extents?"../../../output/m8-helical-extents":"../../../output/m8-blend-trim",id));Directory.CreateDirectory(dir);
        string copy=Path.Combine(dir,"TRIM_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Blend comparison copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        var result=extents?cad.InspectM8HelicalExtents():cad.CompareM8BlendBeforeThreads();
        var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("Blend comparison changed saved bytes.");
        string report=Path.Combine(dir,"comparison.json");File.WriteAllText(report,JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,SavedBytesUnchanged=true,Result=result,Health=health},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[{(extents?"M8 HELICAL EXTENTS":"M8 BLEND TRIM COMPARISON")}] {report}");return;
    }
    if(args.Length==2 && args[0]=="--diagnose-feature-errors")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),hash=Hash(source),id=Guid.NewGuid().ToString("N");
        string dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/feature-errors",id));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"ERR_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("Diagnostic copy mismatch.");
        // Failed models may legitimately have load warnings. Log them, never accept this diagnostic as validation.
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:false);using var batch=cad.BeginApiBatch();
        var result=cad.DiagnoseFeatureErrors();
        if(Hash(source)!=hash||Hash(copy)!=hash)throw new InvalidOperationException("Diagnostic changed saved bytes.");
        string report=Path.Combine(dir,"diagnosis.json");File.WriteAllText(report,JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,Result=result,SavedBytesUnchanged=true},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[FEATURE DIAGNOSIS] {report}");return;
    }
    if(args.Length==2 && args[0]=="--verify-m8-mouths")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),buildPath=Path.Combine(Path.GetDirectoryName(source)!,"trial.json"),buildHash=Hash(buildPath),hash=Hash(source);
        using var build=JsonDocument.Parse(File.ReadAllText(buildPath));
        if(build.RootElement.GetProperty("SourceSha256").GetString()!="DFC1E5F8DAFA092AA56BBBFB4FE785A6ABF91F10686C3E2391E9BDEA0AFA6292"||
            !string.Equals(Path.GetFullPath(build.RootElement.GetProperty("ModelFile").GetString()!),source,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Matching current-base M8 mouth trial report required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m8-mouth-verification",id));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"VERIFY_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("M8 mouth verify copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        bool noEndTrim=build.RootElement.GetProperty("Result").TryGetProperty("NoEndTrimExperiment",out var trimFlag)&&trimFlag.GetBoolean();
        var mouths=cad.InspectM8MouthFillets(noEndTrim);var entries=cad.InspectKgEntryChamfers();
        var ii=cad.InspectTiffIiChannel(true,true);var aa=cad.InspectTiffAaChannel(true,true,true,true);
        var holes=cad.InspectFlangeDiameter19();var slot=cad.InspectLeftSlotWidth();var health=cad.InspectTiffFeatureHealth();
        double volume=cad.VerifyCorrectedFlangeVolume(build.RootElement.GetProperty("Result").GetProperty("FinalMm3").GetDouble());
        if(Hash(source)!=hash||Hash(copy)!=hash||Hash(buildPath)!=buildHash)throw new InvalidOperationException("M8 mouth evidence changed.");
        string report=Path.Combine(dir,"verification.json");File.WriteAllText(report,JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,BuildReportSha256=buildHash,
            FreshDiskCopyLoaded=true,SavedBytesUnchanged=true,Mouths=mouths,Entries=entries,II=ii,AA=aa,Flange19=holes,Slot=slot,Health=health,
            VolumeMm3=volume,Accepted=false,FullInheritedRegressionRepeated=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[M8 MOUTH FRESH DIAGNOSTIC; NOT ACCEPTED] {report}");return;
    }
    if(args.Length==2 && args[0]=="--verify-kg-helical")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),buildPath=Path.Combine(Path.GetDirectoryName(source)!,"trial.json"),buildHash=Hash(buildPath),hash=Hash(source);
        using var build=JsonDocument.Parse(File.ReadAllText(buildPath));
        string? parentHash=build.RootElement.GetProperty("SourceSha256").GetString();
        bool combined=parentHash=="892040FDA4C49A6E0F8CE88618BF157A12D7D45099FD3BADF5C1C30A48005EFD";
        if((parentHash!="DFC1E5F8DAFA092AA56BBBFB4FE785A6ABF91F10686C3E2391E9BDEA0AFA6292"&&!combined)||
            !string.Equals(Path.GetFullPath(build.RootElement.GetProperty("ModelFile").GetString()!),source,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Matching KG helical trial report required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/kg-helical-verification",id));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"VERIFY_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=hash)throw new InvalidOperationException("KG helical verify copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        bool customProfile=build.RootElement.GetProperty("Result").TryGetProperty("CustomTruncatedProfile",out var customFlag)&&customFlag.GetBoolean();
        var geometry=cad.InspectKgHelicalTrial(customProfile);
        var mouths=combined?cad.InspectM8MouthFillets(noEndTrim:true):null;
        var blendSurfaces=combined?cad.InspectM8BlendSurfaces():null;
        var dk=combined?cad.InspectDkTrial():null;
        var spline=combined?cad.VerifyTrialSplineSurfaces():null;
        var entries=cad.InspectKgEntryChamfers();
        var preparation=cad.InspectKgPrepTrial();
        var ii=cad.InspectTiffIiChannel(true,true);var aa=cad.InspectTiffAaChannel(true,true,true,true);
        var holes=cad.InspectFlangeDiameter19();var slot=cad.InspectLeftSlotWidth();
        // Repeat the inherited checks on this SAME saved combined body. Earlier reports
        // for ancestors do not establish preservation after M8-mouth/KG operations.
        object? inherited = combined ? new {
            M6=cad.InspectTiffM6Pattern(true), M6Points=cad.InspectExperimentalM6Points(),
            M12=cad.InspectM12Trial(), M90=cad.InspectM90Trial(),
            Exterior=cad.InspectTiffExterior(), RearCollar=cad.InspectTiffRearCollar(),
            BoreL=cad.InspectExperimentalLExit(true), PpChamfer=cad.InspectTiffPpChamfer(),
            ChannelPoints=cad.InspectExperimentalChannelPoints(true,true),
            FeedPoints=cad.InspectExperimentalFeedPoints(true),
            RadialPoint=cad.InspectExperimentalRadialPoint(),
            M8Transitions=cad.InspectExperimentalM8Transitions(),
            LeftEnd=cad.InspectLeftEndNominals(), Involutes=cad.CheckTrialInvolutes()
        } : null;
        var health=cad.InspectTiffFeatureHealth();
        double volume=cad.VerifyCorrectedFlangeVolume(build.RootElement.GetProperty("Result").GetProperty("FinalMm3").GetDouble());
        if(Hash(source)!=hash||Hash(copy)!=hash||Hash(buildPath)!=buildHash)throw new InvalidOperationException("KG trial evidence changed.");
        string report=Path.Combine(dir,"verification.json");File.WriteAllText(report,JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=hash,BuildReportSha256=buildHash,
            FreshDiskCopyLoaded=true,SavedBytesUnchanged=true,CustomTruncatedProfile=customProfile,CombinedM8MouthsAndKgTrial=combined,Mouths=mouths,BlendSurfaces=blendSurfaces,Dk=dk,Spline=spline,Geometry=geometry,Entries=entries,Preparation=preparation,
            II=ii,AA=aa,Flange19=holes,Slot=slot,Inherited=inherited,
            InheritedChecksRepeatedOnCombinedBody=combined,
            Health=health,VolumeMm3=volume,Accepted=false,FullInheritedRegressionRepeated=false,
            RemainingVerification="Full thread profiles/endings, continuous spline/root/runout geometry, left restoration volume discrepancy, M6 volume discrepancy, source acceptance and STEP round-trip remain open."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[KG HELICAL FRESH DIAGNOSTIC; NOT ACCEPTED] {report}");return;
    }
    if(args.Length==2 && args[0] is "--trial-kg-helical" or "--build-agreed-variant")
    {
        string Hash(string p){using var s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));}
        string source=Path.GetFullPath(args[1]),expected=Hash(source);
        if(expected is not ("DFC1E5F8DAFA092AA56BBBFB4FE785A6ABF91F10686C3E2391E9BDEA0AFA6292" or "892040FDA4C49A6E0F8CE88618BF157A12D7D45099FD3BADF5C1C30A48005EFD"))
            throw new InvalidOperationException("Pinned KG-entry or freshly checked experimental M8-mouth source required.");
        bool agreed=args[0]=="--build-agreed-variant";
        if(agreed&&expected!="892040FDA4C49A6E0F8CE88618BF157A12D7D45099FD3BADF5C1C30A48005EFD")
            throw new InvalidOperationException("Agreed variant requires the pinned M8-mouth predecessor without sharp KG cuts.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,agreed?"../../../output/agreed-variant":"../../../output/kg-helical-trial",id));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"WORK_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=expected)throw new InvalidOperationException("KG helical copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        try{
            var result=cad.TrialKgHelicalCut(agreed);string destination=Path.Combine(dir,agreed?"DESIGN_VARIANT_PENDING_CHECK.SLDPRT":"UNVERIFIED_KG_HELICAL.SLDPRT");cad.SaveTestPart(destination);
            File.WriteAllText(Path.Combine(dir,"trial.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=expected,ModelFile=destination,Result=result,Accepted=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"[KG HELICAL TRIAL; NOT ACCEPTED] {destination}");
        }catch(Exception e){try{cad.SaveTestPart(Path.Combine(dir,"UNVERIFIED_FAILED_BUILD.SLDPRT"));}catch(Exception se){Console.Error.WriteLine(se.Message);}
            File.WriteAllText(Path.Combine(dir,"failure.json"),JsonSerializer.Serialize(new{Accepted=false,Error=e.ToString()}));throw;}
        finally{if(Hash(source)!=expected||Hash(copy)!=expected)throw new InvalidOperationException("KG source/copy changed.");}
        return;
    }
    if(args.Length==2 && args[0]=="--verify-kg-entries")
    {
        string Hash(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));}
        string source=Path.GetFullPath(args[1]),buildPath=Path.ChangeExtension(source,".json"),buildHash=Hash(buildPath);
        using var build=JsonDocument.Parse(File.ReadAllText(buildPath));
        string expected=build.RootElement.GetProperty("ModelSha256").GetString()!;
        if(Hash(source)!=expected||!build.RootElement.GetProperty("Result").GetProperty("ExperimentalKgEntriesBuilt").GetBoolean()||
            !string.Equals(Path.GetFullPath(build.RootElement.GetProperty("ModelFile").GetString()!),source,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Matching KG build report required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/kg-entry-verification",id));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"KG_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=expected)throw new InvalidOperationException("KG copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        var entries=cad.InspectKgEntryChamfers();var ii=cad.InspectTiffIiChannel(true,true);var aa=cad.InspectTiffAaChannel(true,true,true,true);
        var holes=cad.InspectFlangeDiameter19();var slot=cad.InspectLeftSlotWidth();var health=cad.InspectTiffFeatureHealth();
        double volume=cad.VerifyCorrectedFlangeVolume(build.RootElement.GetProperty("Result").GetProperty("FinalMm3").GetDouble());
        if(Hash(source)!=expected||Hash(copy)!=expected||Hash(buildPath)!=buildHash)throw new InvalidOperationException("KG evidence changed.");
        string report=Path.Combine(dir,"verification.json");
        File.WriteAllText(report,JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=expected,BuildReport=buildPath,BuildReportSha256=buildHash,
            FreshDiskCopyLoaded=true,SavedBytesUnchanged=true,Entries=entries,II=ii,AA=aa,Flange19=holes,Slot=slot,Health=health,VolumeMm3=volume,
            FullInheritedRegressionRepeated=false,DrawingInterpretationAccepted=false,KgThreadBuilt=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[KG ENTRY FRESH CHECK] {report}");return;
    }
    if(args.Length==3 && args[0] is "--verify-left-shoulders" or "--audit-left-mass")
    {
        string Hash(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));}
        string previous=Path.GetFullPath(args[1]),current=Path.GetFullPath(args[2]);
        const string oldHash="30BFB8F43B63D575850F3DA3F446B4E085EEC2ACBC1DE2609F58A10DC150F277",newHash="3DAE51B34AFF03B950D7F3D671050C2827A5CD9A5E9C0B97514840DA8019AA56";
        if(Hash(previous)!=oldHash||Hash(current)!=newHash)throw new InvalidOperationException("Pinned pre/post slot revisions required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/left-shoulders",id));
        Directory.CreateDirectory(dir);string a=Path.Combine(dir,"OLD_"+id+".SLDPRT"),b=Path.Combine(dir,"NEW_"+id+".SLDPRT");
        File.Copy(previous,a,false);File.Copy(current,b,false);
        if(Hash(a)!=oldHash||Hash(b)!=newHash)throw new InvalidOperationException("Shoulder copies mismatch.");
        var cad=new SolidWorksService();cad.Connect();using var batch=cad.BeginApiBatch();
        var result=cad.VerifyLeftShoulderRestoration(a,b,args[0]=="--audit-left-mass");
        if(Hash(a)!=oldHash||Hash(previous)!=oldHash||Hash(b)!=newHash||Hash(current)!=newHash)throw new InvalidOperationException("Shoulder audit changed saved bytes.");
        string report=Path.Combine(dir,"verification.json");
        File.WriteAllText(report,JsonSerializer.Serialize(new{PreviousFile=previous,PreviousSha256=oldHash,CurrentFile=current,CurrentSha256=newHash,
            PreviousCopy=a,CurrentCopy=b,FreshDiskCopiesLoaded=true,SavedBytesUnchanged=true,GeometryChanged=false,Result=result},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[LEFT SHOULDERS AUDIT; CHECK ENVELOPE FLAG] {report}");return;
    }
    if(args.Length==2 && args[0]=="--verify-left-end")
    {
        string source=Path.GetFullPath(args[1]);
        string Hash(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));}
        const string expected="3DAE51B34AFF03B950D7F3D671050C2827A5CD9A5E9C0B97514840DA8019AA56";
        if(Hash(source)!=expected)throw new InvalidOperationException("Pinned slot16 revision required.");
        string id=Guid.NewGuid().ToString("N"),dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/left-end-verification",id));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"LEFT_"+id+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=expected)throw new InvalidOperationException("Left end copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        var result=cad.InspectLeftEndNominals();var health=cad.InspectTiffFeatureHealth();
        if(Hash(source)!=expected||Hash(copy)!=expected)throw new InvalidOperationException("Left end audit changed saved bytes.");
        string report=Path.Combine(dir,"verification.json");
        File.WriteAllText(report,JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=expected,VerifiedCopy=copy,
            FreshDiskCopyLoaded=true,SavedBytesUnchanged=true,GeometryChanged=false,Result=result,Health=health},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[LEFT END VERIFIED] {report}");return;
    }
    if(args.Length==2 && args[0] is "--correct-left-slot-width" or "--build-kg-entries")
    {
        string source=Path.GetFullPath(args[1]);
        string Hash(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));}
        bool kg=args[0]=="--build-kg-entries";
        string expected=kg?"3DAE51B34AFF03B950D7F3D671050C2827A5CD9A5E9C0B97514840DA8019AA56":"30BFB8F43B63D575850F3DA3F446B4E085EEC2ACBC1DE2609F58A10DC150F277";
        if(Hash(source)!=expected)throw new InvalidOperationException("Pinned M8 transition source required for slot correction.");
        var parent=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.ChangeExtension(source,".json")))!;
        string dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,kg?"../../../output/kg-entries":"../../../output/left-slot",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"WORK_"+Path.GetFileName(dir)+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=expected)throw new InvalidOperationException("Slot source copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        try
        {
            var result=JsonSerializer.SerializeToNode(kg?cad.BuildKgEntryChamfers():cad.CorrectLeftSlotWidth())!;
            string destination=Path.Combine(dir,kg?"Tsapfa_EXPERIMENT_KG_ENTRIES.SLDPRT":"Tsapfa_LEFT_SLOT16.SLDPRT");
            cad.SaveTestPart(destination);cad.ExportTrialPreview(Path.ChangeExtension(destination,".preview.png"));
            var inherited=result.AsObject();
            foreach(var property in parent["Result"]!.AsObject())
                if(property.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<bool>(out var flag))
                    inherited[property.Key]=flag;
            inherited["InheritedBuildReport"]=Path.ChangeExtension(source,".json");
            inherited["InheritedBuildReportSha256"]=Hash(Path.ChangeExtension(source,".json"));
            File.WriteAllText(Path.ChangeExtension(destination,".json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=expected,
                ModelFile=destination,ModelSha256=Hash(destination),GeometryChanged=true,Result=inherited,FullTiffConformanceChecked=false},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"[{(kg?"KG ENTRIES":"LEFT SLOT")} SAVED; FRESH CHECK REQUIRED] {destination}");
        }
        catch(Exception failure)
        {
            try{cad.SaveTestPart(Path.Combine(dir,"UNVERIFIED_FAILED_BUILD.SLDPRT"));}catch(Exception e){Console.Error.WriteLine(e.Message);}
            File.WriteAllText(Path.Combine(dir,"failure.json"),JsonSerializer.Serialize(new{Accepted=false,Error=failure.ToString()}));throw;
        }
        finally{if(Hash(source)!=expected||Hash(copy)!=expected)throw new InvalidOperationException("Slot source/copy bytes changed.");}
        return;
    }
    if(args.Length==2 && args[0] is "--trial-m8-mouth-r05" or "--trial-m8-mouth-before-thread" or "--trial-m8-mouth-rollback" or "--trial-m8-mouth-no-trim")
    {
        string source=Path.GetFullPath(args[1]);
        string Hash(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));}
        string expected=args[0] is "--trial-m8-mouth-rollback" or "--trial-m8-mouth-no-trim"?"DFC1E5F8DAFA092AA56BBBFB4FE785A6ABF91F10686C3E2391E9BDEA0AFA6292":"30BFB8F43B63D575850F3DA3F446B4E085EEC2ACBC1DE2609F58A10DC150F277";
        if(Hash(source)!=expected)throw new InvalidOperationException("Pinned M8 transition source required.");
        string dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/m8-mouth-trial",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"MOUTH_"+Guid.NewGuid().ToString("N")+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=expected)throw new InvalidOperationException("Mouth copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);using var batch=cad.BeginApiBatch();
        try
        {
            var result=cad.TrialM8MouthFillets(dir,args[0]=="--trial-m8-mouth-before-thread",args[0] is "--trial-m8-mouth-rollback" or "--trial-m8-mouth-no-trim",args[0]=="--trial-m8-mouth-no-trim");string destination=Path.Combine(dir,"UNVERIFIED_M8_MOUTHS.SLDPRT");
            cad.SaveTestPart(destination);
            File.WriteAllText(Path.Combine(dir,"trial.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=expected,ModelFile=destination,Accepted=false,Result=result},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"[UNVERIFIED M8 MOUTH TRIAL] {destination}");
        }
        catch(Exception failure)
        {
            try{cad.SaveTestPart(Path.Combine(dir,"UNVERIFIED_FAILED_BUILD.SLDPRT"));}
            catch(Exception saveFailure){Console.Error.WriteLine(saveFailure.Message);}
            File.WriteAllText(Path.Combine(dir,"failure.json"),JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=expected,Accepted=false,Error=failure.ToString()}));throw;
        }
        finally{if(Hash(source)!=expected||Hash(copy)!=expected)throw new InvalidOperationException("M8 mouth source/copy bytes changed.");}
        return;
    }
    if(args.Length==2 && args[0] is "--verify-m8-definitions" or "--verify-other-geometry")
    {
        string source=Path.GetFullPath(args[1]);
        string Hash(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));}
        const string expected="30BFB8F43B63D575850F3DA3F446B4E085EEC2ACBC1DE2609F58A10DC150F277";
        string sourceHash=Hash(source);
        bool correctedSlot=sourceHash=="3DAE51B34AFF03B950D7F3D671050C2827A5CD9A5E9C0B97514840DA8019AA56";
        if(sourceHash!=expected&&!correctedSlot)throw new InvalidOperationException("Pinned M8-transition or slot16 revision required.");
        bool other=args[0]=="--verify-other-geometry";
        string dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,other?"../../../output/other-geometry-verification":"../../../output/m8-definition-verification",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir);string copy=Path.Combine(dir,"VERIFY_"+Path.GetFileName(dir)+".SLDPRT");File.Copy(source,copy,false);
        if(Hash(copy)!=sourceHash)throw new InvalidOperationException("M8 definition copy mismatch.");
        var cad=new SolidWorksService();cad.Connect();cad.OpenPartForInspection(copy,requireFresh:true);
        using var batch=cad.BeginApiBatch();
        object result=other ? new {Dk=cad.InspectDkTrial(),Spline=cad.VerifyTrialSplineSurfaces(),LeftSlot=correctedSlot?cad.InspectLeftSlotWidth():null,Health=cad.InspectTiffFeatureHealth(),
            DrawingInterpretationAccepted=false,FullSplineProfileVerified=false,DkDatumAccepted=false,Scope="Current saved model: inherited approximate DK and spline surface checks only."} : cad.InspectCurrentM8Definitions();
        if(Hash(source)!=sourceHash||Hash(copy)!=sourceHash)throw new InvalidOperationException("M8 definition audit changed bytes.");
        string report=Path.Combine(dir,"verification.json");
        File.WriteAllText(report,JsonSerializer.Serialize(new{SourceFile=source,SourceSha256=sourceHash,VerifiedCopy=copy,
            FreshDiskCopyLoaded=true,SavedBytesUnchanged=true,GeometryChanged=false,Result=result},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"[{(other?"OTHER GEOMETRY":"M8 DEFINITIONS")} VERIFIED] {report}");return;
    }
    if (args.Length == 2 && args[0] == "--finish-tiff-m6-checkpoint")
    {
        var source = Path.GetFullPath(args[1]);
        string Hash(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
        const string expected = "CA7F0A4CFF628E7A2EEC94A74D2F27BBA1FD45200F660E2AB2B36EE3DEEA2FE9";
        if (Hash(source) != expected) throw new InvalidOperationException("Known M6 diagnostic checkpoint required.");
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../output/tiff-m6-checked", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir); var copy = Path.Combine(dir, "M6_CHECK_COPY.SLDPRT"); File.Copy(source, copy, false);
        if (Hash(copy) != expected) throw new InvalidOperationException("M6 checkpoint copy mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy, requireFresh: true);
        using var batch = cad.BeginApiBatch();
        var result = cad.FinishTiffM6Pattern();
        var destination = Path.Combine(dir, "Tsapfa_TIFF_M6_WIP.SLDPRT");
        cad.SaveTestPart(destination); cad.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        if (Hash(source) != expected) throw new InvalidOperationException("M6 checkpoint source changed.");
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = expected, ModelFile = destination, Result = result,
            GeometryChanged = true, GeometryChangeOccurredInCheckpoint = true, IsManufacturingReady = false, TiffConformancePercent = (double?)null
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[TIFF WIP SAVED] {destination}"); return;
    }
    if (args.Length == 1 && args[0] == "--diagnose-known-rear-m8")
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../output"));
        var work = Path.Combine(root, "tiff-rear/df02fb962a264726a651b52e21a10846/TIFF_WORK_df02fb962a264726a651b52e21a10846.SLDPRT");
        var dir = Path.Combine(root, "tiff-rear-diagnostic", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var cad = new SolidWorksService(); cad.Connect();
        var result = cad.DiagnoseKnownRearM8(work, Path.Combine(dir, "UNVERIFIED_REAR_CHECKPOINT.SLDPRT"));
        File.WriteAllText(Path.Combine(dir, "m8-diagnostic.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return;
    }
    if (args.Length == 1 && args[0] == "--resume-known-tiff-rear")
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../output"));
        var source = Path.Combine(root, "tiff-aa/ef557f09684a4b76a190601a7a80029d/Tsapfa_TIFF_AA_WIP.SLDPRT");
        var work = Path.Combine(root, "tiff-rear/df02fb962a264726a651b52e21a10846/TIFF_WORK_df02fb962a264726a651b52e21a10846.SLDPRT");
        string Hash(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
        const string expected = "D79F6497ED817948C3E5AE4D351F4848B804ADA602C4B9050D8FC6BC902630BB";
        if (Hash(source) != expected || Hash(work) != expected) throw new InvalidOperationException("Known rear source/work bytes changed.");
        var dir = Path.Combine(root, "tiff-rear-resumed", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var cad = new SolidWorksService(); cad.Connect(); var result = cad.ResumeTiffRearDatum(work);
        var destination = Path.Combine(dir, "Tsapfa_TIFF_REAR_DATUM_WIP.SLDPRT");
        cad.SaveTestPart(destination); cad.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        if (Hash(source) != expected || Hash(work) != expected) throw new InvalidOperationException("Source/work bytes changed during resume.");
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = expected, ResumedUnsavedWork = work, ModelFile = destination,
            Result = result, GeometryChanged = true, IsManufacturingReady = false, TiffConformancePercent = (double?)null
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[TIFF WIP SAVED] {destination}"); return;
    }
    if (args.Length == 2 && args[0] is "--trial-tiff-l-exit" or "--complete-trial-l-profile" or "--trial-m6-points" or "--trial-channel-points" or "--trial-feed-points" or "--trial-radial-point" or "--trial-m8-transitions")
    {
        bool completeL = args[0] == "--complete-trial-l-profile";
        bool m6Points = args[0] == "--trial-m6-points";
        bool channelPoints = args[0] == "--trial-channel-points";
        bool feedPoints = args[0] == "--trial-feed-points";
        bool radialPoint = args[0] == "--trial-radial-point";
        bool m8Transitions = args[0] == "--trial-m8-transitions";
        var source = Path.GetFullPath(args[1]);
        string Hash(string path) { using var s = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)); }
        string expected = channelPoints ? "E729D2C6269FB345C6B7E1384469FD5695C8B9DE4E2639CDD030F08253EA1386" : m6Points ? "0855E597CB8B26370D6E811103ED26EAE5A9939E8AFAA51F80182629AD6359A1" : completeL ? "E50990BB83476BB13D69E1A86B975ADF4ABC876A3A6A6EB48FCA8B6E12DC96B1" : "3DDF18B41FA38C74FD149ACE71DF30B92363EF643C289498B1F1680FB37F6CC4";
        if (feedPoints) expected = "966D87E47D17FE4FE7F9DBD8D4A0F9E7C2CC083C998228F939BAAEA11960AF9B";
        if (radialPoint) expected = "268E953889D160545AB4E6BF1B82FF1616C8C79FB1CF9912B0ACE58229ED6D4A";
        if (m8Transitions) expected = "E2DAA128692AC4C2C0C164D4589E2015F0192064569BAE3F0013DFB7D4FE5B2B";
        if (Hash(source) != expected) throw new InvalidOperationException("Pinned verified predecessor required.");
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output",channelPoints ? "experimental-channel-points" : m6Points ? "experimental-m6-points" : completeL ? "experimental-l-profile" : "experimental-l-exit",Guid.NewGuid().ToString("N")));
        if (feedPoints) dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/experimental-feed-points",Guid.NewGuid().ToString("N")));
        if (radialPoint) dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/experimental-radial-point",Guid.NewGuid().ToString("N")));
        if (m8Transitions) dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../output/experimental-m8-transitions",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir); var copy = Path.Combine(dir,"L_EXPERIMENT_WORK.SLDPRT"); File.Copy(source,copy,false);
        if (Hash(copy) != expected) throw new InvalidOperationException("Experimental copy mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy,requireFresh:true);
        using var batch = cad.BeginApiBatch();
        object result;
        try { result = m8Transitions ? cad.BuildExperimentalM8Transitions() : radialPoint ? cad.BuildExperimentalRadialPoint() : feedPoints ? cad.BuildExperimentalFeedPoints() : channelPoints ? cad.BuildExperimentalChannelPoints() : m6Points ? cad.BuildExperimentalM6Points() : completeL ? cad.CompleteExperimentalLProfile() : cad.BuildExperimentalLExit(); }
        catch (Exception failure)
        {
            try {
                var failed = Path.Combine(dir,"UNVERIFIED_FAILED_BUILD.SLDPRT"); cad.SaveTestPart(failed);
                File.WriteAllText(Path.Combine(dir,"failure-diagnostic.json"),JsonSerializer.Serialize(new { Accepted=false,SourceFile=source,SourceSha256=expected,Error=failure.ToString() }));
            } catch (Exception saveFailure) { Console.Error.WriteLine("Diagnostic save failed: "+saveFailure.Message); }
            throw;
        }
        var destination = Path.Combine(dir,channelPoints ? "Tsapfa_EXPERIMENT_CHANNEL_POINTS.SLDPRT" : m6Points ? "Tsapfa_EXPERIMENT_M6_POINTS.SLDPRT" : completeL ? "Tsapfa_EXPERIMENT_L_PROFILE.SLDPRT" : "Tsapfa_EXPERIMENT_L_EXIT.SLDPRT");
        if (feedPoints) destination = Path.Combine(dir,"Tsapfa_EXPERIMENT_FEED_POINTS.SLDPRT");
        if (radialPoint) destination = Path.Combine(dir,"Tsapfa_EXPERIMENT_RADIAL_POINT.SLDPRT");
        if (m8Transitions) destination = Path.Combine(dir,"Tsapfa_EXPERIMENT_M8_TRANSITIONS.SLDPRT");
        cad.SaveTestPart(destination); cad.ExportTrialPreview(Path.ChangeExtension(destination,".preview.png"));
        if (Hash(source) != expected) throw new InvalidOperationException("Verified base changed.");
        File.WriteAllText(Path.ChangeExtension(destination,".json"),JsonSerializer.Serialize(new {
            SourceFile=source,SourceSha256=expected,ModelFile=destination,Result=result,GeometryChanged=true,
            IsExperimental=true,IsManufacturingReady=false,TiffConformancePercent=(double?)null
        },new JsonSerializerOptions { WriteIndented=true }));
        Console.WriteLine($"[EXPERIMENT SAVED] {destination}"); return;
    }
    if (args.Length == 2 && args[0] == "--diagnose-tiff-pp")
    {
        var source = Path.GetFullPath(args[1]);
        string Hash(string path) { using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)); }
        string hash = Hash(source);
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../output/pp-diagnostic", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir); var copy = Path.Combine(dir, "PP_DIAGNOSTIC.SLDPRT"); File.Copy(source, copy, false);
        if (Hash(copy) != hash) throw new InvalidOperationException("PP diagnostic copy mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy, requireFresh: true);
        using var batch = cad.BeginApiBatch();
        try { Console.WriteLine(JsonSerializer.Serialize(cad.InspectTiffPpChamfer())); }
        finally { if (Hash(source) != hash || Hash(copy) != hash) throw new InvalidOperationException("PP diagnostic changed saved bytes."); }
        return;
    }
    if (args.Length == 3 && args[0] is "--rebuild-tiff-exterior" or "--build-tiff-m6" or "--build-tiff-pp-chamfer")
    {
        bool exteriorRevision = args[0] == "--rebuild-tiff-exterior";
        bool ppRevision = args[0] == "--build-tiff-pp-chamfer";
        var source = Path.GetFullPath(args[1]); var evidenceFile = Path.GetFullPath(args[2]);
        string Hash(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
        string sourceHash = Hash(source), evidenceHash = Hash(evidenceFile);
        using var evidence = JsonDocument.Parse(File.ReadAllText(evidenceFile));
        var e = evidence.RootElement;
        bool Flag(string name) => e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        if (!string.Equals(Path.GetFullPath(e.GetProperty("SourceFile").GetString()!), source, StringComparison.OrdinalIgnoreCase)
            || e.GetProperty("SourceSha256").GetString() != sourceHash || !Flag("FreshDiskCopyLoaded") || !Flag("SavedBytesUnchanged")
            || !Flag("RearCollarRebuilt") || !Flag("AaChannelRebuilt") || !Flag("RightM8Rebuilt") || !Flag("LeftM8Rebuilt") || !Flag("DetailLCoreBuilt")
            || (ppRevision ? !Flag("M6PatternBuilt") || Flag("PpChamferBuilt") : Flag("M6PatternBuilt"))
            || (exteriorRevision ? Flag("OuterProfileRebuilt") : !Flag("OuterProfileRebuilt")))
            throw new InvalidOperationException("Matching fresh verified predecessor required: rear -> exterior -> M6.");
        var buildFile = Path.ChangeExtension(source, ".json");
        if (Hash(buildFile) != e.GetProperty("BuildReportSha256").GetString()) throw new InvalidOperationException("Predecessor build evidence changed.");
        string id = Guid.NewGuid().ToString("N");
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../output", ppRevision ? "tiff-pp-chamfer" : exteriorRevision ? "tiff-exterior" : "tiff-m6", id));
        Directory.CreateDirectory(dir); var copy = Path.Combine(dir, $"TIFF_WORK_{id}.SLDPRT"); File.Copy(source, copy, false);
        if (Hash(copy) != sourceHash) throw new InvalidOperationException("Copy mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy, requireFresh: true);
        cad.VerifyCorrectedFlangeVolume(e.GetProperty("VolumeMm3").GetDouble());
        using var apiBatch = cad.BeginApiBatch();
        object result;
        try { result = ppRevision ? cad.BuildTiffPpChamfer() : exteriorRevision ? cad.RebuildTiffExterior() : cad.BuildTiffM6Pattern(); }
        catch (Exception buildError)
        {
            // Preserve diagnostic geometry without publishing a normal build/verification report.
            // Unique revision directory prevents overwriting a previously accepted model.
            var checkpoint = Path.Combine(dir, "UNVERIFIED_FAILED_BUILD.SLDPRT");
            try
            {
                cad.SaveTestPart(checkpoint);
                File.WriteAllText(Path.Combine(dir, "failure-diagnostic.json"), JsonSerializer.Serialize(new {
                    SourceFile = source, SourceSha256 = sourceHash, Checkpoint = checkpoint,
                    Accepted = false, Error = buildError.ToString()
                }, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"[UNVERIFIED FAILURE CHECKPOINT] {checkpoint}");
            }
            catch (Exception checkpointError) { Console.Error.WriteLine("Failure checkpoint also failed: " + checkpointError.Message); }
            throw;
        }
        if (Hash(source) != sourceHash || Hash(evidenceFile) != evidenceHash) throw new InvalidOperationException("Predecessor evidence changed.");
        var destination = Path.Combine(dir, ppRevision ? "Tsapfa_TIFF_PP_CHAMFER_WIP.SLDPRT" : exteriorRevision ? "Tsapfa_TIFF_EXTERIOR_WIP.SLDPRT" : "Tsapfa_TIFF_M6_WIP.SLDPRT");
        cad.SaveTestPart(destination); cad.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = sourceHash, PredecessorVerification = evidenceFile, PredecessorVerificationSha256 = evidenceHash,
            ModelFile = destination, Result = result, GeometryChanged = true, IsManufacturingReady = false, TiffConformancePercent = (double?)null
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (Hash(source) != sourceHash || Hash(evidenceFile) != evidenceHash) throw new InvalidOperationException("Source changed after save.");
        Console.WriteLine($"[TIFF WIP SAVED] {destination}"); return;
    }
    if (args.Length == 2 && args[0] is "--rebuild-tiff-m8" or "--rebuild-tiff-bore-start" or "--rebuild-tiff-bore80" or "--restore-tiff-bore80" or "--build-tiff-l-core" or "--rebuild-tiff-aa" or "--rebuild-tiff-rear")
    {
        bool rearRevision = args[0] == "--rebuild-tiff-rear";
        bool aaRevision = args[0] == "--rebuild-tiff-aa";
        bool detailL = args[0] == "--build-tiff-l-core";
        bool restore80 = args[0] == "--restore-tiff-bore80";
        bool bore80 = args[0] == "--rebuild-tiff-bore80";
        bool boreRevision = args[0] == "--rebuild-tiff-bore-start";
        var source = Path.GetFullPath(args[1]);
        string Hash(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
        string hash = Hash(source);
        string requiredHash = rearRevision ? "D79F6497ED817948C3E5AE4D351F4848B804ADA602C4B9050D8FC6BC902630BB"
            : aaRevision ? "A88BC2C67A11AC5E407F41622932970046B7852269CBDACD9AEE0561801B1147"
            : detailL ? "78D0CD5EC34CCC1A72C8B7FC5B047926BFBE28A84741CB34F7A4016C314D003D"
            : restore80 ? "B455A1F22ACE5B47F1DB8E307B4B058104409B6F34A1A8001B511D3A6D0388FC"
            : bore80 ? "55F365670E96C126A053A4F1AD810BCF76F97126B75784FD72188E7D86C7E708"
            : boreRevision ? "3718440ACD31E6CC221C1E8C3ADC0DEBD713BA4E73860E01039F2234B1EE26E4"
            : "4F592E305EA5AA290E8AF6FED3FFEE154EA04B0BFF8DC74364EA5665DFF4ED75";
        if (hash != requiredHash) throw new InvalidOperationException("Verified TIFF baseline required for this operation.");
        string id = Guid.NewGuid().ToString("N"); var dir = bore80 || restore80 || detailL || aaRevision || rearRevision
            ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rearRevision ? "../../../output/tiff-rear" : aaRevision ? "../../../output/tiff-aa" : detailL ? "../../../output/tiff-l-core" : restore80 ? "../../../output/tiff-bore-restored" : "../../../output/tiff-bore80", id))
            : Path.Combine(Path.GetDirectoryName(source)!, boreRevision ? "bore-revision" : "m8-revision", id);
        Directory.CreateDirectory(dir); var copy = Path.Combine(dir, $"TIFF_WORK_{id}.SLDPRT"); File.Copy(source, copy, false);
        if (Hash(copy) != hash) throw new InvalidOperationException("Copy mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy, requireFresh: true);
        var result = rearRevision ? cad.RebuildTiffRearDatum() : aaRevision ? cad.RebuildTiffAaChannel() : detailL ? cad.BuildTiffDetailLCore() : restore80 ? cad.RestoreTiffBore80() : bore80 ? cad.RebuildTiffBore80() : boreRevision ? cad.RebuildTiffBoreStart() : cad.RebuildTiffM8();
        if (Hash(source) != hash) throw new InvalidOperationException("Source changed.");
        var destination = Path.Combine(dir, rearRevision ? "Tsapfa_TIFF_REAR_DATUM_WIP.SLDPRT" : aaRevision ? "Tsapfa_TIFF_AA_WIP.SLDPRT" : detailL ? "Tsapfa_TIFF_L_CORE_WIP.SLDPRT" : restore80 ? "Tsapfa_TIFF_BORE80_RESTORED_WIP.SLDPRT" : bore80 ? "Tsapfa_TIFF_BORE80_PARTIAL_WIP.SLDPRT" : boreRevision ? "Tsapfa_TIFF_BORE68_X181_WIP.SLDPRT" : "Tsapfa_TIFF_II_M8_NOMINAL_WIP.SLDPRT"); cad.SaveTestPart(destination);
        cad.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = hash, ModelFile = destination, Result = result,
            GeometryChanged = true, IsManufacturingReady = false, TiffConformancePercent = (double?)null
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (Hash(source) != hash) throw new InvalidOperationException("Source changed after save.");
        Console.WriteLine($"[TIFF WIP SAVED] {destination}"); return;
    }
    if (args.Length == 2 && args[0] == "--verify-tiff-ii")
    {
        var source = Path.GetFullPath(args[1]);
        string Hash(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
        var buildFile = Path.ChangeExtension(source, ".json"); var buildHash = Hash(buildFile);
        using var build = JsonDocument.Parse(File.ReadAllText(buildFile));
        if (!string.Equals(Path.GetFullPath(build.RootElement.GetProperty("ModelFile").GetString()!), source, StringComparison.OrdinalIgnoreCase)
            || !build.RootElement.GetProperty("GeometryChanged").GetBoolean()) throw new InvalidOperationException("Matching TIFF build report required.");
        double expected = build.RootElement.GetProperty("Result").GetProperty("FinalMm3").GetDouble();
        string hash = Hash(source), id = Guid.NewGuid().ToString("N");
        // Keep verification paths short: deeply nested CAD revision paths can exceed SolidWorks limits.
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../output/tiff-verification", id));
        Directory.CreateDirectory(dir);
        var copy = Path.Combine(dir, $"TIFF_II_VERIFY_{id}.SLDPRT"); File.Copy(source, copy, false);
        if (Hash(copy) != hash || Hash(source) != hash) throw new InvalidOperationException("Copy mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy, requireFresh: true);
        bool hasRear = build.RootElement.GetProperty("Result").TryGetProperty("RearCollarRebuilt", out var rearFlag) && rearFlag.GetBoolean();
        using var apiBatch = cad.BeginApiBatch();
        bool channelPoints = build.RootElement.GetProperty("Result").TryGetProperty("ExperimentalChannelPointsBuilt",out var channelFlag) && channelFlag.GetBoolean();
        bool feedPoints = build.RootElement.GetProperty("Result").TryGetProperty("ExperimentalFeedPointsBuilt",out var feedFlag) && feedFlag.GetBoolean();
        bool radialPoint = build.RootElement.GetProperty("Result").TryGetProperty("ExperimentalRadialPointBuilt",out var radialFlag) && radialFlag.GetBoolean();
        var geometry = cad.InspectTiffIiChannel(hasRear,channelPoints);
        var channelEnds = channelPoints ? cad.InspectExperimentalChannelPoints(feedPoints,radialPoint) : null;
        var feedEnds = feedPoints ? cad.InspectExperimentalFeedPoints(radialPoint) : null;
        var radialEnd = radialPoint ? cad.InspectExperimentalRadialPoint() : null;
        bool m8Transitions = build.RootElement.GetProperty("Result").TryGetProperty("ExperimentalM8TransitionsBuilt",out var m8TransitionFlag) && m8TransitionFlag.GetBoolean();
        var m8TransitionGeometry = m8Transitions ? cad.InspectExperimentalM8Transitions() : null;
        bool hasM8 = build.RootElement.GetProperty("Result").TryGetProperty("RightM8Rebuilt", out var m8flag) && m8flag.GetBoolean();
        var m8 = hasM8 ? cad.InspectTiffM8(rearDatum: hasRear) : null;
        bool hasAa = build.RootElement.GetProperty("Result").TryGetProperty("AaChannelRebuilt", out var aaFlag) && aaFlag.GetBoolean();
        bool hasLeftM8 = build.RootElement.GetProperty("Result").TryGetProperty("LeftM8Rebuilt", out var leftM8Flag) && leftM8Flag.GetBoolean();
        var aa = hasAa ? cad.InspectTiffAaChannel(hasRear,channelPoints,feedPoints,radialPoint) : null;
        var leftM8 = hasLeftM8 ? cad.InspectTiffM8(aa: true, rearDatum: hasRear) : null;
        var rearCollar = hasRear ? cad.InspectTiffRearCollar() : null;
        bool hasOuter = build.RootElement.GetProperty("Result").TryGetProperty("OuterProfileRebuilt", out var outerFlag) && outerFlag.GetBoolean();
        bool hasM6 = build.RootElement.GetProperty("Result").TryGetProperty("M6PatternBuilt", out var m6Flag) && m6Flag.GetBoolean();
        var exterior = hasOuter ? cad.InspectTiffExterior() : null;
        bool m6Points = build.RootElement.GetProperty("Result").TryGetProperty("ExperimentalM6PointsBuilt",out var pointsFlag) && pointsFlag.GetBoolean();
        var m6 = hasM6 ? cad.InspectTiffM6Pattern(m6Points) : null;
        var points = m6Points ? cad.InspectExperimentalM6Points() : null;
        bool hasPp = build.RootElement.GetProperty("Result").TryGetProperty("PpChamferBuilt", out var ppFlag) && ppFlag.GetBoolean();
        var pp = hasPp ? cad.InspectTiffPpChamfer() : null;
        // Reordering the stock operations must not invalidate inherited flange/threads.
        object? inheritedAfterStock = hasOuter ? new {
            Flange19 = cad.InspectFlangeDiameter19(), M12 = cad.InspectM12Trial(), M90 = cad.InspectM90Trial()
        } : null;
        bool hasBore = build.RootElement.GetProperty("Result").TryGetProperty("Bore68StartRebuilt", out var boreFlag) && boreFlag.GetBoolean();
        bool hasBore80 = build.RootElement.GetProperty("Result").TryGetProperty("Bore80PartialRebuilt", out var bore80Flag) && bore80Flag.GetBoolean();
        bool restored80 = build.RootElement.GetProperty("Result").TryGetProperty("Bore80Restored", out var restoredFlag) && restoredFlag.GetBoolean();
        bool hasDetailL = build.RootElement.GetProperty("Result").TryGetProperty("DetailLCoreBuilt", out var lFlag) && lFlag.GetBoolean();
        bool experimentalL = build.RootElement.GetProperty("Result").TryGetProperty("ExperimentalLExitBuilt", out var expLFlag) && expLFlag.GetBoolean();
        bool completeL = build.RootElement.GetProperty("Result").TryGetProperty("ExperimentalLProfileCompleted", out var completeLFlag) && completeLFlag.GetBoolean();
        var bore = experimentalL ? cad.InspectExperimentalLExit(completeL) : hasDetailL ? cad.InspectTiffDetailLCore() : restored80 ? cad.InspectTiffRestoredBore() : hasBore80 ? cad.InspectTiffBore80() : hasBore ? cad.InspectTiffBoreStart() : null;
        bool leftSlot = build.RootElement.GetProperty("Result").TryGetProperty("LeftSlotWidthCorrected",out var slotFlag) && slotFlag.GetBoolean();
        var slot = leftSlot ? cad.InspectLeftSlotWidth() : null;
        var health = cad.InspectTiffFeatureHealth();
        double volume = cad.VerifyCorrectedFlangeVolume(expected);
        if (Hash(copy) != hash || Hash(source) != hash || Hash(buildFile) != buildHash) throw new InvalidOperationException("Evidence changed.");
        var report = Path.Combine(dir, "verification.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = hash, VerifiedCopy = copy, BuildReport = buildFile, BuildReportSha256 = buildHash,
            FreshDiskCopyLoaded = true, SavedBytesUnchanged = true, OpenWarnings = 0, VolumeMm3 = volume, Geometry = geometry,
            M8 = m8, RightM8Rebuilt = hasM8, Bore68 = bore, Bore68StartRebuilt = hasBore, Bore80PartialRebuilt = hasBore80, Bore80Restored = restored80, DetailLCoreBuilt = hasDetailL,
            AaChannel = aa, AaChannelRebuilt = hasAa, LeftM8 = leftM8, LeftM8Rebuilt = hasLeftM8,
            FeatureHealth = health, LeftSlotWidthCorrected = leftSlot, LeftSlot = slot,
            ExperimentalLExitBuilt = experimentalL, IsExperimental = experimentalL, DrawingInterpretationAccepted = false,
            ExperimentalLProfileCompleted = completeL, FullDetailLSourceAccepted = false,
            ExperimentalM6PointsBuilt = m6Points, M6Points = points,
            ExperimentalChannelPointsBuilt = channelPoints, ChannelPoints = channelEnds,
            ExperimentalFeedPointsBuilt = feedPoints, FeedPoints = feedEnds,
            ExperimentalRadialPointBuilt = radialPoint, RadialPoint = radialEnd,
            ExperimentalM8TransitionsBuilt = m8Transitions, M8Transitions = m8TransitionGeometry,
            RearCollar = rearCollar, RearCollarRebuilt = hasRear,
            Exterior = exterior, OuterProfileRebuilt = hasOuter, M6Pattern = m6, M6PatternBuilt = hasM6,
            PpChamfer = pp, PpChamferBuilt = hasPp,
            InheritedAfterStock = inheritedAfterStock,
            IsManufacturingReady = false, FullTiffConformanceChecked = false,
            Scope = "Focused saved-file check of TIFF channels, nominal M8, bore, L core, rear collar, exterior and M6 only when flagged. M8 length, drill ends and L transitions remain unaccepted. Not full drawing acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[TIFF FOCUSED CHECK] {report}"); return;
    }
    if (args.Length == 2 && args[0] is "--prepare-tiff-revision" or "--rebuild-tiff-ii")
    {
        var source = Path.GetFullPath(args[1]);
        string Hash(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
        var sourceHash = Hash(source);
        const string baselineHash = "FB50A31A5478799B17B7E1F7757E7D3C87301BB3FCBC36620E23AB0CC442DD07";
        if (sourceHash != baselineHash) throw new InvalidOperationException("Expected the verified D19 baseline; other inherited revisions require separate assessment.");
        var dir = Path.Combine(Path.GetDirectoryName(source)!, "tiff-revision", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var work = Path.Combine(dir, $"Tsapfa_TIFF_WIP_BASELINE_{Path.GetFileName(dir)}.SLDPRT");
        File.Copy(source, work, false);
        if (Hash(work) != sourceHash) throw new InvalidOperationException("Baseline copy mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(work, requireFresh: true);
        var inventory = cad.InspectTiffRevisionBaseline();
        if (args[0] == "--rebuild-tiff-ii")
        {
            var result = cad.RebuildTiffIiChannel();
            if (Hash(source) != sourceHash) throw new InvalidOperationException("Source changed.");
            var rebuilt = Path.Combine(dir, "Tsapfa_TIFF_II_CHANNEL_WIP.SLDPRT");
            cad.SaveTestPart(rebuilt);
            cad.ExportTrialPreview(Path.ChangeExtension(rebuilt, ".preview.png"));
            File.WriteAllText(Path.ChangeExtension(rebuilt, ".json"), JsonSerializer.Serialize(new {
                SourceFile = source, SourceSha256 = sourceHash, ModelFile = rebuilt, Result = result,
                GeometryChanged = true, IsManufacturingReady = false, TiffConformancePercent = (double?)null
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (Hash(source) != sourceHash) throw new InvalidOperationException("Source changed after save.");
            Console.WriteLine($"[TIFF WIP SAVED] {rebuilt}");
            return;
        }
        if (Hash(source) != sourceHash || Hash(work) != sourceHash) throw new InvalidOperationException("Baseline bytes changed during inventory.");
        var report = Path.Combine(dir, "tiff-revision-inventory.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = sourceHash, WorkFile = work, SavedBytesUnchanged = true,
            SourcePolicy = ChannelSourceReconciliation.CreateReport(), Inventory = inventory,
            GeometryChanged = false, TiffConformanceAccepted = false, IsManufacturingReady = false,
            Scope = "Separate work branch initialized only. Original preserved; inherited geometry is NOT a completed TIFF model."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[WIP ONLY] TIFF revision inventory: {report}");
        return;
    }
    if (args.Length == 2 && args[0] == "--propose-channel-chain")
    {
        ChannelCorrectionProposal.WriteReport(args[1]);
        return;
    }
    if (args.Length == 2 && args[0] == "--audit-current-m8-envelope")
    {
        CurrentM8EnvelopeStudy.WriteReport(args[1]);
        return;
    }
    // A bare IDE/run launch must not silently rebuild the obsolete D15 baseline.
    if (args.Length == 0 || (args.Length == 1 && args[0] is "--help" or "-h"))
    {
        Console.WriteLine("DrawingTo3D: no CAD action selected; no files changed.");
        Console.WriteLine("Current D19 model and evidence: CURRENT_MODEL.md");
        Console.WriteLine("Primary source is now TIFF5309-2304083; current D19 geometry is a historical baseline, not TIFF-accepted.");
        Console.WriteLine("  --prepare-tiff-revision <file>      Copy verified baseline and inventory features; no geometry edits");
        Console.WriteLine("  --rebuild-tiff-bore-start <file>    Extend D68 to TIFF X181 in a new copy of verified M8 baseline");
        Console.WriteLine("  --rebuild-tiff-bore80 <file>        Correct D80 X237..254 only; rear oversized bore remains pending");
        Console.WriteLine("  --restore-tiff-bore80 <file>        Restore rear bore material R40..45/X254..265 in a new copy");
        Console.WriteLine("  --build-tiff-l-core <file>          Cut nominal L D71/1.9 core; transition details remain pending");
        Console.WriteLine("  --rebuild-tiff-aa <file>            Replace old AA network at X132/4deg in a separate WIP copy");
        Console.WriteLine("  --rebuild-tiff-rear <file>          Rebuild D115 collar and channels referenced to collar endX265");
        Console.WriteLine("  --rebuild-tiff-exterior <file> <verification.json>  Correct outer profile in a fresh verified rear copy");
        Console.WriteLine("  --build-tiff-m6 <file> <verification.json>          Add four M6 to a fresh verified exterior copy");
        Console.WriteLine("  --verify-tiff-ii <file>             Fresh-copy checks of TIFF channel, built M8 and bore-start revision");
        Console.WriteLine("  --self-test                         Calculations only, no SolidWorks");
        Console.WriteLine("  --trial-kg-helical <file>           Unaccepted conical V-groove sweep experiment on pinned KG source");
        Console.WriteLine("  --verify-kg-helical <file>          Fresh diagnostic bounds of saved experimental helical cuts");
        Console.WriteLine("  --build-agreed-variant <file>      Separate custom truncated-profile variant; NOT exact TIFF/OST");
        Console.WriteLine("  --export-agreed-step <file>        Export pinned variant and compare reimported solid");
        Console.WriteLine("  --audit-variant-m6-mass <file>     Compare document and temporary-body M6 volumes");
        Console.WriteLine("  --audit-variant-thread-edges <file> Measure actual M6/M12/M90 helical edges");
        Console.WriteLine("  --audit-variant-m90-short <file>   Measure interrupted M90 edges and flank normals");
        Console.WriteLine("  --audit-variant-flanks <file>      Measure M6/M8/M12/M90 flank surface normals");
        Console.WriteLine("  --prepare-agreed-sldprt <file>     Save named agreed variant and check fresh native solid");
        Console.WriteLine("  --extend-variant-m6-starts <file>  Trial one-pitch M6 start extension on pinned native copy");
        Console.WriteLine("  --compare-m6-start-extension <file>  Check material differences against pinned predecessor");
        Console.WriteLine("  --verify-final-thread-sections <file>  Inspect radial sections of all metric threads on pinned M6 revision");
        Console.WriteLine("  --trial-precision-m6 <file>        Build and check one refined-path M6 on a separate copy");
        Console.WriteLine("  --verify-final-thread-endings <file>  Repeat corrected M90/M8 profile checks on pinned M6 revision");
        Console.WriteLine("  --complete-m12-entries <file>      Move M12 helix starts outside the flange on a pinned copy");
        Console.WriteLine("  --align-precision-m6 <file>       Trial fixed-axis orientation on pinned precision copy");
        Console.WriteLine("  --repair-m12-start-trim <file>    Check start-face trim on pinned extended M12 copy");
        Console.WriteLine("  --verify-precision-m6 <file>     Check a precision trial against its prescribed phase");
        Console.WriteLine("  --complete-precision-m6-pattern <file>  Build four direction-constrained M6 sweeps on pinned M12 copy");
        Console.WriteLine("  --verify-precision-m6-pattern <file>  Resume fresh verification of a saved pattern with matching native report");
        Console.WriteLine("  --verify-precision-m6-boundaries <file>  Check saved sweep settings and final boundary envelopes");
        Console.WriteLine("  --repair-precision-m6-alignment <file>  Diagnose alignment edits on a pinned separate trial");
        Console.WriteLine("  --repair-precision-m6-overlap <file>  Extend tool overlap inside the existing M6 void in a separate copy");
        Console.WriteLine("  --verify-precision-m6-repair <file>  Check repair boundaries, void and all four working profiles in one open");
        Console.WriteLine("  --verify-m6-repair-final <file>  Check exact outside-mask locality and finite M6 end profiles on the pinned repair");
        Console.WriteLine("  --verify-m6-integration <file>  Compare latest M6 repair against the common M12 baseline outside all four M6 holes");
        Console.WriteLine("  --inspect-m8-inner-ends <file>  Diagnose full profile over the first inward pitch of both M8 holes on the pinned working model");
        Console.WriteLine("  --inspect-m12-end-profiles <file>  Check M12 front profiles and rear chamfer transitions on the pinned working model");
        Console.WriteLine("  --inspect-m8-blend-interstitial <file>  Check retained R0.5 blend geometry on a new offset grid, without rollback");
        Console.WriteLine("  --clear-precision-m6-pilots <file>  Restore nominal pilot voids in a pinned separate trial");
        Console.WriteLine("  --verify-precision-m6-void <file>  Check nominal void against final solid with positive controls");
        Console.WriteLine("  --verify-precision-m6-profiles <file>  Measure all four custom profiles without changing geometry");
        Console.WriteLine("  --inspect-precision-m6-witnesses <file>  Cross-check suspect edge points against trimmed faces and vertices");
        Console.WriteLine("  --verify-completed-endings-model <file>  Compare M12 changes and run inherited nominal checks");
        Console.WriteLine("  --build-kg-entries <file>           Experimental entry chamfers on pinned slot16 revision");
        Console.WriteLine("  --verify-kg-entries <file>          Fresh focused checks against matching KG build report");
        Console.WriteLine("  --verify-left-end <file>            Fresh-copy nominal left-end surfaces and edge bounds");
        Console.WriteLine("  --verify-left-shoulders <old> <new> Boolean comparison of pinned pre/post slot revisions");
        Console.WriteLine("  --audit-left-mass <old> <new>       Compare whole-body and isolated volumes in scratch parts");
        Console.WriteLine("  --correct-left-slot-width <file>    Pinned-copy correction of inherited through-all cut to slot16");
        Console.WriteLine("  --trial-m8-mouth-r05 <file>          Separate unaccepted R0.5 entry-fillet experiment");
        Console.WriteLine("  --trial-m8-mouth-before-thread <file> R0.5 pilot fillet before restored thread; unaccepted experiment");
        Console.WriteLine("  --trial-m8-mouth-rollback <file>     R0.5 created in rolled-back history of pinned KG-entry model");
        Console.WriteLine("  --trial-m8-mouth-no-trim <file>      R0.5 rollback experiment with thread end-face trim disabled");
        Console.WriteLine("  --verify-m8-mouths <file>           Fresh-copy checks of experimental M8 entry fillets");
        Console.WriteLine("  --diagnose-feature-errors <file>   Read-only inventory of a fresh diagnostic copy");
        Console.WriteLine("  --compare-m8-blend-trim <file>      Compare ideal blend coverage before threads and in final combined trial");
        Console.WriteLine("  --inspect-m8-helical-extents <file> Measure actual M8 long helical edges on pinned combined trial");
        Console.WriteLine("  --verify-m8-definitions <file>      Fresh read-only M8 parameter audit of pinned current revision");
        Console.WriteLine("  --verify-other-geometry <file>      Current approximate DK and inherited spline surface audit; no CAD edits");
        Console.WriteLine("  --audit-current-m8-envelope <json>  Current AA/II M8 envelope limits; no CAD change");
        Console.WriteLine("  --trial-tiff-l-exit <file>           Experimental1.2/30deg exit from pinned verified P-P base");
        Console.WriteLine("  --complete-trial-l-profile <file>   Selected experimental L profile;51 to right wall, no TIFF acceptance");
        Console.WriteLine("  --trial-m6-points <file>            Experimental120deg M6 tips within22 total depth; pinned L-profile base");
        Console.WriteLine("  --trial-channel-points <file>       Experimental120deg AA/II ends; pinned M6-points base");
        Console.WriteLine("  --trial-feed-points <file>          Experimental120deg feed ends; pinned channel-points base");
        Console.WriteLine("  --trial-radial-point <file>         Experimental120deg radial end; pinned feed-point base");
        Console.WriteLine("  --trial-m8-transitions <file>       Experimental pilot/D6.3 transitions; thread lengths unchanged");
        Console.WriteLine("  --build-tiff-pp-chamfer <file> <verification.json>  Add TIFF P-P 2x45 in a new verified-M6 copy");
        Console.WriteLine("  --verify-saved-d19-focused <file>    Fresh-copy D19 and volume check only");
        Console.WriteLine("  --verify-saved-d19 <file>            Full implemented geometry checks on a fresh copy");
        Console.WriteLine("  --audit-drawing-conformance <json>  Audit existing verification evidence");
        Console.WriteLine("  --propose-channel-chain <json>      Write separate design proposal; no CAD action");
        Console.WriteLine("  --build-legacy-stage16              Explicit HISTORICAL D15 baseline build, NOT current model");
        Console.WriteLine("Other experimental modes: VERIFICATION_WORKFLOW.md. Passing checks does not certify drawing conformity.");
        return;
    }
    if (args.Length == 2 && args[0] == "--verify-saved-d19-focused")
    {
        var source = Path.GetFullPath(args[1]);
        string Hash(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }
        var buildFile = Path.ChangeExtension(source, ".json");
        var buildHash = Hash(buildFile);
        using var build = JsonDocument.Parse(File.ReadAllText(buildFile));
        if (!build.RootElement.GetProperty("CorrectedFlange19").GetBoolean()
            || !string.Equals(Path.GetFullPath(build.RootElement.GetProperty("ModelFile").GetString()!), source, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Matching D19 build report required.");
        double expected = build.RootElement.GetProperty("Result").GetProperty("AfterMm3").GetDouble();
        var hash = Hash(source);
        var dir = Path.Combine(Path.GetDirectoryName(source)!, "verification", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var copy = Path.Combine(dir, "D19_FOCUSED_VERIFY_ONLY.SLDPRT");
        File.Copy(source, copy, false);
        if (Hash(copy) != hash || Hash(source) != hash) throw new InvalidOperationException("Copy hash mismatch.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy, requireFresh: true);
        var flange = cad.InspectFlangeDiameter19();
        double volume = cad.VerifyCorrectedFlangeVolume(expected);
        if (Hash(copy) != hash || Hash(source) != hash || Hash(buildFile) != buildHash)
            throw new InvalidOperationException("Saved evidence changed during focused check.");
        var report = Path.Combine(dir, "focused-verification.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = hash, VerifiedCopy = copy,
            BuildReport = buildFile, BuildReportSha256 = buildHash,
            FreshDiskCopyLoaded = true, OpenWarnings = 0, SavedBytesUnchanged = true,
            Flange19 = flange, VolumeMm3 = volume, ExpectedVolumeMm3 = expected,
            FullInheritedGeometryRecheckedAfterReload = false, IsManufacturingReady = false,
            CheckedAtUtc = DateTime.UtcNow,
            Scope = "Focused fresh-disk check of D19 cylinders and total volume. Earlier full pre-save checks are in build report, NOT rerun here. Not full drawing acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] Focused D19 saved-file check: {report}");
        return;
    }
    if (args.Length == 2 && args[0] == "--audit-drawing-conformance")
    {
        DrawingConformanceAudit.WriteReport(args[1]);
        return;
    }
    if (args.Length == 2 && args[0] is "--verify-saved-approx-dk" or "--verify-saved-d19")
    {
        bool corrected19 = args[0] == "--verify-saved-d19";
        var source = Path.GetFullPath(args[1]);
        string Hash(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }
        var hash = Hash(source);
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output", "verification", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(dir);
        var copy = Path.Combine(dir, "ApproxDK_KGPrep_VERIFY_ONLY.SLDPRT");
        File.Copy(source, copy, false);
        if (Hash(copy) != hash || Hash(source) != hash) throw new InvalidOperationException("Approx source changed during copy.");
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(copy, requireFresh: true);
        var flange = corrected19 ? cad.InspectFlangeDiameter19() : null;
        var dk = cad.InspectDkTrial(); var kg = cad.InspectKgPrepTrial(); var feeds = cad.InspectRearFeedTrial();
        var m90 = cad.InspectM90Trial(); var m8 = cad.InspectM8Threads();
        var inherited = cad.InspectSavedConeTrial(DrawingRevision.Create(), approximateDk: true, correctedFlange19: corrected19);
        if (Hash(copy) != hash || Hash(source) != hash) throw new InvalidOperationException("Approx saved bytes changed.");
        var report = Path.Combine(dir, "verification.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = hash, VerifiedCopy = copy, FreshDiskCopyLoaded = true,
            OpenWarnings = 0, SavedBytesUnchanged = true, IsManufacturingReady = false, HasKgHelicalThread = false,
            CheckedAtUtc = DateTime.UtcNow, Flange19 = flange, Dk = dk, KgPrep = kg, Feeds = feeds, M90 = m90, M8 = m8, Inherited = inherited,
            Scope = "Verification of user-authorized approximation, NOT confirmation of original drawing datums, OST thread or sealing."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] Saved APPROX DK/KG pilot verified: {report}"); return;
    }
    if (args.Length == 2 && args[0] is "--trial-kg-prep-from" or "--trial-dk-from" or "--correct-flange19-from")
    {
        bool corrected19 = args[0] == "--correct-flange19-from";
        bool dk = args[0] != "--trial-kg-prep-from";
        var source = Path.GetFullPath(args[1]);
        string Hash()
        {
            using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }
        var sourceHash = Hash();
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
        var destination = Path.Combine(dir, $"Tsapfa_APPROX_{(corrected19 ? "D19_DK_KGPrep" : dk ? "DK_KGPrep" : "KGPrep")}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
        var work = Path.ChangeExtension(destination, ".WIP.SLDPRT");
        File.Copy(source, work, false);
        var cad = new SolidWorksService(); cad.Connect(); cad.OpenPartForInspection(work, requireFresh: true);
        var result = corrected19 ? cad.CorrectFlangeDiameter19() : dk ? cad.CreateDkTrial() : cad.CreateKgPrepTrial();
        var kg = cad.InspectKgPrepTrial();
        var feeds = cad.InspectRearFeedTrial();
        var m90 = cad.InspectM90Trial();
        var m8 = cad.InspectM8Threads();
        var inherited = cad.InspectSavedConeTrial(DrawingRevision.Create(), approximateDk: dk, correctedFlange19: corrected19);
        if (Hash() != sourceHash) throw new InvalidOperationException("KG preparation source changed.");
        cad.SaveTestPart(destination);
        cad.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            ModelFile = destination, SourceFile = source, SourceSha256 = sourceHash,
            IsManufacturingReady = false, UserApprovedApproximation = true, HasKgHelicalThread = false,
            Result = result, CorrectedFlange19 = corrected19, KgPrep = kg, HasApproximateDk = dk, Feeds = feeds, M90 = m90, M8 = m8, Inherited = inherited,
            Limitations = "Experimental tapered preparation only, not completed KG1/8. No OST profile, no sealing claim. DK status explicit; when present its depth is rebased to X260, not confirmed drawing datum. Inherited limitations apply."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] APPROX {(dk ? "DK and KG pilot" : "KG pilot")} saved: {destination}");
        return;
    }
    if (args.Length == 1 && args[0] == "--thread-library-sizes")
    {
        var cad = new SolidWorksService(); cad.Connect(); cad.PrintMetricDieSizes(); return;
    }
    if (!((args.Length == 2 && args[0] is "--trial-m90-from" or "--verify-saved-m8-trial" or "--trial-entry-cones-from" or "--verify-saved-cone-trial" or "--verify-saved-feed-trial" or "--trial-rear-feeds-from" or "--trial-m8-pilots-from" or "--trial-m8-from" or "--resume-m8-work") || (args.Length == 1 && args[0] is "--build-legacy-stage16" or "--self-test" or "--validate-only" or "--audit-dk" or "--audit-right" or "--audit-spline" or "--trial-spline" or "--test-trial-spline" or "--trial-full-spline" or "--test-spline-transfer")
        || (args.Length == 2 && args[0] is "--resume-m90-work" or "--inspect-m90-trial" or "--verify-saved-m90-trial" or "--inspect" or "--inspect-spline-trial" or "--preview-spline-trial" or "--check-spline-accuracy" or "--trial-m12-from" or "--inspect-m12-trial" or "--check-m12-edges" or "--trial-channels-from" or "--inspect-channel-connections" or "--trial-entry-chamfers-from" or "--inspect-entry-chamfers")))
        throw new ArgumentException("Usage: CadTest [--validate-only | --self-test | --audit-dk | --audit-right | --audit-spline | --inspect <file.SLDPRT>]");
    if (args.Contains("--test-spline-transfer"))
    {
        SplineTrialProfile.RunChecks();
        double coarse = SplineTransferVolume.RemovedVolume(300), fine = SplineTransferVolume.RemovedVolume(600);
        Console.WriteLine($"[TEST] Spline transfer removed volume: {coarse:F6} / {fine:F6} mm3; delta {Math.Abs(coarse - fine):F6}.");
        if (Math.Abs(coarse - fine) > 0.2) throw new InvalidOperationException("Transfer integral did not converge.");
        return;
    }
    if (args.Contains("--trial-spline") || args.Contains("--test-trial-spline"))
    {
        SplineTrialProfile.RunChecks();
        var previewDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
        Directory.CreateDirectory(previewDirectory);
        SplineTrialProfile.WritePreview(Path.Combine(previewDirectory, "spline-trial-section.svg"));
        if (args.Contains("--test-trial-spline")) return;
        var trialCad = new SolidWorksService();
        trialCad.Connect();
        trialCad.CreateTestPart();
        double actual = trialCad.CreateSplineTrial();
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"Spline_TRIAL_RootApprox_20mm_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
        trialCad.SaveTestPart(path);
        File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(new {
            ModelFile = path, IsManufacturingReady = false, IsFullPart = false,
            UserApprovedCircularRootApproximation = true, RootRadius = SplineTrialProfile.Root.FilletRadius,
            Teeth = 38, LengthMm = 20, BoreDiameterMm = 62,
            ActualVolumeMm3 = actual, ExpectedVolumeMm3 = SplineTrialProfile.Volume(),
            Limitations = "Standalone constant-section coupon along Z, not the tsapfa. R50, R35, lead-in, axial placement and angular phase absent. Involutes interpolated with 33 points per flank. Nominal diameters and midpoint tooth thickness. Stage15 unchanged."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] Standalone spline TRIAL saved: {path}");
        return;
    }
    if (args.Contains("--audit-spline"))
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
        Directory.CreateDirectory(directory);
        SplineSectionStudy.WriteOutputs(directory);
        SplineAxialStudy.WriteOutput(directory);
        SplineRootStudy.WriteOutput(directory);
        Console.WriteLine("[AUDIT] Spline cross-section JSON/SVG, axial constraints and unselected root candidates generated; no CAD changes.");
        return;
    }
    if (args.Contains("--audit-right"))
    {
        RightTransitionStudy.RunChecks();
        var reportDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "right-transition-candidates.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(RightTransitionStudy.CreateReport(), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[AUDIT] Условные варианты правого перехода, CAD не изменён: {reportPath}");
        return;
    }
    if (args.Contains("--audit-dk"))
    {
        DkRecessAssessment.RunChecks();
        DkCompoundAssessment.RunChecks();
        var reportDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "dk-constraint-assessment.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(DkRecessAssessment.CreateReport(), new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(reportDirectory, "dk-compound-assessment.json"), JsonSerializer.Serialize(DkCompoundAssessment.CreateReport(), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[AUDIT] Д/К: сохранён анализ гипотез, CAD не изменён: {reportPath}");
        return;
    }
    var part = DrawingRevision.Create();
    DrawingRevision.Validate(part);
    var outputDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
    Directory.CreateDirectory(outputDirectory);
    File.WriteAllText(Path.Combine(outputDirectory, $"{DrawingRevision.Revision}-description.json"),
        JsonSerializer.Serialize(new
        {
            Revision = DrawingRevision.Revision,
            IsManufacturingReady = false,
            TemporaryAssumptions = new { RightTransitionStationMm = DrawingRevision.AssumedRightStation,
                UserApproved = true, ConfirmedDrawingDimension = false },
            Source = "chertezh_corrected_final.pdf, sections A-A / P-P and end view",
            Limitations = DrawingRevision.Limitations,
            ExpectedVolumeMm3 = DrawingRevision.ExpectedDraftVolumeMm3(part),
            Part = part
        }, new JsonSerializerOptions { WriteIndented = true }));
    DrawingRevision.WriteSectionSvg(part, Path.Combine(outputDirectory, $"{DrawingRevision.Revision}-section.svg"));
    DrawingRevision.WriteCollarDetailSvg(Path.Combine(outputDirectory, $"{DrawingRevision.Revision}-collar-detail.svg"));
    DrawingRevision.WriteEndViewSvg(part, Path.Combine(outputDirectory, $"{DrawingRevision.Revision}-end-view.svg"));
    Console.WriteLine("[OK] Этап 16: отдельное отверстие Р–Р Ø22 при смещении 111,5, сквозь фланец 18 мм.");
    Console.WriteLine("[ASSUMPTION] X=245 согласован пользователем ВРЕМЕННО; это не подтверждённый размер чертежа.");
    Console.WriteLine("[BASE] Описание stage16 содержит заготовки слева; резьбы, шлицы и каналы экспериментальных файлов проверяются отдельно выбранным режимом.");
    Console.WriteLine("[BASE] R0,5 построены; торец воротника X=265 выбран с нулевым отступом в пределах 1,6 max. Д/К ещё отсутствует.");
    Console.WriteLine("[INFO] Старый вариант M90 длиной 3 мм запрещён; новая номинальная проба доступна только в отдельном режиме M90.");
    if (!args.Contains("--validate-only"))
    {
        DrawingRevision.RunRegressionChecks(part);
        SplineSectionStudy.RunChecks();
        SplineAxialStudy.RunChecks();
        SplineRootStudy.RunChecks();
        DkRecessAssessment.RunChecks();
        DkCompoundAssessment.RunChecks();
        RightTransitionStudy.RunChecks();
    }
    M12Study.RunChecks();
    M90TrialChecks.RunChecks();
    EntryConeStudy.RunChecks();
    ChannelTrialStudy.RunChecks();
    ChannelMouthStudy.WriteReport(Path.Combine(outputDirectory, "channel-mouth-assessment.json"));
    RearPortStudy.WriteReport(Path.Combine(outputDirectory, "rear-port-assessment.json"));
    RearFeedTrial.RunChecks();
    M8PilotStudy.RunChecks();
    KgPortEnvelopeStudy.WriteReport(Path.Combine(outputDirectory, "kg-port-envelope-assessment.json"));
    KgPrepStudy.RunChecks();
    DkTrialStudy.RunChecks();
    DrawingConformanceAudit.RunChecks();
    Flange19Study.RunChecks();
    ChannelCorrectionProposal.RunChecks();
    if (args.Contains("--self-test"))
    {
        SolidWorksService.TestExperimentalChannelPoints();
        CurrentM8EnvelopeStudy.RunChecks();
        SolidWorksService.TestExperimentalFeedPoints();
        SolidWorksService.TestExperimentalRadialPoint();
        SolidWorksService.TestM8Transition();
        SolidWorksService.TestLeftSlotDepths();
        SolidWorksService.TestKgEntryChamfer();
        HelicalEdgeFit.RunChecks();
        M8DefinitionChecks.RunChecks();
        TiffBoreStudy.RunChecks();
        TiffAaStudy.RunChecks();
        TiffRearStudy.RunChecks();
        TiffExteriorStudy.RunChecks();
        TiffM6LibraryStudy.RunChecks();
        PpChamferStudy.RunChecks();
        ExperimentalLProfileStudy.RunChecks();
        M6PointStudy.RunChecks();
        MetricThreadSectionProfile.RunChecks();
        _ = MassLocalizationStudy.CreateReport(DrawingRevision.ExpectedDraftVolumeMm3(part));
        return;
    }
    if (args.Contains("--validate-only")) return;

    var solidWorks = new SolidWorksService();
    solidWorks.Connect();
    if (args.Length == 2 && args[0] == "--inspect-m90-trial")
    {
        solidWorks.OpenPartForInspection(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(solidWorks.InspectM90Trial()));
        return;
    }
    if (args.Length == 2 && args[0] is "--trial-m90-from" or "--resume-m90-work")
    {
        bool resumeM90 = args[0] == "--resume-m90-work";
        var source = Path.GetFullPath(args[1]);
        if (resumeM90 && !source.EndsWith(".WIP.SLDPRT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("M90 resume requires an experimental WIP.");
        string Hash()
        {
            using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }
        var sourceHash = Hash();
        var destination = Path.Combine(outputDirectory, $"Tsapfa_TRIAL_M90_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
        var work = Path.ChangeExtension(destination, ".WIP.SLDPRT");
        if (!resumeM90) File.Copy(source, work, false);
        solidWorks.OpenPartForInspection(resumeM90 ? source : work, requireFresh: !resumeM90);
        var result = resumeM90 ? solidWorks.InspectM90Trial() : solidWorks.CreateM90Trial();
        var m8 = solidWorks.InspectM8Threads();
        var inherited = solidWorks.InspectSavedConeTrial(part);
        var feeds = solidWorks.InspectRearFeedTrial();
        if (Hash() != sourceHash) throw new InvalidOperationException("M90 source changed.");
        solidWorks.SaveTestPart(destination);
        solidWorks.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            ModelFile = destination, SourceFile = source, SourceSha256 = sourceHash,
            IsManufacturingReady = false, M90 = result, M8 = m8, InheritedGeometry = inherited, RearFeeds = feeds,
            RecoveredUnsavedWipInMemory = resumeM90,
            SourceNote = resumeM90 ? "Source hash identifies original WIP bytes, not the unsaved M90 operation. Final file requires its own saved-byte verification." : "Fresh source copy.",
            Limitations = "Trial nominal M90: helix end X38, swept boundary X39.75 before spline lead-in X42. Full-profile length, runout, entry partial thread and 6g not verified. Inherited spline, channel length, M8 and right-transition assumptions. KG1/8 and DK absent."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] M90 trial saved: {destination}");
        return;
    }
    if (args.Length == 2 && args[0] is "--verify-saved-cone-trial" or "--verify-saved-feed-trial" or "--verify-saved-m8-trial" or "--verify-saved-m90-trial")
    {
        bool m90 = args[0] == "--verify-saved-m90-trial";
        bool m8 = args[0] == "--verify-saved-m8-trial" || m90;
        bool feeds = args[0] == "--verify-saved-feed-trial" || m8;
        var source = Path.GetFullPath(args[1]);
        string HashFile(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }
        var hash = HashFile(source);
        var directory = Path.Combine(outputDirectory, "verification", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var copy = Path.Combine(directory, m90 ? "SavedM90Trial_VERIFY_ONLY.SLDPRT" : m8 ? "SavedM8Trial_VERIFY_ONLY.SLDPRT" : feeds ? "SavedFeedTrial_VERIFY_ONLY.SLDPRT" : "SavedConeTrial_VERIFY_ONLY.SLDPRT");
        File.Copy(source, copy, false);
        if (HashFile(copy) != hash || HashFile(source) != hash)
            throw new InvalidOperationException("Source changed during verification copy.");
        solidWorks.OpenPartForInspection(copy, requireFresh: true);
        var m90Report = m90 ? solidWorks.InspectM90Trial() : null;
        var m8Report = m8 ? solidWorks.InspectM8Threads() : null;
        var feedReport = feeds ? solidWorks.InspectRearFeedTrial() : null;
        var report = solidWorks.InspectSavedConeTrial(part);
        if (HashFile(source) != hash || HashFile(copy) != hash)
            throw new InvalidOperationException("Saved bytes changed during read-only verification.");
        var path = Path.Combine(directory, "verification.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new {
            SourceFile = source, SourceSha256 = hash, VerifiedCopy = copy,
            FreshDiskCopyLoaded = true, OpenWarnings = 0, SavedBytesUnchanged = true,
            IsManufacturingReady = false, CheckedAtUtc = DateTime.UtcNow, Report = report, RearFeeds = feedReport, M8 = m8Report, M90 = m90Report,
            Scope = "Reload of byte-identical saved copy. Checks selected trial geometry, not completeness against drawing, tolerances, mass, strength or sealing."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] Saved cone trial verified: {path}");
        return;
    }
    if (args.Length == 2 && args[0] == "--inspect-entry-chamfers")
    {
        solidWorks.OpenPartForInspection(args[1]);
        var report = solidWorks.InspectRadialEntryChamfers();
        var path = Path.Combine(outputDirectory, $"entry-chamfer-audit_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { ModelFile = Path.GetFullPath(args[1]),
            Scope = "Read-only in-memory face classification and sampled boundary residual against radial 45-degree cone with apex at R48.5. Not complete part acceptance.",
            Report = report }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[REPORT] {path}");
        return;
    }
    if (args.Length == 2 && args[0] is "--trial-entry-chamfers-from" or "--trial-entry-cones-from" or "--trial-rear-feeds-from" or "--trial-m8-pilots-from" or "--trial-m8-from" or "--resume-m8-work")
    {
        bool cones = args[0] == "--trial-entry-cones-from";
        bool feeds = args[0] == "--trial-rear-feeds-from";
        bool resume = args[0] == "--resume-m8-work";
        bool m8 = args[0] == "--trial-m8-from" || resume;
        bool pilots = args[0] == "--trial-m8-pilots-from" || m8;
        var source = Path.GetFullPath(args[1]);
        string Hash()
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
        }
        var sourceHash = Hash();
        var destination = Path.Combine(outputDirectory, $"Tsapfa_TRIAL_{(m8 ? "M8Threads" : pilots ? "M8Pilots" : feeds ? "RearFeeds" : cones ? "EntryCones" : "EntryChamfers")}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
        var work = Path.ChangeExtension(destination, ".WIP.SLDPRT");
        if (resume && !source.EndsWith(".WIP.SLDPRT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resume requires the existing experimental WIP, never a released model.");
        if (!resume) File.Copy(source, work, false);
        solidWorks.OpenPartForInspection(resume ? source : work);
        if (feeds) solidWorks.InspectRadialEntryChamfers(requireCone: true);
        if (pilots) solidWorks.InspectRearFeedTrial();
        var result = resume ? new { Scope = "Recovered prepared WIP in memory; source file bytes alone do not contain the unsaved pilot operations. Pilot geometry rechecked by M8 builder." } : pilots ? solidWorks.CreateM8PilotTrial() : feeds ? solidWorks.CreateRearFeedTrial() : cones ? solidWorks.CreateRadialEntryCones() : solidWorks.CreateRadialEntryChamfers();
        var m8Result = m8 ? solidWorks.CreateM8ThreadTrial() : null;
        if (feeds || pilots) solidWorks.InspectRadialEntryChamfers(requireCone: true);
        if (pilots) solidWorks.InspectRearFeedTrial();
        var connections = solidWorks.VerifyChannelTrial(requireOriginalMouth: !pilots);
        solidWorks.VerifyDraftBody(part, checkVolume: false);
        solidWorks.VerifyTrialSplineSurfaces();
        var threads = solidWorks.InspectM12Trial();
        if (Hash() != sourceHash) throw new InvalidOperationException("Source file changed.");
        solidWorks.SaveTestPart(destination);
        solidWorks.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            ModelFile = destination, SourceFile = source, SourceSha256 = sourceHash, IsManufacturingReady = false,
            Result = result, M8 = m8Result, Connections = connections, M12 = threads,
            EntryInterpretation = cones || feeds || pilots ? "Radial 45-degree cone; D8 on tangent plane R52.5, D6 at R51.5. Comparative interpretation, not uniquely established drawing intent." : "Native edge chamfer on curved entry; not conical.",
            Limitations = m8 ? "Nominal M8 library thread with experimental length; 6H and seal not verified. KG1/8, M90, DK unfinished; inherited trial limits." : feeds || pilots ? "Experimental D6.8 bases and flat-ended cutters; M8 thread, KG1/8, M90 and DK unfinished. Inherited trial limitations." : "Inherited channel/spline/M12 trial assumptions. M8, D6.8, KG1/8, M90 and DK remain unfinished."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] Entry chamfer TRIAL: {destination}");
        return;
    }
    if (args.Length == 2 && args[0] == "--inspect-channel-connections")
    {
        solidWorks.OpenPartForInspection(args[1]);
        var connections = solidWorks.VerifyChannelTrial();
        var reportPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(args[1])}_connections_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new {
            ModelFile = Path.GetFullPath(args[1]), IsManufacturingReady = false,
            Scope = "Read-only in-memory check of channel cylinder axes and shared intersection edges only; not full part or hydraulic/structural certification.",
            Connections = connections }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[REPORT] {reportPath}");
        return;
    }
    if (args.Length == 2 && args[0] == "--trial-channels-from")
    {
        var coarse = ChannelTrialStudy.Integrate(160);
        var fine = ChannelTrialStudy.Integrate(320);
        Console.WriteLine($"[TEST] Channel integral: {coarse} / {fine}");
        if (Math.Abs(coarse.MainPairMm3 - fine.MainPairMm3) > .1 ||
            Math.Abs(coarse.ConnectedPairMm3 - fine.ConnectedPairMm3) > .1)
            throw new InvalidOperationException("Channel integral did not converge.");
        var source = Path.GetFullPath(args[1]);
        var destination = Path.Combine(outputDirectory, $"Tsapfa_TRIAL_ExtendedChannels_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
        var work = Path.ChangeExtension(destination, ".WIP.SLDPRT");
        File.Copy(source, work, false);
        solidWorks.OpenPartForInspection(work);
        solidWorks.InspectM12Trial();
        var result = solidWorks.CreateChannelTrial(fine);
        var connections = solidWorks.VerifyChannelTrial();
        solidWorks.VerifyDraftBody(part, checkVolume: false);
        solidWorks.VerifyTrialSplineSurfaces();
        solidWorks.InspectM12Trial();
        solidWorks.SaveTestPart(destination);
        solidWorks.ExportTrialPreview(Path.ChangeExtension(destination, ".preview.png"));
        File.WriteAllText(Path.ChangeExtension(destination, ".json"), JsonSerializer.Serialize(new {
            ModelFile = destination, SourceFile = source, IsManufacturingReady = false,
            UserApprovedExtendedLengthTrial = true, DrawingLengthMm = 110, TrialLengthMm = ChannelTrialStudy.Length,
            AnchorX = 265, AnchorRadius = 51, RadialX = 108, BranchAngles = ChannelTrialStudy.BranchAngles,
            MinimumCalculatedBoreWallMm = ChannelTrialStudy.MinimumBoreWall(),
            IntegralCoarse = coarse, IntegralFine = fine, Result = result, Connections = connections,
            Limitations = "Experimental placement and flat-bottom finite cutters; only length extension explicitly approved. No KG1/8, D6.8 feeds, M8, radial mouth chamfers, M90 or DK. Rear recess clips channel mouths. Inherited spline/M12 trial limitations. Not manufacturing ready."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] Extended channel TRIAL: {destination}");
        return;
    }
    if (args.Length == 2 && args[0] is "--inspect-m12-trial" or "--check-m12-edges")
    {
        solidWorks.OpenPartForInspection(args[1]);
        var report = solidWorks.InspectM12Trial();
        bool fullGeometry = args[0] == "--inspect-m12-trial";
        if (fullGeometry)
        {
            solidWorks.VerifyDraftBody(part, checkVolume: false);
            solidWorks.VerifyTrialSplineSurfaces();
        }
        var reportPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(args[1])}_m12check_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { ModelFile = Path.GetFullPath(args[1]),
            IsManufacturingReady = false, Mode = "read-only-in-memory-M12-inspection",
            BaseGeometryChecked = fullGeometry, SplineSurfacesChecked = fullGeometry, ExactTotalVolumeChecked = false, Report = report },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[REPORT] {reportPath}");
        return;
    }
    if (args.Length == 2 && args[0] == "--trial-m12-from")
    {
        var source = Path.GetFullPath(args[1]);
        var trialPath = Path.Combine(outputDirectory, $"Tsapfa_TRIAL_M12_Splines_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
        var workPath = Path.ChangeExtension(trialPath, ".WIP.SLDPRT");
        // Copy to a unique destination before any CAD edit; source bytes remain untouched.
        File.Copy(source, workPath, false);
        File.WriteAllText(Path.ChangeExtension(workPath, ".json"), JsonSerializer.Serialize(new {
            ModelFile = workPath, SourceFile = source, Status = "Unmodified input copy for trial construction; NOT a completed M12 model",
            IsManufacturingReady = false, M12Validated = false }));
        solidWorks.OpenPartForInspection(workPath);
        solidWorks.VerifyDraftBody(part, DrawingRevision.ExpectedDraftVolumeMm3(part) - SplineTransferVolume.RemovedVolume());
        solidWorks.VerifyTrialSplineSurfaces();
        var report = solidWorks.CreateM12Trial();
        var threadCheck = solidWorks.InspectM12Trial();
        solidWorks.VerifyDraftBody(part, checkVolume: false);
        solidWorks.VerifyTrialSplineSurfaces();
        solidWorks.SaveTestPart(trialPath);
        solidWorks.ExportTrialPreview(Path.ChangeExtension(trialPath, ".preview.png"));
        File.WriteAllText(Path.ChangeExtension(trialPath, ".json"), JsonSerializer.Serialize(new {
            ModelFile = trialPath, SourceFile = source, BaseRevision = DrawingRevision.Revision,
            IsManufacturingReady = false, NominalM12Trial = report, ThreadCheck = threadCheck,
            Limitations = "Inherited spline TRIAL; M12-7H callout retained but tolerance profile not verified. Channels, M90, DK and other unresolved details absent."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] M12/spline TRIAL saved: {trialPath}");
        return;
    }
    if (args.Length == 2 && args[0] == "--check-spline-accuracy")
    {
        solidWorks.OpenPartForInspection(args[1]);
        var accuracy = solidWorks.CheckTrialInvolutes();
        var reportPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(args[1])}_accuracy_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new {
            ModelFile = Path.GetFullPath(args[1]), CheckedAtUtc = DateTime.UtcNow,
            IsManufacturingReady = false, Mode = "read-only-in-memory-CAD-sketch-check", Accuracy = accuracy
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[REPORT] {reportPath}");
        return;
    }
    if (args.Length == 2 && args[0] == "--preview-spline-trial")
    {
        solidWorks.OpenPartForInspection(args[1]);
        solidWorks.ExportTrialPreview(Path.ChangeExtension(Path.GetFullPath(args[1]), ".preview.png"));
        Console.WriteLine("[OK] Preview only; no model save and no new CAD acceptance claimed.");
        return;
    }
    if (args.Length == 2 && args[0] == "--inspect-spline-trial")
    {
        solidWorks.OpenPartForInspection(args[1]);
        double expected = DrawingRevision.ExpectedDraftVolumeMm3(part) - SplineTransferVolume.RemovedVolume();
        double actual = solidWorks.VerifyDraftBody(part, expected);
        solidWorks.VerifyTrialSplineSurfaces();
        Console.WriteLine($"[OK] Trial inspection without save: actual={actual:F6}, expected={expected:F6}.");
        solidWorks.ExportTrialPreview(Path.ChangeExtension(Path.GetFullPath(args[1]), ".preview.png"));
        File.WriteAllText(Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(args[1])}_inspection_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json"),
            JsonSerializer.Serialize(new {
                ModelFile = Path.GetFullPath(args[1]), VerifiedAtUtc = DateTime.UtcNow,
                Mode = "inspect-trial-without-SLDPRT-save", IsManufacturingReady = false,
                BaseFeaturesChecked = true, TrialSplineSurfacesChecked = true,
                ActualVolumeMm3 = actual, ExpectedVolumeMm3 = expected,
                DifferenceMm3 = Math.Abs(actual - expected),
                Scope = "In-memory model; may already be open. Root circle user-approved as approximation; disk-tool construction and angular phase provisional."
            }, new JsonSerializerOptions { WriteIndented = true }));
        return;
    }
    if (args.Length == 2 && args[0] == "--inspect")
    {
        solidWorks.OpenPartForInspection(args[1]);
        var actualVolume = solidWorks.VerifyDraftBody(part);
        var report = AcceptanceReport.Write(args[1], outputDirectory, actualVolume,
            DrawingRevision.ExpectedDraftVolumeMm3(part), "inspect-existing-no-save");
        Console.WriteLine($"[REPORT] {report}");
        return;
    }
    if (args.Length != 1 || args[0] is not ("--build-legacy-stage16" or "--trial-full-spline"))
        throw new InvalidOperationException("An explicit historical build mode is required. Use --help.");
    Console.WriteLine("[WARNING] Building HISTORICAL stage16 with D15 flange holes, NOT the current corrected D19 model.");
    solidWorks.CreateTestPart();
    solidWorks.CreateRevolveSketchFromElements(part);
    solidWorks.CreateRevolveFromDescription(part);
    solidWorks.CreateBoreRevolveProfile(part);
    solidWorks.CreateAnnularCuts(part);
    solidWorks.CreateHolesFromDescription(part);
    solidWorks.CreateLeftEndCuts(part);
    double builtVolume = solidWorks.VerifyDraftBody(part);
    if (args.Contains("--trial-full-spline"))
    {
        SplineTrialProfile.RunChecks();
        double coarse = SplineTransferVolume.RemovedVolume(300), removed = SplineTransferVolume.RemovedVolume(600);
        if (Math.Abs(coarse - removed) > 0.2) throw new InvalidOperationException("Transfer integral did not converge.");
        double actual = solidWorks.TransferTrialSplines();
        double expected = DrawingRevision.ExpectedDraftVolumeMm3(part) - removed;
        if (Math.Abs(actual - expected) > 2.2) throw new InvalidOperationException($"Transferred spline volume mismatch: {actual} vs {expected}.");
        solidWorks.VerifyDraftBody(part, expected);
        solidWorks.VerifyTrialSplineSurfaces();
        var trialPath = Path.Combine(outputDirectory, $"Tsapfa_TRIAL_Splines_RootApprox_R50Disk_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
        solidWorks.SaveTestPart(trialPath);
        File.WriteAllText(Path.ChangeExtension(trialPath, ".json"), JsonSerializer.Serialize(new {
            ModelFile = trialPath, IsManufacturingReady = false, BaseRevision = DrawingRevision.Revision,
            UserApprovedCircularRootApproximation = true, RootRadius = SplineTrialProfile.Root.FilletRadius,
            ActualVolumeMm3 = actual, ExpectedVolumeMm3 = expected, RemovedVolumeMm3 = removed,
            IntegrationConvergenceMm3 = Math.Abs(coarse - removed),
            Limitations = $"TRIAL: disk-tool construction of R50, not confirmed manufacturing tool. Space phase zero in right-plane sketch provisional. Nominal diameters, midpoint thickness. Original files unchanged. Other {DrawingRevision.Revision} limitations retained.",
            BaseLimitations = DrawingRevision.Limitations
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[SUCCESS] Full-part spline TRIAL: {trialPath}");
        return;
    }
    var output = Path.Combine(outputDirectory,
        $"Tsapfa_{DrawingRevision.Revision}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.SLDPRT");
    solidWorks.SaveTestPart(output);
    Console.WriteLine($"[REPORT] {AcceptanceReport.Write(output, outputDirectory, builtVolume, DrawingRevision.ExpectedDraftVolumeMm3(part), "build-verify-save")}");
    Console.WriteLine($"[SUCCESS] Сохранена промежуточная модель: {output}");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}

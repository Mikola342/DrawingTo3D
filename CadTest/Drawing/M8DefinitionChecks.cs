namespace CadTest.Drawing;

// Checks the selected CAD library contract, not a 6H tolerance or full-profile length.
public static class M8DefinitionChecks
{
    public static void Validate(string type,string size,double pitch,double depth,double expectedDepth)
    {
        string file=type?.Replace('\\','/').Split('/').LastOrDefault()??"";
        if(!file.Equals("Metric Tap.SLDLFP",StringComparison.OrdinalIgnoreCase)||size!="M8x1.25"||
            !new[]{pitch,depth,expectedDepth}.All(double.IsFinite)||expectedDepth<=0||
            Math.Abs(pitch-.00125)>1e-9||Math.Abs(depth-expectedDepth)>1e-9)
            throw new InvalidOperationException("M8 nominal library/profile/pitch/depth mismatch.");
    }
    public static void RunChecks()
    {
        void Check(string type="Metric Tap.SLDLFP",string size="M8x1.25",double pitch=.00125,double depth=.006,double expected=.006)
            =>Validate(type,size,pitch,depth,expected);
        Check();Check(type:@"C:\profiles\Metric Tap.SLDLFP");Check(type:"C:/profiles/metric tap.sldlfp");
        Action[] invalid=[()=>Check(type:"Metric Die.SLDLFP"),()=>Check(type:"FakeMetric Tap.SLDLFP"),
            ()=>Check(type:""),()=>Check(size:"M1.2x0.25"),()=>Check(size:"M8x1.0"),
            ()=>Check(pitch:double.NaN),()=>Check(pitch:double.PositiveInfinity),()=>Check(pitch:.001),
            ()=>Check(depth:double.NaN),()=>Check(depth:double.PositiveInfinity),()=>Check(depth:.011),
            ()=>Check(expected:double.NaN),()=>Check(expected:0)];
        foreach(var test in invalid)
        {bool rejected=false;try{test();}catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Invalid M8 definition accepted.");}
        Console.WriteLine($"[TEST OK] M8 exact library contract;{invalid.Length} invalid profiles/numbers rejected. Not full-profile or6H acceptance.");
    }
}

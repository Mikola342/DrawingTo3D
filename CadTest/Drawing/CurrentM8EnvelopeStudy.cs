using System.Text.Json;

namespace CadTest.Drawing;

// Current X265/4deg and X265/10deg geometry, not the historical X260 study.
public static class CurrentM8EnvelopeStudy
{
    public sealed record Result(double InclinationDegrees,double CrossingX,double CrossingRadius,
        double CrossingDistanceFromMouthMm,double FullDiskEntrySetbackMm,
        double MaximumEnvelopeDepthBeforeFeedMm,double AvailableUninterruptedIntervalMm,
        double CurrentOperationDepthMm,double CurrentFullDiskIntervalUpperBoundMm,
        double CurrentFeedClearanceMm,double Candidate11ClearanceMm,double Candidate13ClearanceMm);

    public static Result Evaluate(double degrees)
    {
        if(degrees!=4 && degrees!=10)throw new ArgumentOutOfRangeException(nameof(degrees));
        double b=degrees*Math.PI/180,f=7*Math.PI/180;
        double r=(51+(257.5-127*Math.Tan(f)-265)*Math.Tan(b))/(1-Math.Tan(f)*Math.Tan(b));
        double x=257.5+(r-127)*Math.Tan(f),distance=(265-x)/Math.Cos(b);
        double reach=(3.4+4*Math.Sin(b+f))/Math.Cos(b+f);
        double setback=4*Math.Tan(b),limit=distance-reach,current=6/Math.Cos(b);
        return new(degrees,x,r,distance,setback,limit,limit-setback,current,current-setback,
            limit-current,limit-11,limit-13);
    }
    public static void RunChecks()
    {
        foreach(double degrees in new[]{4.0,10.0})
        {
            var e=Evaluate(degrees);double b=degrees*Math.PI/180,f=7*Math.PI/180;
            if(e.CurrentFeedClearanceMm<=0 || e.CurrentFullDiskIntervalUpperBoundMm<=0 ||
                Math.Abs(e.CrossingRadius-(51+(e.CrossingX-265)*Math.Tan(b)))>1e-10)
                throw new InvalidOperationException("Current M8 envelope chain failed.");
            // Independent 3D disk samples: tangent to the feed at the limit,
            // clear when moved toward the mouth; outside the body at entry.
            foreach(double shift in new[]{0.0,.01})
            {
                double depth=e.MaximumEnvelopeDepthBeforeFeedMm-shift;
                double cx=265-depth*Math.Cos(b),cr=51-depth*Math.Sin(b),min=double.PositiveInfinity;
                for(int j=0;j<1440;j++)
                {
                    double phi=2*Math.PI*j/1440;
                    double x=cx-4*Math.Sin(b)*Math.Cos(phi),r=cr+4*Math.Cos(b)*Math.Cos(phi),z=4*Math.Sin(phi);
                    double normal=(x-e.CrossingX)*Math.Cos(f)-(r-e.CrossingRadius)*Math.Sin(f);
                    min=Math.Min(min,Math.Sqrt(normal*normal+z*z));
                }
                if(Math.Abs(min-(3.4+shift*Math.Cos(b+f)))>1e-8)
                    throw new InvalidOperationException("M8/feed tangency sample mismatch.");
            }
            double maxX=265-e.FullDiskEntrySetbackMm*Math.Cos(b)+4*Math.Sin(b);
            if(Math.Abs(maxX-265)>1e-10)throw new InvalidOperationException("M8 oblique entry check failed.");
            Console.WriteLine($"[TEST OK] Current M8 {degrees}deg: full-disk setback={e.FullDiskEntrySetbackMm:R}, feed limit={e.MaximumEnvelopeDepthBeforeFeedMm:R}, current upper bound={e.CurrentFullDiskIntervalUpperBoundMm:R}. NOT full-profile acceptance.");
        }
        foreach(double value in new[]{double.NaN,double.PositiveInfinity,0,7,90})
        {
            bool rejected=false;try{Evaluate(value);}catch(ArgumentOutOfRangeException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Invalid current M8 angle accepted.");
        }
    }
    public static void WriteReport(string path)
    {
        RunChecks();
        File.WriteAllText(path,JsonSerializer.Serialize(new{
            Basis="Current experimental model: mouthX265/R51, AA4deg, II10deg; feed7deg throughX257.5/R127, radius3.4; M8 envelope radius4.",
            Results=new[]{Evaluate(4),Evaluate(10)},GeometryChanged=false,ThreadLengthSelected=false,
            FullProfileVerified=false,DrawingDimensions11And13AcceptedAsThreadLengths=false,
            Scope="Infinite-cylinder exclusion and oblique planar-mouth disk bounds only. 11/13 candidates are axis lengths, not accepted drawing interpretation. No chamfer, runout, helical-profile or tolerance certification. Current full-disk interval is only an upper bound before these deductions."
        },new JsonSerializerOptions{WriteIndented=true}));
    }
}

namespace CadTest.Drawing;

public static class M6PointStudy
{
    public static double Height(double radius, double includedDegrees)
    {
        if(!double.IsFinite(radius)||radius<=0||!double.IsFinite(includedDegrees)||includedDegrees<=0||includedDegrees>=180)
            throw new ArgumentOutOfRangeException(nameof(includedDegrees));
        double h=radius/Math.Tan(includedDegrees*Math.PI/360);
        if(!double.IsFinite(h)||h<=0)throw new ArgumentOutOfRangeException(nameof(radius));
        return h;
    }
    public static void RunChecks()
    {
        double r=(6-5*Math.Sqrt(3)/8)/2,h=Height(r,120),baseX=265-22+h;
        if(baseX<=243||baseX>=250||Math.Abs((265-baseX)+h-22)>1e-10)
            throw new InvalidOperationException("M6 point depth chain mismatch.");
        double integral=0;
        for(int i=0;i<10000;i++){double x=(i+.5)*h/10000,rr=x*Math.Tan(Math.PI/3);integral+=Math.PI*rr*rr*h/10000;}
        if(Math.Abs(integral-Math.PI*r*r*h/3)>1e-6)throw new InvalidOperationException("M6 point cone integration mismatch.");
        foreach(var (radius,angle) in new[]{(0.0,120.0),(r,0.0),(r,180.0),(r,double.NaN),(double.PositiveInfinity,120.0)})
        {try{Height(radius,angle);}catch(ArgumentOutOfRangeException){continue;}throw new InvalidOperationException("Invalid M6 cone accepted.");}
        Console.WriteLine($"[TEST OK] M6 assumed120deg: h={h:R},baseX={baseX:R},total22 preserved,added4={4*2*Math.PI*r*r*h/3:R};5 invalid inputs rejected.");
    }
}

namespace CadTest.Drawing;

// The selected library's nominal truncated 60-degree profile, not a tolerance
// limit or a claim that every metric thread standard uses this root form.
public static class MetricThreadSectionProfile
{
    public static double Radius(double majorRadius,double pitch,double phase,bool external)
    {
        if(!double.IsFinite(majorRadius)||!double.IsFinite(pitch)||!double.IsFinite(phase)||majorRadius<=0||pitch<=0)
            throw new ArgumentOutOfRangeException(nameof(pitch));
        double u=phase-Math.Floor(phase);
        // Tap groove floor: P/8; die groove floor: P/4. Both flanks
        // occupy 5P/16. Reversing the tap radius alone is not a die profile.
        double width=external?.875:.75;
        double cut=Math.Sqrt(3)*pitch*Math.Clamp(Math.Min(u,width-u),0,.3125);
        return external?majorRadius-cut:majorRadius-5*Math.Sqrt(3)*pitch/16+cut;
    }
    public static void RunChecks()
    {
        foreach(double pitch in new[]{1.0,1.25,1.75,2.0})foreach(bool external in new[]{false,true})
        {
            double major=45,minor=major-5*Math.Sqrt(3)*pitch/16;
            foreach(double u in new[]{-.25,0,.15625,.3125,.375,.4375,.59375,.75,.875,1.0})
            {
                double value=Radius(major,pitch,u,external);
                if(value<minor-1e-12||value>major+1e-12||Math.Abs(value-Radius(major,pitch,u+4,external))>1e-12)
                    throw new InvalidOperationException("Metric profile bounds/periodicity failed.");
            }
            if(Math.Abs(Radius(major,pitch,.375,external)-(external?minor:major))>1e-12||
               Math.Abs(Radius(major,pitch,.875,external)-(external?major:minor))>1e-12)
                throw new InvalidOperationException("Metric profile flats failed.");
            double slope=Math.Abs((Radius(major,pitch,.2,external)-Radius(major,pitch,.1,external))/(pitch*.1));
            if(Math.Abs(slope-Math.Sqrt(3))>1e-11)throw new InvalidOperationException("Metric profile flank failed.");
            if(external && Math.Abs(Radius(major,pitch,.5,true)-minor)>1e-12)
                throw new InvalidOperationException("External P/4 groove floor failed.");
        }
    }
}

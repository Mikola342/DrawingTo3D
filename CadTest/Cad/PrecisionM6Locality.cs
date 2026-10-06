using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace CadTest.Cad;

public partial class SolidWorksService
{
    public object InspectPrecisionM6Locality(string original,string revised,int holeCount=2)
    {
        if(holeCount is not (2 or 4))throw new ArgumentOutOfRangeException(nameof(holeCount));
        using var batch=BeginApiBatch();
        OpenPartForInspection(original,requireFresh:true);var before=CopyPilotBody();
        OpenPartForInspection(revised,requireFresh:true);var after=CopyPilotBody();
        var modeler=(Modeler)_swApp!.GetModeler();
        Body2 Outside(Body2 body)
        {
            for(int i=0;i<holeCount;i++)
            {
                var c=TiffM6Centres[i];
                var mask=(Body2)modeler.CreateBodyFromCyl(new double[]{.2489,c.Y/1000,c.Z/1000,1,0,0,.003001,.016101});
                var pieces=(object[]?)body.Operations2((int)swBodyOperationType_e.SWBODYCUT,mask,out int error);
                if(error!=0||pieces?.Length!=1)throw new InvalidOperationException("Locality mask cut failed.");
                body=(Body2)pieces[0];
            }
            return body;
        }
        var a=Outside(before);var b=Outside(after);
        object Difference(Body2 left,Body2 right)
        {
            var pieces=(object[]?)((Body2)left.Copy()).Operations2((int)swBodyOperationType_e.SWBODYCUT,(Body2)right.Copy(),out int error);
            int count=pieces?.Length??0;
            bool passed=count==0&&(error==0||error==(int)swBodyOperationError_e.swBodyOperationEmptyBody);
            Console.WriteLine($"[M6 OUTSIDE MASK] components={count},error={error},passed={passed}");
            return new{Components=count,Error=error,Passed=passed};
        }
        var removed=Difference(a,b);var added=Difference(b,a);
        var bodies=(object[])((PartDoc)_model!).GetBodies2((int)swBodyType_e.swSolidBody,false);
        int faults=((Body2)bodies[0]).Check3?.Count??0;
        return new{RemovedOutside=removed,AddedOutside=added,MaskHoles=Enumerable.Range(1,holeCount).ToArray(),MaskRadiusMm=3.001,MaskStartXmm=248.9,MaskEndXmm=265.001,KernelFaults=faults,Scope="Exact bidirectional differences outside explicit local cylinders; not an assertion about inside-mask geometry"};
    }
}

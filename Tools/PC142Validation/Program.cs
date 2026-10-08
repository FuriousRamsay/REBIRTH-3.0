using System;
class Program
{
    static void Main()
    {
        // Expected outputs from the native V3.2 integer-weight calculation, including
        // rounding boundaries, minimum output, full-recovery tag and output modifiers.
        var cases = new (int weight,int count,int output,float recovery,float modifier,int expected)[] {
            (10,1,2,.75f,1f,3), (10,2,2,.75f,1f,7), (1,1,2,.75f,1f,0),
            (2,1,2,.75f,1f,1), (10,1,2,1f,1f,5), (10,2,2,.75f,.5f,3),
            (10,2,2,.75f,2f,15), (3,3,2,.75f,1f,3), (10,0,2,.75f,1f,0),
            (10,2,0,.75f,1f,0), (10,2,2,.75f,0f,0)
        };
        foreach (var c in cases)
        {
            int actual=RebirthScrapPreviewMath.Output(c.weight,c.count,c.output,c.recovery,c.modifier);
            if(actual!=c.expected) throw new Exception($"Scrap preview mismatch: {c}, actual {actual}");
        }
        Console.WriteLine($"PASS: {cases.Length} native scrap-output boundary cases against production calculation.");
    }
}

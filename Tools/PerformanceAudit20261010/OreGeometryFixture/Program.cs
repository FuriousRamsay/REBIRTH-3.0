using System;
struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);}
class LineRenderer {public Vector3[] positions=new Vector3[16];public void SetPositions(Vector3[] p){Array.Copy(p,positions,16);}}
class Program {
 static readonly Vector3[] CubePositions=new Vector3[16];
    private static void Old(LineRenderer l,Vector3 c,float h){Vector3[] p={c+new Vector3(-h,-h,-h),c+new Vector3(h,-h,-h),c+new Vector3(h,-h,h),c+new Vector3(-h,-h,h),c+new Vector3(-h,-h,-h),c+new Vector3(-h,h,-h),c+new Vector3(h,h,-h),c+new Vector3(h,-h,-h),c+new Vector3(h,h,-h),c+new Vector3(h,h,h),c+new Vector3(h,-h,h),c+new Vector3(h,h,h),c+new Vector3(-h,h,h),c+new Vector3(-h,-h,h),c+new Vector3(-h,h,h),c+new Vector3(-h,h,-h)};l.SetPositions(p);}
    private static void SetCube(LineRenderer l,Vector3 c,float h)
    {
        // GameUpdate owns this scratch array; SetPositions submits the complete geometry.
        CubePositions[0]=c+new Vector3(-h,-h,-h);
        CubePositions[1]=c+new Vector3(h,-h,-h);
        CubePositions[2]=c+new Vector3(h,-h,h);
        CubePositions[3]=c+new Vector3(-h,-h,h);
        CubePositions[4]=c+new Vector3(-h,-h,-h);
        CubePositions[5]=c+new Vector3(-h,h,-h);
        CubePositions[6]=c+new Vector3(h,h,-h);
        CubePositions[7]=c+new Vector3(h,-h,-h);
        CubePositions[8]=c+new Vector3(h,h,-h);
        CubePositions[9]=c+new Vector3(h,h,h);
        CubePositions[10]=c+new Vector3(h,-h,h);
        CubePositions[11]=c+new Vector3(h,h,h);
        CubePositions[12]=c+new Vector3(-h,h,h);
        CubePositions[13]=c+new Vector3(-h,-h,h);
        CubePositions[14]=c+new Vector3(-h,h,h);
        CubePositions[15]=c+new Vector3(-h,h,-h);
        l.SetPositions(CubePositions);
    }

 static void Main(){var a=new LineRenderer();var b=new LineRenderer();var r=new Random(173);
 for(int i=0;i<10000;i++){var c=new Vector3((float)r.NextDouble()*10000,(float)r.NextDouble()*10000,(float)r.NextDouble()*10000);float h=(float)r.NextDouble();Old(a,c,h);SetCube(b,c,h);for(int j=0;j<16;j++)if(!a.positions[j].Equals(b.positions[j]))throw new Exception("geometry changed");}
 long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)SetCube(b,new Vector3(i,2,3),.46f);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;if(bytes!=0)throw new Exception("allocation "+bytes);
 Console.WriteLine("PASS: 10000 exact geometry comparisons; zero managed bytes in 10000 new submissions with copying renderer double. Native rendering not tested.");}
}

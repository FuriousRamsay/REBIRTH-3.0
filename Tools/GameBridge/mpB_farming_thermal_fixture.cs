using System;
struct Vector3i { public int x,y,z; public Vector3i(int a,int b,int c){x=a;y=b;z=c;} }
static class Fixture {
// PRODUCTION_CONSTANTS
static readonly Vector3i[] s_thermalSearchQueue=new Vector3i[ThermalSearchMaxNodes];
static readonly int[] s_thermalSearchStamp=new int[ThermalSearchMemoCells];
// PRODUCTION_METHODS
static void Check(bool value,string message){if(!value)throw new Exception(message);}
static void Main(){
 int tail=0;const int stamp=1;
 // Deliberately visit every bounded cell, including the 239 previously lost.
 for(int y=-4;y<=10;y++)for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++)
  EnqueueThermalCell(new Vector3i(x,y,z),-8,-4,-8,17,17,15,stamp,ref tail);
 Check(tail==4335,"thermal queue dropped a configured cell");
 Check(s_thermalSearchQueue[4334].x==8&&s_thermalSearchQueue[4334].y==10&&s_thermalSearchQueue[4334].z==8,"final cell unavailable");
 Check(MakeThermalIndex(new Vector3i(9,0,0),-8,-4,-8,17,17,15)==-1,"out-of-domain admitted");
 // Duplicate insertion cannot consume a second queue position, even at capacity.
 EnqueueThermalCell(new Vector3i(0,0,0),-8,-4,-8,17,17,15,stamp,ref tail);
 Check(tail==4335,"duplicate admitted");
 Console.WriteLine("PASS actual thermal index/enqueue: 4335 cells, final cell, bounds, duplicate");
}
}

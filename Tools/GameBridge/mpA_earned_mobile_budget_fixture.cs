// PREPARED NOT RUN. Actual CountStream and native vehicle envelope preflight.
// Doubles: only EntityClass registry and type inheritance; no native read/hydration.
using System;using System.IO;using System.Collections.Generic;
class EntityVehicle{}class Foreign{}
class EntityClass{internal Type classname;internal static Dictionary<int,EntityClass> list=new Dictionary<int,EntityClass>{{7,new EntityClass{classname=typeof(EntityVehicle)}},{8,new EntityClass{classname=typeof(Foreign)}}};}
static class RebirthCamperSavedVehicle {
// ACTUAL_COUNT_STREAM
// ACTUAL_PREFLIGHT
// ACTUAL_SKIP
// ACTUAL_FLAG
internal static bool Probe(byte[] b,out long end,out long allocation){using(var s=new MemoryStream(b,false)){bool ok=TryMeasureVehicleRecord(s,out end,out allocation);if(s.Position!=0)throw new Exception("Preflight moved original reader");return ok;}}
internal static void Counter(){using(var s=new CountStream(8)){s.Write(new byte[8],0,8);s.Position=0;s.WriteByte(1);if(s.Length!=8||s.Position!=1)throw new Exception("Backpatch");s.Position=8;try{s.WriteByte(0);throw new Exception("Limit accepted");}catch(InvalidDataException){} }}
}
class Program {
static byte[] Frame(int bag,int body){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write((byte)38);w.Write(7);w.Write(20);for(int i=0;i<7;i++)w.Write(0f);w.Write(false);w.Write(4);w.Write(new byte[8]);w.Write(false);w.Write((short)0);w.Write(true);w.Write(bag);w.Write(new byte[bag]);w.Write(new byte[15]);w.Write((ushort)body);w.Write(new byte[body]);w.Write(false);w.Write(new byte[28]);return s.ToArray();}}
static void Need(bool b,string n){if(!b)throw new Exception(n);Console.WriteLine("PASS "+n);}
static void Main(){var b=Frame(64,128);Need(RebirthCamperSavedVehicle.Probe(b,out long end,out long bytes)&&end==b.Length&&bytes==2*(64+128)+128*1024,"actual-framing-and-copy-charge");var v=(byte[])b.Clone();v[0]=37;Need(!RebirthCamperSavedVehicle.Probe(v,out _,out _),"legacy-retains-unknown-budget-lane");v=(byte[])b.Clone();v[1]=8;Need(!RebirthCamperSavedVehicle.Probe(v,out _,out _),"nonvehicle-refused-before-native-read");v=(byte[])b.Clone();v[50]=1;Need(!RebirthCamperSavedVehicle.Probe(v,out _,out _),"stats-allocation-refused");v=(byte[])b.Clone();Buffer.BlockCopy(BitConverter.GetBytes(int.MaxValue),0,v,54,4);Need(!RebirthCamperSavedVehicle.Probe(v,out _,out _),"malformed-bag-length-before-allocation");Array.Resize(ref v,60);Need(!RebirthCamperSavedVehicle.Probe(v,out _,out _),"truncated-row");RebirthCamperSavedVehicle.Counter();Need(true,"actual-count-stream-seek-and-fixed-limit");}
}

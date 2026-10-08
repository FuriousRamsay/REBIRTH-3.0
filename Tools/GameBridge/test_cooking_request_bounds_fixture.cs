using System;
using System.IO;
using System.Collections.Generic;
class Value { public int type; }
class ItemStack { public Value itemValue=new Value(); public static ItemStack Empty=new ItemStack(); public static int Reads; public void Read(BinaryReader r){Reads++;itemValue.type=r.ReadInt32();} }
class ItemClass { public static object GetForId(int id){return id==1?new object():null;} }
class Check {
// PRODUCTION_METHOD
static void Main(){foreach(int n in new[]{0,1,12,13,65535}){using(var ms=new MemoryStream()){var w=new BinaryWriter(ms);w.Write((ushort)n);if(n<=12)for(int i=0;i<n;i++)w.Write(i%2==0?1:999);ms.Position=0;ItemStack.Reads=0;try{var items=ReadIngredientRequests(new BinaryReader(ms));if(n>12||items.Count!=n||ItemStack.Reads!=n)throw new Exception("Count");if(n>1&&!object.ReferenceEquals(items[1],ItemStack.Empty))throw new Exception("Unknown item");}catch(InvalidDataException){if(n<=12||ItemStack.Reads!=0||ms.Position!=2)throw;}}}Console.WriteLine("PASS: valid counts, unknown items, oversized rejection before item decoding");}
}

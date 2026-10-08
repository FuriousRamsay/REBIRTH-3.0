using System;using System.Threading.Tasks;
using P=RebirthGearNativeWritePermit;
class Program{
 static int checks;static void Check(bool b,string why){if(!b)throw new Exception(why);checks++;}
 static void Main(){object inv=new(),value=new();var k=P.Kind.BagSlot;
 Check(!P.TryConsume(k,inv,3,value,1),"ambient permission");
 P.Execute(k,inv,3,value,1,()=>true,()=>{
 Check(!P.TryConsume(k,new object(),3,value,1),"wrong inventory");
 Check(!P.TryConsume(k,inv,4,value,1),"wrong index");
 Check(!P.TryConsume(k,inv,3,new object(),1),"wrong payload");
 Check(!P.TryConsume(k,inv,3,value,2),"wrong count");
 Check(!P.TryConsume(P.Kind.BeltSlot,inv,3,value,1),"wrong kind");
 Check(!Task.Run(()=>P.TryConsume(k,inv,3,value,1)).Result,"cross thread");
 Check(P.TryConsume(k,inv,3,value,1),"exact entry refused");
 Check(!P.TryConsume(k,inv,3,value,1),"callback reused");
 });
 Check(!P.TryConsume(k,inv,3,value,1),"scope leaked");
 bool threw=false;try{P.Execute(k,inv,3,value,1,()=>true,()=>{});}catch(InvalidOperationException){threw=true;}Check(threw,"unconsumed setter accepted");
 bool current=true;threw=false;try{P.Execute(k,inv,3,value,1,()=>current,()=>{current=false;Check(!P.TryConsume(k,inv,3,value,1),"lost context consumed");});}catch(InvalidOperationException){threw=true;}Check(threw,"lost context accepted");
 P.Execute(k,inv,3,value,1,()=>true,()=>{bool nested=false;try{P.Execute(k,inv,3,value,1,()=>true,()=>{});}catch(InvalidOperationException){nested=true;}Check(nested,"nested bypass opened");Check(P.TryConsume(k,inv,3,value,1),"nested refusal cleared outer");});
 threw=false;try{P.Execute(k,inv,3,value,1,()=>true,()=>throw new Exception());}catch{threw=true;}Check(threw&&!P.TryConsume(k,inv,3,value,1),"exception leaked permission");
 Console.WriteLine("PASS "+checks+" actual one-use native write permission checks; no Harmony/game execution.");
 }}
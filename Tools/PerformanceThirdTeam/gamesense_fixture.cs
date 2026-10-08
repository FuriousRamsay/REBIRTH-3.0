using System;
using System.Collections.Generic;
public static class Time {public static float unscaledTime;}
// PRODUCTION_CLASSES
public static class GameSenseChecks {
 sealed class Throws {public override string ToString(){throw new InvalidOperationException("value conversion");}}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Same(object[] args){bool a=Before.Prefix(args),b=After.Prefix(args);Check(a==b,"decision");Check(Before.SentMessages==After.SentMessages&&Before.SkippedMessages==After.SkippedMessages,"counters");}
 public static void Run(){
 Time.unscaledTime=0;Same(null);Same(new object[0]);Same(new object[]{"x"});Same(new object[]{null,1});Same(new object[]{"x",null});Same(new object[]{3,1});
 Same(new object[]{"health",10});Same(new object[]{"health",10});Time.unscaledTime=4.999f;Same(new object[]{"health",10});Time.unscaledTime=5;Same(new object[]{"health",10});Same(new object[]{"health",11});Same(new object[]{"health",11,new object()});Time.unscaledTime=-1;Same(new object[]{"health",11});
 foreach(float f in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,0f}){Time.unscaledTime=f;Same(new object[]{"special",1});Same(new object[]{"special",1});}
 long sent=Before.SentMessages,skipped=Before.SkippedMessages;bool first=false,second=false;try{Before.Prefix(new object[]{"throw",new Throws()});}catch(InvalidOperationException){first=true;}try{After.Prefix(new object[]{"throw",new Throws()});}catch(InvalidOperationException){second=true;}Check(first&&second,"conversion exceptions");Check(sent==Before.SentMessages&&sent==After.SentMessages&&skipped==Before.SkippedMessages&&skipped==After.SkippedMessages,"exception no cache writes");
 var random=new Random(71006);for(int i=0;i<10000;i++){Time.unscaledTime+=(float)(random.NextDouble()*2-.3);string name="event"+random.Next(8);object value=random.Next(4)==0?(object)"text":random.Next(6);if(random.Next(20)==0)value=null;Same(random.Next(5)==0?new object[]{name,value,new object()}:new object[]{name,value});}
 }
}

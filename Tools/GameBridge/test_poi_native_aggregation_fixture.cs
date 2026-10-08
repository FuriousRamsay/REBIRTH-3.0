using System;
// ACTUAL_STATE
namespace InControl {static class Time{public static float realtimeSinceStartup;}static class Utility{
// ACTUAL_UTILITY
}
class Aggregation {
// ACTUAL_FIELDS
// ACTUAL_METHODS
public InputControlState Next(){return nextState;}public InputControlState Current(){return thisState;}public void Configure(bool raw,float lower,float upper,float threshold,bool nul=false){Raw=raw;lowerDeadZone=lower;upperDeadZone=upper;stateThreshold=threshold;isNullControl=nul;}
}}
class Program{static int n;static void A(bool condition,string label){n++;if(!condition)throw new Exception(label);}static bool Close(float a,float b){return Math.Abs(a-b)<.00001f;}static void Main(){
var a=new InControl.Aggregation();A(a.UpdateWithValue(.4f,1,.1f),"first accepted");A(a.UpdateWithValue(-.8f,1,.1f)&&Close(a.Next().RawValue,-.8f),"greater opposing magnitude wins");A(!a.UpdateWithValue(.6f,1,.1f)&&Close(a.Next().Value,-.8f),"weaker loses");A(!a.UpdateWithValue(.8f,1,.1f)&&Close(a.Next().RawValue,-.8f),"equal opposing retains first");A(!a.UpdateWithValue(0,1,.1f),"zero cannot erase nonzero");a.Commit();A(Close(a.Current().Value,-.8f),"actual commit publishes");A(a.UpdateWithValue(.2f,2,.1f)&&Close(a.Next().Value,.2f),"next tick resets accumulator");bool failed=false;try{a.UpdateWithValue(.3f,1,.1f);}catch(InvalidOperationException){failed=true;}A(failed,"older tick refuses");failed=false;try{a.UpdateWithValue(.3f,3,.1f);}catch(InvalidOperationException){failed=true;}A(failed,"uncommitted next tick refuses");
a=new InControl.Aggregation();a.UpdateWithValue(-.8f,1,.1f);a.UpdateWithValue(.4f,1,.1f);A(Close(a.Next().RawValue,-.8f),"unequal reverse order same strongest");a=new InControl.Aggregation();a.UpdateWithValue(.8f,1,.1f);a.UpdateWithValue(-.8f,1,.1f);A(Close(a.Next().RawValue,.8f),"equal reverse order changes retained sign");
a=new InControl.Aggregation();a.Configure(false,.2f,.8f,.5f);a.UpdateWithValue(.5f,1,.1f);A(Close(a.Next().RawValue,.5f)&&Close(a.Next().Value,.5f),"raw magnitude preserved and deadzone converts");A(!a.Next().State,"threshold equality not over");a.Commit();a.UpdateWithValue(.6f,2,.1f);A(a.Next().State&&Close(a.Next().Value,2f/3f),"converted threshold over");a=new InControl.Aggregation();a.Configure(true,.2f,.8f,.5f);a.UpdateWithValue(.6f,1,.1f);A(Close(a.Next().Value,.6f),"Raw bypasses deadzone");a=new InControl.Aggregation();a.Configure(false,0,1,0,true);A(!a.UpdateWithValue(1,1,.1f)&&a.Next().RawValue==0,"null control refuses");Console.WriteLine("PASS "+n+" captured installed aggregation checks; actual prepare/value/commit/state/utility extracted, Unity Time doubled; NO physical sampling/Harmony/native runtime arbitration claim.");}}

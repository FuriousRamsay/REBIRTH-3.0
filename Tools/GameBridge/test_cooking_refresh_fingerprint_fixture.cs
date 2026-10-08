using System;using System.Collections.Generic;
class Recipe{public string Name="dish";public int Definition;public string GetName(){return Name;}}
class Subject{public bool rendering=true;public int? renderFeasibilityFingerprint;int feasibilityFingerprint=int.MinValue;Dictionary<string,int> feasibilityCache=new Dictionary<string,int>();public int State,Hashes,Plans;
int FeasibilityInputFingerprint(){Hashes++;return State;}
int RecipeDefinitionFingerprint(Recipe r){return r.Definition;}
int Plan(Recipe r,out int[] types){Plans++;types=null;return State+r.Definition;}
// PRODUCTION_METHOD
public int Run(Recipe r){return Feasible(r);}public void Refresh(int state){State=state;renderFeasibilityFingerprint=null;}}
class Check{static void Main(){var s=new Subject();var a=new Recipe();var b=new Recipe{Name="other"};s.Refresh(4);for(int i=0;i<160;i++){if(s.Run(a)!=4||s.Run(b)!=4)throw new Exception("result");}if(s.Hashes!=1||s.Plans!=2)throw new Exception("refresh work");s.Refresh(7);if(s.Run(a)!=7||s.Hashes!=2||s.Plans!=3)throw new Exception("stale inventory");a.Definition=2;if(s.Run(a)!=9||s.Plans!=4||s.Hashes!=2)throw new Exception("recipe definition");s.rendering=false;s.State=11;if(s.Run(a)!=13)throw new Exception("outside refresh");s.State=13;if(s.Run(a)!=15||s.Hashes!=4)throw new Exception("outside stale");if(s.Run(null)!=0||s.Hashes!=4)throw new Exception("null");Console.WriteLine("PASS: 320 recipe checks share one refresh hash; next refresh and recipe definition invalidate correctly; outside-refresh checks stay fresh");}}

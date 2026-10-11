using System;
public class Recipe {}
public class Entry { public Recipe recipe; public Recipe GetRecipe()=>recipe; }
public class Queue {public Entry[] entries={new Entry(),new Entry(),new Entry(),new Entry()};public Entry[] GetRecipesToCraft()=>entries;}
public class Station {public bool IsMilling=true;public Queue craftingQueue=new Queue();public bool CraftingRequirementsValid(Recipe r)=>true;}
public class Prep {public bool IsPreparing;}
public sealed partial class XUiC_RebirthCookingWorkspace {
 public bool open=true,pulling,submittingCook,preparingRequest;public Station station=new Station();public Prep Preparation=new Prep();private int batch;
 private object MissingTool(Recipe r)=>null;private int Feasible(Recipe r)=>100;private void Select(Recipe r){}private void SetBatch(int n){batch=n;}private void Pull(){}
}
class Test {static void Check(bool b,string s){if(!b)throw new Exception(s);}static void Main(){var w=new XUiC_RebirthCookingWorkspace();var r=new Recipe();Check(w.CanCraftShared(r,1),"empty");w.station.craftingQueue.entries[3].recipe=r;Check(w.CanCraftShared(r,10),"second queued batch");foreach(var e in w.station.craftingQueue.entries)e.recipe=r;Check(!w.CanCraftShared(r,1),"full queue");w.station.craftingQueue.entries[0].recipe=null;w.station.IsMilling=false;Check(!w.CanCraftShared(r,1),"cooking excluded");w.station.IsMilling=true;Check(!w.CanCraftShared(r,101),"materials");Check(!w.CanCraftShared(r,0),"zero");w.pulling=true;Check(!w.CanCraftShared(r,1),"pending pull");Console.WriteLine("PASS: actual shared milling admission; second batch accepted, full/cooking/zero/material/pending rejected. Engine dependencies doubled; runtime not tested.");}}

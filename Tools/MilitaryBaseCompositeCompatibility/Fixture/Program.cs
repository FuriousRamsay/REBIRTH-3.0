using System.Collections.Generic;
public static class RebirthBlockPickupClassifier {public const string PropertyPickup="RebirthPickup";}
public class DynamicProperties {public Dictionary<string,string> Values=new();}
public class WorldBase {public object Entity;public int Queries;public object GetTileEntity(Vector3i pos){Queries++;return Entity;}}
public class BlockEntityData{}
public class Chunk{}public struct Vector3i{}public struct BlockValue {public bool ischild;}
public class PlatformUserIdentifierAbs{}
public class TileEntityComposite {public readonly object Contents=new();public int Added,Models;}
public class Block {
 public DynamicProperties Properties=new();public int BaseAdds,CompositeAdds,BaseModels,CompositeModels,InitCalls;
 public virtual void Init(){InitCalls++;}
 public virtual void OnBlockEntityTransformAfterActivated(WorldBase w,Vector3i p,BlockValue v,BlockEntityData b){BaseModels++;}
 public virtual void OnBlockAdded(WorldBase w,Chunk c,Vector3i p,BlockValue v,PlatformUserIdentifierAbs a){BaseAdds++;}
}
public class BlockCompositeTileEntity:Block {
 public override void OnBlockEntityTransformAfterActivated(WorldBase w,Vector3i p,BlockValue v,BlockEntityData bed){if(bed==null)return;CompositeModels++;var te=w.Entity as TileEntityComposite;if(te==null)w.Entity=te=new();te.Models++;base.OnBlockEntityTransformAfterActivated(w,p,v,bed);}

 public override void OnBlockAdded(WorldBase w,Chunk c,Vector3i p,BlockValue v,PlatformUserIdentifierAbs a){base.OnBlockAdded(w,c,p,v,a);if(v.ischild)return;CompositeAdds++;var te=w.Entity as TileEntityComposite;if(te==null)w.Entity=te=new();te.Added++;}
}
public static class Program {
 static int checks;static void A(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 public static int Main(){System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(ctx,name)=>{foreach(var root in new[]{"C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/7DaysToDie_Data/Managed","C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/0_TFP_Harmony"}){var path=Path.Combine(root,name.Name+".dll");if(File.Exists(path))return ctx.LoadFromAssemblyPath(path);}return null;};return Run();}
 static int Run(){try{
 for(int run=0;run<1000;run++){
  var b=new BlockRebirthLegacyMilitaryPalletBase();b.Properties.Values["RebirthPickup"]="Allow";b.Init();A(b.InitCalls==1&&b.Properties.Values["RebirthPickup"]=="Deny","native init retained, per-instance pickup denied");
  var tile=new BlockCompositeTileEntity();tile.Properties.Values["RebirthPickup"]="Allow";tile.Init();A(tile.Properties.Values["RebirthPickup"]=="Allow","independent tile policy unchanged");
  var w=new WorldBase();b.OnBlockAdded(w,new(),new(),new(),null);A(w.Entity==null&&b.BaseAdds==1&&b.CompositeAdds==0,"fresh base/cap performs ordinary block lifecycle without new TE/loot");A(w.Queries==1,"only explicit existing TE lookup");
  b.OnBlockAdded(w,new(),new(),new(){ischild=true},new());A(w.Entity==null&&b.BaseAdds==2&&w.Queries==1,"child follows ordinary native callback without TE lookup");
  var saved=new TileEntityComposite();var content=saved.Contents;w.Entity=saved;b.OnBlockAdded(w,new(),new(),new(),null);A(ReferenceEquals(w.Entity,saved)&&ReferenceEquals(saved.Contents,content)&&saved.Added==1&&b.CompositeAdds==1&&b.BaseAdds==3,"existing native composite lifecycle retained exactly once");
  var other=new object();w.Entity=other;b.OnBlockAdded(w,new(),new(),new(),null);A(ReferenceEquals(w.Entity,other)&&b.BaseAdds==4&&b.CompositeAdds==1,"foreign TE not replaced");
  w.Entity=null;b.OnBlockEntityTransformAfterActivated(w,new(),new(),new());A(w.Entity==null&&b.BaseModels==1&&b.CompositeModels==0,"fresh model activation does not create TE/loot; native base model callback retained");
  b.OnBlockEntityTransformAfterActivated(w,new(),new(),null);A(w.Entity==null&&b.BaseModels==1,"fresh null model preserves native admission without callback");
  w.Entity=saved;b.OnBlockEntityTransformAfterActivated(w,new(),new(),new());A(ReferenceEquals(w.Entity,saved)&&saved.Models==1&&b.CompositeModels==1&&b.BaseModels==2,"legacy model activation retains native composite callback");
  b.OnBlockEntityTransformAfterActivated(w,new(),new(),null);A(saved.Models==1&&b.CompositeModels==1&&b.BaseModels==2,"existing native null model behavior retained");
  var nativeWorld=new WorldBase();tile.OnBlockAdded(nativeWorld,new(),new(),new(),null);A(nativeWorld.Entity is TileEntityComposite,"ordinary tile native creation unchanged");
  nativeWorld.Entity=null;tile.OnBlockEntityTransformAfterActivated(nativeWorld,new(),new(),new());A(nativeWorld.Entity is TileEntityComposite,"ordinary tile native model creation unchanged");
 }
 Console.WriteLine($"PASS {checks} actual compatibility-class checks using actual nonvirtual native-call emitter");return 0;
 }catch(Exception e){Console.WriteLine(e.ToString());return 1;}}
}

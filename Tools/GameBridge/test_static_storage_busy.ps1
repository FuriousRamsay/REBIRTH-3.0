$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$s=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Crafting/RemoteCrafting/RemoteResourceNetwork.cs'))
$m=[regex]::Match($s,'public bool IsBusy \{ get \{ TileEntity te = TileEntity; TEFeatureStorage loot = Loot;[^\r\n]+')
if(!$m.Success){throw 'Static source busy property missing'}
$code=@"
public class TileEntity { public bool Accessing; public bool IsUserAccessing(){return Accessing;} }
public class TEFeatureStorage {}
public static class RemoteResourceAccess { public static object Locked; public static bool IsServerBusy(object target){return object.ReferenceEquals(target,Locked);} }
public class Source {public TileEntity TileEntity;public TEFeatureStorage Loot;
$($m.Value)
}
public static class Fixture {static void Check(bool v,string s){if(!v)throw new System.Exception(s);}public static void Run(){
var p=new TileEntity();var storage=new TEFeatureStorage();var source=new Source{TileEntity=p,Loot=storage};
Check(!source.IsBusy,"unlocked source available");RemoteResourceAccess.Locked=storage;Check(source.IsBusy,"feature lock excludes source");RemoteResourceAccess.Locked=p;Check(!source.IsBusy,"parent lock does not substitute storage lease");RemoteResourceAccess.Locked=null;p.Accessing=true;Check(source.IsBusy,"user access still excludes");p.Accessing=false;source.Loot=null;Check(source.IsBusy,"missing storage fails closed");source.Loot=storage;source.TileEntity=null;Check(source.IsBusy,"unloaded parent fails closed");}}
"@
Add-Type -TypeDefinition $code
[Fixture]::Run()
'PASS actual static-source busy property; native lock lookup doubled. No game execution.'
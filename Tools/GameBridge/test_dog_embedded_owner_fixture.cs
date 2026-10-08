using System;
struct RebirthNpcStableId { public int Value; }
enum RebirthNpcOwnershipKind { None,Player }
class World {public bool Remote=true; public bool IsRemote(){return Remote;}}
class RebirthNpcRuntimeState {public RebirthNpcStableId StableId; public string OwnerId; public RebirthNpcOwnershipKind OwnershipKind; public uint Revision=7;public int Order=4; public void MarkPersisted(){} }
class Check {
World world=new World(); RebirthNpcRuntimeState RebirthRuntimeState=new RebirthNpcRuntimeState{StableId=new RebirthNpcStableId{Value=5}};
bool rebirthClientOwnershipReceived; RebirthNpcStableId rebirthSavedStableId=new RebirthNpcStableId{Value=5};string rebirthSavedOwnerId="player";int projected;
void OnRebirthClientOwnershipReceived(){rebirthClientOwnershipReceived=true;projected++;}
// PRODUCTION_METHOD

static void A(bool b,string m){if(!b)throw new Exception(m);}
static void Main(){var c=new Check();c.InitializeClientEmbeddedOwnership();A(c.RebirthRuntimeState.OwnerId=="player"&&c.projected==1,"owned initialization");A(c.RebirthRuntimeState.Revision==7&&c.RebirthRuntimeState.Order==4,"other state preserved");c.rebirthSavedOwnerId="old";c.InitializeClientEmbeddedOwnership();A(c.RebirthRuntimeState.OwnerId=="player"&&c.projected==1,"received state protected");c=new Check();c.rebirthSavedOwnerId="";c.InitializeClientEmbeddedOwnership();A(c.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.None,"unowned");c=new Check();c.world.Remote=false;c.InitializeClientEmbeddedOwnership();A(c.projected==0,"server excluded");c=new Check();c.rebirthSavedStableId=new RebirthNpcStableId{Value=6};c.InitializeClientEmbeddedOwnership();A(c.projected==0,"identity mismatch");Console.WriteLine("PASS: embedded owner initialization, authority, identity and transition protection");}}

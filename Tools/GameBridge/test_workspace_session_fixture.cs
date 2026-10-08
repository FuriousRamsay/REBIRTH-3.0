using System;
class World{}
class EntityPlayerLocal {public World world;}
class PlayerUI {public EntityPlayerLocal entityPlayer;}
class XUi {public PlayerUI playerUI;}
class Workspace {internal bool open;internal int pullUiGeneration;internal World sessionWorld;internal XUi xui;
// PRODUCTION_CLASS
internal bool Accept(int generation,EntityPlayerLocal player)=>IsCurrentSession(generation,player);
}
class Check {static void Assert(bool v,string reason){if(!v)throw new Exception(reason);}static void Main(){var world=new World();var player=new EntityPlayerLocal{world=world};var w=new Workspace{open=true,pullUiGeneration=4,sessionWorld=world,xui=new XUi{playerUI=new PlayerUI{entityPlayer=player}}};Assert(w.Accept(4,player),"Current session accepted");w.open=false;Assert(!w.Accept(4,player),"Closed window rejects");w.open=true;w.pullUiGeneration=5;Assert(!w.Accept(4,player),"Reopened generation rejects old response");Assert(!w.Accept(5,new EntityPlayerLocal{world=world}),"Different player reference rejects");player.world=new World();Assert(!w.Accept(5,player),"World change rejects");w.xui=null;Assert(!w.Accept(5,player),"Torn down UI rejects");Console.WriteLine("PASS actual workspace session predicate with UI/player/world doubles: current, closed, reopened, different owner/world and teardown");}}

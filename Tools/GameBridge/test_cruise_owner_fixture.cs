using System;
class World{}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
class EntityPlayerLocal{public int entityId=1;public object AttachedToEntity;public World world=GameManager.Instance.World;public bool Dead;public bool IsDead()=>Dead;public LocalPlayerUI PlayerUI=new LocalPlayerUI();}
class EntityVehicle{public object AttachedMainEntity;}
class EntityVHelicopter:EntityVehicle{}
class EntityVGyroCopter:EntityVehicle{}
class MovementInput{public float moveForward;public bool running;public void Clear(){moveForward=0;running=false;}}
class Axis{public float Y;}
class ActionButton{public bool WasPressed,IsPressed;}
class PlayerActionsVehicle{public Axis Move=new Axis();public ActionButton MoveBack=new ActionButton(),Brake=new ActionButton();}
class Input{public PlayerActionsVehicle VehicleActions=new PlayerActionsVehicle();}
class WindowManager{public bool Active;public bool IsInputActive()=>Active;}
class LocalPlayerUI{public WindowManager windowManager=new WindowManager();public Input playerInput=new Input();public static LocalPlayerUI GetUIForPlayer(EntityPlayerLocal player)=>player.PlayerUI;}
class ActionSet{public bool Enabled=true;public ActionButton ToggleCruiseControl=new ActionButton();}
static class Time{public static int frameCount;}
static class RebirthConsoleInputGuardRuntime{public static bool Blocked;public static bool BlocksGameplayInput()=>Blocked;}
static class Subject
{
    public static ActionSet Actions=new ActionSet();
// PRODUCTION_FIELDS
// PRODUCTION_TOGGLE
// PRODUCTION_MOVE
}
public static class CruiseOwnerChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        var player=new EntityPlayerLocal();var vehicle=new EntityVehicle{AttachedMainEntity=player};player.AttachedToEntity=vehicle;
        var movement=new MovementInput();Subject.Actions.ToggleCruiseControl.WasPressed=true;Time.frameCount++;
        Subject.MoveByAttachedEntityPostfix(vehicle,player,ref movement);
        Check(movement.moveForward==1&&!movement.running&&Subject.GetCruiseState(player)==1,"driver toggle");
        Subject.Actions.ToggleCruiseControl.WasPressed=false;vehicle.AttachedMainEntity=new object();movement.moveForward=.25f;movement.running=true;
        RebirthConsoleInputGuardRuntime.Blocked=true;Subject.MoveByAttachedEntityPostfix(vehicle,player,ref movement);
        Check(movement.moveForward==.25f&&movement.running&&Subject.GetCruiseState(player)==0,"passenger modified movement");
        RebirthConsoleInputGuardRuntime.Blocked=false;vehicle.AttachedMainEntity=player;movement.moveForward=0;
        Subject.MoveByAttachedEntityPostfix(vehicle,player,ref movement);Check(movement.moveForward==0,"seat return resumed cruise");
        Subject.Actions.ToggleCruiseControl.WasPressed=true;Time.frameCount++;Subject.MoveByAttachedEntityPostfix(vehicle,player,ref movement);
        var replacement=new EntityVehicle{AttachedMainEntity=player};player.AttachedToEntity=replacement;Subject.Actions.ToggleCruiseControl.WasPressed=false;movement.moveForward=0;
        Subject.MoveByAttachedEntityPostfix(replacement,player,ref movement);Check(movement.moveForward==0&&Subject.GetCruiseState(player)==0,"vehicle switch retained cruise");
        Subject.Actions.ToggleCruiseControl.WasPressed=true;Time.frameCount++;Subject.MoveByAttachedEntityPostfix(replacement,player,ref movement);player.Dead=true;movement.moveForward=.5f;
        Subject.MoveByAttachedEntityPostfix(replacement,player,ref movement);Check(movement.moveForward==.5f&&Subject.GetCruiseState(player)==0,"dead owner drive");
        Console.WriteLine("PASS actual cruise methods: native driver, passenger untouched, seat/vehicle/death reset. Native topology NOT covered.");
    }
}

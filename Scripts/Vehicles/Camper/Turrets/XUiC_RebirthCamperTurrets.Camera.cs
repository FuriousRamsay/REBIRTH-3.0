using System;
using System.Collections.Generic;
using InControl;
using UnityEngine;

public sealed partial class XUiC_RebirthCamperTurrets
{
    internal Newtonsoft.Json.Linq.JObject ObservePresentation()=>new Newtonsoft.Json.Linq.JObject
    {
        ["open"]=opened,["waiting"]=waiting,["selectedCorner"]=selectedCorner,["backpackFilter"]=Backpack?.Filter??0,
        ["cameraCorner"]=cameraCorner,["cameraActive"]=turretCamera!=null,["automaticZoom"]=automaticZoom,
        ["localPlayerFirstPerson"]=xui.playerUI.entityPlayer.bFirstPersonView,
        ["cameraInstance"]=turretCamera?turretCamera.GetInstanceID():0,
        ["cameraWorldPosition"]=turretCamera?new Newtonsoft.Json.Linq.JArray(turretCamera.transform.position.x+Origin.position.x,turretCamera.transform.position.y+Origin.position.y,turretCamera.transform.position.z+Origin.position.z):null,
        ["cameraForward"]=turretCamera?new Newtonsoft.Json.Linq.JArray(turretCamera.transform.forward.x,turretCamera.transform.forward.y,turretCamera.transform.forward.z):null,
        ["selectedZoom"]=selectedZoom,["autoZoom"]=autoZoom,
        ["fieldOfView"]=turretCamera?new Newtonsoft.Json.Linq.JValue(turretCamera.fieldOfView):Newtonsoft.Json.Linq.JValue.CreateNull(),
        ["warningVisible"]=Time.realtimeSinceStartup<rangeWarningUntil,
        ["cursorCount"]=xui.DragAndDropWindow?.CurrentStack?.count??0,
        ["backpackInput"]=ObserveBackpackInput()
    };
    private EntityVehicle cameraVehicle;
    private Action<int,Guid,float,float,bool> cameraInput;
    private Action<int,Guid,bool> cameraControl;
    private Guid controlToken;
    private int pendingCamera=-1;
    private int requestedCamera=-1;
    internal void RequestCamera(int corner){requestedCamera=corner;}
    private float controlDeadline;
    private Camera turretCamera;
    private readonly RebirthCamperCctvPlayerView cctvPlayerView=new RebirthCamperCctvPlayerView();
    private RenderTexture cameraTexture;
    private int cameraCorner=-1;
    private float cameraYaw,cameraPitch,nextCameraInput;
    private Vector2 previousCameraMouse;
    private static readonly HarmonyLib.AccessTools.FieldRef<PlayerMoveController,int> CameraInvert=HarmonyLib.AccessTools.FieldRefAccess<PlayerMoveController,int>("invertMouse");
    private static readonly HarmonyLib.AccessTools.FieldRef<PlayerMoveController,float> CameraZoomSensitivity=HarmonyLib.AccessTools.FieldRefAccess<PlayerMoveController,float>("mouseZoomSensitivity");
    private static readonly HarmonyLib.AccessTools.FieldRef<PlayerMoveController,float> CameraZoomAcceleration=HarmonyLib.AccessTools.FieldRefAccess<PlayerMoveController,float>("zoomAccel");
    private float rangeWarningUntil;
    private bool automaticZoom;
    private int selectedZoom=1,autoZoom=1;
    private float nextZoomFocus;
    private float cameraZoomTarget=80,cameraZoomFrom=80,cameraZoomStarted;
    internal void RangeDenied(Guid token,int corner,float range,float distance)
    {
        if(token!=controlToken||corner!=cameraCorner||cameraCorner<0)return;
        Text("turretCameraWarning",string.Format(Localization.Get("xuiCamperTurretOutOfRange"),distance.ToString("0.#"),range.ToString("0.#")));
        rangeWarningUntil=Time.realtimeSinceStartup+1.25f;
        Audio.Manager.PlayInsidePlayerHead("ui_denied");
    }
    private bool fireReleased,exitReleased;
    private readonly List<Tuple<PlayerAction,BindingSource>> cameraBindings=new List<Tuple<PlayerAction,BindingSource>>();
    private void MouseAxis(PlayerAction action,Mouse mouse)
    {var binding=new MouseBindingSource(mouse);if(!action.HasBinding(binding)&&action.AddBinding(binding))cameraBindings.Add(Tuple.Create(action,(BindingSource)binding));}
    internal void BindCamera(EntityVehicle vehicle,Action<int,Guid,float,float,bool> input,Action<int,Guid,bool> control)
    {cameraVehicle=vehicle;cameraInput=input;cameraControl=control;}
    private void StartCamera(int corner)
    {
        if(waiting||state?.Slots[corner].HasTurret!=true||current?.Invoke()!=true||!cameraVehicle)return;
        StopCamera();
        controlToken=Guid.NewGuid();pendingCamera=corner;controlDeadline=Time.realtimeSinceStartup+5;
        Status("xuiCamperTurretWaiting");cameraControl?.Invoke(corner,controlToken,true);
    }
    internal void ControlResult(Guid token,int corner,bool accepted)
    {
        if(token!=controlToken){if(accepted)cameraControl?.Invoke(corner,token,false);return;}
        if(!accepted){StopCamera();Status("xuiCamperTurretBusy");return;}
        if(pendingCamera==corner){pendingCamera=-1;OpenCamera(corner);}
    }
    private void OpenCamera(int corner)
    {
        if(!RebirthCamperTurretRuntime.For(cameraVehicle).CameraPose(corner,out var muzzle,out cameraYaw,out cameraPitch)){StopCamera();return;}
        try
        {
            // Native view regeneration must precede parenting the CCTV to its muzzle.
            cctvPlayerView.Begin(xui.playerUI.entityPlayer);
            // Reuse the native CCTV prefab/render setup; a vehicle has no world-block camera tile.
            var prefab=Resources.Load<GameObject>("Prefabs/ElectricityCamera");
            if(!prefab)throw new InvalidOperationException("Native CCTV camera prefab unavailable.");
            var go=UnityEngine.Object.Instantiate(prefab,muzzle);go.transform.localPosition=new Vector3(0,0,.04f);go.transform.localRotation=Quaternion.identity;
            turretCamera=go.GetComponent<Camera>();
            if(!turretCamera){UnityEngine.Object.Destroy(go);throw new InvalidOperationException("Native CCTV camera component unavailable.");}
            turretCamera.gameObject.AddComponent<RebirthCamperLaserCameraClip>();
            cameraTexture=new RenderTexture(1280,720,24);cameraTexture.Create();
            turretCamera.nearClipPlane=.01f;turretCamera.depth=-10;turretCamera.farClipPlane=1000;turretCamera.fieldOfView=80;
            cameraZoomTarget=cameraZoomFrom=80;cameraZoomStarted=Time.realtimeSinceStartup;
            turretCamera.cullingMask&= -513;turretCamera.renderingPath=RenderingPath.DeferredShading;turretCamera.clearFlags=CameraClearFlags.Color;turretCamera.targetTexture=cameraTexture;
            var view=(XUiV_Texture)GetChildById("turretCameraView").ViewComponent;view.Texture=cameraTexture;
            if(SystemInfo.graphicsDeviceVersion.Contains("OpenGL"))view.Flip=UIBasicSprite.Flip.Vertically;
            cameraCorner=corner;previousCameraMouse=Vector2.zero;fireReleased=exitReleased=false;nextCameraInput=0;pickerCorner=-1;PaintPicker();
            var gui=xui.playerUI.playerInput.GUIActions;
            MouseAxis(gui.CameraLeft,Mouse.NegativeX);MouseAxis(gui.CameraRight,Mouse.PositiveX);MouseAxis(gui.CameraUp,Mouse.PositiveY);MouseAxis(gui.CameraDown,Mouse.NegativeY);
            Visible("turretCameraPanel",true); CameraCards(false);
            xui.playerUI.CursorController.Locked=true;xui.playerUI.CursorController.SetCursorHidden(true);
            OcclusionManager.Instance.SetMultipleCameras(true);
        }
        catch(Exception e){StopCamera();Status("xuiCamperTurretInvalid");Log.Warning("[CamperTurrets] CCTV refused: "+e.Message);}
    }
    private void UpdateCamera()
    {
        if(requestedCamera>=0&&!waiting&&state!=null){int corner=requestedCamera;requestedCamera=-1;StartCamera(corner);}
        if(pendingCamera>=0&&Time.realtimeSinceStartup>=controlDeadline){StopCamera();Status("xuiCamperTurretInvalid");}
        if(cameraCorner<0)return;
        if(Time.realtimeSinceStartup>=rangeWarningUntil)Text("turretCameraWarning","");
        if(!cameraVehicle||!turretCamera||state?.Slots[cameraCorner].HasTurret!=true){StopCamera();return;}
        var input=xui.playerUI.playerInput;
        bool pointer=false;
        xui.playerUI.CursorController.Locked=!pointer;xui.playerUI.CursorController.SetCursorHidden(!pointer);
        bool fire=!pointer&&input.GUIActions.LeftClick.IsPressed;
        if(pointer)fireReleased=false;else if(!fire)fireReleased=true;
        if(!input.PermanentActions.Activate.IsPressed)exitReleased=true;
        if(exitReleased&&input.PermanentActions.Activate.WasPressed){StopCamera();return;}
        var movement=xui.playerUI.entityPlayer.MoveController;
        bool controller=input.GUIActions.LastInputType==BindingSourceType.DeviceBindingSource;
        var sensitivity=controller?movement.controllerLookSensitivity:movement.mouseLookSensitivity;
        // Match native mouse axes, inversion and two-frame averaging. GUI camera
        // actions have different scaling and must not replace gameplay mouse input.
        var raw=RebirthCamperCctvMouseInput.Read();raw.y*=CameraInvert(movement);
        var look=(raw+previousCameraMouse)*.5f;previousCameraMouse=raw;
        if(controller)look=new Vector2(input.GUIActions.Camera.X,input.GUIActions.Camera.Y*movement.invertController);
        if(controller&&input.GUIActions.RightClick.IsPressed)sensitivity*=movement.controllerZoomSensitivity;
        if(!controller&&input.GUIActions.RightClick.IsPressed&&look.magnitude>0)
        {
            float acceleration=1+Mathf.Pow(look.magnitude*.4f,2.5f)/look.magnitude*CameraZoomAcceleration(movement);
            float scale=CameraZoomSensitivity(movement)*acceleration*Mathf.Lerp(.2f,1,(turretCamera.fieldOfView-10)/(Constants.cDefaultCameraFieldOfView-10f));
            sensitivity*=Mathf.Min(1,scale);
        }
        float mouseTiming=controller?Mathf.Min(1f/30f,Time.unscaledDeltaTime)*PlayerMoveController.lookDeltaTimeScale:
            PlayerMoveController.useScaledMouseLook?Mathf.Min(1f/30f,Time.unscaledDeltaTime)*PlayerMoveController.mouseDeltaTimeScale:1f;
        if(!pointer){cameraYaw+=look.x*sensitivity.x*mouseTiming;cameraPitch-=look.y*sensitivity.y*mouseTiming;}
        RebirthCamperTurretRuntime.For(cameraVehicle).ClampAim(cameraCorner,ref cameraYaw,ref cameraPitch);
        RebirthCamperTurretRuntime.For(cameraVehicle).PreviewManualAim(cameraCorner,cameraYaw,cameraPitch);
        turretCamera.backgroundColor=GameManager.Instance.World.m_WorldEnvironment.GetAmbientColor();
        if(Time.realtimeSinceStartup>=nextCameraInput)
        {nextCameraInput=Time.realtimeSinceStartup+.05f;cameraInput?.Invoke(cameraCorner,controlToken,cameraYaw,cameraPitch,fireReleased&&fire);}
        if(cameraCorner<0)return;
        var slot=state.Slots[cameraCorner];int ammo=slot.LoadedAmmo;
        if(input.GUIActions.MiddleClick.WasPressed){automaticZoom=!automaticZoom;nextZoomFocus=0;}
        selectedZoom=RebirthCamperTurretOptics.Clamp(slot,selectedZoom);
        if(!automaticZoom)
        {
            float wheel=input.GUIActions.scrollUp.WasPressed?1:input.GUIActions.scrollDown.WasPressed?-1:Input.GetAxis("Mouse ScrollWheel");
            if(Mathf.Abs(wheel)>.001f)selectedZoom=RebirthCamperTurretOptics.Step(slot,selectedZoom,wheel>0?1:-1);
        }
        else if(Time.realtimeSinceStartup>=nextZoomFocus)
        {
            nextZoomFocus=Time.realtimeSinceStartup+.2f;
            autoZoom=RebirthCamperTurretOptics.Auto(slot,RebirthCamperTurretRuntime.For(cameraVehicle).FocusDistance(cameraCorner),autoZoom);
        }
        float magnification=automaticZoom?RebirthCamperTurretOptics.Clamp(slot,autoZoom):selectedZoom;
        var available=RebirthCamperTurretOptics.Available(slot);
        foreach(int level in new[]{1,2,4,8})Visible("turretZoom"+level,false);
        Visible("turretZoomAuto",false);
        bool zoom=!pointer&&input.GUIActions.RightClick.IsPressed;
        // Magnification scales the projection, rather than dividing the angle itself.
        float targetFov=zoom?2*Mathf.Atan(Mathf.Tan(80*Mathf.Deg2Rad*.5f)/magnification)*Mathf.Rad2Deg:80;
        if(Mathf.Abs(cameraZoomTarget-targetFov)>.01f)
        {cameraZoomFrom=turretCamera.fieldOfView;cameraZoomTarget=targetFov;cameraZoomStarted=Time.realtimeSinceStartup;}
        // Installed ItemActionZoom uses a .15-second scope transition.
        turretCamera.fieldOfView=Mathf.Lerp(cameraZoomFrom,cameraZoomTarget,Mathf.SmoothStep(0,1,(Time.realtimeSinceStartup-cameraZoomStarted)/.15f));
        Text("turretZoomStatus",(automaticZoom?"AUTO · ":"")+magnification+"× · "+Localization.Get("xuiCamperTurretZoomHelp"));
        Text("cctvInfoBodyZoom",(automaticZoom?Localization.Get("xuiCamperCctvAutomatic"):Localization.Get("xuiCamperCctvSelected"))+" "+magnification+"×\n"+Localization.Get("xuiCamperCctvZoomHelp"));
        Text("cctvSelectedZoom",magnification+"×");
        Text("cctvInfoBodyFire",Localization.Get(RebirthCamperRelayWiring.TurretPowered(cameraVehicle,cameraCorner)?"xuiCamperCctvFireHelp":"xuiCamperWireNoPower"));
        Text("turretCameraStatus",Localization.Get(new[]{"xuiCamperTurretFrontLeft","xuiCamperTurretFrontRight","xuiCamperTurretRearLeft","xuiCamperTurretRearRight"}[cameraCorner])+"  |  "+(ammo<0?Localization.Get("xuiCamperTurretNoAmmoLoaded"):slot.Ammo[ammo].itemValue.ItemClass.GetLocalizedItemName()+" x"+slot.Ammo[ammo].count));
        Text("turretCameraStatus",((XUiV_Label)GetChildById("turretCameraStatus").ViewComponent).Text+"  |  "+RangeText(slot));
        if(!RebirthCamperRelayWiring.TurretPowered(cameraVehicle,cameraCorner))
            Text("turretCameraWarning",Localization.Get("xuiCamperWireNoPower"));
    }
    private void CameraCards(bool visible)
    {
        for(int i=0;i<4;i++)Visible("turretCard"+i,visible);Visible("instructions",visible);
        var bag=windowGroup?.Controller?.GetChildById("camperTurretBackpack")?.ViewComponent;if(bag!=null)bag.IsVisible=visible;
        var info=windowGroup?.Controller?.GetChildById("camperTurretItemInfo")?.ViewComponent;if(info!=null)info.IsVisible=visible;
    }
    private void StopCamera()
    {
        requestedCamera=-1;
        foreach(var binding in cameraBindings)binding.Item1.RemoveBinding(binding.Item2);cameraBindings.Clear();
        var token=controlToken;controlToken=Guid.Empty;pendingCamera=-1;
        rangeWarningUntil=0;Text("turretCameraWarning","");
        if(token!=Guid.Empty)cameraControl?.Invoke(cameraCorner,token,false);
        bool active=cameraCorner>=0||turretCamera;cameraCorner=-1;
        if(turretCamera){turretCamera.targetTexture=null;UnityEngine.Object.Destroy(turretCamera.gameObject);turretCamera=null;}
        if(cameraTexture){cameraTexture.Release();UnityEngine.Object.Destroy(cameraTexture);cameraTexture=null;}
        cctvPlayerView.Restore();
        if(GetChildById("turretCameraView")?.ViewComponent is XUiV_Texture view)view.Texture=null;
        Visible("turretCameraPanel",false); CameraCards(true);
        if(active){OcclusionManager.Instance.SetMultipleCameras(false);xui.playerUI.CursorController.Locked=false;xui.playerUI.CursorController.SetCursorHidden(false);}
    }
}

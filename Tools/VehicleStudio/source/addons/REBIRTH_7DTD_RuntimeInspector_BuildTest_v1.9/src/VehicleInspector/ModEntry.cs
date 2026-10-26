using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace RebirthVehicleInspector
{
    [Preserve]
    public sealed class ModEntry : IModApi
    {
        private static string modFolder;
        internal static VehicleInspectorService Service;
        public void InitMod(Mod modInstance)
        {
            modFolder=modInstance.Path;
            ModEvents.GameStartDone.RegisterHandler(GameStartDone);
            Log.Out("[VehicleInspector] Registered; in local game open F1 and type 'vinspect help'. Read-only inspection.");
        }
        private static void GameStartDone()
        {
            if(GameManager.IsDedicatedServer) return;
            try
            {
                if(Service==null) Service=new VehicleInspectorService(modFolder);
                else Service.ResetForWorldStart();
                if(VehicleInspectorBehaviour.Live==null)
                {
                    GameObject go=new GameObject("REBIRTH_VehicleInspector_ReadOnly");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.AddComponent<VehicleInspectorBehaviour>();
                }
                Log.Out("[VehicleInspector] Client collector ready (F1: vinspect). EAC must be off for code mods.");
            }
            catch(Exception ex) {Log.Out("[VehicleInspector] Bootstrap failed: "+ex);}
        }
    }
    internal sealed class VehicleInspectorBehaviour : MonoBehaviour
    {
        internal static VehicleInspectorBehaviour Live;
        private void Awake() { Live=this; }
        private void LateUpdate()
        {
            if(ModEntry.Service!=null) ModEntry.Service.LateTick();
        }
        private void OnDestroy() {if(Live==this) Live=null;}
    }
}

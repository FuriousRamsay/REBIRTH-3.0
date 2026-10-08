using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// Original candidate's process-local physical suspension; graphics coroutine hosts stay active.
internal static class RebirthNpcPreparedPhysicalHold
{
    private sealed class ColliderState{internal Collider Component;internal bool Enabled;}
    private sealed class BodyState{internal Rigidbody Component;internal bool Kinematic,Collisions;internal RebirthNpcNativeRigidbodyState Dynamics;}
    private sealed class Hold
    {
        internal World World;internal Transform Root;internal bool ActorEnabled,ControllerEnabled,Quarantined;
        internal CharacterControllerAbstract Controller;
        internal RebirthNpcStableId Stable;internal string Profile;internal int NativeId;internal uint Generation;
        internal readonly List<ColliderState> Colliders=new List<ColliderState>();
        internal readonly List<BodyState> Bodies=new List<BodyState>();
    }
    private static readonly ConditionalWeakTable<EntityRebirthHumanoidNPC,Hold> Holds=new ConditionalWeakTable<EntityRebirthHumanoidNPC,Hold>();
    internal static bool TryHold(EntityRebirthHumanoidNPC npc,RebirthNpcPersistentRecord person)
    {
        if(npc==null||person?.NativeReconstruction==null||!person.NativeReconstruction.HasNativeHeader||
            !person.NativeReconstruction.Matches(person)||!npc.IsPreparedRestorationPending||npc.RootTransform==null||
            npc.world==null||npc.world.IsRemote()||!ReferenceEquals(npc.world,GameManager.Instance?.World)||
            npc.world.GetEntity(npc.entityId)!=null||npc.RebirthRuntimeState==null||
            npc.RebirthRuntimeState.StableId!=person.Identity.StableNpcId||npc.RebirthRuntimeState.ProfileId!=person.Profile.ProfileId)return false;
        try
        {
            if(!Holds.TryGetValue(npc,out var hold))
            {
                if(!ControllerEnabled(npc.m_characterController,out bool enabled)){Quarantine(npc);return false;}
                hold=new Hold{World=npc.world,Root=npc.RootTransform,ActorEnabled=npc.enabled,
                    Controller=npc.m_characterController,ControllerEnabled=enabled,Stable=person.Identity.StableNpcId,
                    Profile=person.Profile.ProfileId,NativeId=npc.entityId,Generation=person.Presence.EmbodimentGeneration};
                Holds.Add(npc,hold);
            }
            if(hold.Quarantined||!Matches(npc,hold,person.Presence.EmbodimentGeneration)||!Discover(hold)){Quarantine(npc);return false;}
            Suspend(npc,hold);
            return IsPhysicallyHeld(npc,hold.Generation);
        }
        catch(Exception){TrySuspendAgain(npc);Quarantine(npc);return false;}
    }
    internal sealed class DerivedHandRetirement
    {
        internal EntityRebirthHumanoidNPC Npc;internal uint Generation;internal Transform Root;
        internal readonly List<Transform> Models=new List<Transform>();
        internal readonly List<Collider> Colliders=new List<Collider>();
        internal readonly List<Rigidbody> Bodies=new List<Rigidbody>();
    }
    internal static bool TryBeginDerivedHandRetirement(EntityRebirthHumanoidNPC npc,uint generation,
        IEnumerable<Transform> models,out DerivedHandRetirement ticket)
    {
        ticket=null;
        if(models==null||!IsPhysicallyHeld(npc,generation)||!Holds.TryGetValue(npc,out var hold))return false;
        try
        {
            var result=new DerivedHandRetirement{Npc=npc,Generation=generation,Root=hold.Root};
            foreach(var model in models)
            {
                if(model==null||result.Models.Contains(model))continue;
                if(result.Models.Count>=4098||ReferenceEquals(model,hold.Root)||!Owns(model,hold.Root))return false;
                result.Models.Add(model);
            }
            foreach(var item in hold.Colliders)if(result.Models.Exists(m=>Owns(item.Component.transform,m)))result.Colliders.Add(item.Component);
            foreach(var item in hold.Bodies)if(result.Models.Exists(m=>Owns(item.Component.transform,m)))result.Bodies.Add(item.Component);
            ticket=result;return true;
        }
        catch(Exception){return false;}
    }
    // Called even after partial native cleanup. Exact surviving original models remain held;
    // only the ticket's detached/inactive or destroyed participants can leave that hold.
    internal static bool TryFinishDerivedHandRetirement(EntityRebirthHumanoidNPC npc,DerivedHandRetirement ticket)
    {
        if(ticket==null||!ReferenceEquals(ticket.Npc,npc)||!IsOriginalBindingCurrent(npc,ticket.Generation)||
            !Holds.TryGetValue(npc,out var hold)||!ReferenceEquals(ticket.Root,hold.Root))return false;
        try
        {
            foreach(var component in ticket.Colliders)
            {
                if(component==null){hold.Colliders.RemoveAll(c=>ReferenceEquals(c.Component,component));continue;}
                if(Owns(component.transform,hold.Root))continue;
                if(!ticket.Models.Exists(m=>m!=null&&m.parent==null&&!m.gameObject.activeSelf&&Owns(component.transform,m)))return false;
                hold.Colliders.RemoveAll(c=>ReferenceEquals(c.Component,component));
            }
            foreach(var component in ticket.Bodies)
            {
                if(component==null){hold.Bodies.RemoveAll(c=>ReferenceEquals(c.Component,component));continue;}
                if(Owns(component.transform,hold.Root))continue;
                if(!ticket.Models.Exists(m=>m!=null&&m.parent==null&&!m.gameObject.activeSelf&&Owns(component.transform,m)))return false;
                hold.Bodies.RemoveAll(c=>ReferenceEquals(c.Component,component));
            }
            return IsPhysicallyHeld(npc,ticket.Generation);
        }
        catch(Exception){return false;}
    }
    internal static bool IsOriginalBindingCurrent(EntityRebirthHumanoidNPC npc,uint generation)
    {
        try{return npc!=null&&Holds.TryGetValue(npc,out var hold)&&!hold.Quarantined&&Matches(npc,hold,generation);}catch(Exception){return false;}
    }
    internal static bool IsPhysicallyHeld(EntityRebirthHumanoidNPC npc,uint generation)
    {
        try
        {
            if(npc==null||!Holds.TryGetValue(npc,out var hold)||hold.Quarantined||!Matches(npc,hold,generation)||npc.enabled||
                !ControllerEnabled(hold.Controller,out var enabled)||enabled)return false;
            foreach(var collider in hold.Colliders)if(collider.Component==null||!Owns(collider.Component.transform,hold.Root)||collider.Component.enabled)return false;
            foreach(var body in hold.Bodies)if(body.Component==null||!Owns(body.Component.transform,hold.Root)||!body.Component.isKinematic||body.Component.detectCollisions||body.Dynamics==null||!body.Dynamics.Matches(body.Component,false,false))return false;
            // New asynchronous model colliders are not silently covered by an old hold.
            return Discover(hold,false);
        }
        catch(Exception){return false;}
    }
    internal static bool TryRelease(EntityRebirthHumanoidNPC npc,uint generation)
    {
        if(npc==null||!npc.IsPreparedRestorationPending||!ReferenceEquals(npc.world?.GetEntity(npc.entityId),npc)||
            !Holds.TryGetValue(npc,out var hold)||!Matches(npc,hold,generation)||!IsPhysicallyHeld(npc,generation))return false;
        try
        {
            foreach(var body in hold.Bodies){body.Component.isKinematic=body.Kinematic;body.Component.detectCollisions=body.Collisions;if(body.Dynamics==null||!body.Dynamics.Apply(body.Component,true))throw new InvalidOperationException("Original Rigidbody dynamics release failed.");}
            foreach(var collider in hold.Colliders)collider.Component.enabled=collider.Enabled;
            hold.Controller?.Enable(hold.ControllerEnabled);npc.enabled=hold.ActorEnabled;
            if(!IsOriginalReleaseReady(npc,generation))throw new InvalidOperationException("Original NPC physical release readback failed.");
            return true;
        }
        catch(Exception){TrySuspendAgain(npc);Quarantine(npc);return false;}
    }
    internal static bool TryRestoreDesiredBodyPolicy(EntityRebirthHumanoidNPC npc,RebirthNpcPersistentRecord person,RebirthNpcNativeBodyPolicy policy)
    {
        if(policy==null||npc==null||person?.Presence==null||person.Identity==null||person.Profile==null||!IsPhysicallyHeld(npc,person.Presence.EmbodimentGeneration)||
            npc.world.GetEntity(npc.entityId)!=null||!Holds.TryGetValue(npc,out var hold)||!Matches(npc,hold,person.Presence.EmbodimentGeneration)||
            person.Identity.StableNpcId!=hold.Stable||person.Profile.ProfileId!=hold.Profile)return false;
        try
        {
            var image=policy.Write();var colliders=new List<Tuple<ColliderState,bool>>();var bodies=new List<Tuple<BodyState,bool,bool,RebirthNpcNativeRigidbodyState>>();var used=new HashSet<object>();
            foreach(var node in image.Elements())
            {
                var target=RebirthNpcNativeGeometryProjection.Resolve(hold.Root,(string)node.Attribute("path"));if(target==null)return false;
                int index=int.Parse((string)node.Attribute("index"),System.Globalization.CultureInfo.InvariantCulture);
                if(node.Name=="collider")
                {
                    var components=target.GetComponents<Collider>();if(index>=components.Length||!used.Add(components[index]))return false;
                    var saved=hold.Colliders.Find(c=>ReferenceEquals(c.Component,components[index]));if(saved==null)return false;
                    colliders.Add(Tuple.Create(saved,(string)node.Attribute("enabled")=="1"));
                }
                else
                {
                    var components=target.GetComponents<Rigidbody>();if(index>=components.Length||!used.Add(components[index]))return false;
                    var saved=hold.Bodies.Find(b=>ReferenceEquals(b.Component,components[index]));if(saved==null)return false;
                    if(!RebirthNpcNativeRigidbodyState.TryRead(node.Element("rigidDynamics"),out var dynamics)||!dynamics.CompatibleWithKinematic((string)node.Attribute("kinematic")=="1"))return false;
                    bodies.Add(Tuple.Create(saved,(string)node.Attribute("kinematic")=="1",(string)node.Attribute("collisions")=="1",dynamics));
                }
            }
            // Validate every original/qualified derived participant before replacing any desired flags.
            if(colliders.Count!=hold.Colliders.Count||bodies.Count!=hold.Bodies.Count)return false;
            hold.ActorEnabled=(string)image.Attribute("actor")=="1";hold.ControllerEnabled=(string)image.Attribute("controller")=="1";
            foreach(var item in colliders)item.Item1.Enabled=item.Item2;
            foreach(var item in bodies){item.Item1.Kinematic=item.Item2;item.Item1.Collisions=item.Item3;item.Item1.Dynamics=item.Item4;if(!item.Item4.Apply(item.Item1.Component,false))return false;}
            return IsPhysicallyHeld(npc,hold.Generation)&&TryCaptureDesiredBodyPolicy(npc,person,out var readback)&&System.Xml.Linq.XNode.DeepEquals(readback.Write(),image);
        }
        catch(Exception){return false;}
    }
    internal static bool TryCaptureDesiredBodyPolicy(EntityRebirthHumanoidNPC npc,RebirthNpcPersistentRecord person,out RebirthNpcNativeBodyPolicy policy)
    {
        policy=null;
        try
        {
            if(npc==null||person?.Identity==null||person.Profile==null||person.Presence==null||npc.RootTransform==null||npc.world==null||npc.world.IsRemote()||
                !ReferenceEquals(GameManager.Instance?.World,npc.world)||npc.RebirthRuntimeState==null||npc.RebirthRuntimeState.StableId!=person.Identity.StableNpcId||npc.RebirthRuntimeState.ProfileId!=person.Profile.ProfileId||person.Presence.EmbodimentGeneration==0)return false;
            Holds.TryGetValue(npc,out var hold);
            // Published checkpoints read current native motion, not obsolete restoration intent.
            if(!npc.IsPreparedRestorationPending){if(!ReferenceEquals(npc.world.GetEntity(npc.entityId),npc))return false;hold=null;}
            if(hold!=null&&(hold.Quarantined||!Matches(npc,hold,person.Presence.EmbodimentGeneration)||!Discover(hold,false)))return false;
            if(!ControllerOwned(npc.m_characterController,npc.RootTransform)||!ControllerEnabled(npc.m_characterController,out var controller))return false;
            var colliders=npc.RootTransform.GetComponentsInChildren<Collider>(true);var bodies=npc.RootTransform.GetComponentsInChildren<Rigidbody>(true);
            if(colliders.Length>1024||bodies.Length>1024)return false;
            var node=new System.Xml.Linq.XElement("nativeBodyPolicy",new System.Xml.Linq.XAttribute("version",2),new System.Xml.Linq.XAttribute("required",1),new System.Xml.Linq.XAttribute("actor",(hold?.ActorEnabled??npc.enabled)?1:0),new System.Xml.Linq.XAttribute("controller",(hold?.ControllerEnabled??controller)?1:0));
            foreach(var component in colliders)
            {
                var saved=hold?.Colliders.Find(c=>ReferenceEquals(c.Component,component));
                node.Add(new System.Xml.Linq.XElement("collider",new System.Xml.Linq.XAttribute("path",RebirthNpcNativeGeometryProjection.Path(npc.RootTransform,component.transform)),new System.Xml.Linq.XAttribute("index",Array.IndexOf(component.transform.GetComponents<Collider>(),component)),new System.Xml.Linq.XAttribute("enabled",(saved?.Enabled??component.enabled)?1:0)));
            }
            foreach(var component in bodies)
            {
                var saved=hold?.Bodies.Find(b=>ReferenceEquals(b.Component,component));
                var dynamics=saved?.Dynamics;if(dynamics==null&&!RebirthNpcNativeRigidbodyState.TryCapture(component,out dynamics))return false;
                node.Add(new System.Xml.Linq.XElement("body",new System.Xml.Linq.XAttribute("path",RebirthNpcNativeGeometryProjection.Path(npc.RootTransform,component.transform)),new System.Xml.Linq.XAttribute("index",Array.IndexOf(component.transform.GetComponents<Rigidbody>(),component)),new System.Xml.Linq.XAttribute("kinematic",(saved?.Kinematic??component.isKinematic)?1:0),new System.Xml.Linq.XAttribute("collisions",(saved?.Collisions??component.detectCollisions)?1:0),dynamics.Write()));
            }
            return RebirthNpcNativeBodyPolicy.TryRead(node,out policy);
        }
        catch(Exception){return false;}
    }
    internal static bool IsOriginalReleaseReady(EntityRebirthHumanoidNPC npc,uint generation)
    {
        try
        {
            if(npc==null||npc.hasAI||!ReferenceEquals(npc.world?.GetEntity(npc.entityId),npc)||
                !Holds.TryGetValue(npc,out var hold)||hold.Quarantined||!Matches(npc,hold,generation)||!Discover(hold,false)||
                npc.enabled!=hold.ActorEnabled||!ControllerEnabled(hold.Controller,out var enabled)||enabled!=hold.ControllerEnabled)return false;
            foreach(var collider in hold.Colliders)if(collider.Component==null||!Owns(collider.Component.transform,hold.Root)||collider.Component.enabled!=collider.Enabled)return false;
            foreach(var body in hold.Bodies)if(body.Component==null||!Owns(body.Component.transform,hold.Root)||body.Component.isKinematic!=body.Kinematic||body.Component.detectCollisions!=body.Collisions||body.Dynamics==null||!body.Dynamics.Matches(body.Component,true,false))return false;
            return true;
        }
        catch(Exception){return false;}
    }
    internal static void TrySuspendAgain(EntityRebirthHumanoidNPC npc)
    {
        if(npc==null)return;npc.hasAI=false;
        if(!Holds.TryGetValue(npc,out var hold))return;
        try{npc.enabled=false;}catch(Exception){}
        try{if(ControllerOwned(hold.Controller,hold.Root))hold.Controller?.Enable(false);}catch(Exception){}
        foreach(var collider in hold.Colliders)try{if(collider.Component!=null&&Owns(collider.Component.transform,hold.Root))collider.Component.enabled=false;}catch(Exception){}
        foreach(var body in hold.Bodies)try{if(body.Component!=null&&Owns(body.Component.transform,hold.Root)){body.Component.detectCollisions=false;body.Component.isKinematic=true;}}catch(Exception){}
    }
    private static void Quarantine(EntityRebirthHumanoidNPC npc)
    {npc.hasAI=false;Transform root=npc.RootTransform;if(Holds.TryGetValue(npc,out var hold)){hold.Quarantined=true;root=hold.Root;}try{npc.enabled=false;if(Owns(npc.transform,root))root.gameObject.SetActive(false);}catch(Exception){}}
    private static void Suspend(EntityRebirthHumanoidNPC npc,Hold hold)
    {
        npc.enabled=false;npc.hasAI=false;hold.Controller?.Enable(false);
        foreach(var collider in hold.Colliders){if(!Owns(collider.Component.transform,hold.Root))throw new InvalidOperationException("Original collider binding moved.");collider.Component.enabled=false;}
        foreach(var body in hold.Bodies){if(!Owns(body.Component.transform,hold.Root))throw new InvalidOperationException("Original rigidbody binding moved.");body.Component.detectCollisions=false;body.Component.isKinematic=true;}
    }
    private static bool Discover(Hold hold,bool append=true)
    {
        var colliders=hold.Root.GetComponentsInChildren<Collider>(true);var bodies=hold.Root.GetComponentsInChildren<Rigidbody>(true);
        if(colliders.Length>1024||bodies.Length>1024||!append&&(colliders.Length!=hold.Colliders.Count||bodies.Length!=hold.Bodies.Count))return false;
        foreach(var component in colliders)
        {
            if(component==null)return false;
            if(hold.Colliders.Exists(c=>ReferenceEquals(c.Component,component)))continue;
            if(!append)return false;
            hold.Colliders.Add(new ColliderState{Component=component,Enabled=component.enabled});
        }
        foreach(var component in bodies)
        {
            if(component==null)return false;
            if(hold.Bodies.Exists(b=>ReferenceEquals(b.Component,component)))continue;
            if(!append)return false;
            if(!RebirthNpcNativeRigidbodyState.TryCapture(component,out var dynamics)||!dynamics.CompatibleWithKinematic(component.isKinematic))return false;
            hold.Bodies.Add(new BodyState{Component=component,Kinematic=component.isKinematic,Collisions=component.detectCollisions,Dynamics=dynamics});
        }
        return hold.Colliders.Count<=1024&&hold.Bodies.Count<=1024;
    }
    private static bool ControllerOwned(CharacterControllerAbstract controller,Transform root)
    {
        if(controller==null)return true;
        if(controller.GetType()==typeof(CharacterControllerUnity))return ((CharacterControllerUnity)controller).cc!=null&&Owns(((CharacterControllerUnity)controller).cc.transform,root);
        if(controller.GetType()==typeof(CharacterControllerKinematic))return ((CharacterControllerKinematic)controller).motor!=null&&Owns(((CharacterControllerKinematic)controller).motor.transform,root);
        return false;
    }
    private static bool Owns(Transform component,Transform root)=>component!=null&&root!=null&&(ReferenceEquals(component,root)||component.IsChildOf(root));
    private static bool ControllerEnabled(CharacterControllerAbstract controller,out bool enabled)
    {
        enabled=false;if(controller==null)return true;
        if(controller.GetType()==typeof(CharacterControllerUnity)){var native=(CharacterControllerUnity)controller;if(native.cc==null)return false;enabled=native.cc.enabled;return true;}
        if(controller.GetType()==typeof(CharacterControllerKinematic)){var native=(CharacterControllerKinematic)controller;if(native.motor==null)return false;enabled=native.motor.enabled;return true;}
        return false;
    }
    private static bool Matches(EntityRebirthHumanoidNPC npc,Hold hold,uint generation)=>generation>0&&hold.Generation==generation&&
        ReferenceEquals(hold.World,npc.world)&&ReferenceEquals(GameManager.Instance?.World,hold.World)&&
        ReferenceEquals(hold.Root,npc.RootTransform)&&hold.Root!=null&&hold.NativeId==npc.entityId&&
        ReferenceEquals(hold.Controller,npc.m_characterController)&&ControllerOwned(hold.Controller,hold.Root)&&npc.RebirthRuntimeState!=null&&
        npc.RebirthRuntimeState.StableId==hold.Stable&&npc.RebirthRuntimeState.ProfileId==hold.Profile;
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcDialogueService
{
    private static World dialogueWorld;
    private static bool DialogueScope(bool bind=true)
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(dialogueWorld!=null&&!ReferenceEquals(dialogueWorld,world))return false;if(dialogueWorld==null){if(Conversations.Count!=0||ConversationOrder.Count!=0)return false;if(bind)dialogueWorld=world;}return true;}
    }
    internal static bool CanRetireEmptyDialogue{get{lock(Sync)return ThreadManager.IsMainThread()&&Conversations.Count==0&&ConversationOrder.Count==0;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync)foreach(var value in Conversations.Values){if(value==null)throw new InvalidOperationException("Conversation original missing");RebirthNpcSaveQuarantineIds.Add(ids,value.NpcId);}}
    internal static RebirthNpcSaveQuarantineDomain PrepareDialogueQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(dialogueWorld!=null&&!ReferenceEquals(dialogueWorld,scope.World))||(dialogueWorld==null&&!CanRetireEmptyDialogue))throw new InvalidOperationException("Original dialogue binding unavailable");
            var records=Conversations;var order=ConversationOrder;var world=dialogueWorld;var next=new Dictionary<Guid,RebirthNpcConversationState>();var nextOrder=new RebirthNpcFairRoundRobin<Guid>();var doc=DialogueImage();
            return new RebirthNpcSaveQuarantineDomain("dialogue-conversations",doc,
                ()=>{lock(Sync)return ReferenceEquals(Conversations,records)&&ReferenceEquals(ConversationOrder,order)&&ReferenceEquals(dialogueWorld,world)&&DialogueImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Conversations=next;ConversationOrder=nextOrder;dialogueWorld=null;}},
                ()=>{lock(Sync){Conversations=records;ConversationOrder=order;dialogueWorld=world;}});
        }
    }
    private static XmlDocument DialogueImage()
    {
        if(Conversations.Count>MaxConversations||ConversationOrder.Count!=Conversations.Count)throw new InvalidOperationException("Conversation retention bound/index unavailable");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("conversations");doc.AppendChild(root);long characters=0;
        var keys=new List<Guid>(Conversations.Keys);keys.Sort();foreach(var key in keys)
        {
            var state=Conversations[key];if(state==null||state.ConversationId!=key||state.NpcId.IsEmpty||state.Turns==null||state.Turns.Count>MaxTurns)throw new InvalidOperationException("Conversation original unavailable");var row=doc.CreateElement("conversation");root.AppendChild(row);row.SetAttribute("id",key.ToString("N"));row.SetAttribute("npc",state.NpcId.ToString());row.SetAttribute("generation",state.RecoveryGeneration.ToString(CultureInfo.InvariantCulture));row.SetAttribute("revision",state.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("expires",state.ExpiresUtcTicks.ToString(CultureInfo.InvariantCulture));Text(row,"session",state.SessionKey,ref characters);Text(row,"actor",state.ActorId,ref characters);Text(row,"profile",state.ProfileId,ref characters);
            foreach(var turn in state.Turns){if(turn==null)throw new InvalidOperationException("Conversation turn missing");var t=doc.CreateElement("turn");row.AppendChild(t);t.SetAttribute("index",turn.Index.ToString(CultureInfo.InvariantCulture));t.SetAttribute("created",turn.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture));Text(t,"topic",turn.TopicId,ref characters);Text(t,"text",turn.Text,ref characters);}
        }
        var seen=new HashSet<Guid>();foreach(var key in ConversationOrder.CopyOrder()){if(!seen.Add(key)||!Conversations.ContainsKey(key))throw new InvalidOperationException("Conversation traversal index mismatch");var row=doc.CreateElement("order");root.AppendChild(row);row.SetAttribute("id",key.ToString("N"));}return doc;
    }
    private static void Text(XmlElement row,string name,string value,ref long characters)
    {characters+=value?.Length??0;if((value?.Length??0)>65536||characters>4L*1024*1024)throw new InvalidOperationException("Conversation text retention bound exceeded");row.SetAttribute(name+"Null",value==null?"1":"0");row.SetAttribute(name,value??string.Empty);}
}

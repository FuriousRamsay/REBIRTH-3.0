using System;

// One owner/session display cache. It never authorizes inventory mutations.
public sealed class RebirthBackpackSellStashViewCache
{
    private readonly object session;
    private readonly string creation;
    private Guid request;
    private long revision=-1;
    private RebirthBackpackSellStashView view;
    private bool noBackpack;
    public RebirthBackpackSellStashViewCache(object currentSession,Guid creationId) : this(currentSession,creationId.ToString("N")) {}
    public RebirthBackpackSellStashViewCache(object currentSession,string creationId)
    {
        if(currentSession==null||!RebirthSurvivorRequestScope.TryNormalize(creationId,out var normalized))throw new ArgumentException("Sell Stash view requires an owner session.");
        session=currentSession;creation=normalized;
    }
    public bool BeginRequest(object currentSession,Guid currentCreation,Guid requestId)
        =>BeginRequest(currentSession,currentCreation.ToString("N"),requestId);
    public bool BeginRequest(object currentSession,string currentCreation,Guid requestId)
    {
        if(!Current(currentSession,currentCreation)||requestId==Guid.Empty)return false;
        request=requestId;return true;
    }
    public bool Receive(object currentSession,Guid currentCreation,Guid requestId,RebirthBackpackSellStashView incoming)
        =>Receive(currentSession,currentCreation.ToString("N"),requestId,incoming);
    public bool Receive(object currentSession,string currentCreation,Guid requestId,RebirthBackpackSellStashView incoming)
    {
        if(!Current(currentSession,currentCreation)||request==Guid.Empty||requestId!=request||incoming==null||
            incoming.CreationId!=creation||incoming.GearRevision<revision)return false;
        noBackpack=false;view=incoming;revision=incoming.GearRevision;request=Guid.Empty;return true;
    }
    public bool ReceiveNoBackpack(object currentSession,Guid currentCreation,Guid requestId,long gearRevision)
        =>ReceiveNoBackpack(currentSession,currentCreation.ToString("N"),requestId,gearRevision);
    public bool ReceiveNoBackpack(object currentSession,string currentCreation,Guid requestId,long gearRevision)
    {
        if(!Current(currentSession,currentCreation)||request==Guid.Empty||requestId!=request||gearRevision<0||gearRevision<revision)return false;
        noBackpack=true;view=null;revision=gearRevision;request=Guid.Empty;return true;
    }
    public bool IsNoBackpack(object currentSession,Guid currentCreation)=>IsNoBackpack(currentSession,currentCreation.ToString("N"));
    public bool IsNoBackpack(object currentSession,string currentCreation)=>Current(currentSession,currentCreation)&&noBackpack;
    public bool TryGet(object currentSession,Guid currentCreation,out RebirthBackpackSellStashView current)
        =>TryGet(currentSession,currentCreation.ToString("N"),out current);
    public bool TryGet(object currentSession,string currentCreation,out RebirthBackpackSellStashView current)
    {current=null;if(!Current(currentSession,currentCreation)||view==null)return false;current=view;return true;}
    private bool Current(object currentSession,string currentCreation)
        =>ReferenceEquals(session,currentSession)&&RebirthSurvivorRequestScope.Matches(creation,currentCreation);
    public void Reset(){noBackpack=false;request=Guid.Empty;view=null;revision=-1;}
}
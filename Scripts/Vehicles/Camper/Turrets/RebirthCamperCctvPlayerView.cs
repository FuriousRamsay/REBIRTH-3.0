// Native SDCS first-person rigs omit the head/hair; a culling-mask change cannot
// restore missing meshes. Generate the native complete view once per CCTV session.
internal sealed class RebirthCamperCctvPlayerView
{
    private EntityPlayerLocal player;
    private bool priorFirstPerson;
    internal void Begin(EntityPlayerLocal local)
    {
        Restore();
        if(local==null)return;
        player=local;priorFirstPerson=local.bFirstPersonView;
        if(priorFirstPerson)local.SetFirstPersonView(false,false);
    }
    internal void Restore()
    {
        var prior=player;bool first=priorFirstPerson;player=null;
        if(prior!=null&&prior.bFirstPersonView!=first)prior.SetFirstPersonView(first,false);
    }
}

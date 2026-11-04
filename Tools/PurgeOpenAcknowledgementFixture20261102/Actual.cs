partial class Harness {void Submit(){float now=42;var request=Head;var ui=new Ui();
        float previousRequest=request.RequestedAt;
        int previousAttempts=request.Attempts;
        request.RequestedAt=now;request.Attempts++;
        if(XUiC_RebirthPurgeInformation.Open(ui.xui,request.Entry,request.Sprite,request.Manual))
        {lastBlockedReason=null;nextBlockedTrace=0;}
        else
        {
            request.RequestedAt=previousRequest;request.Attempts=previousAttempts;
            TraceBlocked(request,"window-controller-unavailable",now);
        }

}}
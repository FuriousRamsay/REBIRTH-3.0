class FoundationGateHarness:NativeGateCallbacks { private bool rebirthPreparedRestorationPending; public void Prepare(bool held){rebirthPreparedRestorationPending=held;}    public override void OnUpdateEntity()
    {
        if(rebirthPreparedRestorationPending){hasAI=false;return;}
        base.OnUpdateEntity();
    }
    public override void OnUpdateLive()
    {
        if(rebirthPreparedRestorationPending){hasAI=false;return;}
        base.OnUpdateLive();
    }
}
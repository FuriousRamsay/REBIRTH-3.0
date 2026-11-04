using System;
partial class Tuner {
    private void CommitText(int slot)
    {
        if(refreshing||editEpoch[slot]!=epoch||editing[slot]==null||
            !ReferenceEquals(editing[slot],displayed[slot])||!ReferenceEquals(session,RebirthVehicleTuningUi.Session))return;
        string text=inputs[slot].Text;
        if(string.Equals(text,editText[slot],StringComparison.Ordinal))return;
        NeedSession();editing[slot].ApplyText(text);editText[slot]=text;
        status=Localization.Get(editing[slot].Proposal?"xuiRbVehicleTune11":"xuiRbVehicleTune12");
    }
    private void CommitEdits()
    {
        if(session==null||!session.Valid||!ReferenceEquals(session,RebirthVehicleTuningUi.Session))return;
        for(int slot=0;slot<4;slot++)CommitText(slot);
        if(heldSlider>=0&&ReferenceEquals(heldControl,displayed[heldSlider]))
        {
            // Commit the last drag sample; the pointer may already be over Drive/Close.
            heldControl.Apply(heldNumber);ReleaseSlider();
        }
    }
}
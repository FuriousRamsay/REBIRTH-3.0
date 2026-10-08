class RebirthGearPreparationRefusalFlow{public static bool Handled,Throw;public static int Calls;public static bool TryAdvance(EntityPlayerLocal p,object session){Calls++;if(Throw)throw new System.Exception("flow");return Handled;}}

class RefusalHint{public string CreationId;public long ObservedRevision;}
static class RebirthGearPreparationRefusalClient{public static RefusalHint Value;public static bool Current=true;public static bool TryGetCurrent(EntityPlayerLocal p,object session,out RefusalHint h){h=Value;return Current&&h!=null;}}

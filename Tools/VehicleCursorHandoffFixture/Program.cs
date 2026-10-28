using System.Reflection;
static class Program
{
    static int checks;
    static void A(bool v,string label){if(!v)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static ItemStack S(int count,int type=1,bool quality=false)=>new ItemStack(new ItemValue{type=type,Metadata="full-image",HasQuality=quality},count);
    static void Plan()
    {
        var target=new XUiC_ItemStack{ItemStack=S(8),StackLocation=XUiC_ItemStack.StackLocationTypes.Backpack};
        var cursor=S(5);
        A(RebirthVehicleCursorDropPlan.TryPlan(target,cursor,false,out var after,out var returned),"merge admitted");
        A(after.count==10&&returned.count==3,"native overflow returns remainder");
        A(target.ItemStack.count==8&&cursor.count==5,"planning leaves sources unchanged");
        target.ItemStack=S(10);A(RebirthVehicleCursorDropPlan.TryPlan(target,cursor,true,out after,out returned)&&after.count==10&&returned.count==5,"drop one at capacity no debit");
        target.ItemStack=ItemStack.Empty;A(RebirthVehicleCursorDropPlan.TryPlan(target,S(1),true,out after,out returned)&&after.count==1&&returned.IsEmpty(),"final drop one empty cursor");
        target.ItemStack=S(1,2,true);cursor=S(1,1,true);
        A(RebirthVehicleCursorDropPlan.TryPlan(target,cursor,false,out after,out returned)&&after.itemValue.type==1&&returned.itemValue.type==2,"quality swap returns previous item");
        target.SlotNumber=-99;A(!RebirthVehicleCursorDropPlan.TryPlan(target,cursor,false,out _,out _),"native slot admission refusal");
        target.SlotNumber=0;target.AllowDropping=false;A(!RebirthVehicleCursorDropPlan.TryPlan(target,cursor,true,out _,out _),"disabled target refused");
        target.AllowDropping=true;target.ItemStack=ItemStack.Empty;cursor=S(14);
        A(RebirthVehicleCursorDropPlan.TryPlan(target,cursor,false,out after,out returned)&&after.count==10&&returned.count==4,"empty slot split capacity");
    }
    static byte[] Bytes(Equipment e){using var s=new MemoryStream();using var w=new PooledBinaryWriter();w.SetBaseStream(s);e.Write(w,StreamModeWrite.Persistency);w.Flush();return s.ToArray();}
    static (World,EntityPlayerLocal,XUiC_EquipmentStack) Setup()
    {
        var world=new World();var owner=new EntityPlayerLocal{world=world};world.Player=owner;GameManager.Instance.World=world;
        owner.equipment.Slots[0]=S(1,2);var target=new XUiC_EquipmentStack{EquipSlot=EquipmentSlots.Head,ItemStack=S(1,2),xui=new XUi()};
        target.xui.playerUI.entityPlayer=owner;target.xui.DragAndDropWindow.CurrentStack=S(1,3);
        return(world,owner,target);
    }
    static void Gear()
    {
        var(world,owner,target)=Setup();var incoming=S(1,3);
        A(RebirthVehicleCursorGearWitness.TryCapture(target,incoming,out var witness),"native slot gear capture");
        A(owner.equipment.Slots[0].itemValue.type==2&&incoming.itemValue.type==3,"capture no mutation");
        A(!witness.MatchesLive(owner),"before image not proof");
        owner.equipment.Slots[0]=incoming.Clone();A(witness.MatchesLive(owner),"actual after image observed");
        var op=Guid.NewGuid();RebirthVehicleClientCustody.Operation=op;RebirthGearNativePlayerFile.Saved=new PlayerDataFile{equipmentData=new MemoryStream(Bytes(owner.equipment))};
        A(!witness.RequestAndVerifySaved(owner,op),"saved afterimage without receipt refused");
        RebirthVehicleClientCustody.Receipt=true;A(!witness.RequestAndVerifySaved(owner,Guid.NewGuid()),"wrong receipt operation refused");
        A(witness.RequestAndVerifySaved(owner,op),"same-file receipt exact gear accepted");
        var original=RebirthGearNativePlayerFile.Saved.equipmentData;RebirthGearNativePlayerFile.Saved.equipmentData=new MemoryStream(new byte[]{6});
        A(!witness.RequestAndVerifySaved(owner,op),"saved equipment mismatch");RebirthGearNativePlayerFile.Saved.equipmentData=original;
        var payload=witness.Encode();A(RebirthVehicleCursorGearWitness.TryRead(payload,out var decoded)&&decoded.MatchesLive(owner),"scoped serialized roundtrip");
        RebirthSurvivorClientState.Creation="22222222222222222222222222222222";
        A(!witness.MatchesLive(owner)&&!RebirthVehicleCursorGearWitness.TryRead(payload,out _),"changed creation refuses live and recovery");
        RebirthSurvivorClientState.Creation="11111111111111111111111111111111";
        RebirthCharacterCreationHoldService.Held=true;A(!witness.MatchesLive(owner),"creation hold refuses");RebirthCharacterCreationHoldService.Held=false;
        GameManager.Instance.World=new World();A(!witness.MatchesLive(owner),"changed current world refuses");GameManager.Instance.World=world;
        world.Player=new EntityPlayerLocal{world=world};A(!witness.MatchesLive(owner),"same ID replacement owner refuses");world.Player=owner;
        var equipment=owner.equipment;owner.equipment=new Equipment();A(!witness.MatchesLive(owner),"equipment reference replacement refuses");owner.equipment=equipment;
        target.EquipSlot=EquipmentSlots.Count;A(!RebirthVehicleCursorGearWitness.TryCapture(target,incoming,out _),"invalid gear slot refused");
    }
    static bool Prefix(XUiC_EquipmentStack target)
    {var args=new object[]{target,null};return(bool)typeof(RebirthVehicleCursorGearHooks).GetMethod("Before",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);}
    static void Hooks()
    {
        var(_,owner,target)=Setup();int writes=Equipment.Writes;
        RebirthVehicleClientCustody.Live=false;A(Prefix(target)&&Equipment.Writes==writes&&RebirthVehicleClientCustody.Admissions==0,"ordinary native bypass no serialization");
        RebirthVehicleClientCustody.Pending=true;A(!Prefix(target)&&Equipment.Writes==writes,"awaiting ACK cursor held");RebirthVehicleClientCustody.Pending=false;
        RebirthVehicleClientCustody.Live=true;A(Prefix(target)&&RebirthVehicleClientCustody.Admissions==1,"active escrow intent observed");
        A(owner.equipment.Slots[0].itemValue.type==2&&target.xui.DragAndDropWindow.CurrentStack.itemValue.type==3,"hook no native movement");
    }
    static void Main(){Plan();Gear();Hooks();Console.WriteLine("PASS "+checks+" SIMULATION ONLY: actual planner/gear witness/hooks linked; native codec, gear serialization, custody receipt, saved file and Harmony invocation are doubles. No crash/network/native codec proof.");}
}

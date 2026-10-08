using System;using System.Linq;using System.Xml.Linq;
static class NativeHandFixture
{
    internal static int Run()
    {
        int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};
        var frame=Program.HeaderMigrationRoot();var npc=new EntityRebirthHumanoidNPC();var slots=frame.Element("slots");
        check(RebirthNpcNativeHand.TryCapture(npc,slots,out var hand),"actual original hand snapshot capture");
        check(hand.Selected==0&&hand.Mode==0&&hand.Stack==(string)slots.Element("slot").Attribute("stack"),"original selected custody binding");
        foreach(var attr in hand.Write().Attributes()){var mutation=hand.Write();mutation.Attribute(attr.Name).Remove();check(!RebirthNpcNativeHand.TryRead(mutation,out _),"mandatory native hand "+attr.Name);}
        foreach(var pair in new[]{("version","2"),("required","0"),("mode","2"),("selected","01"),("selected","4096"),("selected","-1"),("stack","00000000000000000000000000000000"),("barePayload","%%%"),("bareHash",new string('0',64))}){var mutation=hand.Write();mutation.SetAttributeValue(pair.Item1,pair.Item2);check(!RebirthNpcNativeHand.TryRead(mutation,out _),"malformed native hand "+pair.Item1);}
        var copy=hand.Write();copy.SetAttributeValue("selected",3);check(hand.Selected==0,"hand descriptor immutable");
        var badPayload=hand.Write();badPayload.SetAttributeValue("barePayload",new string('A',262145));check(!RebirthNpcNativeHand.TryRead(badPayload,out _),"bare payload bound before allocation");
        RebirthNpcNativeHeaderProjection.TryCapture(npc,out var header);RebirthNpcNativeGeometryProjection.TryCapture(npc,out var geometry);
        frame.SetAttributeValue("version",3);frame.Add(header.Write(),geometry.Write());check(RebirthNpcNativeReconstruction.TryRead(frame,out var old)&&!old.HasNativeHand,"schema3 preserves missing hand unresolved");
        frame.SetAttributeValue("version",4);check(!RebirthNpcNativeReconstruction.TryRead(frame,out _),"schema4 requires original native hand");frame.Add(hand.Write());check(RebirthNpcNativeReconstruction.TryRead(frame,out var modern)&&modern.HasNativeHand&&XNode.DeepEquals(modern.Write(),frame),"schema4 exact native hand composition");
        var bad=modern.Write();bad.SetAttributeValue("version",3);check(!RebirthNpcNativeReconstruction.TryRead(bad,out _),"older schema refuses required hand adjacency");bad=modern.Write();bad.Add(hand.Write());check(!RebirthNpcNativeReconstruction.TryRead(bad,out _),"duplicate native hand refused");bad=modern.Write();bad.Element("nativeHand").SetAttributeValue("mode",1);check(!RebirthNpcNativeReconstruction.TryRead(bad,out _),"hand/header mode disagreement");bad=modern.Write();bad.Element("nativeHand").SetAttributeValue("stack",Guid.NewGuid().ToString("N"));check(!RebirthNpcNativeReconstruction.TryRead(bad,out _),"selected original custody substitution refused");
        string nativePayload=(string)NativePreflightFixture.Valid().Element("slots").Element("slot").Attribute("payload");
        check(RebirthNpcNativeReconstructionPreflight.TryValidateItemPayload(nativePayload,100000,id=>id==42?(bool?)false:null),"actual bounded native bare-item preflight");
        check(!RebirthNpcNativeReconstructionPreflight.TryValidateItemPayload(nativePayload,100000,id=>null),"unresolved native bare-item class refused");
        check(!RebirthNpcNativeReconstructionPreflight.TryValidateItemPayload(nativePayload+"AA==",100000,id=>false),"trailing or noncanonical item bytes refused");
        npc.Hand.mode=Hand.HoldingMode.Transient;check(!RebirthNpcNativeHand.TryCapture(npc,slots,out _),"transient original hand snapshot refused");
        return count;
    }
}

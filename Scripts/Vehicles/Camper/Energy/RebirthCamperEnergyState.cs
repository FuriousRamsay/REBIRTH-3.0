using System;
using System.Collections.Generic;
using System.IO;

// Separate metadata section, carried by the native vehicle ItemValue save/sync.
// Never writes vehicles.dat. Invalid state is retained and refused, not reset.
internal sealed class RebirthCamperEnergyState
{
    internal const string Key="RebirthCamperEnergy";
    internal const string PortableKey="RebirthCamperEnergySource";
    internal const int Limit=524288;
    internal Guid Identity;
    internal int Version=2;
    internal long Revision;
    internal RebirthCamperEnergyMode Mode;
    internal float Remaining;
    internal double ConsumptionRemainder;
    internal readonly List<Source> Sources=new List<Source>();
    internal sealed class Source
    {
        internal Guid Id;
        internal int Position;
        internal ItemStack Installed;
        internal PowerSource Bank;
        internal double SolarRemainder;
    }
    internal static bool TryCreateBank(ItemStack item,out PowerSource bank)
    {
        bank=null;
        if(item==null||item.IsEmpty()||item.count!=1||item.itemValue?.ItemClass==null)return false;
        var value=Block.GetBlockValue(item.itemValue.ItemClass.GetItemName());
        var block=value.Block as BlockPowerSource;
        if(block==null)return false;
        switch(block.Properties.GetString("Class"))
        {
            case "Generator":bank=new PowerGenerator();break;
            case "BatteryBank":bank=new PowerBatteryBank();break;
            case "SolarPanel":bank=new PowerSolarPanel();break;
            default:return false;
        }
        bank.BlockID=(ushort)value.type;
        bank.SetValuesFromBlock();
        if(block.OutputPerStack<0||block.OutputPerStack>ushort.MaxValue)return false;
        bank.OutputPerStack=(ushort)block.OutputPerStack;
        bank.SetSlots(ItemStack.CreateArray(6));
        // Solar's native RefreshPowerStats derives zero storage/output for an
        // empty bank. Installation precedes cell loading, so that is legitimate.
        return (!(bank is PowerBatteryBank battery)||battery.OutputPerCharge>0)
            &&(!(bank is PowerGenerator generator)||generator.OutputPerFuel>0&&generator.OutputPerFuel<=generator.MaxPower);
    }
    internal static RebirthCamperEnergySourceKind Kind(PowerSource bank)
        =>bank is PowerGenerator?RebirthCamperEnergySourceKind.Generator:bank is PowerBatteryBank?RebirthCamperEnergySourceKind.Battery:RebirthCamperEnergySourceKind.Solar;
    internal static byte[] Encode(RebirthCamperEnergyState state)
    {
        using(var stream=new MemoryStream())
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            if(state.Version!=1&&state.Version!=2)throw new InvalidDataException("Unsupported energy version.");
            writer.SetBaseStream(stream);writer.Write(state.Version==1?0x31454352:0x32454352);writer.Write(state.Identity.ToByteArray());writer.Write(state.Revision);
            writer.Write((byte)state.Mode);writer.Write(state.Remaining);writer.Write(state.ConsumptionRemainder);writer.Write((byte)state.Sources.Count);
            foreach(var source in state.Sources)
            {
                writer.Write(source.Id.ToByteArray());if(state.Version>=2)writer.Write((byte)source.Position);WriteStacks(writer,new[]{source.Installed});
                WriteStacks(writer,source.Bank.Stacks);writer.Write(source.Bank.CurrentPower);
                // Do not invoke IsOn's native sound/graph callback on detached banks.
                writer.Write(source.Bank.isOn);writer.Write(source.Bank is PowerGenerator generator?generator.CurrentFuel:(ushort)0);
                writer.Write(source.Bank is PowerSolarPanel solar&&solar.HasLight);
                writer.Write(source.SolarRemainder);
            }
            writer.Flush();if(stream.Length>Limit)throw new InvalidOperationException("Energy metadata is too large; retain source custody.");
            return stream.ToArray();
        }
    }
    internal static RebirthCamperEnergyState Decode(byte[] bytes,Guid expected)
    {
        if(bytes==null||bytes.Length>Limit)throw new InvalidDataException("Bounded energy frame required.");
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);
            int magic=reader.ReadInt32();int version=magic==0x31454352?1:magic==0x32454352?2:0;
            if(version==0)throw new InvalidDataException("Unsupported energy version.");
            var state=new RebirthCamperEnergyState {Version=version,Identity=new Guid(reader.ReadBytes(16)),Revision=reader.ReadInt64(),
                Mode=(RebirthCamperEnergyMode)reader.ReadByte(),Remaining=reader.ReadSingle(),ConsumptionRemainder=reader.ReadDouble()};
            if(state.Identity!=expected||expected==Guid.Empty||state.Revision<0||(int)state.Mode>3
                ||float.IsNaN(state.Remaining)||float.IsInfinity(state.Remaining)||state.Remaining<0||state.Remaining>3600)
                throw new InvalidDataException("Energy identity/mode invalid.");
            if(!Fraction(state.ConsumptionRemainder))throw new InvalidDataException("Native demand carry invalid.");
            int count=reader.ReadByte();if(count>12)throw new InvalidDataException("Too many installed sources.");
            var ids=new HashSet<Guid>();var positions=new HashSet<int>();
            for(int i=0;i<count;i++)
            {
                Guid id=new Guid(reader.ReadBytes(16));int position=version>=2?reader.ReadByte():i;
                if(position<0||position>=12||!positions.Add(position))throw new InvalidDataException("Energy source position invalid.");
                var installed=ReadStacks(reader,1);
                var components=ReadStacks(reader,6);
                if(id==Guid.Empty||!ids.Add(id)||installed.Length!=1||components.Length!=6||!TryCreateBank(installed[0],out var bank)
                    ||!ValidComponents(bank,components))throw new InvalidDataException("Native energy source invalid.");
                bank.SetSlots(components);bank.CurrentPower=reader.ReadUInt16();bank.isOn=reader.ReadBoolean();ushort fuel=reader.ReadUInt16();
                if(bank.CurrentPower>bank.MaxPower||(!(bank is PowerGenerator)&&fuel!=0))throw new InvalidDataException("Source resource state invalid.");
                if(bank is PowerGenerator generator){if(fuel>generator.MaxFuel)throw new InvalidDataException("Generator fuel invalid.");generator.CurrentFuel=fuel;}
                bool light=reader.ReadBoolean();if(bank is PowerSolarPanel solar)solar.HasLight=light;else if(light)throw new InvalidDataException("Foreign solar observation.");
                double residual=reader.ReadDouble();if(!Fraction(residual)||!(bank is PowerSolarPanel)&&residual!=0)throw new InvalidDataException("Solar carry invalid.");
                state.Sources.Add(new Source {Id=id,Position=position,Installed=installed[0],Bank=bank,SolarRemainder=residual});
            }
            if(stream.Position!=stream.Length)throw new InvalidDataException("Energy trailing bytes.");
            return state;
        }
    }
    internal static RebirthCamperEnergyState DecodeBase64(string encoded,Guid expected)
    {
        if(encoded==null||encoded.Length>((Limit+2)/3)*4)throw new InvalidDataException("Bounded energy metadata required.");
        byte[] bytes=Convert.FromBase64String(encoded);
        if(Convert.ToBase64String(bytes)!=encoded)throw new InvalidDataException("Canonical energy metadata required.");
        return Decode(bytes,expected);
    }
    private static bool Fraction(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=0&&value<1;
    internal static bool ValidComponents(PowerSource bank,ItemStack[] items)
    {
        if(items==null||items.Length!=6)return false;
        string name=(Block.list[bank.BlockID] as BlockPowerSource)?.SlotItemName;
        var expected=ItemClass.GetItemClass(name);
        if(expected==null)return false;
        foreach(var item in items)
            if(item==null||(!item.IsEmpty()&&(item.count!=1||item.itemValue?.type!=expected.Id)))return false;
        return true;
    }
    private static void WriteStacks(PooledBinaryWriter writer,ItemStack[] items)
    {
        writer.Write((byte)items.Length);
        foreach(var item in items)
        {
            if(item==null||item.count<0||item.count>1)throw new InvalidDataException("Single native energy item required.");
            // Native empty UI slot shapes can retain count1/type0. Canonicalize
            // only this detached empty image; never mutate the live native slot.
            writer.Write(item.IsEmpty()?0:item.count);
            writer.Write(RebirthNativeItemCodec.Encode(item.IsEmpty()?ItemStack.Empty.itemValue:item.itemValue));
        }
    }
    private static ItemStack[] ReadStacks(PooledBinaryReader reader,int expected)
    {
        if(reader.ReadByte()!=expected)throw new InvalidDataException("Energy slot count invalid.");
        var items=new ItemStack[expected];string empty=RebirthNativeItemCodec.Encode(ItemStack.Empty.itemValue);
        for(int index=0;index<expected;index++)
        {
            int count=reader.ReadInt32();string frame=reader.ReadString();
            if(frame.Length>262144||count<0||count>1)throw new InvalidDataException("Bounded native item image required.");
            if(count==0){if(frame!=empty)throw new InvalidDataException("Canonical empty required.");items[index]=ItemStack.Empty.Clone();}
            else {if(!RebirthNativeItemCodec.TryDecode(frame,out var value))throw new InvalidDataException("Full native image invalid.");items[index]=new ItemStack(value,count);}
        }
        return items;
    }
}

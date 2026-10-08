using System;
class EntityAlive { public bool Allowed=true; }
class ItemStack {} class XUiC_ItemStack {}
class ItemActionEat
{
    public int Calls; public bool Result=true; public float Reserve;
    public virtual bool ExecuteInstantAction(EntityAlive patient,ItemStack stack,bool held,XUiC_ItemStack controller)
    { Calls++; if(Result)Reserve+=12;return Result; } // native +10, shared completion +20% represented here
}
class RebirthServiceCraftSkillService
{
    public static void AdjustMedicalReserveDelta(object a,object b,float c,string d){throw new Exception("Second owner");}
}
class Action:ItemActionEat
{
    bool CanUseTreatment(EntityAlive patient){return patient!=null&&patient.Allowed;}
// PRODUCTION_CLASS
}
class Test
{
    static void Main()
    {
        var action=new Action();var patient=new EntityAlive();
        if(!action.ExecuteInstantAction(patient,new ItemStack(),false,null)||action.Calls!=1||action.Reserve!=12)throw new Exception("Shared owner changed");
        patient.Allowed=false;
        if(action.ExecuteInstantAction(patient,new ItemStack(),false,null)||action.Calls!=1)throw new Exception("Denied action reached base");
        patient.Allowed=true;action.Result=false;
        if(action.ExecuteInstantAction(patient,new ItemStack(),false,null)||action.Reserve!=12)throw new Exception("Failed use adjusted");
        Console.WriteLine("PASS actual instant override: single shared completion owner, denial and failure; native completion doubled");
    }
}

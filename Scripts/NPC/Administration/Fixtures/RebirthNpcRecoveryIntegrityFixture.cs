#if DEBUG
using System;
using System.Collections.Generic;

// PREPARED, NOT RUN. Actual detached cross-store validator; no live game setup.
internal static class RebirthNpcRecoveryIntegrityFixture
{
    internal static void Verify()
    {
        var id=new RebirthNpcStableId(8,9);
        var inventory=new RebirthNpcInventoryPersistentRecord{NpcId=id,Revision=1,
            Quantities=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"helmet",1}},
            Reservations=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"helmet",1}},ReplayJournal=new Guid[0]};
        var equipment=new RebirthNpcEquipmentPersistentRecord{NpcId=id,Revision=2,
            Slots=new Dictionary<RebirthNpcEquipmentSlot,string>{{RebirthNpcEquipmentSlot.Head,"helmet"}},ReplayJournal=new Guid[0]};
        var valid=RebirthNpcItemIntegrityService.AuditRecords(new[]{inventory},new[]{equipment});Require(valid.IsValid&&valid.EquippedSlots==1);
        Require(!RebirthNpcItemIntegrityService.AuditRecords(new[]{inventory,inventory},new[]{equipment}).IsValid);
        Require(!RebirthNpcItemIntegrityService.AuditRecords(new[]{inventory},new[]{equipment,equipment}).IsValid);
        inventory.Reservations.Clear();var missing=RebirthNpcItemIntegrityService.AuditRecords(new[]{inventory},new[]{equipment});
        Require(!missing.IsValid&&missing.MissingInventoryReservations==1);
        inventory.Reservations["helmet"]=2;var excess=RebirthNpcItemIntegrityService.AuditRecords(new[]{inventory},new[]{equipment});
        Require(!excess.IsValid&&excess.ReservationQuantityViolations==1);
        Require(!RebirthNpcItemIntegrityService.AuditRecords(null,new[]{equipment}).IsValid);
        Require(RebirthNpcItemIntegrityService.AuditRecords(new RebirthNpcInventoryPersistentRecord[0],new RebirthNpcEquipmentPersistentRecord[0]).IsValid);
        Require(equipment.Revision==2&&equipment.Slots.Count==1&&inventory.Quantities["helmet"]==1&&inventory.Reservations["helmet"]==2);
    }
    private static void Require(bool value){if(!value)throw new Exception("Detached recovery integrity invariant failed.");}
}

#endif

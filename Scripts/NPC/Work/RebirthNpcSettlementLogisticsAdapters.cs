using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

#nullable disable

public interface IRebirthNpcExternalInventoryQuantityEndpoint
{
    int GetAvailableDebitQuantity(string itemKey);
    int GetAvailableCreditQuantity(string itemKey);
}


public sealed class RebirthNpcSettlementEndpointSnapshot
{
    public string EndpointId { get; internal set; }
    public int ActiveDebitReservations { get; internal set; }
    public int ActiveCreditReservations { get; internal set; }
    public int ReservedDebitQuantity { get; internal set; }
}

public sealed class RebirthNpcSettlementInventoryEndpoint : IRebirthNpcExternalInventoryEndpoint,
    IRebirthNpcExternalInventoryQuantityEndpoint
{
    private sealed class Reservation : IRebirthNpcExternalInventoryReservation, IRebirthNpcExternalInventoryReservationOutcome
    {
        private enum ReservationState:byte { Prepared=0, Committed=1, Failed=2, RolledBack=3, Indeterminate=4 }
        private readonly RebirthNpcSettlementInventoryEndpoint owner;
        private readonly string itemKey;
        private readonly int quantity;
        private readonly bool debit;
        private ReservationState state;
        private bool released;
        private string terminalError=string.Empty;
        public string EndpointId { get { return owner.EndpointId; } }
        public Guid TransactionId { get; private set; }
        RebirthTransactionState IRebirthNpcExternalInventoryReservationOutcome.State
        {
            get
            {
                switch(state)
                {
                    case ReservationState.Committed:return RebirthTransactionState.Committed;
                    case ReservationState.RolledBack:return RebirthTransactionState.RolledBack;
                    case ReservationState.Failed:return RebirthTransactionState.Failed;
                    case ReservationState.Indeterminate:return RebirthTransactionState.Indeterminate;
                    default:return RebirthTransactionState.Prepared;
                }
            }
        }
        string IRebirthNpcExternalInventoryReservationOutcome.Detail { get { return terminalError??string.Empty; } }


        public Reservation(RebirthNpcSettlementInventoryEndpoint owner, Guid transactionId,
            string itemKey, int quantity, bool debit)
        {
            this.owner=owner; TransactionId=transactionId; this.itemKey=itemKey;
            this.quantity=quantity; this.debit=debit;
        }

        public bool Commit(out string error)
        {
            lock(this)
            {
                if(state==ReservationState.Committed){error=string.Empty;return true;}
                if(state!=ReservationState.Prepared){error=terminalError.Length>0?terminalError:"Reservation is no longer commit-capable.";return false;}
                bool success=false;
                try
                {
                    success=debit
                        ? RebirthNpcSettlementSimulation.TryWithdrawResourceWithExternalReservation(
                            owner.SettlementId,itemKey,quantity,TransactionId)
                        : RebirthNpcSettlementSimulation.DepositResource(owner.SettlementId,itemKey,quantity);
                    error=success?string.Empty:(debit ? "Settlement stock changed before debit commit." : "Settlement credit commit failed.");
                    terminalError=error;
                    state=success?ReservationState.Committed:ReservationState.Failed;
                    if(success)Interlocked.Increment(ref owner.commits);
                    return success;
                }
                catch(Exception ex)
                {
                    terminalError=ex.GetType().Name+": "+ex.Message;
                    error=terminalError;
                    state=ReservationState.Indeterminate;
                    return false;
                }
                finally { ReleaseOnce(); }
            }
        }

        public bool Rollback(out string error)
        {
            lock(this)
            {
                if(state==ReservationState.RolledBack){error=string.Empty;return true;}
                if(state==ReservationState.Committed||state==ReservationState.Indeterminate){error="Committed or indeterminate settlement reservation requires an explicit compensation receipt.";return false;}
                ReleaseOnce();state=ReservationState.RolledBack;terminalError=string.Empty;error=string.Empty;
                Interlocked.Increment(ref owner.rollbacks);return true;
            }
        }

        public void Dispose()
        {
            lock(this)
            {
                if(state!=ReservationState.Prepared)return;
                ReleaseOnce();state=ReservationState.RolledBack;Interlocked.Increment(ref owner.rollbacks);
            }
        }

        private void ReleaseOnce()
        {
            if(released)return;
            owner.Release(TransactionId,itemKey,quantity,debit);released=true;
        }
    }

    private readonly object sync=new object();
    private readonly Dictionary<Guid,int> reservedDebit=new Dictionary<Guid,int>();
    private readonly Dictionary<Guid,string> reservedDebitItemByTransaction=new Dictionary<Guid,string>();
    private readonly Dictionary<string,int> reservedDebitByItem=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid,int> reservedCredit=new Dictionary<Guid,int>();
    private long debitReservations,creditReservations,commits,rollbacks,rejections;

    public string SettlementId { get; private set; }
    public string EndpointId { get { return "settlement:"+SettlementId; } }

    public RebirthNpcSettlementInventoryEndpoint(string settlementId)
    {
        SettlementId=(settlementId??string.Empty).Trim();
        if(SettlementId.Length==0) throw new ArgumentException("Settlement id is required.",nameof(settlementId));
    }

    public int GetAvailableDebitQuantity(string itemKey)
    {
        itemKey=(itemKey??string.Empty).Trim(); if(itemKey.Length==0)return 0;
        long available=RebirthNpcSettlementSimulation.GetAvailableResource(SettlementId,itemKey);
        return (int)Math.Max(0,Math.Min(int.MaxValue,available));
    }

    public int GetAvailableCreditQuantity(string itemKey)
    {
        return string.IsNullOrWhiteSpace(itemKey)?0:int.MaxValue;
    }

    public bool TryReserveDebit(Guid transactionId,string itemKey,int quantity,
        out IRebirthNpcExternalInventoryReservation reservation,out string error)
    {
        reservation=null; error=string.Empty; itemKey=(itemKey??string.Empty).Trim();
        if(transactionId==Guid.Empty||itemKey.Length==0||quantity<=0){error="Invalid settlement debit reservation.";Interlocked.Increment(ref rejections);return false;}
        lock(sync)
        {
            if(reservedDebit.ContainsKey(transactionId)){error="Transaction already has a settlement debit reservation.";return false;}
            long stock=RebirthNpcSettlementSimulation.GetAvailableResource(SettlementId,itemKey);
            int existing; reservedDebitByItem.TryGetValue(itemKey,out existing);
            if(stock<quantity){error="Settlement has insufficient unreserved quantity.";Interlocked.Increment(ref rejections);return false;}
            reservedDebit[transactionId]=quantity; reservedDebitItemByTransaction[transactionId]=itemKey;
            reservedDebitByItem[itemKey]=existing+quantity;
        }
        Interlocked.Increment(ref debitReservations);
        reservation=new Reservation(this,transactionId,itemKey,quantity,true); return true;
    }

    public bool TryReserveCredit(Guid transactionId,string itemKey,int quantity,
        out IRebirthNpcExternalInventoryReservation reservation,out string error)
    {
        reservation=null;error=string.Empty;itemKey=(itemKey??string.Empty).Trim();
        if(transactionId==Guid.Empty||itemKey.Length==0||quantity<=0){error="Invalid settlement credit reservation.";Interlocked.Increment(ref rejections);return false;}
        lock(sync){if(reservedCredit.ContainsKey(transactionId)){error="Transaction already has a settlement credit reservation.";return false;}reservedCredit[transactionId]=quantity;}
        Interlocked.Increment(ref creditReservations);
        reservation=new Reservation(this,transactionId,itemKey,quantity,false);return true;
    }

    private void Release(Guid transactionId,string itemKey,int quantity,bool debit)
    {
        lock(sync)
        {
            if(debit)
            {
                if(reservedDebit.Remove(transactionId))
                {
                    reservedDebitItemByTransaction.Remove(transactionId);
                    int value;if(reservedDebitByItem.TryGetValue(itemKey,out value)){value-=quantity;if(value<=0)reservedDebitByItem.Remove(itemKey);else reservedDebitByItem[itemKey]=value;}
                }
            }
            else reservedCredit.Remove(transactionId);
        }
    }

    internal int GetReservedDebitQuantity(string itemKey,Guid excludeTransaction)
    {
        itemKey=(itemKey??string.Empty).Trim();if(itemKey.Length==0)return 0;
        lock(sync)
        {
            int total;reservedDebitByItem.TryGetValue(itemKey,out total);
            if(excludeTransaction!=Guid.Empty)
            {
                int own;string ownItem;
                if(reservedDebit.TryGetValue(excludeTransaction,out own)&&
                    reservedDebitItemByTransaction.TryGetValue(excludeTransaction,out ownItem)&&
                    string.Equals(ownItem,itemKey,StringComparison.OrdinalIgnoreCase))
                    total=Math.Max(0,total-own);
            }
            return Math.Max(0,total);
        }
    }

    public RebirthNpcSettlementEndpointSnapshot CaptureSnapshot()
    {
        lock(sync)
        {
            int quantity = 0;
            foreach(int value in reservedDebitByItem.Values) quantity += value;
            return new RebirthNpcSettlementEndpointSnapshot
            { EndpointId = EndpointId, ActiveDebitReservations = reservedDebit.Count,
              ActiveCreditReservations = reservedCredit.Count, ReservedDebitQuantity = quantity };
        }
    }

    public void ResetForWorldChange()
    {
        lock(sync)
        {
            reservedDebit.Clear();
            reservedDebitItemByTransaction.Clear();
            reservedDebitByItem.Clear();
            reservedCredit.Clear();
        }
    }

    public string GetReport()
    {
        lock(sync)return "endpoint="+EndpointId+" debitActive="+reservedDebit.Count+" creditActive="+reservedCredit.Count+
            " debitReservations="+Interlocked.Read(ref debitReservations)+" creditReservations="+Interlocked.Read(ref creditReservations)+
            " commits="+Interlocked.Read(ref commits)+" rollbacks="+Interlocked.Read(ref rollbacks)+" rejected="+Interlocked.Read(ref rejections);
    }
}

public static class RebirthNpcSettlementInventoryEndpointRegistry
{
    private static readonly object Sync=new object();
    private static readonly Dictionary<string,RebirthNpcSettlementInventoryEndpoint> Endpoints=
        new Dictionary<string,RebirthNpcSettlementInventoryEndpoint>(StringComparer.OrdinalIgnoreCase);

    public static RebirthNpcSettlementInventoryEndpoint Ensure(string settlementId)
    {
        settlementId=(settlementId??string.Empty).Trim(); if(settlementId.Length==0)return null;
        lock(Sync)
        {
            RebirthNpcSettlementInventoryEndpoint endpoint;
            if(!Endpoints.TryGetValue(settlementId,out endpoint))
            {endpoint=new RebirthNpcSettlementInventoryEndpoint(settlementId);Endpoints.Add(settlementId,endpoint);RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);}
            return endpoint;
        }
    }

    public static int GetReservedDebitQuantity(string settlementId,string itemKey,Guid excludeTransaction)
    {
        settlementId=(settlementId??string.Empty).Trim();if(settlementId.Length==0)return 0;
        RebirthNpcSettlementInventoryEndpoint endpoint;
        lock(Sync)
        {
            if(!Endpoints.TryGetValue(settlementId,out endpoint))return 0;
        }
        return endpoint.GetReservedDebitQuantity(itemKey,excludeTransaction);
    }

    public static RebirthNpcSettlementEndpointSnapshot[] CaptureSnapshots()
    {
        lock(Sync)
        {
            RebirthNpcSettlementEndpointSnapshot[] result = new RebirthNpcSettlementEndpointSnapshot[Endpoints.Count];
            int index = 0;
            foreach(RebirthNpcSettlementInventoryEndpoint endpoint in Endpoints.Values)
                result[index++] = endpoint.CaptureSnapshot();
            return result;
        }
    }

    public static void ResetForWorldChange()
    {
        lock(Sync)
        {
            foreach(RebirthNpcSettlementInventoryEndpoint endpoint in Endpoints.Values)
            {
                endpoint.ResetForWorldChange();
                RebirthNpcExternalInventoryEndpointRegistry.Unregister(endpoint.EndpointId);
            }
            Endpoints.Clear();
        }
    }

    public static string GetReport()
    {
        lock(Sync)
        {
            StringBuilder b=new StringBuilder("[REBIRTH NPC Settlement Inventory Endpoints] registered=").Append(Endpoints.Count);
            foreach(RebirthNpcSettlementInventoryEndpoint e in Endpoints.Values)b.AppendLine().Append("  ").Append(e.GetReport());
            return b.ToString();
        }
    }
}

public sealed class RebirthNpcSettlementHaulingPlanProvider : IRebirthNpcHaulingPlanProvider
{
    public int Priority { get { return 500; } }

    public bool TryPlan(RebirthNpcConcreteWorkContext context,out RebirthNpcHaulingPlan plan,out string detail)
    {
        plan=null;detail=string.Empty;
        string key=context?.Assignment?.TargetKey??string.Empty;
        if(!key.StartsWith("haul:settlement:",StringComparison.OrdinalIgnoreCase))return false;
        string[] p=key.Split(':');
        if(p.Length<7){detail="Settlement hauling target must be haul:settlement:<source>:<destination>:<item>:<quantity>:<partial|exact>.";return false;}
        string source=p[2].Trim(),destination=p[3].Trim(),item=p[4].Trim();int quantity;
        if(source.Length==0||destination.Length==0||item.Length==0||!int.TryParse(p[5],NumberStyles.Integer,CultureInfo.InvariantCulture,out quantity)||quantity<=0)
        {detail="Settlement hauling target contains invalid source, destination, item, or quantity.";return false;}
        bool partial=string.Equals(p[6],"partial",StringComparison.OrdinalIgnoreCase);
        RebirthNpcSettlementInventoryEndpoint src=RebirthNpcSettlementInventoryEndpointRegistry.Ensure(source);
        RebirthNpcSettlementInventoryEndpoint dst=RebirthNpcSettlementInventoryEndpointRegistry.Ensure(destination);
        if(src==null||dst==null){detail="Settlement inventory endpoint registration failed.";return false;}
        plan=new RebirthNpcHaulingPlan{SourceEndpointId=src.EndpointId,DestinationEndpointId=dst.EndpointId,ItemKey=item,Quantity=quantity,AllowPartial=partial};
        detail="Settlement hauling plan resolved.";return true;
    }
}

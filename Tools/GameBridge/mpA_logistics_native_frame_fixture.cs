using System;
using System.Linq;
using System.Reflection;

// PREPARED, NOT RUN. Requires actual current retained original/native ECD and
// no other active proof observer on that original while reading counters.
// NativeFrame is allocation/codec input only, never publication authority.
internal static class mpA_logistics_native_frame_fixture
{
    internal static void Verify(RebirthLogisticsTransferJournal.Record original,byte[] actualNativeFrame,Func<bool> current)
    {
        if(actualNativeFrame==null||actualNativeFrame.Length<1||actualNativeFrame.Length>131072)throw new Exception("Requires actual bounded native frame.");
        var model=RebirthLogisticsNativeFrame.Capture(original,actualNativeFrame);
        if(typeof(RebirthLogisticsNativeFrame).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Any(field=>field.FieldType==typeof(byte[])))throw new Exception("Journal model eagerly retains binary frame.");
        if(!model.Matches(original.Owner,original.Operation,original.World,RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)))throw new Exception("Original binding lost.");
        using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,current))
        {
            if(account==null)throw new Exception("Actual retained original unavailable.");long before=account.WorkspaceBytes;
            var materialization=model.Open();if(materialization==null)throw new Exception("Actual original materialization unavailable.");
            try
            {
                if(!materialization.Bytes.SequenceEqual(actualNativeFrame)||account.WorkspaceBytes!=before+9L*actualNativeFrame.Length)throw new Exception("Frame decode/copy allocation not charged before use.");
                materialization.FinishDecode();if(account.WorkspaceBytes!=before+actualNativeFrame.Length)throw new Exception("Retained original frame lease lost or decoder not released.");
            }
            finally{materialization.Dispose();}
            if(materialization.Bytes!=null||account.WorkspaceBytes!=before)throw new Exception("Materialized frame retained after release.");
        }
    }
}

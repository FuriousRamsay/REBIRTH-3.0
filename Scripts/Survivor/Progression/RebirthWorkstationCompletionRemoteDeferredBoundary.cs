using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class NativeHostOriginal
    {
        private bool IsLoadedDeferredBoundaryBefore(XElement full)
        {
            try
            {
                var loaded=remoteOutputRestart;
                if(loaded?.DeferredAddBody==null||loaded.FinalCallbackSaved||!BeforeSaved||!CurrentViewer()||!AdmissionCurrent()||
                    !loaded.Current()||loaded.Native?.IdentityCurrent()!=true||full?.Name!="nativeOwningFullBoundary"||
                    (string)full.Attribute("operation")!=Operation.ToString("N")||
                    (string)full.Element("recipeFull")!=(string)loaded.Before.Element("recipeFull"))return false;
                string text=(string)full.Element("nativeBefore");
                if(text==null||text.Length>4*((RebirthProductiveEarnedNativeBody.MaximumBodyBytes+2)/3))return false;
                var body=Convert.FromBase64String(text);
                return Convert.ToBase64String(body)==text&&RebirthProductiveEarnedNativeBody.SameExceptElapsed(
                    loaded.DeferredAddBody,loaded.DeferredAddOffset,body,(int)full.Attribute("nativeElapsedOffset"));
            }
            catch{return false;}
        }
    }
}

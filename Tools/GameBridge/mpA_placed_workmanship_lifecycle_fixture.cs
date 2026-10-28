using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
// PREPARED NOT RUN. Harness supplies actual startup/shutdown/save-failure events;
// no fabricated world, native identity or live provenance is created here.
internal static class mpA_placed_workmanship_lifecycle_fixture
{
    private static readonly Type Service=typeof(RebirthPlacedWorkmanshipService);
    private static object Field(string name)=>Service.GetField(name,BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
    internal static void RequireReady(World actual)
    {
        if(!ReferenceEquals(GameManager.Instance.World,actual)||actual.IsRemote()||!RebirthPlacedWorkmanshipService.CanCaptureOriginal(actual)||!ReferenceEquals(Field("loadedWorld"),actual))throw new Exception("Actual final native world not bound after GameStartDone");
        string expected=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","Survivor","PlacedWorkmanship.dat");
        if((string)Field("loadedPath")!=expected)throw new Exception("Sidecar bound to provisional/other native save");
    }
    internal static void RequireUnknownRetained(World prior,string explicitPriorPath,string primarySha,string backupSha,string temporarySha)
    {
        if(RebirthPlacedWorkmanshipService.CanReset||!ReferenceEquals(Field("loadedWorld"),prior)||(string)Field("loadedPath")!=explicitPriorPath)throw new Exception("Unresolved original scope was abandoned");
        Check(explicitPriorPath,primarySha);Check(explicitPriorPath+".bak",backupSha);Check(explicitPriorPath+".tmp",temporarySha);
    }
    internal static void RequireParser(string actualFile,bool expected)
    {
        int count=((IDictionary)Field("records")).Count;object world=Field("loadedWorld");
        object[] args={actualFile,null,null};bool actual=(bool)Service.GetMethod("TryLoadFile",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
        if(actual!=expected||count!=((IDictionary)Field("records")).Count||!ReferenceEquals(world,Field("loadedWorld")))throw new Exception("Detached parser changed live original state");
    }
    private static void Check(string path,string expected)
    {
        if(expected==null){if(File.Exists(path))throw new Exception("Unexpected original candidate");return;}
        using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")!=expected)throw new Exception("Original candidate bytes changed");
    }
}
// Actual lifecycle matrix: native GameStarting before world construction;
// native GameStartDone binds final A; periodic dirty save+readback; WorldShutdown
// then GameShutdown duplicate; A->B clean; failed A save->B must retain A exact
// path/records until published; failed A load blocks B/reset. Parser corpus:
// valid v1 legacy retained/unproven, valid v2, duplicate coords, invalid enum,
// NaN, malformed/truncated/oversized/noncanonical string prefix, extra bytes,
// inaccessible/reparse. Primary corrupt+valid backup preserves exact BOTH raw
// files before backup active; failed retention refuses activation. Startup tmp
// never deleted. Same-process verified temp failed publish retries only same
// mutation; changed memory archives that exact own temp before new snapshot;
// unknown/changed temp refuses. Final readback failure latches unresolved.
// Native local/remote fresh placement, upgrade, carried workmanship, record-cap
// fallback native marker and actual source replacement require real game action.
// Pickup protocol/MP054 is outside this fixture; no source/receipt substitutes.

import {readFile,writeFile} from 'node:fs/promises';const path='Scripts/GameBridge/RebirthGameBridgePath.cs';let s=await readFile(path,'utf8');function method(name){const a=s.indexOf('    public static IEnumerator '+name+'(');let b=s.indexOf('{',a),n=1;while(n&&++b<s.length){if(s[b]==='{')n++;if(s[b]==='}')n--;}return s.slice(a,b+1);}
let follow=method('FollowPath').replace(/    public static IEnumerator FollowPath\([^\n]+\)/,'    private static IEnumerator FollowPathOwned(EntityPlayerLocal p, List<Step> path, float finalRadius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope scope)').replace('        PlayerActionsLocal a =','        if (!scope.Admitted) yield break;\n        PlayerActionsLocal a =').replace('while (Time.realtimeSinceStartup < end &&','while (scope.Admitted && Time.realtimeSinceStartup < end &&').replace('DoorRoutine(p, st.DoorBlock, m => r = m)','DoorRoutine(p, st.DoorBlock, m => r = m, scope)').replaceAll('RebirthGameBridgeInput.HoldFrames(','scope.TryHoldFrames(').replaceAll('RebirthGameBridgeInput.Release(','scope.Release(');
let go=method('GoTo').replace(/    public static IEnumerator GoTo\([^\n]+\)/,'    private static IEnumerator GoToOwned(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope scope)').replace('        List<Step> path =','        if (!scope.Admitted) yield break;\n        List<Step> path =').replace('FollowPath(p, path, radius, maxSeconds, log, r => ok = r)','FollowPath(p, path, radius, maxSeconds, log, r => ok = r, scope)');
const wrappers=String.raw`
    public static IEnumerator FollowPath(EntityPlayerLocal p, List<Step> path, float finalRadius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(FollowPathOwned(p, path, finalRadius, maxSeconds, log, ok => { completed = true; done(ok); }, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(false); }
        }
    }
    public static IEnumerator GoTo(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(GoToOwned(p, target, radius, maxSeconds, log, ok => { completed = true; done(ok); }, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(false); }
        }
    }
`;
const i=s.lastIndexOf('}');s=s.slice(0,i)+follow+'\n'+go+'\n'+wrappers+s.slice(i);await writeFile(path,s);
const poiPath='Scripts/GameBridge/RebirthGameBridgePoiStorage.cs';let poi=await readFile(poiPath,'utf8');poi=poi.replace('ok => routed = ok);','ok => routed = ok, input);').replace('ok => reachedStorage = ok);','ok => reachedStorage = ok, input);').replace('() => aim, 0f, 0f, 1.5f);','() => aim, 0f, 0f, 1.5f, input);').replace('chosen.Value, log, 1.2f, 20f);','chosen.Value, log, 1.2f, 20f, input);');
// Each recovery leg owns its own parent scope and calls the newly granted scoped recovery overload.
poi=poi.replace('                var movement = RebirthGameBridgePlayer.WalkToGuarded(player, marker.ToWorldCenterPos(),','                using (var recoveryInput = new RebirthGameBridgeInput.OwnedInputScope(player, () => interrupted() == null))\n                {\n                var movement = RebirthGameBridgePlayer.WalkToGuarded(player, marker.ToWorldCenterPos(),').replace('4f, 30f, interrupted, result => walked = result);','4f, 30f, interrupted, result => walked = result, recoveryInput);').replace('                finally { (movement as IDisposable)?.Dispose(); }','                finally { (movement as IDisposable)?.Dispose(); }\n                }');
await writeFile(poiPath,poi);

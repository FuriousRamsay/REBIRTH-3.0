// AUTHORED_NOT_RUN. Composition support for the preexisting bounded cohorts.
// New joint reentry cohorts embed the actual new native proof/reentry sources.
// Older cohorts must never manufacture those missing capabilities from a stub.
import {writeFile} from 'node:fs/promises';

export async function writeHydrationFixtureConfig(exe) {
  await writeFile(exe+'.config','<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup><runtime><AppContextSwitchOverrides value="Switch.System.IO.UseLegacyPathHandling=false;Switch.System.IO.BlockLongPaths=false" /></runtime></configuration>');
}

export function alignHydrationSourcePaths(paths) {
  const add = path => { if (!paths.includes(path)) paths.push(path); };
  if (paths.some(path => /RebirthHydrationProposalClient(?:\.|\.cs)/.test(path))) {
    // Ordinary proposal receipt now persists its exact owning original. Compose
    // the real purpose store and WAL, rather than adapting publication to true.
    add('Scripts/Metabolism/RebirthHydrationClientOriginals.cs');
    add('Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs');
  }
}

export function alignHydrationFixture(fixture) {
  // Keep disposable adapter roots short on Windows hosts with long paths off.
  // Root remains mutable so the existing scope-replacement assertions apply.
  fixture=fixture.replace('internal static string Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native");','internal static string Root=AppDomain.CurrentDomain.BaseDirectory;')
    .replace('internal static string GetPlayerDataDir()=>Path.Combine(Root,"Player");','internal static string GetPlayerDataDir()=>Root;');
  // Standalone csc executables otherwise inherit legacy CLR MAX_PATH checks.
  // Keep the production WAL paths intact; use the modern .NET Framework file
  // handling in this isolated adapter process. Unity/Mono remains separate.
  fixture=fixture.replace('internal static class GameIO {','internal static class GameIO {static GameIO(){AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);}');
  // Native ItemStack.itemValue is a stable reference. A fresh object on every
  // property read incorrectly invalidates the production one-use belt permit.
  fixture=fixture.replaceAll('internal ItemValue itemValue=>new ItemValue{Data=Data};',
    'private ItemValue nativeItemValue;internal ItemValue itemValue{get{if(nativeItemValue==null)nativeItemValue=new ItemValue();nativeItemValue.Data=Data;return nativeItemValue;}}');
  const additions = [];
  const hasType = type => new RegExp('\\bclass\\s+' + type + '\\b').test(fixture);
  const hasMethod = name => new RegExp('\\bbool\\s+' + name + '\\s*\\(').test(fixture);
  const hasTypeMethod = (type, name) => {
    // Two independent checkpoint classes expose HasSavedHistorical. Search
    // only the named type so one actual capability cannot hide another type's
    // missing excluded-boundary adapter. Ignore braces in C# literals/comments.
    const code = fixture.replace(/@"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\r\n]*|\/\*[\s\S]*?\*\//g,
      value => ' '.repeat(value.length));
    const declaration = new RegExp('\\bclass\\s+' + type + '\\b', 'g');
    for (let match; (match = declaration.exec(code));) {
      const start = code.indexOf('{', match.index);
      if (start < 0) continue;
      let end = start + 1, depth = 1;
      for (; depth && end < code.length; end++) {
        if (code[end] === '{') depth++;
        else if (code[end] === '}') depth--;
      }
      if (!depth && new RegExp('\\bbool\\s+' + name + '\\s*\\(').test(code.slice(start, end))) return true;
    }
    return false;
  };
  const makePartial = type => {
    fixture = fixture.replace(new RegExp('((?:internal|public)\\s+static\\s+)(class\\s+' + type + '\\b)', 'g'), '$1partial $2');
  };
  const appendStatic = (type, members) => {
    if (!members.length) return;
    const declaration = fixture.match(new RegExp('\\b(public|internal)\\s+static(?:\\s+partial)?\\s+class\\s+' + type + '\\b'));
    const access = declaration ? declaration[1] : 'internal';
    makePartial(type);
    additions.push(access + ' static partial class ' + type + '{\n' + members.join('\n') + '\n}');
  };
  // These members represent explicit excluded native/cold boundaries. They
  // always refuse. They cannot issue a grant, advance a receipt or release hold.
  if (fixture.includes('RebirthHydrationProposalClient.TryGetCurrent(') &&
      !hasTypeMethod('RebirthHydrationProposalClient', 'TryGetCurrent'))
    appendStatic('RebirthHydrationProposalClient', [
      'internal sealed class ExcludedColdIntent{internal RebirthHydrationTransferOriginal Original;internal Guid Operation;}',
      'internal static bool TryGetCurrent(EntityPlayerLocal player,out ExcludedColdIntent intent){intent=null;return false;}'
    ]);
  if (hasType('RebirthHydrationOwnerHold')) {
    const members = [];
    if (fixture.includes('RebirthHydrationOwnerHold.TryReenterRemote(') && !hasMethod('TryReenterRemote'))
      members.push('internal static bool TryReenterRemote(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;');
    if (fixture.includes('RebirthHydrationOwnerHold.RetireHistoricalRemote(') && !hasMethod('RetireHistoricalRemote'))
      members.push('internal static bool RetireHistoricalRemote(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;');
    if (fixture.includes('RebirthHydrationOwnerHold.RetireHistoricalRefusal(') && !hasTypeMethod('RebirthHydrationOwnerHold', 'RetireHistoricalRefusal'))
      members.push('internal static bool RetireHistoricalRefusal(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;');
    appendStatic('RebirthHydrationOwnerHold', members);
  }
  if (hasType('RebirthHydrationServerSourceHold') && /\bclass\s+Lease\b/.test(fixture)) {
    if (!hasType('RebirthHydrationRemoteOwnerWitness'))
      additions.push('internal static class RebirthHydrationRemoteOwnerWitness{internal static bool HasSavedBefore(RebirthHydrationServerSourceHold.Lease lease)=>false;internal static bool HasApplied(RebirthHydrationServerSourceHold.Lease lease)=>false;}');
    const members = [];
    if (!hasMethod('HasSavedRemoteHydrationBefore'))
      members.push('internal static bool HasSavedRemoteHydrationBefore(RebirthHydrationServerSourceHold.Lease lease)=>false;');
    if (!hasMethod('HasSavedRemoteHydrationAfter'))
      members.push('internal static bool HasSavedRemoteHydrationAfter(RebirthHydrationServerSourceHold.Lease lease)=>false;');
    appendStatic('RebirthMetabolismStateRepository', members);
    if (fixture.includes('RebirthHydrationTransferJournal.PreparedRejection.TryPublish(') && !hasType('PreparedRejection')) {
      additions.push('internal static partial class RebirthHydrationTransferJournal{internal sealed class PreparedRejection{internal static bool TryPublish(RebirthHydrationServerSourceHold.Lease lease,out PreparedRejection rejection){rejection=null;return false;}}}');
      if (!hasMethod('TryReleasePreparedRejection'))
        appendStatic('RebirthHydrationServerSourceHold', ['internal static bool TryReleasePreparedRejection(RebirthHydrationTransferJournal.PreparedRejection rejection)=>false;']);
    }
  }
  if (hasType('RebirthHydrationTerminalSeenCheckpoint') && !hasTypeMethod('RebirthHydrationTerminalSeenCheckpoint', 'HasSavedHistorical'))
    appendStatic('RebirthHydrationTerminalSeenCheckpoint', ['internal static bool HasSavedHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;']);
  if (fixture.includes('RebirthHydrationRefusedCheckpoint.HasSavedHistorical(') &&
      !hasTypeMethod('RebirthHydrationRefusedCheckpoint', 'HasSavedHistorical'))
    appendStatic('RebirthHydrationRefusedCheckpoint', ['internal static bool HasSavedHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>false;']);
  if (fixture.includes('RebirthHydrationTransferJournal.TryPersistColdAcknowledgment(') && !hasMethod('TryPersistColdAcknowledgment'))
    appendStatic('RebirthHydrationTransferJournal', ['internal static bool TryPersistColdAcknowledgment(EntityPlayer player,Guid operation,string digest)=>false;']);
  if (fixture.includes('RebirthHydrationTransferJournal.TryReadAcknowledged(') && !hasTypeMethod('RebirthHydrationTransferJournal','TryReadAcknowledged'))
    appendStatic('RebirthHydrationTransferJournal', ['internal static bool TryReadAcknowledged(EntityPlayer player,Guid operation,string digest,out RebirthHydrationTransferOriginal original){original=null;return false;}']);
  if (fixture.includes('RebirthCamperTransferHistoryScope.TryGet(') && !hasType('RebirthCamperTransferHistoryScope'))
    additions.push('internal sealed class ExcludedHistoryIndex{internal RebirthCamperTransferJournal.Record FindPending(Guid group)=>throw new InvalidOperationException("Excluded history index");internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid group,string creation)=>throw new InvalidOperationException("Excluded history index");internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid group,string creation)=>throw new InvalidOperationException("Excluded history index");}internal static class RebirthCamperTransferHistoryScope{internal static bool TryGet(RebirthCamperTransferJournal journal,out ExcludedHistoryIndex index){index=null;return false;}}');
  if (fixture.includes('RebirthHydrationTransferGrant.TryIssueLocalReentered(') && !hasMethod('TryIssueLocalReentered'))
    additions.push('internal sealed partial class RebirthHydrationTransferGrant{internal static bool TryIssueLocalReentered(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthHydrationTransferGrant grant){grant=null;return false;}}');
  if (fixture.includes('RebirthHydrationOwnerCheckpoint.HasSaved(') && !hasTypeMethod('RebirthHydrationOwnerCheckpoint','HasSaved'))
    appendStatic('RebirthHydrationOwnerCheckpoint',['internal static bool HasSaved(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthGearOwnerReceipt stage)=>HasSavedBefore(player,original)&&player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))==(float)stage;']);
  // Shared C# source imports are stripped by each existing runner. The new
  // actual purpose WAL source needs these imports before any declaration.
  return 'using System.Text;using System.Xml;using System.Security.Cryptography;using System.Globalization;using System.IO;using System.Linq;using System.Diagnostics;\n' + fixture + '\n' + additions.join('\n');
}

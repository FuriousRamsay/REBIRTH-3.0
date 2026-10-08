from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
ERR=[]; WARN=[]
def req(v,m):
    if not v: ERR.append(m)
release=ROOT/'Scripts/Survivor/Progression/Explorer/RebirthProgressionExplorerReleaseGate.cs'
req(release.exists(),'PE-12 release-gate service missing')
if release.exists():
    s=release.read_text(encoding='utf-8')
    for token in ['BuildAuthorityFingerprint','RebirthSurvivorDefinitionRegistry.SemanticHash','RebirthCapabilityRegistry.SemanticHash','RebirthProgressionGraphRegistry.SemanticHash','RebirthSurvivorNetworkProtocol.Version','StaticReady','RuntimeApproved=false','OwnerDefinitionsCompatible']:
        req(token in s,'release gate missing '+token)
    req('SHA256.Create()' in s,'authority fingerprint is not deterministic SHA-256')
console=(ROOT/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(encoding='utf-8')
req('mode=="release"' in console,'progressiongraph release diagnostic missing')
req('RebirthProgressionExplorerReleaseGate.Evaluate().BuildText()' in console,'release command not bound to release gate')
harness=(ROOT/'Scripts/Survivor/Debug/RebirthSurvivorTestHarness.cs').read_text(encoding='utf-8')
req('progression explorer release gate' in harness,'rbsurvivor test all does not include PE-12 release gate')
req('runtimeApproved=false' in harness,'test-all output does not preserve runtime approval as pending')
req('compare full fingerprint across host/P2P/dedicated' in harness,'multiplayer fingerprint comparison guidance missing')
# PE-12 must not mutate protocol or forge runtime acceptance evidence.
network_models=(ROOT/'Scripts/Survivor/Network/RebirthSurvivorNetworkModels.cs').read_text(encoding='utf-8')
req('AuthorityFingerprint' not in network_models,'PE-12 unexpectedly changed persisted/network owner-state protocol for an informational release fingerprint')
WARN.append('Static PE-12 can prove deterministic local authority identity but cannot prove that remote peers loaded the same fingerprint; compare runtime output on host/P2P/dedicated clients.')
WARN.append('Runtime approval remains intentionally false until compile, visual/controller, networking and timed performance acceptance are executed in 7DTD.')
print(f'PE-12 multiplayer/regression release-gate static audit: errors={len(ERR)} warnings={len(WARN)}')
for e in ERR: print('ERROR:',e)
for w in WARN: print('WARN:',w)
raise SystemExit(1 if ERR else 0)

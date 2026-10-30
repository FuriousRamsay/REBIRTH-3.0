"""Test-only native loot capacity observation driver; never launches/builds/edits saves."""
import argparse, ctypes, hashlib, json, os, re, time, urllib.request, urllib.parse
import xml.etree.ElementTree as E
from pathlib import Path

SLOT=re.compile(r'^contextContainerGridSlot(\d+)$')
WINDOW='rebirthContextNavigation'
CATALOG={
 'dumpster':('cntDumpster',48,'dumpster'),
 'reinforced_t1':('cntLootChestHeroInsecureT1',30,'reinforcedChestT1'),
 'ice_machine':('cntIceMachine',42,'shamwayFreezer; natural block override6x7'),
 'shared_freezer':('cntFreezerGroceriesMiddleFullClosed',24,'shamwayFreezer'),
 'locked_wall_safe':('cntWallSafe',24,'smallSafes; natural block override6x4'),
 'shared_safe_control':('cntWallSafeInsecure',40,'smallSafes; independent non-inheriting block'),
 'picked_bookcase':('cntBookcaseFullRebirthPlayer',24,'explicit player RebirthContainerSize6x4'),
 'picked_ice_machine':('cntIceMachineRebirthPlayer',42,'legacy inherited FRFreezer6x7; current gap must fail until lead fixes player size'),
}

def require(ok,message):
    if not ok: raise ValueError(message)
def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()
def write(path,value):
    path=Path(path).resolve();require(not any(p.lower() in ('saves','saveslocal') for p in path.parts),'Never write evidence into game saves')
    require(not path.exists(),'Refuse evidence overwrite: '+str(path))
    path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')

def physical_slots(tree,expected):
    require(not tree.get('truncated',True),'UI tree truncated or no truncation witness')
    require(WINDOW in tree.get('openWindows',[]),'Unified context is not open')
    slots={}
    for node in tree.get('nodes',[]):
        match=SLOT.fullmatch(node.get('id',''))
        if not match or '/contextContainerGrid/' not in node.get('path',''): continue
        require(node.get('ctrl')=='XUiC_RebirthContextSlot','Wrong native-backed controller')
        require('/contextBackpackGrid/' not in node['path'],'Backpack mistaken for container')
        index=int(match[1]);require(index not in slots,'Duplicate physical slot '+str(index))
        rect=node.get('rect');require(isinstance(rect,list) and len(rect)==4 and rect[2]>0 and rect[3]>0,'No real physical slot rect')
        require(abs(rect[0])<50000 and abs(rect[1])<50000,'Hidden padding offscreen')
        slots[index]=node.get('item')
    require(set(slots)==set(range(expected)),f'Physical indices {sorted(slots)} do not equal0..{expected-1}; never count198pool or padded rows')
    return [{'index':i,'item':slots[i]} for i in sorted(slots)]

def identity_equal(old,new,cold=False):
    for field in ('gameName','worldKey','ownerKey','creationId'):
        require(old.get(field) is not None and old.get(field)==new.get(field),'World/owner/character identity changed: '+field)
    if cold:
        require(old.get('processIdentity')!=new.get('processIdentity'),'Same process is NOT cold reload')
        require(old.get('worldInstance')!=new.get('worldInstance'),'Same world instance is NOT cold reload')

class Bridge:
    def __init__(self,session):
        self.path=Path(session).resolve();self.session=read(self.path)
        require(self.path.parent.name.startswith(str(self.session['pid'])+'-'),'Pin unique PID-session directory, not mutable out/session.json')
        self.sequence=0;self.output=None
    def call(self,path,query=None,method='GET'):
        current=read(self.path)
        require(all(current.get(k)==self.session.get(k) for k in ('pid','processStartUtc','startedUtc','port','token')),'Pinned session changed')
        headers={'X-Bridge-Token':self.session['token']}
        if os.environ.get('REBIRTH_BRIDGE_RUN_TOKEN'):headers['X-Bridge-Run']=os.environ['REBIRTH_BRIDGE_RUN_TOKEN']
        request=urllib.request.Request('http://127.0.0.1:'+str(self.session['port'])+path+'?'+urllib.parse.urlencode(query or {}),data=b'' if method=='POST' else None,headers=headers,method=method)
        with urllib.request.urlopen(request,timeout=90) as response: value=json.load(response)
        require(value.get('ok',True) is not False,'Native request failed: '+path)
        if self.output:
            self.sequence+=1;write(self.output/(f'{self.sequence:03d}-'+path.strip('/')+'.json'),value)
        return value
    def topology(self):
        ping=self.call('/ping');require(ping.get('pid')==self.session['pid'] and ping.get('state')=='ingame','Wrong live process or not in game')
        return self.call('/auditstate',{'domain':'topology'})['domains']['topology']

def process_alive(pid):
    require(os.name=='nt','Cold seal process check requires Windows; never use os.kill on Windows')
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    kernel.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];kernel.OpenProcess.restype=ctypes.c_void_p
    kernel.GetExitCodeProcess.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)]
    kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x1000,False,int(pid))
    if not handle:
        require(ctypes.get_last_error()==87,'Cannot establish old process exit');return False
    try:
        code=ctypes.c_ulong();require(kernel.GetExitCodeProcess(handle,ctypes.byref(code)),'Cannot read process exit');return code.value==259
    finally:kernel.CloseHandle(handle)

def custody(manifest):
    receipt=read(manifest['ownershipReceipt']);require(digest(manifest['ownershipReceipt'])==manifest['ownershipSha256'],'Ownership receipt changed')
    require(receipt.get('originalRetained') is True,'Original save preservation not recorded')
    target=Path(receipt['target']).resolve();source=Path(receipt['source']).resolve()
    require(target!=source and target.name==manifest['gameName'] and target.is_dir(),'Disposable copy target invalid')
    require(manifest.get('disposable') is True,'Explicit disposable fixture required')
    return target

def check_contract(mod,contract):
    for entry in contract:
        require(digest(Path(mod)/entry['path'])==entry['sha256'],'Source binding/config drift: '+entry['path'])
    xml=E.parse(Path(mod)/'Config/XUi_InGame/windows.xml')
    grids=[n for n in xml.iter('rect') if n.get('name')=='contextContainerGrid']
    require(len(grids)==1,'Expected one exact container grid XML')
    pool=list(grids[0].iter('item_stack'))
    require(len(pool)==198 and all(n.get('name')=='contextContainerGridSlot'+str(i) and n.get('repeat_i')==str(i) and n.get('visible')=='false' for i,n in enumerate(pool)),'Physical slot ID/index/padding binding changed')

def probe(bridge,case):
    require(case['catalog'] in CATALOG,'Unknown case');block,expected,_=CATALOG[case['catalog']]
    require(case.get('provenance') in ('fresh_natural','saved_existing','real_picked_up_placed'),'Fixture provenance missing')
    if case['catalog'].startswith('picked_'):
        require(case['provenance']=='real_picked_up_placed' and case.get('pickupReceipt'),'Actual native pickup/placement receipt required')
        pickup=read(case['pickupReceipt'])
        require(pickup.get('realInputPickupAndPlacement') is True and pickup.get('placedBlock')==block,'Pickup setup receipt does not identify this real-input placement')
        require(pickup.get('evidence'),'Raw native pickup/placement evidence required')
        for evidence in pickup['evidence']:require(digest(evidence['path'])==evidence['sha256'],'Pickup evidence changed')
    for receipt in case.get('receipts',[]):require(digest(receipt['path'])==receipt['sha256'],'Fixture provenance receipt changed')
    bridge.call('/walkto',{'x':case['approach']['x'],'z':case['approach']['z'],'radius':.5},'POST')
    bridge.call('/lookat',dict(zip(('x','y','z'),case['position']),block=1),'POST')
    target=bridge.call('/target')['target']['block']
    require(target['name']==block and target['position']==case['position'],'Target identity/position mismatch; do not open different downgrade/control')
    bridge.call('/activate',dict(zip(('x','y','z'),case['position']),block=1),'POST')
    deadline=time.monotonic()+15
    while True:
        tree=bridge.call('/uitree',{'window':WINDOW,'all':1})
        if WINDOW in tree.get('openWindows',[]):break
        require(time.monotonic()<deadline,'Native open failed/locked. Unlock legally without downgrading exact block.');time.sleep(.25)
    slots=physical_slots(tree,expected)
    shot=bridge.call('/screenshot',{'label':'loot-capacity-'+case['id']},'POST')
    bridge.call('/key',{'name':'Escape'},'POST');time.sleep(.3)
    closed=bridge.call('/uitree',{'window':WINDOW,'all':1});require(WINDOW not in closed.get('openWindows',[]),'Context remained open (lock mode?); no inferred close')
    return {'caseId':case['id'],'catalog':case['catalog'],'block':block,'position':case['position'],'capacity':expected,'slots':slots,'screenshot':shot,'provenance':case['provenance']}

def run(args):
    manifest=read(args.manifest);save=custody(manifest);check_contract(args.mod_root,read(Path(__file__).with_name('source-contract.json')))
    bridge=Bridge(args.session);topology=bridge.topology();require(topology.get('gameName')==manifest['gameName'] and topology.get('remote') is False,'Wrong disposable world/authority')
    identity_equal(topology,topology)
    require(bridge.call('/guard').get('enabled') is True,'Native instincts must remain enabled; this driver never changes guard settings')
    cases=manifest['cases'];require(len({c['id'] for c in cases})==len(cases),'Duplicate cases')
    for case in cases:
        require(re.fullmatch(r'[a-z0-9_-]{1,50}',case['id']) is not None,'Case ID must be a safe evidence label')
        require(isinstance(case.get('position'),list) and len(case['position'])==3 and all(type(x) is int for x in case['position']),'Observed integer block coordinates required; no invented template positions')
        require(isinstance(case.get('approach'),dict) and all(type(case['approach'].get(x)) in (int,float) for x in ('x','z')),'Observed safe walk approach required')
        fixture=read(case['fixtureReceipt'])
        require(fixture.get('gameName')==manifest['gameName'] and fixture.get('block')==CATALOG[case['catalog']][0] and fixture.get('position')==case['position'] and fixture.get('provenance')==case['provenance'],'Staged exact disposable fixture receipt required')
    require(set(CATALOG) <= {c['catalog'] for c in cases},'Six natural controls plus picked bookcase and inherited ice-machine controls required')
    prior=None
    if args.phase=='cold-verify':
        prior=read(args.prior);require(prior.get('phase')=='cold-sealed' and prior['manifestSha256']==digest(args.manifest),'Cold-sealed exact fixture receipt required')
        identity_equal(prior['topology'],topology,cold=True)
        require(prior['sessionPid']!=bridge.session['pid'] or prior['sessionStartedUtc']!=bridge.session['startedUtc'],'New process/session required')
    bridge.output=Path(args.output).resolve();require(not bridge.output.exists(),'New output directory required')
    require(not (bridge.output==save or save in bridge.output.parents),'Evidence cannot be written into a save')
    require(not any(p.lower() in ('saves','saveslocal') for p in bridge.output.parts),'Evidence cannot be written into any game save')
    bridge.output.mkdir(parents=True)
    initial=bridge.call('/ping')['lastLogSeq'];observations=[]
    for case in cases:
        first=probe(bridge,case);again=probe(bridge,case)
        require(first['slots']==again['slots'],'Warm reopen changed visible slot contents; preserve fixture, inspect native behavior')
        if prior:
            old=next(r for r in prior['observations'] if r['caseId']==case['id'])
            require(first['block']==old['block'] and first['position']==old['position'] and first['slots']==old['slots'],'Cold reopened observed inventory/capacity differs')
        observations.append(first)
    identity_equal(topology,bridge.topology())
    errors=bridge.call('/log',{'since':initial,'level':'error','limit':200});require(isinstance(errors.get('entries'),list),'Native log observation unavailable');require(not errors['entries'],'New game errors')
    result={'phase':'cold-verified' if prior else 'prepared-open-and-warm-verified','manifestSha256':digest(args.manifest),'gameName':manifest['gameName'],'savePath':str(save),'topology':topology,'sessionPid':bridge.session['pid'],'sessionStartedUtc':bridge.session['startedUtc'],'observations':observations,'freshCoverage':[c['id'] for c in cases if c['provenance']=='fresh_natural'],'savedFixtureCoverage':[c['id'] for c in cases if c['provenance']=='saved_existing'],'nativeMetadataEquality':'NOT_OBSERVED; UI exposes name/count/quality only','legacyMigration':'NOT_PROVEN without separate pre-fix saved-grid witness','coldPersistence':'OBSERVED_SAME_COORDINATES_LOGICAL_SLOTS_AFTER_NEW_PROCESS' if prior else 'NOT_VERIFIED'}
    write(bridge.output/'receipt.json',result)
    print(result['phase']+'; '+str(len(observations))+' cases; see receipt limits')

def seal(args):
    receipt=read(args.prior);require(receipt['phase']=='prepared-open-and-warm-verified','Need preparation receipt')
    require(not process_alive(receipt['sessionPid']),'Old process still active; lead must quit cleanly (driver never quits)')
    save=Path(receipt['savePath']);files=[p for p in save.rglob('*') if p.is_file()]
    require(any(p.suffix.lower()=='.7rg' for p in files),'No saved region artifacts')
    receipt['closedSaveArtifacts']=[{'path':str(p.relative_to(save)),'sha256':digest(p),'bytes':p.stat().st_size} for p in files]
    receipt['phase']='cold-sealed';write(args.output,receipt);print('cold-sealed; lead must start same disposable save in new process')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);sub=parser.add_subparsers(dest='command',required=True)
    p=sub.add_parser('run');p.add_argument('--phase',choices=['prepare','cold-verify'],required=True);p.add_argument('--session',required=True);p.add_argument('--manifest',required=True);p.add_argument('--mod-root',required=True);p.add_argument('--output',required=True);p.add_argument('--prior');p.set_defaults(func=run)
    p=sub.add_parser('seal');p.add_argument('--prior',required=True);p.add_argument('--output',required=True);p.set_defaults(func=seal)
    args=parser.parse_args();args.func(args)

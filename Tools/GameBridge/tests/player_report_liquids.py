"""Prepared real-input regressions. Does not launch, build, reset or quit the game.
Lead owns the enclosing launch/try/finally-quit session. Corrected transfer procedure:
_Documentation/LiquidReports20261029/NativeWaterReview/REVIEW.md.
"""
import argparse, collections, json, pathlib, subprocess, time

def strings(value):
    if isinstance(value,str): yield value
    elif isinstance(value,dict):
        for child in value.values(): yield from strings(child)
    elif isinstance(value,list):
        for child in value: yield from strings(child)

class Run:
    def __init__(self,args):
        self.a=args;self.n=0;self.out=pathlib.Path(args.out);self.out.mkdir(parents=True,exist_ok=True)
        self.mouse_held=None;self.checkpoints=[]
    def call(self,*args):
        self.n+=1
        with (self.out/'commands.jsonl').open('a',encoding='utf-8') as log:
            log.write(json.dumps({'record':f'{self.n:03}.json','args':list(args),'phase':'issued'})+'\n')
        try:
            p=subprocess.run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',self.a.bridge,*map(str,args)],capture_output=True,text=True,timeout=20 if args[0]=='release' else 70)
        except Exception as error:
            with (self.out/'commands.jsonl').open('a',encoding='utf-8') as log:
                log.write(json.dumps({'record':f'{self.n:03}.json','phase':'transport-failed','error':str(error)})+'\n')
            raise
        (self.out/f'{self.n:03}.json').write_text(p.stdout,encoding='utf-8')
        with (self.out/'commands.jsonl').open('a',encoding='utf-8') as log:
            log.write(json.dumps({'record':f'{self.n:03}.json','phase':'completed','exitCode':p.returncode})+'\n')
        if p.returncode: raise RuntimeError(f'Bridge failed: {args}: {p.stdout[-1500:]} {p.stderr[-500:]}')
        value=json.loads(p.stdout)
        if isinstance(value,dict) and value.get('ok') is False: raise RuntimeError(str(value))
        return value
    def observe(self,source=False):
        suffix=''
        if source and self.a.source=='tank': suffix=' tank '+' '.join(map(str,self.a.xyz))
        if source and self.a.source=='sink': suffix=' sink '+str(self.a.entity)
        data=self.call('console','rbliquidobserve'+suffix)
        lines=[line.split('LIQUID_OBSERVE ',1)[1] for line in strings(data) if line.startswith('LIQUID_OBSERVE ')]
        assert len(lines)==1, 'Exactly one read-only observation required'
        result=json.loads(lines[0]);assert result['observationOnly'] and result['schema']==1
        return result
    def row(self,snapshot,slot):
        rows=[r for r in snapshot['toolbelt'] if r['slot']==slot]
        assert len(rows)==1, f'Expected one liquid/empty jar in owned toolbelt slot {slot}'
        return rows[0]
    def select(self,slot):
        self.call('select',f'slot={slot+1}')
        assert self.observe()['selectedSlot']==slot, 'Selected slot did not change'
    def close_crafting(self):
        # Escape can first dismiss a selected item/tooltip without closing the
        # crafting window. Inspect the real UI instead of assuming one key did it.
        for _ in range(4):
            if 'crafting' not in self.call('uitree').get('openWindows',[]): return
            self.call('key','name=Escape');time.sleep(.25)
        raise AssertionError('Crafting window did not close; no gameplay input attempted')
    def sip(self,slot,name):
        before=self.row(self.observe(),slot);assert before['name']==name and before['ml']==500 and before['count']==1
        self.call('console','rbmet autosip off','rbmet clearstomach','rbmet set hydration 20')
        self.select(slot);self.call('press','action=Primary','seconds=0.2');time.sleep(4)
        after=self.row(self.observe(),slot)
        assert after['name']==name and after['ml']==375 and after['count']==1, 'Real sip must produce exactly 375/500 mL'
    @staticmethod
    def identity(row):
        return tuple(row[key] for key in ['name','ml','capacityMl','profile','seed','nativeHash'])
    @classmethod
    def units(cls,snapshot):
        result=collections.Counter()
        for group in ['toolbelt','backpack','cursor']:
            for row in snapshot[group]:result[cls.identity(row)]+=row['count']
        return result
    def maybe_row(self,snapshot,slot):
        rows=[row for row in snapshot['toolbelt'] if row['slot']==slot]
        assert len(rows)<=1,'Duplicate owned slot observation'
        return rows[0] if rows else None
    def same_row(self,actual,pinned,count=None):
        return actual is not None and self.identity(actual)==self.identity(pinned) and actual['count']==(pinned['count'] if count is None else count)
    def cursor(self,snapshot,pinned,count=None):
        return not snapshot['cursorEmpty'] and len(snapshot['cursor'])==1 and self.same_row(snapshot['cursor'][0],pinned,count)
    def unchanged_elsewhere(self,before,after,slots):
        def cells(s):return {(group,row['slot']):row for group in ['toolbelt','backpack'] for row in s[group] if group!='toolbelt' or row['slot'] not in slots}
        assert cells(before)==cells(after),'Unrelated liquid slot changed'
        assert self.units(before)==self.units(after),'Native image/volume/profile/count conservation failed'
    def checkpoint(self,name,snapshot):
        self.checkpoints.append({'stage':name,'observationRecord':f'{self.n:03}.json','snapshot':snapshot})
        (self.out/'checkpoints.json').write_text(json.dumps(self.checkpoints,indent=2),encoding='utf-8')
    def node(self,path,empty=False):
        tree=self.call('uitree');assert not tree.get('truncated',False),'Truncated UI tree cannot pin a slot'
        matches=[node for node in tree.get('nodes',[]) if node.get('path')==path]
        assert len(matches)==1,'Exact visible inventory slot path required'
        node=matches[0]
        assert node.get('ctrl') in ['XUiC_ItemStack','XUiC_RebirthToolbeltItemSlot'] and node.get('clickable'), 'Current native item-slot controller required'
        assert node.get('enabled',True),'Slot is disabled'
        x,y,w,h=node['rect'];sw,sh=tree['screen']
        assert w>=20 and h>=20 and x>=0 and y>=0 and x+w<=sw and y+h<=sh,'Slot must be fully visible'
        if empty:assert not node.get('item'),'Positive control destination must really be empty, including non-liquids'
        return node
    def hold_pickup(self,before,slot,path,button='LeftClick',take=None):
        pin=self.row(before,slot);take=pin['count'] if take is None else take
        assert before['cursorEmpty'] and before['cursor']==[] and self.mouse_held is None
        node=self.node(path);assert node['item']['name']==pin['name'] and node['item']['count']==pin['count']
        x,y,w,h=node['rect'];offset=min(8,w*.25)
        assert offset>4,'Visible slot too small for native pickup snap distance'
        self.call('hover',f'path={path}','seconds=0.15')
        # Native Update picks up only while held movement passes PickupSnapDistance.
        action='gui.'+button;self.mouse_held=action
        reply=self.call('press',f'action={action}','hold=1');assert self.native_action_ack(action,reply.get('holding'))
        self.call('hover',f'x={x+w/2+offset:.3f}',f'y={y+h/2:.3f}','seconds=0.15')
        picked=self.observe()
        assert self.cursor(picked,pin,take),'NO PICKUP: exact pinned native source must be on cursor before targeting destination'
        remaining=pin['count']-take
        if remaining==0:assert self.maybe_row(picked,slot) is None,'Native pickup left source occupied'
        else:assert self.same_row(self.maybe_row(picked,slot),pin,remaining),'Native split changed its retained source'
        self.unchanged_elsewhere(before,picked,{slot});self.checkpoint('actual-native-pickup-'+button,picked)
        return picked,pin
    def destination_held(self,picked,path):
        self.node(path);self.call('hover',f'path={path}','seconds=0.15');held=self.observe()
        assert self.units(held)==self.units(picked) and held['toolbelt']==picked['toolbelt'] and held['backpack']==picked['backpack'] and held['cursor']==picked['cursor'],'Destination changed before release'
        self.checkpoint('destination-before-native-release',held);return held
    @staticmethod
    def native_action_ack(action,label):
        # Requests resolve GUI fields; replies contain PlayerAction.Name.
        # Current native b9 names deliberately differ in spacing.
        expected={'gui.LeftClick':'GUI Left Click','gui.RightClick':'GUI RightClick'}.get(action)
        return expected is not None and isinstance(label,str) and label==expected
    def release_mouse(self):
        if self.mouse_held is not None:
            action=self.mouse_held
            reply=self.call('release',f'action={action}')
            assert self.native_action_ack(action,reply.get('released')),'Native release confirmation required'
            self.mouse_held=None
    def cleanup_mouse(self):
        if self.mouse_held is None:return
        failures=[]
        for _ in range(2):
            try:self.release_mouse()
            except Exception as error:failures.append(str(error))
            else:
                try:self.checkpoint('failure-cleanup-after-native-release',self.observe())
                except Exception as error:(self.out/'cleanup-observation-error.json').write_text(json.dumps({'releaseConfirmed':True,'error':str(error)}))
                return
        (self.out/'cleanup-failure.json').write_text(json.dumps({'unconfirmedAction':self.mouse_held,'failures':failures,'requiredAction':'Lead-owned outer finally must quit the test game.'},indent=2))
        raise RuntimeError('Mouse release unconfirmed; lead-owned outer finally must quit the test game.')
    def pulse_mouse(self,button):
        reply=self.call('press',f'action=gui.{button}','seconds=0.15')
        assert self.native_action_ack('gui.'+button,reply.get('pressed')) and reply.get('inputWitness',{}).get('injectedFrames',0)>0,'Native GUI input witness required'
    def release_destination(self,held,button,completed,unchanged=None):
        # A destination may retain lastClicked from an earlier local press.
        # Accept ONLY the caller's exact complete transition, or exact unchanged
        # release followed by one local pulse. Never pulse an already-done drop.
        assert self.mouse_held=='gui.'+button and not held['cursorEmpty'] and len(held['cursor'])==1,'Exact held destination gesture required'
        self.release_mouse();assert self.mouse_held is None
        released=self.observe()
        try:completed(released)
        except AssertionError:
            assert all(released[k]==held[k] for k in ['toolbelt','backpack','cursor','cursorEmpty']),'Unexpected release-only mutation; no destination pulse attempted'
            if unchanged is not None:unchanged()
            self.checkpoint('release-only-before-destination-pulse',released)
            self.pulse_mouse(button)
            finished=self.observe();completed(finished)
            return finished
        self.checkpoint('release-completed-without-extra-pulse',released)
        return released
    def destination_completed(self,snapshot,before,source,target,pin,kind,target_pin=None):
        if kind=='split':
            assert snapshot['cursorEmpty'] and snapshot['cursor']==[] and self.same_row(self.maybe_row(snapshot,source),pin,1) and self.same_row(self.maybe_row(snapshot,target),pin,1),'Positive split/place must produce real identical1+1 stacks'
        elif kind=='merge':
            assert snapshot['cursorEmpty'] and snapshot['cursor']==[] and self.maybe_row(snapshot,source) is None and self.same_row(self.maybe_row(snapshot,target),pin,2),'Positive compatible merge must consume cursor and produce count2 in target'
        elif kind=='swap':
            assert self.maybe_row(snapshot,source) is None and self.same_row(self.maybe_row(snapshot,target),pin) and self.cursor(snapshot,target_pin),'Incompatible LEFT release must actually swap, retaining displaced target on cursor'
        else:raise AssertionError('Unknown completed transition')
        self.unchanged_elsewhere(before,snapshot,{source,target})
    def positive_control(self):
        source,target=self.a.control_slots;source_path,target_path=self.a.control_paths
        before=self.observe();pin=self.row(before,source)
        assert pin['name']==self.a.item and pin['ml']==500 and pin['count']==2,'Stage a full count2 control stack'
        assert self.maybe_row(before,target) is None;self.node(target_path,empty=True)
        picked,pin=self.hold_pickup(before,source,source_path,'RightClick',1)
        self.destination_held(picked,target_path);self.release_destination(picked,'RightClick',lambda s:self.destination_completed(s,before,source,target,pin,'split'));time.sleep(.2);split=self.observe()
        assert split['cursorEmpty'] and split['cursor']==[] and self.same_row(self.row(split,source),pin,1) and self.same_row(self.row(split,target),pin,1),'Positive split/place must produce real identical1+1 stacks'
        self.unchanged_elsewhere(before,split,{source,target});self.checkpoint('positive-right-split-and-place',split)
        picked,pin=self.hold_pickup(split,source,source_path)
        self.destination_held(picked,target_path);self.release_destination(picked,'LeftClick',lambda s:self.destination_completed(s,before,source,target,pin,'merge'));time.sleep(.2);merged=self.observe()
        assert merged['cursorEmpty'] and merged['cursor']==[] and self.maybe_row(merged,source) is None and self.same_row(self.row(merged,target),pin,2),'Positive compatible merge must consume cursor and produce count2 in target'
        self.unchanged_elsewhere(before,merged,{source,target});self.checkpoint('positive-compatible-merge',merged)
    def pair(self,control_preverified=False):
        a,b=self.a.slots
        if control_preverified:
            assert a!=b,'Pair requires distinct slots'
            assert [c['stage'] for c in self.checkpoints[:2]]==['positive-right-split-and-place','positive-compatible-merge'],'Sequential fixture requires this run\'s completed native control witnesses'
        else:
            assert len(set([a,b,*self.a.control_slots]))==4,'Use four distinct owned fixture slots'
        self.close_crafting();self.sip(a,self.a.item)
        full=self.row(self.observe(),b);assert full['name']==self.a.item and full['ml']==500 and full['count']==1
        self.call('key','name=Tab');assert 'crafting' in self.call('uitree').get('openWindows',[])
        paths=self.a.paths
        try:
            if not control_preverified:self.positive_control()
            for ml in [375,500]:
                before=self.observe();assert before['cursorEmpty'] and before['cursor']==[]
                index=next(i for i,slot in enumerate([a,b]) if self.row(before,slot)['ml']==ml);other=1-index
                source,target=[a,b][index],[a,b][other];target_pin=self.row(before,target)
                picked,source_pin=self.hold_pickup(before,source,paths[index])
                assert self.same_row(self.row(picked,target),target_pin),'Pinned target changed during pickup'
                self.destination_held(picked,paths[other]);self.release_destination(picked,'LeftClick',lambda s:self.destination_completed(s,before,source,target,source_pin,'swap',target_pin));time.sleep(.2);swapped=self.observe()
                assert self.maybe_row(swapped,source) is None and self.same_row(self.row(swapped,target),source_pin) and self.cursor(swapped,target_pin),'Incompatible LEFT release must actually swap, retaining displaced target on cursor'
                self.unchanged_elsewhere(before,swapped,{source,target});self.checkpoint('incompatible-swap-from-'+str(ml),swapped)
                # The displaced jar provides the opposite incoming direction for right-drop.
                self.pulse_mouse('RightClick');refused=self.observe()
                assert refused['toolbelt']==swapped['toolbelt'] and refused['backpack']==swapped['backpack'] and refused['cursor']==swapped['cursor'] and not refused['cursorEmpty'],'Incompatible RIGHT single-drop must retain exact cursor and target'
                self.checkpoint('incompatible-right-refusal-from-'+str(target_pin['ml']),refused)
                self.node(paths[index],empty=True);self.call('hover',f'path={paths[index]}','seconds=0.15');self.pulse_mouse('LeftClick');finished=self.observe()
                assert finished['cursorEmpty'] and finished['cursor']==[] and self.same_row(self.row(finished,source),target_pin) and self.same_row(self.row(finished,target),source_pin),'Displaced vessel must return to empty origin with swapped slot positions'
                self.unchanged_elsewhere(before,finished,{source,target});self.checkpoint('completed-actual-swap-from-'+str(ml),finished)
            self.call('screenshot','liquid_real_transfer_'+self.a.item);self.close_crafting()
        finally:self.cleanup_mouse()
    def refill(self):
        slot,empty=self.a.slots;before=self.observe(self.a.source!='natural');jar=self.row(before,slot)
        assert before['cursorEmpty'] and jar['count']==1 and jar['ml']==375 and jar['name'] in ['drinkJarBoiledWater','drinkJarPureMineralWater']
        if self.a.source!='natural':
            assert before['authoritative'] and before['source']['ml']==500 and before['source']['profile']=='unsafeWater', 'Stage exactly 500 mL finite supply'
        self.select(slot);self.call('press','action=Secondary','seconds=0.2');time.sleep(5)
        after=self.observe(self.a.source!='natural');filled=self.row(after,slot)
        assert filled['name']=='drinkJarRiverWater' and filled['profile']=='unsafeWater' and filled['ml']==500 and filled['count']==1
        assert filled['seed']==jar['seed'], 'Physical vessel seed changed'
        if self.a.source!='natural':
            assert after['source']['ml']==375 and after['source']['ml']+filled['ml']==before['source']['ml']+jar['ml']
            # All-or-nothing shortage: 375 available cannot complete an empty 500 mL jar.
            assert self.row(after,empty)['name']=='drinkJarEmpty' and self.row(after,empty)['count']==1
            self.select(empty);short=self.observe(True);self.call('press','action=Secondary','seconds=0.2');time.sleep(5)
            failed=self.observe(True);assert failed['source']==short['source'] and self.row(failed,empty)==self.row(short,empty), 'Shortage changed source or vessel'
        self.call('screenshot','liquid_refill_'+self.a.source)
    def run(self):
        mark=self.call('log','-Limit','1');seq=mark['lastLogSeq']
        state=self.call('state','-Sections','world,inventory,player')
        names=[v['gameName'] for v in self.walk(state) if 'gameName' in v]
        assert names==[self.a.confirm_disposable] and self.a.confirm_disposable.startswith(('CodexTest','RebirthBridge')), 'Exact disposable save mismatch'
        if self.a.mode=='click-pair': self.pair()
        elif self.a.mode=='refill': self.refill()
        else: self.observe(self.a.source!='natural')
        errors=self.call('errors','-Since',str(seq))
        assert not errors.get('entries',[]), 'New native errors require review'
        print('PASS prepared native '+self.a.mode+'; evidence='+str(self.out))
    def walk(self,v):
        if isinstance(v,dict):
            yield v
            for x in v.values(): yield from self.walk(x)
        elif isinstance(v,list):
            for x in v: yield from self.walk(x)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('mode',choices=['observe','click-pair','refill']);p.add_argument('--bridge',required=True);p.add_argument('--confirm-disposable',required=True);p.add_argument('--out',required=True)
    p.add_argument('--item',choices=['drinkJarBoiledWater','drinkJarCoffee'],default='drinkJarBoiledWater')
    p.add_argument('--slots',type=int,nargs=2,default=[0,1],help='Zero-based owned toolbelt slots')
    p.add_argument('--paths',nargs=2,default=['toolbelt/windowToolbelt/toolbelt/inventory/0','toolbelt/windowToolbelt/toolbelt/inventory/1'])
    p.add_argument('--control-slots',type=int,nargs=2,default=[2,3],help='Positive-control full stack2 source and truly empty destination; zero-based')
    p.add_argument('--control-paths',nargs=2,default=['toolbelt/windowToolbelt/toolbelt/inventory/2','toolbelt/windowToolbelt/toolbelt/inventory/3'])
    p.add_argument('--source',choices=['natural','tank','sink'],default='natural');p.add_argument('--xyz',type=int,nargs=3);p.add_argument('--entity',type=int)
    a=p.parse_args()
    if a.source=='tank' and a.xyz is None:p.error('--xyz required for tank')
    if a.source=='sink' and a.entity is None:p.error('--entity required for sink')
    Run(a).run()

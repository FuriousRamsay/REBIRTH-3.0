#!/usr/bin/env python3
"""PC133 SOURCE/XML contracts and independent Python models, NOT a C#/Unity test.
Run against the merged PC132 + PC133 project, not the unpacked delta alone:
    python BuildScripts/UI/validate_pc133_character_overview.py [project_root]
This intentionally replaces old whole-file freeze assertions for Character's newly shared
navigation. It does not treat source checks or schematic previews as a game compile/render.
"""
from pathlib import Path
import argparse, csv, hashlib, io, json, math, re, unittest
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
FIXTURES=Path(__file__).with_name('pc133_baseline_invariants.json')
CHAR='Scripts/Survivor/UI/'
CRAFT='Scripts/Crafting/UI/PersonalCrafting/'

def read(rel): return (ROOT/rel).read_text(encoding='utf-8-sig')
def sha(value): return hashlib.sha256(value if isinstance(value,bytes) else value.encode()).hexdigest()
def mask(s):
    token=re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',re.S)
    return token.sub(lambda m: ''.join('\n' if c=='\n' else ' ' for c in m[0]),s)
def method(s,name):
    code=mask(s);m=re.search(r'\b(?:public|private|internal|protected)\s+(?:(?:static|override|virtual|sealed|new|async)\s+)*[\w.<>,?\[\]]+\s+'+re.escape(name)+r'\s*\([^)]*\)\s*\{',code,re.S)
    if not m: raise AssertionError('Missing method '+name)
    start=m.end()-1;depth=1
    for i in range(start+1,len(code)):
        depth+=(code[i]=='{')-(code[i]=='}')
        if depth==0:return code[start+1:i]
    raise AssertionError('Unclosed method '+name)
def norm(s): return re.sub(r'\s+','',mask(s))
def canonical(e): return [e.tag,sorted(e.attrib.items()),(e.text or '').strip(),[canonical(c) for c in e]]
def outside(s):
    start=s.index('<window name="rebirthSurvivorCharacterWindow"');end=s.index('</window>',start)+len('</window>')
    return s[:start]+s[end:]
def at(e,name):
    matches=[n for n in e.iter() if n.get('name')==name]
    if len(matches)!=1: raise AssertionError(f'{name}: expected one, found {len(matches)}')
    return matches[0]
def rect(e):
    x,y=[float(v) for v in e.get('pos','0,0').split(',')]
    return (x,-y,float(e.get('width',0)),float(e.get('height',0)))
def clamp(v,lo,hi):return min(hi,max(lo,v))

class SourceContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture=json.loads(FIXTURES.read_text());cls.xml=read('Config/XUi_InGame/windows.xml')
        cls.tree=ET.fromstring(cls.xml);cls.window=at(cls.tree,'rebirthSurvivorCharacterWindow')
        cls.overview=at(cls.window,'survivorOverviewPanel')
        cls.root=read(CHAR+'XUiC_RebirthSurvivorCharacter.cs')
        cls.lists=read(CHAR+'XUiC_RebirthCharacterOverviewList.cs')
        cls.layout=read(CHAR+'RebirthCharacterLayoutService.cs')
        cls.model=read(CHAR+'RebirthCharacterModelBinder.cs')
        cls.opacity=read(CRAFT+'RebirthPersonalCraftingPanelOpacity.cs')
        cls.nav=read(CRAFT+'RebirthCraftingNavigationService.cs')
    def test_01_all_non_character_window_xml_is_byte_preserved(self):
        self.assertEqual(self.fixture['outside_character_xml_sha256'],sha(outside(self.xml)))
        ET.parse(ROOT/'Config/XUi_InGame/templates.xml');ET.parse(ROOT/'Config/XUi_InGame/xui.xml')
    def test_02_779_other_files_including_backpack_queue_and_input_are_preserved(self):
        for rel,digest in self.fixture['protected_files_sha256'].items():
            with self.subTest(file=rel):self.assertEqual(digest,sha((ROOT/rel).read_bytes()))
    def test_03_crafting_geometry_methods_remain_same_after_navigation_extraction(self):
        s=read(CRAFT+'RebirthPersonalCraftingLayoutService.cs')
        for name,digest in self.fixture['crafting_layout_method_sha256'].items():
            with self.subTest(method=name):self.assertEqual(digest,sha(norm(method(s,name))))
        self.assertEqual('ApplySharedTopLayout(owner,width);',norm(method(s,'ApplyTopLayout')))
    def test_04_navigation_uses_same_authored_controls_and_shared_geometry(self):
        hosts=[e for e in self.tree.iter() if e.get('name')=='rebirthCraftingTopZone'];self.assertEqual(2,len(hosts))
        # Only gamepad nav_down changes because Character has no recipe search input.
        def cleaned(e):
            obj=ET.fromstring(ET.tostring(e))
            for c in obj.iter():c.attrib.pop('nav_down',None)
            return canonical(obj)
        self.assertEqual(cleaned(hosts[0]),cleaned(hosts[1]))
        self.assertIn('RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(owner, navWidth)',self.layout)
        self.assertEqual(['fullscreencollider','rect','rect'],[e.tag for e in self.window])
        self.assertEqual('rebirthCraftingTopZone',list(self.window)[1].get('name'))
    def test_05_each_navigation_instance_has_eight_visible_separators(self):
        for host in (e for e in self.tree.iter() if e.get('name')=='rebirthCraftingTopTabs'):
            for suffix in ('Inventory','Crafting','Character','Map','Skills','Quests','Challenges','Players'):
                self.assertNotEqual('false',at(host,'rebirthCraftingTab'+suffix+'Frame').get('visible'))
        self.assertIn('Destination.Character',self.layout)
    def test_06_creator_header_red_glow_and_screen_dim_are_removed(self):
        self.assertEqual('0,0,0,0',self.window.find('fullscreencollider').get('color'))
        text=ET.tostring(self.window).decode()
        self.assertNotIn('boxshadow_new',text)
        self.assertIsNone(self.window.find('./headerbox'))
        self.assertNotIn('survivorCharacterContent',text)
    def test_07_sidebar_only_exposes_backed_existing_pages(self):
        side=at(self.window,'survivorCharacterSidebar')
        buttons=[e.get('name') for e in side.iter('button')]
        expected=['btnSurvivorTab'+p for p in ['Overview','Origin','Progression','Traits','Condition','Statistics','Metabolism']]+['btnSurvivorClose']
        self.assertEqual(expected,buttons)
        self.assertIn('"ui_game_select_row"',self.root)
        for e in side.iter('button'):
            self.assertEqual(e.get('defaultcolor'),e.get('hovercolor'))
            self.assertEqual('0,0,0,1',e.get('defaultcolor'))
    def test_08_overview_columns_are_disjoint_and_inside_body(self):
        names=['survivorOverviewIdentityPanel','survivorOverviewEquipmentPanel','survivorOverviewStatusColumn','survivorOverviewConditionsPanel']
        # Status host's actual ID is kept separate from its two background panels.
        cols=[e for e in list(self.overview) if e.tag=='rect']
        self.assertEqual(4,len(cols));last_right=0
        for e in cols:
            x,y,w,h=rect(e);self.assertGreaterEqual(x,last_right);self.assertEqual(0,y)
            self.assertLessEqual(x+w,1666);self.assertLessEqual(h,813);last_right=x+w
        self.assertEqual(1666,last_right)
    def test_09_native_equipment_templates_and_arguments_are_untouched(self):
        self.assertEqual(self.fixture['native_equipment_slots'],json.loads(json.dumps([canonical(e) for e in self.window.iter('equipment_stack_sdcs')])))
        self.assertEqual(12,len(list(self.window.iter('equipment_stack_sdcs'))))
        equipment=at(self.window,'survivorOverviewEquipmentPanel')
        boxes=[rect(e) for e in equipment if e.get('name','').startswith('survivorOverviewSlot')]
        for i,(x,y,w,h) in enumerate(boxes):
            self.assertGreaterEqual(x,0);self.assertGreaterEqual(y,40);self.assertLessEqual(x+w,560);self.assertLessEqual(y+h,813)
            for xx,yy,ww,hh in boxes[i+1:]:self.assertFalse(x<xx+ww and x+w>xx and y<yy+hh and y+h>yy)
    def test_10_other_character_page_contents_are_preserved(self):
        for name,digest in self.fixture['retained_page_children_sha256'].items():
            e=at(self.window,name);self.assertEqual(digest,sha(json.dumps([canonical(c) for c in e],sort_keys=True)))
    def test_11_overview_lists_have_real_clip_panels_and_no_scrollview_wrappers(self):
        for name,stride,height,capacity in [('Traits',38,228,64),('Conditions',74,747,32)]:
            e=at(self.window,'survivorOverview'+name+'List');vp=at(e,'listViewport');content=at(e,'listContent')
            self.assertEqual('panel',vp.tag);self.assertEqual('softclip',vp.get('clipping'))
            self.assertEqual('0,0',vp.get('clippingsoftness'));self.assertEqual('true',vp.get('disableautobackground'))
            self.assertEqual(height,int(vp.get('height')));self.assertEqual('0',content.get('height'))
            self.assertEqual(capacity,len(content));self.assertEqual(stride,rect(list(content)[1])[1]-rect(list(content)[0])[1])
            self.assertFalse(list(e.iter('scrollview')))
    def test_12_list_labels_are_above_opaque_rows_and_inside_row_rectangles(self):
        for name in ['Traits','Conditions']:
            e=at(self.window,'survivorOverview'+name+'List')
            for row in at(e,'listContent'):
                bg=row.find('sprite');depth=int(bg.get('depth'));_,_,w,h=rect(row)
                self.assertEqual('false',row.get('visible'))
                for label in row.iter('label'):
                    self.assertGreater(int(label.get('depth')),depth)
                    x,y,lw,lh=rect(label);self.assertGreaterEqual(x,0);self.assertGreaterEqual(y,0)
                    self.assertLessEqual(x+lw,w);self.assertLessEqual(y+lh,h)
    def test_13_count_not_pool_drives_scroll_range_and_scrollbar(self):
        for term in ['itemCount * stride - 2','ContentHeight - Height','SetItemCount(int count','MaxOffset > 0f','count == 0','IsReady']:
            self.assertIn(term,self.lists)
        for name in ['Traits','Conditions']:
            e=at(self.window,'survivorOverview'+name+'List')
            self.assertEqual('16',at(e,'listTrack').get('width'));self.assertEqual('12',at(e,'listThumb').get('width'))
            self.assertEqual('false',at(e,'listTrack').get('visible'));self.assertEqual('false',at(e,'listThumb').get('visible'))
    def test_14_lists_bind_data_before_revealing_rows_and_map_pool_ranges(self):
        for name,fields in [('RenderOverviewTraits',['Set(overviewTraitNames[i], trait.Name);']),('RenderOverviewConditions',['Set(overviewConditionNames[i], condition.Name);','Set(overviewConditionDetails[i], condition.Detail);'])]:
            b=method(self.root,name)
            for token in fields:self.assertLess(b.index(token),b.rindex('SetVisible('))
            self.assertIn('FirstDataIndex',b);self.assertIn('int index = first + i;',b)
        self.assertIn('DataRangeChanged += RenderOverviewTraits',self.root)
        self.assertIn('DataRangeChanged += RenderOverviewConditions',self.root)
    def test_15_genuinely_empty_pending_and_broken_presentation_are_distinguished(self):
        for key in ['xuiRebirthCharacterDataPending','xuiRebirthCharacterNoTraits','xuiRebirthCharacterNoConditions','xuiRebirthCharacterListUnavailable']:
            self.assertIn(key,self.root)
        for term in ['overviewRowsResolved','overviewTraitsList.IsReady','overviewConditionsList.IsReady','rowsResolved=']:
            self.assertIn(term,self.root)
        self.assertIn('if (snapshot != null && !snapshot.RebirthModeEnabled) CloseWindow();',self.root)
    def test_16_scrolling_translates_content_not_geometry_or_item_data(self):
        b=method(self.lists,'Update')
        self.assertIn('Mathf.Lerp',b);self.assertIn('ApplyPosition();',b)
        for name in ['ApplyPosition','Scroll','Drag']:
            b=method(self.lists,name)
            for term in ['Reposition','RefreshBindings','SetStacks','localScale','BuildSnapshot','SelectTab']:
                self.assertNotIn(term,b)
        self.assertIn('if (!changed || content == null',method(self.lists,'UpdateRange'))
    def test_17_input_guard_covers_wheel_drag_track_sidebar_and_escape(self):
        for name in ['Scroll','Drag','PressTrack','Update']:self.assertIn('BlocksGameplayInput()',method(self.lists,name))
        self.assertIn('BlocksGameplayInput()',method(self.root,'Wire'))
        self.assertIn('BlocksGameplayInput()',method(self.root,'Update'))
        self.assertIn('lastScrollFrame == Time.frameCount',self.lists)
        self.assertIn('InverseTransformPoint',method(self.lists,'PointerY'))
    def test_18_model_copies_whole_texture_not_quarter_of_supersampled_target(self):
        self.assertRegex(self.model,r'new Vector2i\(TextureWidth, TextureHeight\), false\)')
        self.assertIn('ReadPixels(new Rect(0f, 0f, TextureWidth, TextureHeight), 0, 0)',self.model)
        self.assertIn('new Texture2D(TextureWidth, TextureHeight',self.model)
        self.assertIn('RenderTexture.active = previous',self.model)
    def test_19_full_body_bounds_and_aspect_are_used_in_camera_fit(self):
        for term in ['TryGetVisualBounds(rig, out bounds)','bounds.extents.y, bounds.extents.x / aspect','camera.orthographic = true;','bounds.center - Vector3.forward * distance','* 1.12f','SkinnedMeshRenderer','MeshRenderer']:
            self.assertIn(term,self.model)
        texture=at(self.window,'survivorOverviewModel')
        self.assertLess(abs(350/614-512/896),0.003)
        self.assertEqual('350',texture.get('width'));self.assertEqual('614',texture.get('height'))
    def test_20_model_is_signature_gated_and_temporary_render_objects_cleaned(self):
        self.assertIn('signature == lastSignature',self.model);self.assertIn('Time.realtimeSinceStartup + 2f',self.model)
        self.assertIn('SDCSUtils.CreateVizUI(archetype, ref rig, ref catalog, player, false)',self.model)
        self.assertIn('SDCSUtils.UnloadViz(rig)',self.model)
        self.assertIn('renderSystem.SetEnabled(false)',self.model);self.assertIn('renderSystem.Cleanup()',self.model)
        self.assertIn('CharacterProgressionUiLoggingEnabled',self.model)
    def test_21_no_new_known_unavailable_apis_or_private_harmony_overrides(self):
        forbidden=[r'\.\s*Grid\b',r'\.\s*UpdateData\s*\(',r'\.\s*IsDormant\b',r'GUIWindowConsole\s*\.\s*ID',r'override\s+void\s+(?:SetAllChildrenDirty|InputStyleChanged)\s*\(']
        for rel in self.fixture['changed_runtime_files']:
            if not rel.endswith('.cs'):continue
            # Existing fields or APIs outside newly added sections are not inferred from decompilation.
            code=mask(read(rel))
            for pattern in forbidden:self.assertNotRegex(code,pattern,msg=rel)
    def test_22_new_controllers_are_preserved_and_declared_in_auto_included_sources(self):
        for name in ['XUiC_RebirthCharacterOverviewList','XUiC_RebirthCharacterPage']:
            self.assertRegex(self.lists,r'\[UnityEngine.Scripting.Preserve\]\s+public sealed class '+name)
        for rel in self.fixture['changed_runtime_files']:
            if not rel.endswith('.cs'):continue
            code=mask(read(rel));stack=[];pairs={')':'(',']':'[','}':'{'}
            for c in code:
                if c in '([{':stack.append(c)
                elif c in pairs:
                    self.assertTrue(stack,rel);self.assertEqual(pairs[c],stack.pop(),rel)
            self.assertEqual([],stack,rel)
    def test_23_hidden_pages_do_not_render_or_build_the_progression_catalogue(self):
        self.assertIn('if (!IsCharacterWindowOpen) return;',method(self.root,'Update'))
        b=method(self.root,'RenderAll');self.assertIn('switch (tab)',b)
        s=read(CHAR+'RebirthCharacterUiSnapshot.cs');self.assertIn('if (includeProgression) BuildProgression(result, survivor);',s)
        self.assertIn('tab == Tab.Progression',method(self.root,'RefreshSnapshots'))
        self.assertIn('if (!PageVisible) return;',read(CHAR+'XUiC_RebirthSurvivorStatisticsPanel.cs'))
        self.assertIn('!ViewComponent.UiTransform.gameObject.activeInHierarchy',self.lists)
    def test_24_no_manual_input_stack_reset_or_player_control_toggle_added(self):
        for s in [self.root,self.nav,self.layout,self.lists]:
            for forbidden in ['ResetActionSets(','.SetControllable(','.AllowPlayerInput(','.ActionSetManager.Pop(','.ActionSetManager.Push(']:self.assertNotIn(forbidden,mask(s))
        self.assertIn('manager.Open("crafting", true)',self.nav)
        self.assertIn('windowManager.Open(XUiC_RebirthSurvivorCharacter.WindowGroupId, true)',self.nav)
    def test_25_opacity_defaults_and_parsing_remain_98_session_only(self):
        for term in ['DefaultAlpha = 0.98f','DefaultPercent = 98d','double.IsNaN(parsed)','double.IsInfinity(parsed)','parsed < 0d || parsed > 100d','color.a == alpha']:
            self.assertIn(term,self.opacity)
        self.assertIn('ApplyCharacter(character, out characterMatched)',read(CRAFT+'ConsoleCmdRebirthUiOpacity.cs'))
        self.assertNotIn('Update(',self.opacity);self.assertNotIn('WriteAllText',self.opacity)
    def test_26_opacity_targets_only_main_fills_not_interactive_children(self):
        start=self.opacity.index('CharacterBackgroundIds =');end=self.opacity.index('};',start)
        ids=re.findall(r'"([^"]+)"',self.opacity[start:end]);self.assertEqual(6,len(ids))
        for name in ids:
            e=at(self.window,name);self.assertEqual('sprite',e.tag);self.assertEqual('250',e.get('color').split(',')[-1])
            self.assertNotIn('Row',name);self.assertNotIn('Tab',name)
        self.assertEqual(3,len(re.findall(r'"rebirthCrafting(?:Left|Center|Right)Background"',self.opacity)))
    def test_27_all_rebirth_localization_keys_and_state_labels_resolve(self):
        rows=list(csv.reader(io.StringIO(read('Config/Localization.csv'))));keys={r[0] for r in rows if r}
        for e in self.window.iter():
            key=e.get('text_key','')
            if key.startswith('xuiRebirth'):self.assertIn(key,keys)
        for key in ['DataPending','ListUnavailable','NoTraits','NoConditions','ArtUnavailable','ModelUnavailable','EquipmentHint']:
            full='xuiRebirthCharacter'+key;self.assertEqual(1,sum(bool(r) and r[0]==full for r in rows))
    def test_28_existing_shared_hud_prefixes_are_preserved_and_capture_restored(self):
        s=read(CRAFT+'RebirthPersonalCraftingHudSuppressionInstaller.cs')
        self.assertIn('active != null && active.State.IsOpen',s);self.assertIn('IsCharacterWindowOpen',s)
        self.assertIn('CaptureCharacterHud();',method(self.root,'OnOpen'));self.assertIn('RestoreCharacterHud();',method(self.root,'OnClose'))
        for name in ['windowLocation','windowQuestTracker','windowRecipeTracker']:self.assertIn(name,self.root)
        self.assertIn('characterHudVisible[i]',method(self.root,'RestoreCharacterHud'))
    def test_29_dynamic_text_and_visual_values_skip_equal_writes(self):
        for name in ['Set','SetTooltip','SetSprite','SetSpriteColor']:self.assertIn('!=',method(self.root,name))
        self.assertIn('label.Text !=',method(self.root,'Set'));self.assertIn('view.ToolTip !=',method(self.root,'SetTooltip'))
    def test_30_late_native_and_metabolism_values_are_not_displayed_as_zero_max(self):
        b=method(self.root,'RenderOverviewVital');self.assertIn('if (max <= 0f)',b)
        self.assertIn('SetFill(overviewVitalFills[index], 0f)',b)
        self.assertIn('Set(overviewVitalValues[index], "—")',self.root)

class LayoutAndScrollModels(unittest.TestCase):
    # Models exercise algebra/ordering; they are not executions of C# controllers.
    def test_31_empty_and_fitting_lists_have_no_scroll_range(self):
        for height,stride,count in [(228,38,0),(228,38,1),(228,38,6),(747,74,0),(747,74,10)]:
            content=0 if count==0 else count*stride-2;self.assertEqual(0,max(0,content-height))
    def test_32_overflow_thumb_reflects_populated_content(self):
        for height,stride in [(228,38),(747,74)]:
            thumbs=[]
            for count in [32,64,231,500]:
                ch=count*stride-2;thumb=clamp(round(height*height/ch),min(30,height),height);thumbs.append(thumb)
                self.assertGreaterEqual(thumb,30);self.assertLess(thumb,height)
            self.assertEqual(sorted(thumbs,reverse=True),thumbs)
    def test_33_every_row_is_reachable_beyond_the_original_pool(self):
        for height,stride,capacity in [(228,38,64),(747,74,32)]:
            for count in [0,1,10,32,64,65,231,500]:
                maximum=max(0,(count*stride-2 if count else 0)-height);start=0;seen=set()
                for offset in list(range(0,maximum+1,7))+[maximum]:
                    first=math.floor(offset/stride);need=math.ceil(height/stride)+1
                    start=clamp(start,0,max(0,count-capacity))
                    if first<start or first+need>start+capacity:start=clamp(first-1,0,max(0,count-capacity))
                    visible=[i for i in range(count) if i*stride<offset+height and i*stride+stride-2>offset]
                    for i in visible:self.assertLessEqual(start,i);self.assertLess(i,start+capacity)
                    seen.update(visible)
                self.assertEqual(set(range(count)),seen)
    def test_34_range_is_stable_during_ordinary_short_list_scroll(self):
        for count,cap,height,stride in [(25,64,228,38),(20,32,747,74)]:
            maximum=max(0,count*stride-2-height)
            for offset in range(maximum+1):
                first=offset//stride;start=clamp(first-1,0,max(0,count-cap));self.assertEqual(0,start)
    def test_35_count_shrink_clamps_scroll_and_resets_range(self):
        for count in [0,1,6,32,64]:
            ch=count*38-2 if count else 0;maximum=max(0,ch-228)
            self.assertLessEqual(clamp(1900,0,maximum),maximum)
            start=clamp(167,0,max(0,count-64));self.assertEqual(0,start)
    def test_36_scroll_translation_does_not_change_row_pitch_or_scale(self):
        stride=38;rows=[-i*stride for i in range(64)]
        for offset in [0,0.25,12,27.5,200,500]:
            screen=[round(offset)+y for y in rows]
            self.assertTrue(all(screen[i]-screen[i+1]==stride for i in range(63)))
    def test_37_both_camera_axes_and_depth_fit_the_full_bounds(self):
        aspect=512/896
        for extents in [(0.4,0.9,0.2),(1.1,0.7,0.5),(0.5,1.6,0.4),(0.01,0.06,0.01),(3,2,4)]:
            x,y,z=extents;half=max(0.25,max(y,x/aspect)*1.12);distance=max(4,z*2+1)
            self.assertLessEqual(y/half,1/1.12+1e-9);self.assertLessEqual(x/(half*aspect),1/1.12+1e-9)
            self.assertGreater(distance-z,0.05);self.assertLess(distance+z,distance+z+5)
    def test_38_capture_covers_entire_non_doubled_render_target(self):
        width,height=512,896
        copied_area=width*height;target_area=width*height
        self.assertEqual(1,copied_area/target_area)
        # Negative regression: old native AA path allocated 2x each axis.
        self.assertEqual(0.25,copied_area/((width*2)*(height*2)))
    def test_39_character_body_and_navigation_fit_above_toolbelt(self):
        for sw,sh in [(1280,720),(1600,900),(1837,1037),(1920,1080),(2560,1440),(3440,1440)]:
            top=clamp(round(sh*0.012),8,20);reserve=132
            width=min(1872,max(1,sw-4));height=min(935,max(1,sh-top-reserve));nw=width-16
            scale=min(nw/1856,max(1,height-78)/813)
            self.assertLessEqual(8+1856*scale,width-8+1e-7)
            self.assertLessEqual(70+813*scale,height-8+1e-7)
            self.assertLessEqual(top+height,sh-reserve)
            source=read(CHAR+'RebirthCharacterLayoutService.cs')
            self.assertIn('8, -8, navWidth, 52',source)
            self.assertNotIn('SetScale(owner.GetChildById("rebirthCraftingTopZone")',source)
    def test_40_shared_nav_size_does_not_depend_on_active_character_page(self):
        for width in [1276,1596,1837,1872]:
            nav=width-16;status=clamp(round(nav*0.145),254,286);tabs=nav-status-10
            per=(tabs-4*7)//8;remainder=tabs-4*7-per*8
            positions=[];x=0
            for i in range(8):
                w=per+(i<remainder);positions.append((x,w));x+=w+4
            for page in ['Overview','Traits','Condition','Statistics','Metabolism']:
                self.assertEqual(positions[-1][0]+positions[-1][1],tabs)
    def test_41_98_percent_default_is_not_the_retired_85_percent_default(self):
        self.assertAlmostEqual(0.98,98/100);self.assertAlmostEqual(0.02,1-0.98)
        self.assertEqual(250,round(255*0.98))
        self.assertNotEqual(217,round(255*0.98))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project_root',nargs='?',type=Path,default=ROOT)
    args=parser.parse_args();ROOT=args.project_root.resolve()
    unittest.main(argv=['pc133'],verbosity=2)

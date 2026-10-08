import {readFile} from 'node:fs/promises';
import {resolve,dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const read=p=>readFile(join(root,p),'utf8');
const service=await read('Scripts/Survivor/Progression/RebirthSkillWaveAService.cs');
const buffs=await read('Config/buffs.xml');
const native=await read('Tools/StorageGear/inspect/out-installed-25661859/XUiC_QuestTurnInRewardsWindow.cs');
const ui=await read('Config/XUi_InGame/windows.xml');
assert.match(service,/SetCVar\(player, SalespersonRewardOptionCVar, salesperson \? 1f : 0f\)/);
assert.match(service,/SetCVar\(player, TradingRewardOptionCVar, trading \+ 0\.0001f >= RebirthProgressionRuntimeConfig.TradingRewardOptionThreshold \? 1f : 0f\)/);
assert.match(service,/state\.BackgroundId,"background.salesperson"/);
assert.match(service,/record\.Origin\.BackgroundId,"background.salesperson"/);
for(const name of ['Salesperson','Trading']) {
 assert.match(buffs,new RegExp('name="QuestRewardOptionCount" operation="base_add" value="@\\$rbSurvivor'+name+'RewardOptionBonus"'));
 assert.doesNotMatch(buffs,new RegExp('name="QuestRewardChoiceCount"[^>]*rbSurvivor'+name));
}
assert.match(native,/PassiveEffects\.QuestRewardOptionCount, null, currentQuest\.QuestClass\.RewardChoicesCount/);
assert.match(native,/PassiveEffects\.QuestRewardChoiceCount, null, 1f/);
assert.match(native,/num >= entryList\.Length/);
assert.match(ui,/grid\[@name='gridOptions'\]" name="rows">7</);
console.log('PASS Salesperson and Trading producers -> native offered-option effect; claim count unchanged; installed UI capacity guard and seven-row authoring present (static, not live validation)');
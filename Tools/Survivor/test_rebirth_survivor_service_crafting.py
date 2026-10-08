#!/usr/bin/env python3
from __future__ import annotations
import argparse, sys, xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

IDS=['skill.maintenance','skill.gunsmithing','skill.cooking','skill.medicine','skill.chemistry','skill.mechanics','skill.metalworking']
CRAFT=['skill.cooking','skill.chemistry','skill.metalworking','skill.gunsmithing']

@dataclass
class Result:
    passed:int=0
    failures:list[str]=field(default_factory=list)
    def check(self,name,ok,detail=''):
        if ok:self.passed+=1
        else:self.failures.append(name+(f' :: {detail}' if detail else ''))

def signed(v,n,p):
    v=max(-50,min(100,v))
    return n*(-v/50) if v<0 else p*(v/100) if v>0 else 0.0

def text(root:Path,rel:str)->str:
    p=root/rel
    return p.read_text(encoding='utf-8',errors='replace') if p.exists() else ''

def run(root:Path)->Result:
    r=Result()
    cfg=ET.parse(root/'Config/_Survivor/skill_sources.xml').getroot()
    sc=cfg.find('service_crafting')
    r.check('service_crafting authoring exists',sc is not None)
    if sc is None:return r
    f=lambda k:float(sc.get(k))
    vals={k:f(k) for k in ['craft_time_negative','craft_time_positive','repair_time_negative','repair_time_positive','repair_amount_negative','repair_amount_positive','mechanics_repair_negative','mechanics_repair_positive','medicine_reserve_negative','medicine_reserve_positive']}
    cases=[
        ('craft time','craft_time_negative','craft_time_positive',1,-1,.35),
        ('repair time','repair_time_negative','repair_time_positive',1,-1,.35),
        ('repair amount','repair_amount_negative','repair_amount_positive',-1,1,.40),
        ('mechanics repair','mechanics_repair_negative','mechanics_repair_positive',-1,1,.40),
        ('medicine reserve','medicine_reserve_negative','medicine_reserve_positive',-1,1,.30)]
    for label,nk,pk,ns,ps,cap in cases:
        weak=signed(-50,vals[nk],vals[pk]); zero=signed(0,vals[nk],vals[pk]); strong=signed(100,vals[nk],vals[pk])
        r.check(label+' weak sign',weak*ns>0,f'{weak}')
        r.check(label+' zero neutral',abs(zero)<1e-9)
        r.check(label+' strong sign',strong*ps>0,f'{strong}')
        r.check(label+' bounded',abs(weak)<=cap and abs(strong)<=cap,f'{weak},{strong}')

    for attr in ['chemistry_recipe_tokens','metalworking_recipe_tokens','gunsmithing_recipe_tokens','chemistry_area_tokens','metalworking_area_tokens']:
        tokens=[x.strip().lower() for x in (sc.get(attr) or '').split(',') if x.strip()]
        r.check(attr+' populated',bool(tokens),str(tokens))
    r.check('chemistry area exact','chemistry' in (sc.get('chemistry_area_tokens') or '').lower())
    r.check('metalworking forge area','forge' in (sc.get('metalworking_area_tokens') or '').lower())

    awards=cfg.find('awards'); r.check('awards node exists',awards is not None)
    if awards is not None:
        for k in ['maintenance_repair','gunsmithing_repair','cooking_per_output','cooking_max','chemistry_per_output','chemistry_max','metalworking_per_output','metalworking_max','gunsmithing_craft_per_output','gunsmithing_craft_max','medicine_meaningful_treatment','mechanics_repair_base','mechanics_repair_per_100_health']:
            r.check('award '+k,awards.get(k) is not None)
        for rate,cap in [('cooking_per_output','cooking_max'),('chemistry_per_output','chemistry_max'),('metalworking_per_output','metalworking_max'),('gunsmithing_craft_per_output','gunsmithing_craft_max')]:
            rv=float(awards.get(rate)); cv=float(awards.get(cap)); r.check(rate+' bounded',0<rv<=cv<=.75,f'{rv},{cv}')

    prog=ET.parse(root/'Config/_Survivor/progression.xml').getroot(); skills={e.get('id'):e for e in prog.findall('.//skill')}
    r.check('seven chunk8 skills defined',all(x in skills for x in IDS),f'missing={[x for x in IDS if x not in skills]}')
    r.check('seven signed bounds retained',all(skills[x].get('min')=='-50' and skills[x].get('max')=='100' for x in IDS if x in skills))

    service=text(root,'Scripts/Survivor/Progression/RebirthServiceCraftSkillService.cs')
    config=text(root,'Scripts/Survivor/Progression/RebirthProgressionRuntimeConfig.cs')
    router=text(root,'Scripts/Survivor/Progression/RebirthSkillEventRouter.cs')
    patches=text(root,'Scripts/Survivor/Progression/RebirthSurvivorProgressionPatches.cs')
    installer=text(root,'Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs')
    med=text(root,'Scripts/Healing/Actions/ItemActionUseMedRebirth.cs')
    vehicle=text(root,'Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs')
    debug=text(root,'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs')
    vector=text(root,'Scripts/Survivor/Progression/RebirthServiceCraftSkillVectorHarness.cs')

    r.check('service file exists',bool(service))
    r.check('exact seven whitelist',all(x in service for x in IDS) and 'skill.electrical' not in service.split('private static bool installed')[0] and 'skill.construction' not in service.split('private static bool installed')[0])
    r.check('uses real recipe lookup','CraftingManager.GetRecipe' in service)
    r.check('uses current item id api','ItemClass.GetForId(recipe.itemValueType)' in service and 'GetItemClass(recipe.itemValueType)' not in service)
    r.check('cooking classifier priority',service.find('IsCookingRecipe') < service.find('ChemistryAreaTokens') < service.find('IsFirearmSkill') < service.find('MetalworkingAreaTokens'))
    r.check('firearm classifier reuse','ClassifyCombatItemName' in service and 'IsFirearmSkill' in service)
    r.check('client owner snapshot bridge','RebirthSurvivorClientState.GetOwnerStateSnapshot()' in service)
    r.check('server record bridge','RebirthWorldCharacterService.TryGet(player,outr)' in service.replace(' ',''))
    r.check('craft time only uses native result','ApplyCraftTime' in service and 'nativeTime*(1f+delta)' in service.replace(' ',''))
    r.check('specialist repair precedence','IsFirearmSkill(useSkill)?"skill.gunsmithing":"skill.maintenance"' in service.replace(' ',''))
    r.check('one repair owner only','skill.mechanics' not in service[service.find('AdjustRepairQueue'):service.find('AdjustVehicleRepairAmount')])
    r.check('mechanics repair adapter','AdjustVehicleRepairAmount' in service and 'skill.mechanics' in service)
    r.check('medicine delta adapter','AdjustMedicalReserveDelta' in service and 'after-reserveBefore' in service.replace(' ',''))
    r.check('medicine only changes regen reserve','SetCustomVar("medicalRegHealthAmount"' in service and 'HealthMax' not in service and 'SetMaxHealth' not in service)
    r.check('craft output not multiplied','CraftingOutputCount' not in service and 'recipe.count=' not in service)
    r.check('no custom timer service','Coroutine' not in service and 'WaitForSeconds' not in service and 'System.Threading' not in service)
    forbidden=['Delete','Destroy','Random.Range','ExplosionEntityDamage','ItemValue metadata','workmanship']
    r.check('no catastrophic failure mechanics',all(x not in service for x in forbidden[:4]))

    r.check('native craft time patch','typeof(XUiM_Recipes)' in patches and 'GetRecipeCraftTime' in patches and 'ApplyCraftTime' in patches)
    r.check('native repair queue patch','typeof(XUiC_RecipeStack)' in patches and 'SetRepairRecipe' in patches and 'AdjustRepairQueue' in patches)
    r.check('service patches installed','RebirthSurvivorServiceCraftTimePatch' in installer and 'RebirthSurvivorServiceRepairQueuePatch' in installer and 'RebirthServiceCraftSkillService.Install()' in installer)
    r.check('existing craft completion owner retained','TileEntityWorkstation' in patches and 'AddCraftComplete' in patches and 'OnWorkstationCraftComplete' in patches)
    r.check('craft completion routes four skills','ClassifyRecipe(recipeName)' in router and 'GetCraftAward' in router and all(x in service for x in CRAFT))
    r.check('repair LBD routes specialist','GunsmithingRepair' in router and 'MaintenanceRepair' in router and 'OnEquipmentRepairCompleted' in router)
    r.check('mechanics completion retained','OnMechanicsCompleted' in router and 'mechanics:' in router)
    r.check('medicine completion retained','OnMeaningfulTreatmentCompleted' in router and 'skill.medicine' in router)
    r.check('mechanics real repair kit retained','resourceRepairKit' in vehicle and 'AdjustVehicleRepairAmount' in vehicle and 'OnMechanicsCompleted' in vehicle)
    r.check('self medicine adapter','reserveBefore' in med and 'AdjustMedicalReserveDelta' in med and 'OnMeaningfulTreatmentCompleted' in med)
    r.check('other medicine adapter','MedicalReserveBefore' in patches and 'AdjustMedicalReserveDelta' in patches)

    r.check('runtime config loads service node','service_crafting' in config and 'ServiceCraftTimeNegative' in config and 'MedicineReservePositive' in config)
    r.check('debug services command','mode=="services"' in debug and 'RebirthServiceCraftSkillService.BuildDebugReport' in debug)
    r.check('debug vector command','RebirthServiceCraftSkillVectorHarness.RunAll()' in debug)
    r.check('csharp vector harness exists','result=' in vector and 'skill.maintenance' in vector and 'unknown craft excluded' in vector)
    return r

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);a=ap.parse_args()
    r=run(a.root.resolve());total=r.passed+len(r.failures);status='PASS' if not r.failures else 'FAIL'
    print(f'REBIRTH Survivor Chunk 8 Service/Crafting vectors: {status}')
    print(f'passed={r.passed}/{total} failed={len(r.failures)} compile_validation_claimed=False')
    for x in r.failures:print('FAIL',x)
    return 0 if not r.failures else 1
if __name__=='__main__':sys.exit(main())

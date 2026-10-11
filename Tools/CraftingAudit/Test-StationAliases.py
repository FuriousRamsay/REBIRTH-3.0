"""Protect native station identity/module declarations on repairable POI aliases.
Runtime UI activation remains required; inherited XML alone previously hid this bug.
"""
import xml.etree.ElementTree as E
from pathlib import Path
r=Path(__file__).resolve().parents[2]
blocks=E.parse(r/'_Documentation/CraftingAudit_20261010/EFFECTIVE_blocks_rebirth.xml').getroot()
for alias,canonical in [('cntForgeWorkstationBroken','forge'),('cntCollapsedWorkbench','workbench'),('cntCollapsedWorkbenchEmpty','workbench'),('cntCollapsedCementMixer','cementMixer'),('cntCollapsedChemistryStation','chemistryStation')]:
 b=blocks.find(f"block[@name='{alias}']")
 assert b.find("property[@name='RebirthCraftingStation']").get('value')==canonical,alias
 assert b.find("property[@name='Extends']").get('value')==canonical,alias
 if canonical in ('cementMixer','chemistryStation'):
  props=b.find("property[@class='Workstation']")
  assert props.find("property[@name='CraftingAreaRecipes']").get('value')==canonical
  actual=set(props.find("property[@name='Modules']").get('value').split(','))
  required={'tools','output'} | ({'fuel','input'} if canonical=='chemistryStation' else set())
  assert actual==required,(alias,actual)
print('PASS: five POI aliases declare canonical UI recipe identity; local chemistry/cement properties explicitly retain modules')

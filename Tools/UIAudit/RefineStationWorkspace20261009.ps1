$ErrorActionPreference='Stop'
[xml]$doc=Get-Content Config/XUi_InGame/station_templates.xml
function N($name){$doc.SelectSingleNode("//rebirth_station_core//*[@name='$name']")}
function A($node,$values){if(!$node){throw 'Missing station node'};foreach($key in $values.Keys){$node.SetAttribute($key,[string]$values[$key])}}
function AddXml($parent,$xml){$frag=$doc.CreateDocumentFragment();$frag.InnerXml=$xml;[void]$parent.AppendChild($frag)}
A (N 'windowCraftingList') @{width=394;height=813;scale=1}
A (N 'craftingInfoPanel') @{pos='790,0';width=574;height=600;scale=1}
A (N 'rebirthCraftInfoFrame') @{width=574;height=600}
A (N 'stationItemInfo') @{scale=0.94}
A (N 'stationEmpty') @{width=574;height=600}
$info=N 'craftingInfoPanel';$content=$info.SelectSingleNode("rect[@name='contentCraftingInfo']")
A $content @{width=574;height=555}
A (N 'recipeSummary') @{pos='8,-8';width=558;height=435}
A (N 'preview') @{pos='10,-8';width=120;height=132}
A (N 'summaryDescription') @{pos='146,-8';width=396;height=252}
A (N 'stationRecipeDescription') @{width=390;height=220}
A (N 'summaryDescriptionLabel') @{width=358;height=220;font_size=21;depth=5;color='235,235,240,255'}
$reader=N 'stationRecipeDescription';A ($reader.SelectSingleNode('defaultscrollbar')) @{pos='372,0';barheight=220};A ($reader.SelectSingleNode('scrollview')) @{width=366;height=220}
A (N 'recipeMetadata') @{pos='12,-270';width=532;height=145}
foreach($label in (N 'recipeMetadata').SelectNodes('label')){if($label.GetAttribute('text') -eq '{rebirthknowledge}'){A $label @{width=520;height=52;font_size=18;pos='0,-28'}}}
A (N 'recipeCraftCountControl') @{pos='380,-112'}
A (N 'actionBand') @{pos='8,-472';width=558;height=48}
$band=N 'actionBand'; foreach($label in $band.SelectNodes('label')){[void]$band.RemoveChild($label)}
A ($band.SelectSingleNode('sprite')) @{width=558}
A ($band.SelectSingleNode('grid')) @{pos='0,0';width=558;cell_width=186;cell_height=40}
A (N 'rebirthRecipeProgressionLock') @{pos='8,-525';width=558;height=30}
A (N 'ingredients') @{pos='-392,45';width=380;height=600;depth=5}
$ingredients=N 'ingredients'; A ($ingredients.SelectSingleNode('sprite')) @{width=380;height=600;color='16,16,20,220'}
A ($ingredients.SelectSingleNode('label')) @{width=352;text='INGREDIENTS';text_key='xuiRebirthCookingIngredients';color='181,140,255,255';font_size=24}
$grid=$ingredients.SelectSingleNode('grid'); A $grid @{rows=3;cols=3;pos='38,-56';width=306;height=306;cell_width=102;cell_height=102;arrangement='horizontal'}
$grid.InnerXml='<rebirth_station_ingredient_slot name="0"/>'
AddXml $ingredients '<sprite depth="2" pos="0,-38" width="380" height="2" sprite="menu_empty" color="181,140,255,220"/>'
# Scope station recipe rows so native behavior remains intact without changing other catalogues.
[xml]$native=Get-Content ../../Data/Config/XUi_InGame/templates.xml
$template=$doc.CreateElement('rebirth_station_recipe_entry');[void]$template.AppendChild($doc.ImportNode($native.SelectSingleNode('//recipe_entry/rect'),$true));[void]$doc.SelectSingleNode('/configs/append').AppendChild($template)
$row=$template.SelectSingleNode('rect');A $row @{width=374;height=55}
A ($row.SelectSingleNode("sprite[@name='backgroundMain']")) @{width=374;height=55;color='16,16,20,220'}
A ($row.SelectSingleNode("sprite[@name='background']")) @{width=374;height=55;color='16,16,20,220'}
A ($row.SelectSingleNode("label[@name='Name']")) @{pos='56,-8';width=278;height=32;justify='left';pivot='topleft';font_size=21}
A ($row.SelectSingleNode("sprite[@name='Unlocked']")) @{pos='342,-12'}

$recipeGrid=N 'recipes';A $recipeGrid @{width=374;cell_width=374;cell_height=55;height=440};$recipeGrid.InnerXml='<rebirth_station_recipe_entry name="0"/>'
A (N 'rebirthExpectedOutcome') @{pos='3,-546';width=388;height=206}
# Match campfire panel widths; no scaled islands or oversized stock background.
foreach($node in (N 'windowCraftingList').SelectNodes('.//*[@width="424" or @width="430"]')){$node.SetAttribute('width','388')}
foreach($sprite in $info.SelectNodes('.//sprite[@width="650" or @width="634"]')){$sprite.SetAttribute('width','558')}
A ((N 'rebirthCraftingQueueRegion').SelectSingleNode('label')) @{text='CRAFTING QUEUE';text_key='xuiRebirthCraftingQueue'}
AddXml ($doc.SelectSingleNode('/configs/append')) '<rebirth_station_ingredient_slot><rect name="row" width="96" height="96" controller="RebirthIngredientEntry, RebirthUtils" tooltip="{rebirthsourcebreakdown}"><sprite depth="1" width="96" height="96" sprite="menu_empty3px" color="72,72,80,255" type="sliced"/><sprite depth="2" pos="3,-3" width="90" height="90" sprite="menu_empty" color="48,48,54,255"/><sprite depth="4" name="icon" width="68" height="68" atlas="ItemIconAtlas" sprite="{itemicon}" color="{itemicontint}" pos="48,-40" pivot="center" foregroundlayer="true"/><label name="needcount" depth="5" pos="3,-72" width="90" height="22" text="{haveneedcount}" font_size="20" color="235,235,240,255" justify="right"/></rect></rebirth_station_ingredient_slot>'
$settings=[Xml.XmlWriterSettings]::new();$settings.Indent=$true;$settings.Encoding=[Text.UTF8Encoding]::new($false);$writer=[Xml.XmlWriter]::Create((Join-Path (Get-Location) 'Config/XUi_InGame/station_templates.xml'),$settings);$doc.Save($writer);$writer.Dispose()

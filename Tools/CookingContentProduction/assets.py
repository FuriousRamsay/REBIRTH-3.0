"""Resumable cooking icon production manifest and game-size exports."""
from pathlib import Path
import json,sys,zipfile,shutil,re
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
CAT=ROOT/'_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915'
OUT=CAT/'Production';OUT.mkdir(exist_ok=True)
MASTERS=OUT/'Masters';MASTERS.mkdir(exist_ok=True)
ICONS=ROOT/'UIAtlases/ItemIconAtlas';ICONS.mkdir(parents=True,exist_ok=True)
REF=CAT/'IconStylePreview'; BASE=REF/'BaseReferences'
MAN=OUT/'asset_manifest.json'
def write(m):MAN.write_text(json.dumps(m,indent=2)+'\n',encoding='utf-8')
def export(source,target):
    im=Image.open(source).convert('RGBA')
    # Format normalization only: preserve artwork, proportions and transparency.
    im.thumbnail((160,160),Image.Resampling.LANCZOS)
    canvas=Image.new('RGBA',(160,160))
    canvas.alpha_composite(im,((160-im.width)//2,(160-im.height)//2))
    canvas.save(target)
def init():
    if MAN.exists():return json.loads(MAN.read_text())
    data=json.loads((CAT/'COOKING_LITERATURE_CATALOGUE.json').read_text())
    food=json.loads((CAT/'NEW_CROP_RECIPE_PROPOSAL.json').read_text())
    z=zipfile.ZipFile(r'C:/Users/Etienne/Desktop/__0/_0/__Rebirth/7dtd 3.2 ItemIcons.zip')
    original_refs=OUT/'OriginalFoodReferences';original_refs.mkdir(exist_ok=True)
    original={}
    for key,r in data['recipes'].items():
        ident=r.get('game_id')
        if ident and 'ItemIcons/'+ident+'.png' in z.namelist():
            p=original_refs/(ident+'.png');p.write_bytes(z.read('ItemIcons/'+ident+'.png'));original[key]=str(p)
    # Existing Rebirth dishes deliberately reuse native icons today; retain that reference.
    fallbacks={'skillet':'foodSteakAndPotato','soup':'foodVegetableStew','crumble':'foodBlueberryPie','potpie':'foodShepardsPie','dried':'foodGrilledMeat','pickles':'foodVegetableStew'}
    for key,name in fallbacks.items():
        p=original_refs/(name+'.png');p.write_bytes(z.read('ItemIcons/'+name+'.png'));original[key]=str(p)
    tasks=[]
    def task(key,category,title,prompt,refs):
        ident='rebirthCooking'+category.title()+re.sub('[^A-Za-z0-9]','',key)
        tasks.append(dict(key=key,category=category,title=title,icon=ident,path=str(ICONS/(ident+'.png')),master=str(MASTERS/(ident+'.png')),prompt=prompt,refs=[str(p) for p in refs],status='pending'))
    prefix='One 7 Days to Die inventory icon, single centered item on true transparent alpha, square canvas. Match supplied native game icon style: worn practical textures, neutral light, original three-quarter overhead angle, restrained details readable at 160 pixels. No glow, halo, exterior shadow, text, garnish not in the recipe, background, collage or extra items. Fill most of the square without cropping. '
    for r in food:
        title=r['title'];family=r['variety_family']
        ref='foodVegetableStew'
        if r['code'] in {'N37','N38'}:ref='drinkJarRedTea';form='a simple glass jar of the strained infusion, no meal chunks, referencing the native tea jar'
        elif any(s in family for s in ['pie','pastry','crumble','bread','biscuit','bar','custard']):ref='foodBlueberryPie' if any(s in family for s in ['pie','pastry','crumble']) else 'foodCornBread';form='one recognizable portion of the finished baked dish, referencing native pastry icons'
        elif any(s in family for s in ['egg_meal','meatloaf','glazed_meat','fritter','pancake','lettuce_cup','salad','fruit_bowl']):ref='foodBaconAndEggs' if family=='egg_meal' else 'foodFishTacos';form='a compact serving of the named food, referencing native meal presentation, not a pot of soup'
        else:form='the named dish in the worn metal pot; preserve the reference pot angle, not a top-down view'
        ingredients=', '.join(i['label'] for i in r['ingredients'] if i['label']!='water')
        prompt=prefix+f'Create {title}: {form}. Actual ingredients: {ingredients}. Distinguish this dish through the visible ingredients and texture, not labels. Produce ONLY this one item.'
        refs=[BASE/(ref+'.png')]
        if ref=='foodVegetableStew':refs.append(REF/'carrot-ginger-soup-v1.png')
        task(r['code'],'food',title,prompt,refs)
    for key in ['P01','P02','P03','P04']:
        r=data['recipes'][key]
        form={'P01':'a small open worn cloth sack of pale wheat flour','P02':'a small open worn cloth sack of tan flattened oat flakes','P03':'a short plain glass jar of thick amber cane syrup','P04':'a small plain glass jar of chunky dark red cranberry sauce'}[key]
        task(key,'food',r['title'],prefix+'Create '+form+'. No labels, no lettering. This is a cooking ingredient, not a finished dish.',[BASE/('foodCornMeal.png' if key in {'P01','P02'} else 'drinkJarRedTea.png')])
    # Legacy foods have no new 3.0 image yet. Generate separate outputs for card references.
    for key,r in data['recipes'].items():
        if key.startswith('L'):
            form='a jar of golden herbal smoothie' if key=='L11' else 'a modest thick survival stew in the worn cooking pot'
            prompt=prefix+f"Create a game food item illustration for {r['title']}: {form}. Ingredient identities: "+', '.join(i['id'] for i in r['ingredients'])+'. Use a plausible cooked-food appearance, no raw gore or nonfood objects visible.'
            task(key,'food',r['title'],prompt,[BASE/('drinkJarRedTea.png' if key=='L11' else 'foodVegetableStew.png')])
    foods={t['key']:t for t in tasks}
    for c in data['cards']:
        key=c['recipe'];r=data['recipes'][key]
        image=foods[key]['master'] if key in foods else original[key]
        prompt='Create ONE square recipe-card inventory icon in the exact style of reference 1: faded dark sage printed paper field, cream narrow double-rule border, worn rounded corners, muted burnt-orange lower illustrated band. Reference 2 is the FOOD image: preserve its camera angle, proportions and recognizable food, printed LARGE almost edge-to-edge on the card. The card fills almost the whole square. No text, letters, recipe lines, labels or small badges. Lower band shows a simple large illustration of one or two relevant ingredients, no detailed clutter. Subject: '+r['title']+'. Recipe ingredients: '+', '.join(i['id'] for i in r['ingredients'])+'. Do not reuse the carrot illustration unless carrot is relevant. No glow, halo, backdrop, shadow outside paper or top-down rotation of the food. True transparent alpha outside the card. Match approved weathered game icon, one icon only.'
        task(c['id'],'card',c['title'],prompt,[REF/'carrot-ginger-recipe-card-approved-style.png',image])
    for category in ['magazines','books']:
        for entry in data[category]:
            isbook=category=='books'; kind='book' if isbook else 'magazine'
            prompt=('Create ONE game inventory '+kind+' icon. Match reference 1: '+('thick worn hardcover, visible spine and yellowed page block, tilted portrait silhouette' if isbook else 'thin worn portrait magazine, slightly curved paper cover, tilted silhouette and page edges')+'. Reference 2 is a native base-game style reference. Use square canvas, nearly full-height object, true transparent exterior. Title exactly: "'+entry['title']+'". Use a large readable main title, omit tiny supplementary text. Cover subject: '+entry['focus']+'. GENERIC subject artwork: ingredients, tools or preparation process appropriate to the subject, never an image of a specific finished in-game recipe. Do NOT put a soup pot on every cover. Give this title a distinct relevant composition and muted earthy cover palette while staying in the same worn printed survival-game family. Broad readable shapes, restrained detail. No glow, halo, vignette, backdrop, floating items outside the cover or tiny cover lines. '+('Substantial clothbound book, no issue number or magazine footer.' if isbook else 'Thin magazine, not hardcover; prominent issue/subject design, no recipe card border.'))
            task(entry['id'],kind,entry['title'],prompt,[REF/('roots-at-the-table-book-v1.png' if isbook else 'root-cellar-magazine-approved-style.png'),REF/('bookArtOfMiningLuckyStrike.png' if isbook else 'bookHomeCookingWeekly.png')])
    approved={('food','N01'):REF/'carrot-ginger-soup-v1.png',('card','C-N01'):REF/'carrot-ginger-recipe-card-approved-style.png',('magazine','M04'):REF/'root-cellar-magazine-approved-style.png',('book','B04'):REF/'roots-at-the-table-book-v1.png'}
    for t in tasks:
        source=approved.get((t['category'],t['key']))
        if source:
            shutil.copy2(source,t['master']);export(source,t['path']);t['status']='approved_reference';t['source']=str(source)
    m=dict(tasks=tasks,original_food_references=original,notes='Images are generated one asset per call. 160px exports preserve master artwork. XML integration follows asset completion.')
    write(m);return m
def accept(m,index,source):
    t=m['tasks'][index]
    if t.get('source') and t['source'] != str(source):
        t.setdefault('previous_sources', []).append(t['source'])
    shutil.copy2(source,t['master']);export(source,t['path'])
    im=Image.open(t['master']);alpha=im.getchannel('A').getextrema() if im.mode=='RGBA' else None
    t.update(status='generated',source=str(source),alpha_extrema=alpha)
    if alpha is None or alpha[0]!=0:t['status']='needs_transparency_review'
    write(m);print(json.dumps(dict(key=t['key'],status=t['status'],path=t['path'])))
if __name__=='__main__':
    m=init();command=sys.argv[1] if len(sys.argv)>1 else 'status'
    if command=='next':
        category=sys.argv[2];limit=int(sys.argv[3])
        print(json.dumps([dict(index=i,**t) for i,t in enumerate(m['tasks']) if t['category']==category and t['status']=='pending'][:limit]))
    elif command=='accept':accept(m,int(sys.argv[2]),Path(sys.argv[3]))
    else:
        from collections import Counter
        print(json.dumps(dict(total=len(m['tasks']),states=dict(Counter(t['status'] for t in m['tasks'])),categories=dict(Counter(t['category'] for t in m['tasks'])))))

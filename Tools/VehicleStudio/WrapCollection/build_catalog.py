"""Author the 200-wrap catalogue and additive native item/localization data.

Artwork status is deliberately independent of item data. Missing art is never
reported as complete. Run from the mod root; only writes collection-owned files.
"""
import csv
import json
import re
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
ROWS = []
# Internal IDs stay stable while draft display names are checked against the
# installed game's actual POI localization. These IDs have not been installed.
STABLE_IDS = {
    "Bob's Boars & Carl's Corn": "BoarandCorn",
    "The Bear Den": "BearDenLodge",
    "Fats Foods Feast": "FatHawaiianFeast",
    "Dirty Laundry": "ZoesSalonCruiser",
    "Pop's Pawn & Bel's Bakery": "PetesPawnPatrol",
    "Joe's USA Grocery Run": "JoesUSAHauler",
    "Prowlin Petes Takeout": "ProwlinPetesRescue",
    "Auto Wash Express": "WashandGo",
    "The Last Sunset Golden Years": "TheLastSunsetMotel",
    "The Piggy Bank Reserve": "NavezganeBankReserve",
    "Poopy Pants Daycare": "PoopyPantsPlayhouse",
}


def group(category, palette, style, lines):
    for line in lines.strip().splitlines():
        name, hood, side, band = line.split("|")
        slug = STABLE_IDS.get(name, re.sub(r"[^A-Za-z0-9]", "", name))
        ROWS.append(dict(id=slug, name=name, category=category, palette=palette,
                         style=style, hood=hood, side=side, band=band))


group("Approved", "individual approved palette", "match approved concept", """
Midnight Cartographer|copper compass rose|mountain contours and turquoise rivers|copper panoramic maps
The Hungry Road|cracked ivory skull|skeletal hands emerging through thorns|skulls and vertebrae in crimson brambles
Dead Zone Runner|yellow biohazard badge|zombie silhouettes behind torn quarantine tape|distressed containment chevrons
Shotgun Messiah|vintage ammunition company badge|cream brand lettering and shotgun shells|oxblood-edged branded advertising
Jen's Clinic|illustrated Trader Jen portrait|teal clinic script and stethoscope|coral heartbeat and lavender flowers
FuriousRamsay Signature|supplied eye-and-crystal logo|oversized eye emblem and angular blue shards|violet crystal shards and repeated eye logo
""")
group("Traders", "character-specific palette", "recognizable 7DTD trader tribute", """
Rekt's Last Nerve|scowling farmer portrait|tractor silhouettes and barbed-wire lettering|weathered farm-store signs
Hugh's Heavy Supply|stern veteran portrait|ammo crates and olive supply stencils|military quartermaster insignia
Bob's Salvage Route|friendly mechanic portrait|wrench wings and scrapyard truck art|orange steel rivet panels
Joel's Frontier Faith|cowboy trader portrait|desert chapel and frontier lettering|western concho and rope borders
Poppy's Oddities|mysterious trader portrait|mutant specimen jars and violet vines|strange botanical specimen labels
Rekt's Harvest Hauler|angry scarecrow badge|cornfields and farm auction typography|grain sacks and seed packet borders
Hugh's Winter Outpost|snowbound supply shield|winter pine patrol silhouettes|arctic cargo identification panels
Bob's Tow Service|crossed tow hooks|retro roadside recovery advertisement|checkerboard grease-rag ribbons
Joel's Desert Mission|sunset cross-and-hat badge|mesa pilgrimage landscape|faded leather tooling bands
Jen's Night Shift|teal moon and stethoscope|nocturnal clinic sign with lavender glow artwork|dark heartbeat and crescent strips
Rekt's No Refunds|stamped angry receipt|ripped refund notices and pitchfork emblem|rustic price-tag mosaics
Hugh's Brass Standard|brass military crest|precise ammunition inventory diagrams|olive cartridge-belt borders
Bob's Copperhead Garage|coiled copper snake and socket|vintage speed-shop typography|copper scales and checkered flags
Joel's Trading Caravan|wagon-wheel compass|hand-painted caravan and desert tents|woven frontier textile bands
""")
group("Businesses", "authentic brand-led palette", "7DTD business promotional livery; verify native brand reference", """
Shamway Supper Run|Shamway food-can badge|retro grocery lettering and canned supper illustration|pantry-label advertising ribbons
Pass-N-Gas Road Crew|fuel-pump brand badge|vintage filling station advertisement|red-white service chevrons
Pop-N-Pills House Call|medicine-bottle brand badge|pharmacy script and pill-bottle illustrations|clinical pill-capsule patterns
Working Stiffs Tool Run|crossed hammer brand badge|bold hardware-store lettering and hand tools|yellow steel ruler strips
Crack-A-Book Bookmobile|open book brand badge|colorful library spines and reading mascot|stacked vintage book titles
Mo Power Service Van|electrical coil brand badge|blue circuit diagrams and utility lettering|cable and insulator bands
Savage Country Outfitter|antler outdoor-store badge|mountain outfitter catalogue illustration|canvas patches and boot-tread strips
Higashi Research Division|corporate lab insignia|clean pharmaceutical geometry and specimen artwork|sterile blue scientific bands
Dishong Executive Fleet|corporate tower crest|architectural skyline and executive lettering|black-and-gold deco borders
Navezgane General Hospital|medical facility badge|ambulance-era hospital signage|teal emergency pulse strips
Buzz's Bar Fly|beer-cap pub badge|retro tavern sign and foaming mugs|amber bottle-label ribbons
Bob's Boars & Carl's Corn|mutant boar and super-corn farm badge|rustic farm advertisement with huge boar and cornfields|corn-stalk and boar-track borders
Fats Foods Feast|cheerful fast-food chef badge|bold burger-shop lettering and oversized burger illustration|red-yellow takeaway menu ribbons
The Bear Den|roaring bear and beer-cap medallion|rustic bar sign with bear claw marks and frothy mugs|amber beer labels and plaid tavern strips
Dirty Laundry|retro washing-machine badge|cheerful laundromat script and hanging clothes|teal soap-bubble and clothespin ribbons
Pop's Pawn & Bel's Bakery|pawn-shop balls and croissant badge|split vintage pawn-and-bakery storefront advertisement|gold pawn tickets and bread-label borders
Joe's USA Grocery Run|patriotic grocery-store badge|classic grocery advertisement with fresh produce and pantry goods|red-white-blue grocery-label strips
Red Mesa Dispatch|restricted facility emblem|desert test-range stencils and mesa imagery|red restricted-access bands
Navezgane Corrections|prison transport seal|black-white correctional lettering|barred-window graphic strips
Poopy Pants Daycare|colorful toy mascot badge|bright nursery alphabet and block toys|primary-color crayon borders
Prowlin Petes Takeout|playful fast-food mascot badge|retro takeaway advertisement with burgers and fries|bright checkerboard menu strips
Auto Wash Express|soap-bubble carwash badge|midcentury auto-wash lettering with foam and sponge art|turquoise bubble-and-spray ribbons
Ostrich Hotel Wanderer|ostrich travel badge|vintage roadside hotel postcard|sunset motel-key borders
The Last Sunset Golden Years|warm sunset and rocking-chair badge|vintage retirement-home signage and peaceful garden artwork|peach floral and sunset bands
The Piggy Bank Reserve|gold piggy-bank medallion|engraved currency-style bank lettering|green guilloche note borders
Navezgane Fire Rescue|fire brigade crest|vintage fire-engine lettering and ladder art|reflective-style red-gold bands
Navezgane Postal Route|postal wing badge|aged parcel stamps and mail lettering|airmail envelope edge strips
Navezgane Transit Heritage|bus company roundel|retro route map and transit advertisement|colored destination-line bands
Shamway Midnight Snack|grinning canned-food mascot|night-shift snack delivery artwork|purple convenience-food labels
Pass-N-Gas High Octane|racing fuel brand crest|flame-edged pump and pit-crew lettering|racing checker and octane bands
""")
group("Game Themes", "theme-specific palette", "7DTD world and gameplay illustration", """
Blood Moon Express|red lunar eclipse|horde silhouettes against ruined skyline|seven-phase blood moon strips
Navezgane Explorer|county map compass|hand-painted regional travel map|highway shield and landmark borders
Pine Forest Refuge|pinecone crest|misty forest cabin panorama|fern and pine-needle strips
Desert Scavenger|bleached lizard sun badge|sandstone mesas and abandoned highway|sun-baked geometric road bands
Snowbound Survivor|frosted wolf emblem|ice-blue snowy mountains|crystalline snowflake strips
Burnt Forest Embers|ember phoenix|charred trunks and drifting sparks|black ash and copper ember bands
Wasteland Wanderer|broken gas-mask badge|radioactive ruins and twisted steel|muted green radiation-warning strips
Purge Clearance Crew|cleared-building shield|marked POI floor plans and stamped objectives|green mission-complete ribbons
Moonlock Protocol|fractured violet moon|underground hive roots and recovered sunlight|crystal and lunar-phase bands
Queen Hive Hunter|queen-insect crest|organic hive tunnels and glowing specimens|amber chitin and tunnel borders
Horde Night Engineer|hammer-and-spanner shield|fortified homestead blueprint|steel truss and construction-layout strips
Gyrocopter Air Mail|rotor-wing badge|vintage airborne courier illustration|sky-blue cloud and flight-path bands
Mining Guild Hauler|pickaxe and ore emblem|cutaway mine shafts and mineral veins|coal-to-gold ore strata bands
Wrench and Ruin|salvage gear crest|exploded engine diagrams and scrap silhouettes|oxidized gear-tooth strips
Underground Prospector|crystal lamp badge|cross-section cavern wonderland|geode and stalactite borders
Land Claim Homestead|home-and-flag shield|cozy fortified farmstead|fence rail and sunflower bands
Treasure Map Drifter|buried chest compass|old parchment treasure trail|torn-map coordinate strips
Screamer Warning|stylized screaming zombie badge|sound-wave zombie poster art|red alarm waveform bands
Demolition Countdown|warning timer emblem|stylized explosive zombie poster|industrial countdown panel strips
Seven Days Remaining|weathered tally-mark crest|seven-panel survival storyboard|calendar and sunrise sequence bands
""")
group("Nature", "natural greens, sky blues, warm earth tones", "detailed nature illustration", """
Alpine Daybreak|golden mountain sun|alpine panorama|edelweiss and contour borders
Wildflower Meadow|bee on daisy|lush wildflower field|botanical bloom ribbons
Autumn Fox|curled red fox|fall forest and drifting leaves|oak and maple leaf strips
Northern Lights|polar star medallion|aurora over snowy pines|flowing aurora bands
Ocean Nomad|nautilus shell|rolling ocean and seabirds|wave and rope borders
Rainforest Canopy|tree frog emblem|dense tropical leaves and parrots|monstera and orchid strips
Desert Bloom|flowering cactus sun|cactus garden under pink sunset|succulent rosette bands
Stormwatch|lightning cloud badge|dramatic storm front and rain|zigzag storm-cell strips
Mushroom Kingdom Roadtrip|fly-agaric cluster|whimsical mossy forest floor|mushroom and fern borders
""")
group("Animals", "individual animal-inspired palette", "bold wildlife portrait and habitat art", """
Lone Wolf|howling wolf portrait|moonlit running wolf pack|pine and pawprint borders
Tiger Territory|tiger face medallion|prowling tiger and jungle shadows|striped bamboo ribbons
Raven's Flight|raven with moon|sweeping black feather flock|feather and branch strips
Koi Current|yin-yang koi pair|flowing koi pond|water ripple and lotus bands
Octopus Ink|octopus compass|curling tentacles and deep sea|suction-cup and wave strips
Honeybee Highway|queen bee crest|honeycomb and lavender field|golden hexagon borders
Dragonfly Drift|iridescent dragonfly|pond reeds and jeweled wings|art-nouveau insect ribbons
Stag of the Pines|antlered stag portrait|woodland deer silhouettes|antler and pinecone bands
Butterfly Migration|monarch butterfly|swarm across flowering landscape|wing-pattern mosaics
""")
group("Fantasy", "individual fantasy palette", "illustrated fantasy genre", """
Dragon's Hoard|coiled dragon crest|dragon guarding scattered treasure|gold-scale and jewel bands
Moonlit Witch|witch hat and crescent|enchanted cottage and flying broom|herb and constellation ribbons
Paladin's Oath|sun shield and sword|stained-glass knight scene|gold illuminated manuscript borders
Goblin Market|mischievous goblin face|chaotic fantasy market stalls|coin and trinket strips
Forest Dryad|leaf-crowned face|living trees and winding roots|interlaced vine ribbons
Necromancer's Grimoire|arcane skeletal book|violet spirits rising from runes|bone and spell-circle strips
Mermaid's Wake|mermaid shell medallion|underwater city and flowing tail|pearl and seaweed borders
Phoenix Reborn|fiery phoenix|rising flame-feather panorama|embers and gold feather bands
Frostbound Rune|icy runestone crest|frozen giant fortress|carved ice-rune strips
""")
group("Science Fiction", "individual futuristic palette", "speculative science fiction graphics", """
Deep Space Courier|orbital delivery badge|freighter among stars|orbital route bands
Cyber Ronin|neon samurai mask|rain-soaked neon city|angular magenta circuit strips
Lunar Surveyor|moon mission patch|lunar rover and crater panorama|technical moon-phase borders
Alien Botanist|alien flower specimen|bioluminescent extraterrestrial garden|strange leaf-cell bands
Retro Rocket Club|1950s rocket badge|pulp space adventure|atom and starburst ribbons
Mech Bay Seven|robotic visor emblem|giant robot assembly schematic|warning-panel and piston strips
Quantum Arcade|pixel portal|glitch city and neon grids|broken-code mosaic strips
Mars Rust Runner|red planet expedition seal|Martian mesas and habitats|terrain-grid border ribbons
Black Hole Club|gravitational lens emblem|stars spiraling into singularity|warped spacetime bands
""")
group("Retro and Racing", "individual period motorsport palette", "authentic period-inspired automotive graphics", """
Sunset Strip Seventy|retro setting sun|seventies rainbow speed stripes|orange-brown geometric bands
Neon Eighty Eight|pink chrome triangle|synthwave sunset grid|purple neon horizon strips
Boardwalk Surf Club|surfboard wave badge|vintage beach postcard|turquoise wave checker ribbons
Route Sixty Six Rambler|highway shield|classic Americana roadside collage|road-sign and pinstripe borders
Checkered Thunder|racing number badge|bold diagonal motorsport blocks|black-white racing checkers
Salt Flat Special|speed-record roundel|streamlined land-speed graphics|thin heritage speed stripes
Rally Dust Champion|rally crest|mud-splashed angular rally livery|sponsor-style geometric bands
Rockabilly Roadster|swallow and dice|old-school tattoo flames|cherry and checkerboard strips
Disco Inferno|mirrorball badge|colorful seventies dance swirls|rainbow disco tile bands
Atomic Caravan|midcentury atom|boomerang shapes and starbursts|mint-coral atomic ribbons
""")
group("Cute and Cozy", "pastel and warm cozy palettes", "friendly illustrated lifestyle art", """
Strawberry Picnic|strawberry basket|gingham picnic and berries|strawberry blossom ribbons
Sleepy Cat Cafe|sleeping cat cup|cozy cats and coffee|pawprint and steam strips
Bubble Tea Brigade|happy boba cup|colorful drinks and tapioca pearls|pastel bubble bands
Little Ghost Holiday|smiling ghost|friendly ghosts on roadtrip|tiny star and ghost borders
Cozy Book Nook|teacup on books|warm reading den illustration|book-spine and leaf strips
Cloud Puppy|puppy on cloud|playful dogs among pastel clouds|rainbow pawprint ribbons
Frog Pond Friends|frog on lily pad|pond picnic with friendly frogs|lily and raindrop borders
Peachy Days|peach with leaves|sunlit orchard illustration|peach gingham strips
Crochet Camper|crochet flower medallion|granny-square textile pattern|knitted stitch ribbons
Coastal Cottage|seashell wreath|white cottage and hydrangeas|blue ceramic tile bands
""")
group("Art and Geometry", "individual art movement palette", "distinct graphic art direction", """
Art Deco Voyager|gold sunburst|black-gold architectural fans|stepped deco borders
Stained Glass Garden|rose window|jewel-tone botanical glass mosaic|leadlight flower strips
Bauhaus Motion|primary circle composition|red-yellow-blue geometric balance|modular primary-shape bands
Ink Wash Mountain|brushstroke sun|monochrome mist mountains|flowing calligraphic borders
Mosaic Nomad|ceramic rosette|colorful hand-cut tile landscape|intricate tile ribbons
Paper Cut Paradise|layered paper bird|bold layered tropical paper shapes|cut-leaf borders
Op Art Mirage|optical spiral|high-contrast illusion waves|monochrome zigzag strips
Watercolor Wanderlust|painted sun wreath|loose watercolor landscape|blended pigment bands
Copper Circuitry|circuit mandala|intricate copper trace network|technical circuit ribbons
Prismatic Facets|faceted crystal|colorful low-poly shard composition|iridescent polygon bands
""")
group("Music and Subculture", "individual scene palette", "music and street culture graphics", """
Heavy Metal Pilgrim|winged amplifier|jagged metal-album landscape|black-silver guitar-string strips
Punk Patchwork|safety pin crest|torn zine collage and stitched patches|tartan and stud-print bands
Jazz After Midnight|saxophone moon|smoky abstract jazz ensemble|blue-note and keyboard strips
Reggae Road Rhythm|lion sun crest|warm sound-system illustration|green-gold-red rhythm borders
Country Road Serenade|guitar and horseshoe|barn dance and rolling hills|denim and rope bands
Techno Pulse|minimal pulse emblem|acid geometric waveform field|electric rhythm strips
Hip Hop Block Party|turntable badge|urban mural and boombox art|graffiti arrow ribbons
Classical Nocturne|gold violin medallion|elegant orchestral instrument scrollwork|ivory musical-staff bands
Indie Folk Trail|banjo flower wreath|hand-drawn woodland music scene|wildflower instrument borders
Skatepark Concrete|skate deck crest|street-art ramps and paint splashes|sticker-bomb ribbon bands
""")
group("Travel and Culture", "individual travel-poster palette", "respectful travel and regional decorative art", """
Kyoto Evening|lantern and cherry blossom|old street under blossoms|indigo wave and petal bands
Nordic Fjord|longship compass|dramatic fjord panorama|interlaced Nordic knot borders
Tuscan Sun|sunflower pottery badge|rolling vineyard landscape|terracotta olive-leaf strips
Sahara Caravan|desert star|camel caravan and dunes|woven sand-toned geometric bands
Andean Highland|condor medallion|bright mountain village panorama|multicolor woven textile strips
Pacific Island Drift|hibiscus compass|tropical volcanic island|leaf and ocean geometric bands
Highland Rambler|thistle crest|Scottish hills and stone cottage|green-blue tartan strips
Outback Wayfarer|kangaroo sun badge|red earth and eucalyptus panorama|simple road-map dot-trail bands
Alaskan Passage|orca compass|glaciers and rugged coast|ice-and-wave decorative strips
Mediterranean Blue|lemon ceramic badge|white coastal village panorama|blue-and-white tile ribbons
""")
group("Food and Hobbies", "individual playful hobby palette", "illustrated hobby enthusiast designs", """
Coffee Before Zombies|espresso crest|retro coffee roastery advertisement|bean and steam strips
Pizza Patrol|pizza slice badge|cheerful pizzeria illustration|red-white checker ribbons
Smoked and Loaded|barbecue smoker crest|rustic pitmaster advertisement|smoke and spice-label bands
Sushi Night Cruiser|sushi platter emblem|stylized koi and sushi illustration|nori and wave borders
Gardeners' Getaway|watering can wreath|lush vegetable garden|seed packet and herb strips
Angler's Dawn|trout badge|sunrise lake fishing scene|lure and water ripple bands
Mountain Bike Escape|chainring mountain emblem|woodland cycling trail|tire-tread and sprocket ribbons
Astronomy Club|telescope moon badge|constellation atlas|planetary orbit strips
Photography Field Trip|vintage camera crest|filmstrip landscape photographs artwork|negative-frame border bands
Tabletop Quest|twenty-sided die|fantasy gaming table illustration|dice and dungeon-grid strips
""")
group("Dark and Atmospheric", "individual moody palette", "distinct atmospheric illustration", """
Haunted Harvest|jack-o-lantern moon|crooked farm and pumpkin field|vines and lantern strips
Vampire's Velvet|bat-wing goblet|gothic castle and crimson roses|velvet damask borders
Lovecraftian Tide|eldritch eye medallion|impossible sea city and tentacles|ancient tidal glyph bands
Abandoned Carnival|cracked clown mask|derelict ferris wheel and tents|faded carnival diamond strips
Ghost Ship Passage|spectral anchor|misty phantom sailing ship|rope and ghostly wave borders
Moth and Candle|death-head moth|candlelit botanical nocturne|wax-drip and moth-wing bands
Plague Doctor's Remedy|beaked mask crest|apothecary bottles and ravens|herbal engraving strips
Black Rose Funeral|black rose oval|Victorian mourning floral illustration|lace and silver thorn borders
Nightmare Cinema|film reel eye|expressionist monster shadow city|torn vintage film strips
Cryptkeeper's Caravan|stone gargoyle shield|cemetery gate and moonlit crypt|carved stone ivy bands
""")
group("Luxury and Minimal", "individual restrained premium palette", "clean high-end automotive design", """
Pearl Horizon|minimal sunrise medallion|pearl-white negative space with gold horizon|fine champagne pinstripe bands
Obsidian Executive|silver geometric crest|satin black sweeping silver form|subtle brushed-metal strips
Emerald Touring Club|emerald leaf badge|deep green gold botanical accent|thin heritage gold borders
Rose Gold Reverie|rose-gold sun disk|dusty blush metallic geometry|fine art-deco rose-gold bands
Arctic Monochrome|minimal ice crystal|white-gray angular snow forms|silver glacier line strips
Saffron Modern|abstract saffron circle|cream and mustard asymmetrical blocks|clean ochre geometric bands
Denim Heritage|stitched leather badge|indigo denim seam illustration|woven blue textile strips
Walnut Retreat|woodcut tree ring|warm woodgrain and forest silhouette|walnut inlay pattern bands
Cobalt Porcelain|blue floral medallion|ivory cobalt ceramic illustration|blue pottery border strips
""")
group("Adventure and Play", "individual vibrant palette", "distinct adventure and novelty genres", """
Pirate Radio Rover|pirate broadcast skull|treasure island radio station|nautical flags and signal bands
Wild West Showdown|sheriff star|western town and desert rider|tooled leather and rope strips
Steampunk Dispatch|brass pressure gauge|airship and mechanical city|riveted gearwork ribbons
Superhero Delivery|bold comic starburst|original comic action panels|halftone speed-line bands
Dinosaur Safari|tyrannosaur crest|prehistoric jungle panorama|fossil and fern borders
Undersea Laboratory|bathysphere badge|deep-sea research station|pressure-gauge and bubble strips
Mountain Rescue One|rescue rope shield|helicopter and mountain rescue illustration|orange technical rescue bands
Treeline Firewatch|lookout tower badge|forest watchtower at sunset|burnt-orange pine borders
Solarpunk Homestead|sunflower gear|green rooftop village illustration|leaf and solar-grid ribbons
Rainbow Roadtrip|color-wheel compass|joyful multicolor ribbon landscape|rainbow geometric strips
""")
group("Sports", "individual original sports-club palette", "bold original sports enthusiast graphics; no real team logos", """
Frozen Rink Club|crossed hockey sticks and puck crest|ice rink motion and original hockey club lettering|skate-cut ice and hockey-stick borders
Sunday Gridiron|football helmet and laurel badge|football field yard lines and original varsity graphics|orange-black athletic stripe bands
Street Court Legends|basketball city-court emblem|urban basketball mural and dynamic ball trails|purple-orange court-line ribbons
World Pitch Wanderer|soccer ball and globe crest|flowing stadium panorama and original club geometry|green-gold pitch-line strips
Trackside Victory|running shoe and stopwatch badge|colorful track lanes and sprint silhouettes|red-navy finish-line ribbons
""")


def main():
    assert len(ROWS) == 200, len(ROWS)
    assert len({r["id"] for r in ROWS}) == 200
    assert len({(r["hood"], r["side"], r["band"]) for r in ROWS}) == 200
    for index, row in enumerate(ROWS, 1):
        row["number"] = index
        row["item"] = "modVehicleWrap" + row["id"]
        row["displayName"] = row["name"] + " Vehicle Wrap"
        row["description"] = (
            f"{row['name']}: {row['hood'].capitalize()} on the hood; "
            f"{row['side']} along the truck; {row['band']} on the camper. "
            "Install in the camper's cosmetic slot, replacing dye. "
            "Remove to restore the original finish."
        )
        row["artwork"] = f"Resources/CamperWraps/Artwork/{row['id']}.png"
        row["status"] = "artwork-present-unqualified" if (ROOT / row["artwork"]).is_file() else "artwork-pending"
    out = ROOT / "Resources/CamperWraps"
    out.mkdir(parents=True, exist_ok=True)
    (out / "catalog.json").write_text(json.dumps({"version": 1, "count": len(ROWS), "wraps": ROWS}, indent=2) + "\n", encoding="utf-8")
    ids = "\n".join('        "' + r['id'] + '",' for r in ROWS)
    (ROOT / "Scripts/Vehicles/Camper/RebirthCamperWrapCatalog.cs").write_text(
        '// Generated by Tools/VehicleStudio/WrapCollection/build_catalog.py.\n'
        'using System;\nusing System.Collections.Generic;\n\n'
        'internal static class RebirthCamperWrapCatalog\n{\n'
        '    internal const string ItemPrefix = "modVehicleWrap";\n'
        '    private static readonly HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal)\n    {\n'
        + ids + '\n    };\n'
        '    private static readonly Dictionary<string,string> Items = BuildItems();\n'
        '    private static Dictionary<string,string> BuildItems()\n    {\n'
        '        var values = new Dictionary<string,string>(StringComparer.Ordinal);\n'
        '        foreach (string id in Ids) values.Add(ItemPrefix + id, id);\n'
        '        return values;\n    }\n'
        '    internal static bool Contains(string id) => id != null && Ids.Contains(id);\n'
        '    internal static string FromItem(string name)\n    {\n'
        '        string id;\n'
        '        return name != null && Items.TryGetValue(name, out id) ? id : null;\n    }\n}\n', encoding='utf-8')
    # Standalone targeted patch: never replace cumulative modifiers or localization.
    configs = ET.Element("configs")
    append = ET.SubElement(configs, "append", xpath="/item_modifiers")
    for r in ROWS:
        mod = ET.SubElement(append, "item_modifier", name=r["item"], installable_tags="rebirthCamperWrapCollection", modifier_tags="dye,rebirthCamperWrap", type="attachment", cosmetic_install_chance="0")
        props = {"Extends": "modGeneralMaster", "DescriptionKey": r["item"] + "Desc", "CustomIcon": r["item"], "Material": "Mpaint", "Weight": "20", "EconomicValue": "166.66667", "SoundPickup": "dye_grab", "SoundPlace": "dye_place", "RebirthCamperWrapId": r["id"]}
        for name, value in props.items():
            ET.SubElement(mod, "property", name=name, value=value)
    path = ROOT / "Config/_Vehicles/camper_wrap_collection_item_modifiers.xml"
    path.parent.mkdir(parents=True, exist_ok=True)
    ET.indent(configs, space="  ")
    ET.ElementTree(configs).write(path, encoding="utf-8", xml_declaration=False)
    loc = ROOT / "Tools/VehicleStudio/WrapCollection/localization-additions.csv"
    with loc.open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["Key", "English"])
        for r in ROWS:
            writer.writerow([r["item"], r["displayName"]])
            writer.writerow([r["item"] + "Desc", r["description"]])
    baseline = ROOT / '_Documentation/CamperWrapArt/Baseline/Config/Localization.csv'
    if baseline.is_file():
        original = baseline.read_bytes()
        additions = loc.read_bytes().split(b'\n', 1)[1]
        if original and not original.endswith(b'\n'):
            original += b'\r\n'
        (ROOT / 'Config/Localization.csv').write_bytes(original + additions)
    print(f"Authored {len(ROWS)} unique design briefs, native modifier definitions, and 400 English localization entries. Artwork completion is tracked separately.")


if __name__ == "__main__":
    main()

"""Compose review samples with identical, original game ingredient sprites."""
from pathlib import Path
from hashlib import sha256
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '_Documentation/RecipeCardConsistency'
JOBS = {
    'plaster-cast': ['resourceCloth', 'foodCornMeal', 'resourceCrushedSand', 'drinkJarBoiledWater'],
    'vegetable-stew': ['foodCropPotato', 'foodCropCorn', 'foodCropMushrooms', 'drinkJarBoiledWater'],
}

def main():
    cache, manifest = {}, {}
    for name, ingredients in JOBS.items():
        card = Image.open(OUT / 'Templates' / f'{name}.png').convert('RGBA')
        assert card.size == (1254, 1254)
        records = []
        for index, ingredient in enumerate(ingredients):
            if ingredient not in cache:
                source = ROOT / 'UIAtlases/ItemIconAtlas' / f'{ingredient}.png'
                if not source.exists():
                    source = ROOT.parents[1] / 'Data/ItemIcons' / f'{ingredient}.png'
                sprite = Image.open(source).convert('RGBA')
                sprite.thumbnail((200, 200), Image.Resampling.LANCZOS)
                cache[ingredient] = (source, sprite)
            source, sprite = cache[ingredient]
            center_x = 220 + index * 270
            position = (center_x - sprite.width // 2, 1040 - sprite.height // 2)
            card.alpha_composite(sprite, position)
            records.append(dict(ingredient=ingredient, source=str(source),
                                source_sha256=sha256(source.read_bytes()).hexdigest(),
                                layer_sha256=sha256(sprite.tobytes()).hexdigest(),
                                size=sprite.size, position=position))
        card.save(OUT / f'{name}.png')
        manifest[name] = records
    assert manifest['plaster-cast'][-1] == manifest['vegetable-stew'][-1]
    (OUT / 'ingredient-sources.json').write_text(json.dumps(manifest, indent=2))
    print('Created two samples; water source, pixels, size, and position are identical.')

if __name__ == '__main__':
    main()

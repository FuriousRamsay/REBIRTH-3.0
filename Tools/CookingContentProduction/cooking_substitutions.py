"""Explicit 1:1 ingredient roles from the cooking catalogue.

Only settled substitutions are executable. Prose marked 'pending', 'after
redesign', or 'ratio review' deliberately remains documentation only.
"""
CROP = 'foodCrop'
MUSHROOMS = ['Chanterelle', 'BlackTrumpet', 'KingOyster', 'Enoki']
RULES = {
    'meatstew': {'Potato':['Carrot','Turnip','SweetPotato'], 'Corn':['BellPepper','Onion']},
    'vegstew': {'Potato':['Carrot','Turnip','SweetPotato'], 'Corn':['BellPepper','Onion'], 'Mushrooms':MUSHROOMS+['Zucchini']},
    'skillet': {'Mushrooms':MUSHROOMS, 'Potato':['Carrot','Turnip'], 'Corn':['BellPepper','Onion']},
    'steak': {'Potato':['Carrot','SweetPotato','Turnip'], 'Mushrooms':MUSHROOMS},
    'brisket': {'Mushrooms':MUSHROOMS}, 'pasta': {'Mushrooms':MUSHROOMS},
    'chowder': {'Potato':['SweetPotato','Turnip'], 'Corn':['Carrot']},
    'glazed': {'Potato':['Carrot','SweetPotato'], 'Corn':['BellPepper']},
    'ashstew': {'Mushrooms':MUSHROOMS, 'Corn':['Carrot','BellPepper']},
    'soup': {'Pumpkin':['Butternut'], 'Corn':['Carrot','BellPepper'], 'Mushrooms':MUSHROOMS+['Onion']},
    'potpie': {'Mushrooms':MUSHROOMS, 'Potato':['Carrot','Turnip','SweetPotato']},
    'crumble': {'Blueberries':['Raspberry','Strawberry','Currant'], 'Pumpkin':['Butternut']},
    'berrypie': {'Blueberries':['Raspberry','Currant','Strawberry']},
    'pumpkinpie': {'Pumpkin':['Butternut','SweetPotato']},
    'pumpkinbread': {'Pumpkin':['Butternut','SweetPotato']},
    'cheesecake': {'Pumpkin':['Butternut']},
    'N01': {'Carrot':['SweetPotato']},
    'N02': {'Barley':['Rice'], 'Carrot':['Turnip']},
    'N03': {'Turnip':['Carrot']},
    'N04': {'Carrot':['Potato'], 'Turnip':['Potato'], 'SweetPotato':['Potato']},
    'N05': {'SwissChard':['Zucchini'], 'Onion':['BellPepper']},
    'N06': {'SwissChard':['Zucchini'], 'Rice':['Barley']},
    'N07': {'Zucchini':['Carrot']},
    'N08': {'SwissChard':['BellPepper'], 'Tomato':['Zucchini']},
    'N09': {'Zucchini':['BellPepper','Chanterelle']},
    'N10': {'Cucumber':['Zucchini'], 'BellPepper':['Carrot'], 'Tomato':['Radish']},
    'N11': {'Radish':['Carrot'], 'Lettuce':['SwissChard']},
    'N12': {'Chanterelle':['BlackTrumpet'], 'KingOyster':['Enoki']},
    'N13': {'BlackTrumpet':['Chanterelle'], 'Carrot':['Turnip'], 'Barley':['Rice']},
    'N14': {'Enoki':['KingOyster'], 'SwissChard':['Zucchini']},
    'N15': {'BlackTrumpet':['Chanterelle','KingOyster'], 'Potato':['Turnip']},
    'N16': {'Butternut':['Pumpkin'], 'SwissChard':['Zucchini']},
    'N17': {'Cranberries':['Currant','Gooseberry']},
    'N18': {'Strawberry':['Raspberry']},
    'N19': {'Currant':['Cranberries','Gooseberry']},
    'N20': {'Gooseberry':['Currant','Raspberry','Strawberry']},
    'N21': {'Raspberry':['Strawberry','Currant']},
    'N30': {'Barley':['Rice'], 'BellPepper':['Zucchini']},
    'N31': {'Gooseberry':['Cranberries','Raspberry'], 'Currant':['Cranberries','Raspberry']},
    'N34': {'Onion':['Carrot']},
    'N35': {'KingOyster':['Chanterelle']},
    'N36': {'Enoki':['KingOyster'], 'SwissChard':['Zucchini'], 'Rice':['Barley']},
    'N37': {'Raspberry':['Strawberry']}, 'N38': {'Currant':['Gooseberry']},
    'N40': {'Turnip':['Carrot'], 'Chanterelle':['BlackTrumpet']},
    'N42': {'BlackTrumpet':['Chanterelle']},
    'N43': {'Rice':['Barley']},
    'N44': {'SweetPotato':['Butternut'], 'BlackTrumpet':['Chanterelle']},
    'N45': {'Barley':['Rice'], 'KingOyster':['Chanterelle']},
    'N46': {'Lettuce':['SwissChard']}, 'N47': {'Zucchini':['BellPepper']},
    'N48': {'Enoki':['KingOyster']},
    'N50': {'Radish':['Turnip'], 'Enoki':['KingOyster']},
    'N51': {'Butternut':['Pumpkin'], 'Barley':['Rice']},
    'N52': {'Butternut':['SweetPotato']},
    'N53': {'SweetPotato':['Potato'], 'BellPepper':['Zucchini']},
    'N54': {'Carrot':['Turnip']}, 'N55': {'Barley':['Rice']},
    'N56': {'Lettuce':['SwissChard']},
    'N57': {'Strawberry':['Raspberry'], 'Cucumber':['Radish']},
    'N58': {'Strawberry':['Raspberry']}, 'N59': {'Raspberry':['Strawberry']},
    'N60': {'Raspberry':['Strawberry','Currant'], 'Gooseberry':['Strawberry','Currant']},
    'N61': {'Gooseberry':['Currant']},
    'N62': {'Cranberries':['Currant'], 'Barley':['Rice']},
    'N63': {'Cranberries':['Currant']},
    'N64': {'SweetPotato':['Butternut'], 'Cranberries':['Currant']},
    'N65': {'Currant':['Gooseberry'], 'Rice':['Barley']},
    'N66': {'Carrot':['Radish'], 'Currant':['Cranberries']},
}

def substitutions(key):
    result = {CROP+source:[CROP+x for x in choices] for source,choices in RULES.get(key,{}).items()}
    if key in ('boiledmeat','cornbread','egg'):
        result['drinkJarBoiledWater']=['drinkJarPureMineralWater']
    if key in ('tacos',): result['foodCanSalmon']=['foodCanTuna']
    if key in ('tunatoast','N56'): result['foodCanTuna']=['foodCanSalmon']
    if key in ('tunatoast','gumbo'): result['foodCanPeas']=['foodCropCorn']
    if key == 'gumbo': result['foodCanPeas'].append('foodCropBellPepper')
    if key == 'shepherd':
        result.update(foodCropPotato=['foodCropSweetPotato'],foodCanPeas=['foodCropCarrot'],foodCropCorn=['foodCropOnion','foodCropBellPepper'])
    if key == 'N33': result['foodHoney']=['rebirthCookingFoodP03']
    if key == 'N41': result['foodCornMeal']=['rebirthCookingFoodP02']
    return result

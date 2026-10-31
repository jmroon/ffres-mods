"""Compare every authored Slate font size with the vanilla source, including inheritance."""
import json
from pathlib import Path

import sys
ROOT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent
SOURCE = ROOT / 'original-font-json'
CLONES = {
    'CL_NarrationText': 'ChapterTitleText',
    'CL_OpeningNarrationText': 'TelopText',
    'CL_BattleNameText': 'UnipueText',
    'CL_BattleNumberStyle': 'BP_SystemTextStyle_PramNUM',
}

def font_sizes(node, path=()):
    found = {}
    if isinstance(node, dict):
        if node.get('StructType') == 'SlateFontInfo':
            found[path] = next((p['Value'] for p in node['Value'] if p.get('Name') == 'Size'), 'inherited')
        for key, value in node.items():
            found.update(font_sizes(value, path + (key,)))
    elif isinstance(node, list):
        for index, value in enumerate(node):
            key = (value.get('Name', index), value.get('ArrayIndex', 0)) if isinstance(value, dict) else index
            found.update(font_sizes(value, path + (key,)))
    return found

def sizes_by_export(asset, rename_from, rename_to):
    def name(index):
        e = asset['Exports'][index - 1]
        parent = name(e['OuterIndex']) + '.' if e['OuterIndex'] > 0 else ''
        return (parent + e['ObjectName']).replace(rename_from, rename_to)
    return {name(i + 1): font_sizes(e.get('Data', []))
            for i, e in enumerate(asset['Exports']) if font_sizes(e.get('Data', []))}

for path in (ROOT / 'default-font-json').glob('*.json'):
    original_name = CLONES.get(path.stem, path.stem)
    original = json.loads(next(SOURCE.rglob(original_name + '.json')).read_text(encoding='utf-8'))
    edited = json.loads(path.read_text(encoding='utf-8'))
    assert sizes_by_export(edited, path.stem, original_name) == sizes_by_export(original, original_name, original_name), path.stem
    print('Default font sizes verified:', path.stem)

# No reduced shared styles or old dialogue-preservation clones in the new pack.
assert len(list((ROOT / 'default-size-fonts').rglob('*.uasset'))) == 11
assert not list((ROOT / 'default-size-fonts').rglob('CL_UnchangedDialogueStyle.uasset'))

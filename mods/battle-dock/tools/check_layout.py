"""Check fixed bounds and the retained native highlight/limit paths before install."""
import json
from pathlib import Path

import sys
ROOT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent

def asset(name):
    return json.loads((ROOT / 'edited-json' / (name + '.json')).read_text())

def export(data, name):
    return next(e for e in data['Exports'] if e['ObjectName'] == name)

def value(item, name):
    return next(p['Value'] for p in item['Data'] if p['Name'] == name)

def bounds(data, name, width, height):
    item = export(data, name)
    assert value(item, 'WidthOverride') == width
    assert value(item, 'HeightOverride') == height
    assert value(item, 'bOverride_WidthOverride') is True
    assert value(item, 'bOverride_HeightOverride') is True

def ref(data, index):
    return data['Exports'][index - 1]['ObjectName']

bounds(asset('WBP_BattleLayout'), 'BD_DockBounds', 742, 472)
bounds(asset('WBP_BattleLayout'), 'BD_HelpBounds', 726, 120)
bounds(asset('WBP_BtlCmd_ListFrame'), 'BD_CommandBounds', 318, 328)
bounds(asset('WBP_BtlCmd_PrimaryList'), 'SizeBox_0', 318, 328)
bounds(asset('WBP_BtlCmd_SecondaryList'), 'SizeBox_0', 318, 304)
party = asset('WBP_Btl_UnitStatus')
bounds(party, 'SizeBox_0', 400, 82)
bounds(party, 'BD_ConditionBounds', 128, 16)
highlight = export(party, 'WBP_Subhead')
slot = party['Exports'][value(highlight, 'Slot') - 1]
assert ref(party, value(slot, 'Parent')) == 'Overlay_1'
assert ref(party, value(slot, 'Content')) == 'WBP_Subhead'
binding = value(highlight, 'NamedSlotBindings')[0]['Value']
assert next(p['Value'] for p in binding if p['Name'] == 'Name') == 'NamedSlot_63'
assert ref(party, next(p['Value'] for p in binding if p['Name'] == 'Content')) == 'BD_ActiveOutline'
function = export(party, 'SetIsAction')['ScriptBytecode']
assert any(e.get('ContextExpression', {}).get('VirtualFunctionName') == 'SetVisibility'
           and e.get('ObjectExpression', {}).get('Variable', {}).get('New', {}).get('Path') == ['WBP_Subhead']
           for e in function)
for name in ('WBP_Btl_UnitStatus', 'WBP_BtlCmd_PrimaryList', 'WBP_BtlCmd_SecondaryList'):
    data = asset(name)
    frame = export(data, 'WBP_BtlWindow')
    assert float(value(frame, 'RenderOpacity')) == 0
    assert value(frame, 'NamedSlotBindings') == []
    assert value(frame, 'Slot') > 0  # still in the tree for original callbacks
header = asset('WBP_BtlCmd_ListHeader')
assert value(export(header, 'CPP_WBP_HorizontalGradient_125'), 'Visibility') == 'Collapsed'
assert export(header, 'WBP_TagKeyGuide')
assert export(header, 'Overlay_Limit')
print('Fixed dock bounds, party rows, native highlight and retained limit controls checked.')

# Borders must sit above content; a transparent frame must not intercept input.
dock = asset('WBP_BattleLayout')
def children(data, name):
    return [ref(data, value(data['Exports'][slot['Value'] - 1], 'Content'))
            for slot in value(export(data, name), 'Slots')]
assert children(dock, 'Overlay_Padding')[-1] == 'BD_BorderBounds'
assert value(export(dock, 'BD_BorderBounds'), 'Visibility') == 'HitTestInvisible'
assert children(party, 'Overlay_1')[-1] == 'WBP_Subhead'
assert value(export(dock, 'BD_DockBorder'), 'Visibility') == 'HitTestInvisible'
assert value(export(dock, 'BD_HelpScroll'), 'Clipping') == 'ClipToBounds'
assert 'BD_HelpScroll' in [p['Name'] for p in export(dock, 'WBP_BattleLayout_C')['LoadedProperties']]
assert children(party, 'BD_ActiveOutline') == ['BD_ActiveShape', 'BD_ActiveFrame']

background = asset('BD_ClearBackground')
for name in ('CPP_WBP_HorizontalGradient_573', 'LineImage'):
    assert float(value(export(background, name), 'RenderOpacity')) == 0
button = asset('BD_FlatButton')
assert value(export(button, 'Image_Shadow'), 'Visibility') == 'Collapsed'
assert export(button, 'BD_RowFrame')
assert export(button, 'Overlay_ButtonEvent')  # original selection effects still have a target
for entry, replacement in (('WBP_BtlCmd_RootEntry', 'BD_ActionEntry_C'),
                           ('WBP_BtlCmd_SkillEntry', 'BD_ActionEntry_C'),
                           ('WBP_BtlCmd_ItemEntry', 'BD_InventoryItemEntry_C')):
    data = asset(entry)
    index = export(data, 'PathButton')['ClassIndex']
    assert data['Imports'][-index - 1]['ObjectName'] == replacement
assert any(i['ObjectName'] == 'BD_ButtonStyle' for i in button['Imports'])
print('Menu artwork, fixed scrollable help strip and battle-only button chain checked.')

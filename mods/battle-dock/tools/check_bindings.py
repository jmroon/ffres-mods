"""Check that the new portrait binding resolves to the intended native fields."""
import json
from pathlib import Path

import sys
ROOT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent
asset = json.loads((ROOT / 'edited-json/WBP_Btl_UnitStatus.json').read_text())


def field(properties, name):
    return next(p for p in properties if p['Name'] == name)['Value']


def resolve(index):
    if not index:
        return ''
    obj = asset['Exports'][index - 1] if index > 0 else asset['Imports'][-index - 1]
    parent = resolve(obj['OuterIndex'])
    return (parent + '.' if parent else '') + obj['ObjectName']


vm = next(e for e in asset['Exports'] if e['ObjectName'] == 'MVVMViewClass_0')['Data']
library = field(vm, 'BindingLibrary')
names = [p['Value'] for p in field(library, 'CompiledFieldNames')]
properties, functions = [], []
for group in field(library, 'CompiledFields'):
    data = group['Value']
    start = field(data, 'LibraryStartIndex')
    count = field(data, 'NumberOfProperties')
    owner = resolve(field(data, 'ClassOrScriptStruct'))
    properties.extend((owner, n) for n in names[start:start + count])
    functions.extend((owner, n) for n in names[start + count:start + count + field(data, 'NumberOfFunctions')])

paths = field(library, 'FieldPaths')
bindings = field(vm, 'Bindings')
binding = bindings[-1]['Value']
compiled = field(binding, 'Binding')


def path(name):
    data = field(compiled, name)
    start, count = field(data, 'StartIndex'), field(data, 'Num')
    result = []
    for entry in paths[start:start + count]:
        values = entry['Value']
        table = properties if field(values, 'bIsProperty') else functions
        result.append(table[field(values, 'Index')])
    return result


assert path('SourceFieldPath') == [
    ('WBP_Btl_UnitStatus_C', 'CPP_VM_BtlUnitParam'),
    ('/Script/mucha.CPP_VM_UnitBase', 'UnitId'),
]
assert path('DestinationFieldPath') == [
    ('WBP_Btl_UnitStatus_C', 'BD_Portrait'),
    ('/Game/UI/Common/UnitList/Widget/Entry/Parts/Icon/BD_Portrait.BD_Portrait_C', 'SetUnitId'),
]
sources = field(vm, 'Sources')
source_index = next(i for i, s in enumerate(sources) if field(s['Value'], 'PropertyName') == 'CPP_VM_BtlUnitParam')
assert field(binding, 'SourceBitField') == 1 << source_index
trigger = field(sources[source_index]['Value'], 'Bindings')[-1]['Value']
assert field(field(trigger, 'FieldId'), 'FieldName') == 'UnitId'
assert field(field(trigger, 'BindingKey'), 'Index') == len(bindings) - 1
cls = next(e for e in asset['Exports'] if e['ObjectName'] == 'WBP_Btl_UnitStatus_C')
portrait_field = next(p for p in cls['LoadedProperties'] if p['Name'] == 'BD_Portrait')
assert resolve(portrait_field['PropertyClass']).endswith('.BD_Portrait_C')
portrait = next(e for e in asset['Exports'] if e['ObjectName'] == 'BD_Portrait')
assert portrait['ClassIndex'] == portrait_field['PropertyClass']
assert resolve(portrait['TemplateIndex']).endswith('.Default__BD_Portrait_C')

# Existing bindings and class properties must survive unchanged; only append.
changes = json.loads((ROOT / 'asset-changes.json').read_text())
card = next(c for c in changes if c['asset'].endswith('/WBP_Btl_UnitStatus.uasset') or c['asset'].endswith('\\WBP_Btl_UnitStatus.uasset'))
before, after = card['before'], card['after']
for key, old in before.items():
    if old['class'] == '/Script/CoreUObject.Function':
        assert old == after[key], key
print('Portrait source, destination, notification, reflected property and original function checks passed.')

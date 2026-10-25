"""Build a separate four-choice AA-method row. Never edit installed game assets."""
from pathlib import Path
import struct, shutil
from decode_menu import decode, names_for, Reader, parse



def pack(fmt, *args):
    return struct.pack('<' + fmt, *args)

def string(text):
    raw = text.encode('utf-8') + b'\0'
    return pack('i', len(raw)) + raw

def text(label):
    return pack('Ib', 0, 0) + string('') + string('AAProbe_' + label) + string(label)

def write_asset(source, output, payload, additions=(), rename=False):
    original = source.read_bytes()
    header = bytearray(original.replace(b'_Rank', b'_Type') if rename else original)
    reader = Reader(header, 52)
    reader.string(); reader.get('I')
    summary = reader.p
    count, names_offset = struct.unpack_from('<2i', header, summary)
    export_count, export_offset = struct.unpack_from('<2i', header, summary + 24)
    assert export_count == 1
    reader.p = names_offset
    for _ in range(count):
        reader.string(); reader.p += 4
    names_end = reader.p
    added = b''.join(string(name) + b'\0' * 4 for name in additions)
    delta = len(added)
    old_size = struct.unpack_from('<q', header, export_offset + 28)[0]
    assert len(source.with_suffix('.uexp').read_bytes()) == old_size + 4
    # Retoc legacy summary layout, verified for these two UE5.6 exports.
    for relative in (12, 28, 36, 44, 52, 60, 136, 160):
        pos = summary + relative
        value = struct.unpack_from('<i', header, pos)[0]
        assert names_end <= value <= len(header), (relative, value)
        struct.pack_into('<i', header, pos, value + delta)
    struct.pack_into('<i', header, 44, len(header) + delta)
    for relative in (0, 88):
        struct.pack_into('<i', header, summary + relative, count + len(additions))
    if additions:
        # Zen conversion preserves this prefix as the export's name map.
        struct.pack_into('<i', header, summary + 164, count + len(additions))
    struct.pack_into('<q', header, summary + 140, len(header) + delta + len(payload))
    struct.pack_into('<q', header, export_offset + 28, len(payload))
    struct.pack_into('<q', header, export_offset + 36, len(header) + delta)
    header[names_end:names_end] = added
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(header)
    output.with_suffix('.uexp').write_bytes(payload + pack('I', 0x9e2a83c1))
    assert parse(output)[2][0]['size'] == len(payload)

def build_menu(SOURCE, DEST):
    assert not DEST.exists(), 'Build folder already exists; use a fresh folder.'
    source = next(SOURCE.rglob('DT_OP_GraphicSetting.uasset'))
    rank = next(SOURCE.rglob('DT_OP_GraphicSetting_Rank.uasset'))
    spans = {}
    original = decode(source, spans)
    data = source.with_suffix('.uexp').read_bytes()[:-4]
    names = names_for(source)
    additions = [
        'Delegate.UI.System.Option.Root.GraphicSetting.AAType',
        '/Game/Datatable/UI/Option/Root/GraphicSetting/DT_OP_GraphicSetting_Type',
        'DT_OP_GraphicSetting_Type',
    ]
    tag_index, path_index, asset_index = range(len(names), len(names) + 3)
    start, end = spans['rows']['NewRow_8']
    row = bytearray(data[start:end])
    edits = {
        'DisplayText': text('AA Method'),
        'EntryTag': pack('H2i', 0x300, tag_index, 0),
        'Command': text('r.AntiAliasingMethod'),
        # UE5.6 uses FUtf8String here, not FString: empty is length0, with no terminator.
        'DT_RadioButton': pack('5i', path_index, 0, asset_index, 0, 0),
        'DefaultRadioButtonIndex': pack('i', 1),
        'IsBranchInitValue': pack('B', 0),
        'ItemDescription': text('Choose the anti-aliasing method.'),
    }
    for name, a, b, zero in sorted(spans['fields'], key=lambda x: x[1], reverse=True):
        if start <= a < end and name in edits:
            assert not zero, name
            row[a-start:b-start] = edits.pop(name)
    assert not edits, edits
    struct.pack_into('<2i', row, 0, names.index('NewRow'), 101)
    payload = bytearray(data[:end] + row + data[end:])
    count_offset = min(a for a,b in spans['rows'].values()) - 4
    struct.pack_into('<i', payload, count_offset, len(original['rows']) + 1)
    out = DEST / source.relative_to(SOURCE)
    write_asset(source, out, payload, additions)
    result = decode(out)
    assert len(result['rows']) == 11
    for name, info in original['rows'].items():
        assert result['rows'][name] == info, name
    new = result['rows']['NewRow_100']
    assert new['EntryTag']['TagName'] == additions[0]
    assert new['Command']['text'] == 'r.AntiAliasingMethod'
    assert new['IsBranchInitValue'] == 0
    rank_spans = {}
    decode(rank, rank_spans)
    rank_names = names_for(rank)
    first_row = min(a for a,b in rank_spans['rows'].values())
    prefix = bytearray(rank.with_suffix('.uexp').read_bytes()[:first_row])
    struct.pack_into('<i', prefix, first_row - 4, 4)
    labels = ['Off', 'FXAA', 'TAA', 'TSR']
    for i, label in enumerate(labels):
        prefix += pack('2iH', rank_names.index('NewRow'), i, 0x300) + text(label)
    rank_out = DEST / rank.relative_to(SOURCE).with_name('DT_OP_GraphicSetting_Type.uasset')
    write_asset(rank, rank_out, prefix, rename=True)
    assert [r['DisplayText']['text'] for r in decode(rank_out)['rows'].values()] == labels
    shutil.copy2(SOURCE / 'scriptobjects.bin', DEST / 'scriptobjects.bin')
    print('Verified: all 10 original rows unchanged; native AAType selector added with TSR at index3 (runtime maps to method4).')

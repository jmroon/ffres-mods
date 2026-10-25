"""Read caller-supplied uncompressed v4 mappings; no mappings are bundled."""
from legacy_asset import Reader
schemas = {}

def load_mappings(path):
    global schemas
    r=Reader(path.read_bytes())
    assert r.get('H')==0x30c4 and r.get('B')==4
    assert r.get('I')==0 and r.get('B')==0
    compressed,size=r.get('2I');assert compressed==size
    names=[]
    for _ in range(r.get('I')):
        n=r.get('H');names.append(r.b[r.p:r.p+n].decode());r.p+=n
    def name():
        i=r.get('i');return None if i==-1 else names[i]
    for _ in range(r.get('I')):
        name()
        for _ in range(r.get('H')):r.get('Q');name()
    def typ():
        t={'kind':r.get('B')}
        if t['kind']==9:t['struct']=name()
        elif t['kind']==26:t['inner']=typ();t['enum']=name()
        elif t['kind'] in (8,25,28):t['inner']=typ()
        elif t['kind']==24:t['inner']=typ();t['value']=typ()
        return t
    schemas={}
    for _ in range(r.get('I')):
        sn,supername=name(),name();count,n=r.get('2H');fields={}
        for _ in range(n):
            i,dim=r.get('HB');pn=name();pt=typ()
            for j in range(dim):fields[i+j]=(pn,pt)
        schemas[sn]={'super':supername,'count':count,'fields':fields}

def field(sn,i):
    s=schemas[sn]
    if i<s['count']:return s['fields'][i]
    return field(s['super'],i-s['count'])
def header(a):
    fragments=[];bits=0
    while True:
        v=a.get('H');skip=v&127;count=v>>9;has=bool(v&128)
        fragments.append((skip,count,has));bits+=count if has else 0
        if v&256:break
        assert len(fragments)<1000
    n=0 if not bits else 1 if bits<=8 else 2 if bits<=16 else ((bits+31)//32)*4
    mask=int.from_bytes(a.b[a.p:a.p+n],'little');a.p+=n
    fields=[];index=0;bit=0
    for skip,count,has in fragments:
        index+=skip
        for _ in range(count):
            zero=bool((mask>>bit)&1) if has else False
            fields.append((index,zero));index+=1
            if has:bit+=1
    return fields

"""Reader for the retoc UE5.6 legacy format used by this mod."""
from pathlib import Path
import struct
class Reader:
    def __init__(self,b,p=0):self.b,self.p=b,p
    def get(self,f):
        v=struct.unpack_from('<'+f,self.b,self.p); self.p+=struct.calcsize('<'+f)
        return v[0] if len(v)==1 else v
    def string(self):
        n=self.get('i'); size=n if n>=0 else -n*2
        b=self.b[self.p:self.p+size]; self.p+=size
        return b.decode('utf-8' if n>=0 else 'utf-16le').rstrip('\0')
def parse(path):
    b=path.read_bytes();r=Reader(b)
    assert r.get('I')==0x9e2a83c1
    assert r.get('i')==-9
    versions=r.get('4i');r.p+=20
    header_size=r.get('i');assert header_size==len(b)
    n=r.get('i');r.p+=20*n
    package=r.string(); flags=r.get('I'); assert flags&0x80002000==0x80002000
    nc,no=r.get('2i');r.p+=16
    ec,eo=r.get('2i');ic,io=r.get('2i')
    r=Reader(b,no);names=[]
    for _ in range(nc):names.append(r.string());r.p+=4
    def name(i,n):return names[i]+('_'+str(n-1) if n else '')
    imports=[];r=Reader(b,io)
    for _ in range(ic):
        cp,cn,outer,on,opt=r.get('2i'),r.get('2i'),r.get('i'),r.get('2i'),r.get('I')
        imports.append({'class':name(*cn),'name':name(*on),'outer':outer})
    exports=[];r=Reader(b,eo)
    for _ in range(ec):
        start=r.p
        cl,su,te,ou=r.get('4i');on=r.get('2i');of=r.get('I');size,off=r.get('2q')
        r.p+=8*4+5*4
        assert r.p-start==96
        exports.append({'class':imports[-cl-1]['name'] if cl<0 else cl,'name':name(*on),'offset':off-len(b),'size':size})
    return package,imports,exports

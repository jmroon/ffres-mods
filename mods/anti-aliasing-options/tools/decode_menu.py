"""Decode and compare the two menu tables used by the mod."""
from legacy_asset import Reader, parse
from unversioned import header, field

def names_for(path):
 a=Reader(path.read_bytes());a.p=8;a.get('4i');a.p+=20;a.get('i');n=a.get('i');a.p+=20*n;a.string();a.get('I');count,offset=a.get('2i')
 a.p=offset;names=[]
 for i in range(count):names.append(a.string());a.p+=4
 return names
def decode(path, spans=None):
 names=names_for(path)
 def name(a):
  i,n=a.get('2i')
  assert 0<=i<len(names),(a.p,i)
  return names[i]+('_'+str(n-1) if n else '')
 def text(a):
  flags=a.get('I');history=a.get('b')
  if history==11:return {'table':name(a),'key':a.string()}
  if history==0:return {'namespace':a.string(),'key':a.string(),'text':a.string()}
  if history==-1:
   return {'invariant':a.string()} if a.get('i') else ''
  raise ValueError(('text',a.p,history))
 def value(a,t):
  k=t['kind']
  if k==5:return name(a)
  if k==11:return text(a)
  if k==17:return {'package':name(a),'asset':name(a),'subpath':a.string()}
  if k==26:return value(a,t['inner'])
  if k==24:
   removed=a.get('i');assert removed==0,(a.p,removed)
   count=a.get('i');assert 0<=count<100,(a.p,count)
   return [[name(a) if t['inner']['kind']==26 else value(a,t['inner']),value(a,t['value'])] for _ in range(count)]
  if k==9:
   sn=t['struct']
   if sn=='Guid':return a.get('4I')
   return obj(a,sn)
  if k==8:return [value(a,t['inner']) for _ in range(a.get('i'))]
  if k==10:return a.string()
  fmts={0:'B',1:'B',2:'i',3:'f',4:'i',7:'d',19:'I'}
  if k in fmts:return a.get(fmts[k])
  raise ValueError(('kind',a.p,t))
 def obj(a,sn):
  result={}
  for index,zero in header(a):
   pn,pt=field(sn,index)
   start=a.p
   try:result[pn]=0 if zero else value(a,pt)
   except Exception as e:raise ValueError((sn,pn,a.p,result)) from e
   if spans is not None and sn=='OP_BaseMenu_EntryInfo':
    spans.setdefault('fields',[]).append((pn,start,a.p,zero))
  return result
 pkg,imports,exports=parse(path)
 a=Reader(path.with_suffix('.uexp').read_bytes())
 props=obj(a,'DataTable');assert a.get('i')==0
 rows={}
 count=a.get('i')
 rowtype=imports[-props['RowStruct']-1]['name']
 for _ in range(count):
  start=a.p
  rowname=name(a);rows[rowname]=obj(a,rowtype)
  if spans is not None:spans.setdefault('rows',{})[rowname]=(start,a.p)
 assert a.p==exports[0]['size'],(path.name,a.p,exports[0]['size'])
 return {'package':pkg,'row_type':rowtype,'rows':rows}

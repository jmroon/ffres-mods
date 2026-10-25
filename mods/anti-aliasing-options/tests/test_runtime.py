"""Exercise the production Lua with simulated Unreal objects and reset events."""
from pathlib import Path

ROOT = Path(__file__).parent
from lupa.lua54 import LuaRuntime

lua = LuaRuntime()
lua.execute('''
values={['r.AntiAliasingMethod']=1,['r.ScreenPercentage']=100,
 ['r.TSR.History.ScreenPercentage']=100,['r.DepthOfFieldQuality']=2,
 ['sg.AntiAliasingQuality']=3,['r.BloomQuality']=5}
saved={Value=3,IsDisplay=true,Type=2}
registered={}; commands={}; saveWrites=0; controllerLookups=0; validWorld=true; validOptions=true
failRead=false; failQueue=false; holdQueue=false; logs={}
print=function(text) logs[#logs+1]=text end
FName=function(text) return text end
world={IsValid=function() return validWorld end}
controller={IsValid=function() return true end,GetWorld=function() return world end}
console={IsValid=function() return true end,
 GetConsoleVariableFloatValue=function(_,name) return values[name] end,
 ExecuteConsoleCommand=function(_,context,command,pc)
  local key,value=command:match('^(%S+) (%S+)$')
  assert(key=='r.AntiAliasingMethod', 'Unexpected graphics mutation')
  commands[#commands+1]=command;values[key]=tonumber(value)
 end}
options={IsValid=function() return validOptions end,
 GetOptionSetting=function(_,tag)
  assert(tag.TagName=='Delegate.UI.System.Option.Root.GraphicSetting.AAType')
  if failRead then error('save unavailable') end
  return {Value=saved.Value,IsDisplay=saved.IsDisplay,Type=saved.Type}
 end,
 SetOptionSettingData=function(_,tag,value)
  saveWrites=saveWrites+1;error("Script must not write option saves")
 end}
package.preload.UEHelpers=function() return {
 GetPlayerController=function() controllerLookups=controllerLookups+1;return controller end,
 GetKismetSystemLibrary=function() return console end} end
StaticFindObject=function(path)
 if path=='/Script/mucha.Default__CPP_BFL_SaveDataOption' then return options end
 return {IsValid=function() return true end}
end
RegisterHook=function(path,pre,post)
 assert(not registered[path]);registered[path]={pre,post};return 1,2
end
LoopAsync=function(interval,callback) assert(interval==250);poll=callback end
ExecuteInGameThread=function(callback)
 if failQueue then error('queue failure') end
 if holdQueue then assert(not queued);queued=callback else callback() end
end
''')
lua.execute((ROOT.parent / 'src/main.lua').read_text())
lua.execute('''
poll();assert(values['r.AntiAliasingMethod']==4)
local count=0;for _ in pairs(registered) do count=count+1 end;assert(count==6)
local methodHook=registered['/Script/mucha.CPP_OptionFunctionHandler:HandleChangeAAType']
for choice,method in pairs({[0]=0,[1]=1,[2]=2,[3]=4}) do
 saved.Value=choice
 for name,hook in pairs(registered) do
  values['r.AntiAliasingMethod']=1
  local parameter={get=function() return {SelectedItem=choice} end}
  assert(hook[1](nil,parameter)==nil and hook[2](nil,parameter)==nil)
  assert(values['r.AntiAliasingMethod']==method, name)
 end
 values['r.AntiAliasingMethod']=1;poll();assert(values['r.AntiAliasingMethod']==method)
 local writes=#commands;poll();assert(#commands==writes, 'Unnecessary console write')
end
-- The direct menu callback maps its argument without modifying the delegate.
saved.Value=1
local argument={SelectedItem=3}
methodHook[2](nil,{get=function() return argument end})
assert(values['r.AntiAliasingMethod']==4 and argument.SelectedItem==3)
poll();assert(values['r.AntiAliasingMethod']==1, 'Cancelled edit was retained')
saved.Value=3;poll();assert(values['r.AntiAliasingMethod']==4, 'Saved choice not followed')
-- Unknown/uninitialized data must not guess an AA choice.
for _,unknown in ipairs({-1,4,7,1.5}) do
 saved.Value=unknown;values['r.AntiAliasingMethod']=2
 local lookups=controllerLookups;poll()
 methodHook[2](nil,{get=function() return {SelectedItem=unknown} end})
 assert(values['r.AntiAliasingMethod']==2 and controllerLookups==lookups)
end
saved.Value=3;validOptions=false;poll();assert(values['r.AntiAliasingMethod']==2)
validOptions=true;validWorld=false;poll();assert(values['r.AntiAliasingMethod']==2)
validWorld=true;failRead=true;poll();assert(values['r.AntiAliasingMethod']==2)
failRead=false;failQueue=true;poll();assert(values['r.AntiAliasingMethod']==2)
failQueue=false;holdQueue=true;poll();poll();assert(queued)
local pending=queued;queued=nil;holdQueue=false;pending()
assert(values['r.AntiAliasingMethod']==4)
values['r.ScreenPercentage']=75;values['r.TSR.History.ScreenPercentage']=200;poll()
assert(values['r.ScreenPercentage']==75 and values['r.TSR.History.ScreenPercentage']==200)
assert(values['r.DepthOfFieldQuality']==2 and values['sg.AntiAliasingQuality']==3
 and values['r.BloomQuality']==5)
assert(saveWrites==0, 'Unexpected save mutation')
''')
print('PASS: all four choices, six hooks, poll fallback, menu cancel/apply, read-only save access,')
print('      missing/invalid saves, transient errors, queue guard, no-op writes, graphics scope.')

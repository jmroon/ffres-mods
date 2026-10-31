"""Exercise tooltip ownership, Show Tips, battle exit and destroyed-widget recovery."""
from pathlib import Path
from lupa import LuaRuntime

lua = LuaRuntime()
lua.execute('''
function widget()
    local w = {valid=true, visible=true, children={}, visibility=0}
    function w:IsValid() return self.valid end
    function w:GetFullName() return '/Engine/Transient.' .. tostring(self) end
    function w:IsVisible() return self.visible end
    function w:IsActivated() return self.active end
    function w:GetVisibility() return self.visibility end
    function w:SetVisibility(v) self.visibility=v end
    function w:GetChildAt(i) return self.children[i+1] end
    function w:RemoveFromParent()
        if self.parent then
            for i,c in ipairs(self.parent.children) do
                if c==self then table.remove(self.parent.children,i); break end
            end
        end
        self.parent=nil
    end
    function w:AddChild(child)
        if self.failNext then self.failNext=false; error('Transient add failure') end
        table.insert(self.children,child); child.parent=self
        return {SetHorizontalAlignment=function()end, SetPadding=function()end}
    end
    function w:ScrollToStart() self.resets=(self.resets or 0)+1 end
    return w
end
background=widget(); content=widget(); root=widget(); scroll=widget()
content:AddChild(background); root:AddChild(content)
tooltip=widget(); tooltip.SizeBox_0=root
battle=widget(); battle.active=true; battle.BD_HelpScroll=scroll
function FindAllOf(class)
    if class=='WBP_BattleLayout_C' then return {battle} end
    return {tooltip}
end
function LoopAsync(_, callback) tick=callback end
function ExecuteInGameThread(callback) callback() end
''')
lua.execute((Path(__file__).parent.parent / 'runtime/main.lua').read_text())
lua.execute('''
tick(); assert(content.parent==scroll and #root.children==0)
assert(background.visibility==1 and content.visibility==0)
tick(); assert(#scroll.children==1) -- no duplicate attachment
tooltip.visible=false; tick(); assert(content.visibility==1)
tooltip.visible=true; tick(); assert(content.visibility==0 and scroll.resets==3)
battle.active=false; tick()
assert(content.parent==root and background.visibility==0 and content.visibility==0)
battle.active=true; scroll.failNext=true; tick()
assert(content.parent==root) -- failed move rolled back
tick(); assert(content.parent==scroll)
battle.valid=false; tick(); assert(content.parent==root)
battle.valid=true; tick(); assert(content.parent==scroll)
scroll.valid=false; tick(); assert(content.parent==root)
''')
print('Help strip lifecycle checks passed (mocked Unreal API; live verification still required).')

local AA = require("aa_core")

-- Menu positions are zero-based. Unreal reserves method 3 for MSAA.
local methods = { [0] = 0, [1] = 1, [2] = 2, [3] = 4 }
local settingTag = "Delegate.UI.System.Option.Root.GraphicSetting.AAType"

local function savedMethod()
    local options = StaticFindObject("/Script/mucha.Default__CPP_BFL_SaveDataOption")
    if options == nil or not options:IsValid() then
        return nil
    end

    local tag = { TagName = FName(settingTag) }
    local saved = options:GetOptionSetting(tag)
    return methods[saved.Value] -- Unknown/uninitialized choices are skipped.
end

local function menuMethod(parameter)
    local choice = parameter:get().SelectedItem
    if choice == nil then
        return savedMethod()
    end
    return methods[choice]
end

AA.start({
    name = "Anti-Aliasing Options",
    getMethod = savedMethod,
    getMenuMethod = menuMethod,
})

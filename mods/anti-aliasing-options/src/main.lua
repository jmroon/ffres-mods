local Unreal = require("UEHelpers")

-- Menu positions are zero-based. Unreal reserves method 3 for MSAA.
local methods = { [0] = 0, [1] = 1, [2] = 2, [3] = 4 }
local settingTag = "Delegate.UI.System.Option.Root.GraphicSetting.AAType"
local registeredHooks = {}
local pollPending = false
local nextErrorLog = 0

local function isValid(object)
    return object ~= nil and object:IsValid()
end

local function log(message)
    print("[Anti-Aliasing Options] " .. message .. "\n")
end

-- Skip failures and retry on the next poll; rate-limit persistent errors.
local function safely(action)
    local success, problem = pcall(action)
    if not success and os.time() >= nextErrorLog then
        log(tostring(problem))
        nextErrorLog = os.time() + 60
    end
    return success
end

local function savedChoice()
    local options = StaticFindObject("/Script/mucha.Default__CPP_BFL_SaveDataOption")
    if not isValid(options) then
        return nil
    end

    local tag = { TagName = FName(settingTag) }
    local saved = options:GetOptionSetting(tag)

    return saved.Value -- -1 means the game has not initialized this option yet.
end

local function applyChoice(choice)
    if choice == nil then
        choice = savedChoice()
    end

    local method = methods[choice]
    if method == nil then
        return
    end

    local controller = Unreal.GetPlayerController()
    if not isValid(controller) or not isValid(controller:GetWorld()) then
        return
    end

    local console = Unreal.GetKismetSystemLibrary()
    if not isValid(console) then
        return
    end

    if console:GetConsoleVariableFloatValue("r.AntiAliasingMethod") ~= method then
        local command = "r.AntiAliasingMethod " .. method
        console:ExecuteConsoleCommand(controller, command, controller)
        assert(console:GetConsoleVariableFloatValue("r.AntiAliasingMethod") == method,
            "Could not apply " .. command)
        log("Applied " .. command)
    end
end

local function restoreSavedChoice()
    safely(applyChoice)
end

local function applyMenuChoice(_, parameter)
    safely(function()
        local choice = parameter:get().SelectedItem
        applyChoice(choice)
    end)
end

local hooks = {
    ["CPP_OptionFunctionHandler:HandleChangeAAType"] = applyMenuChoice,
    ["CPP_MuchaSystemManager:HandleGameLoadOrReturnTitle"] = restoreSavedChoice,
    ["CPP_TalkEventBlueprint:Header"] = restoreSavedChoice,
    ["CPP_TalkEventBlueprint:Footer"] = restoreSavedChoice,
    ["CPP_TalkEventSequenceDirector:ExecuteHeader"] = restoreSavedChoice,
    ["CPP_TalkEventSequenceDirector:ExecuteFooter"] = restoreSavedChoice,
}

local function maintainSelection()
    for name, callback in pairs(hooks) do
        if not registeredHooks[name] then
            safely(function()
                local path = "/Script/mucha." .. name
                if isValid(StaticFindObject(path)) then
                    local before, after = RegisterHook(path, function() end, callback)
                    assert(before and after, "Could not register " .. path)
                    registeredHooks[name] = true
                end
            end)
        end
    end
    applyChoice()
end

-- Hooks repair known resets immediately. Polling covers missed events and loading,
-- and reads the saved choice again so cancelled menu edits are not kept.
LoopAsync(250, function()
    if not pollPending then
        pollPending = true
        local queued = safely(function()
            ExecuteInGameThread(function()
                safely(maintainSelection)
                pollPending = false
            end)
        end)
        if not queued then
            pollPending = false
        end
    end
    return false
end)

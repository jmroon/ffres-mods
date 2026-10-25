local Unreal = require("UEHelpers")

local registeredHooks = {}
local pollPending = false
local nextErrorLog = 0

local function isValid(object)
    return object ~= nil and object:IsValid()
end

local function log(message)
    print("[TSR Always On] " .. message .. "\n")
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

local function applyTSR()
    local controller = Unreal.GetPlayerController()
    if not isValid(controller) or not isValid(controller:GetWorld()) then
        return
    end

    local console = Unreal.GetKismetSystemLibrary()
    if not isValid(console) then
        return
    end

    if console:GetConsoleVariableFloatValue("r.AntiAliasingMethod") ~= 4 then
        console:ExecuteConsoleCommand(controller, "r.AntiAliasingMethod 4", controller)
        assert(console:GetConsoleVariableFloatValue("r.AntiAliasingMethod") == 4,
            "Could not apply TSR")
        log("Applied TSR")
    end
end

local function afterGameSettingsChange()
    safely(applyTSR)
end

local hooks = {
    "CPP_MuchaSystemManager:HandleGameLoadOrReturnTitle",
    "CPP_OptionFunctionHandler:HandleChangeAAType",
    "CPP_TalkEventBlueprint:Header",
    "CPP_TalkEventBlueprint:Footer",
    "CPP_TalkEventSequenceDirector:ExecuteHeader",
    "CPP_TalkEventSequenceDirector:ExecuteFooter",
}

local function maintainTSR()
    for _, name in ipairs(hooks) do
        if not registeredHooks[name] then
            safely(function()
                local path = "/Script/mucha." .. name
                if isValid(StaticFindObject(path)) then
                    local before, after = RegisterHook(path, function() end, afterGameSettingsChange)
                    assert(before and after, "Could not register " .. path)
                    registeredHooks[name] = true
                end
            end)
        end
    end
    applyTSR()
end

-- Hooks repair known resets immediately. Polling covers missed events and loading.
-- Queue at most one check while the game thread is busy.
LoopAsync(250, function()
    if pollPending then
        return false
    end

    pollPending = true
    local queued = safely(function()
        ExecuteInGameThread(function()
            safely(maintainTSR)
            pollPending = false
        end)
    end)
    if not queued then
        pollPending = false
    end
    return false
end)

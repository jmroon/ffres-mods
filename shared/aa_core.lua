local Unreal = require("UEHelpers")
local AA = {}

local function isValid(object)
    return object ~= nil and object:IsValid()
end

-- Each mod supplies its desired method; this core keeps it applied.
function AA.start(settings)
    local registeredHooks = {}
    local pollPending = false
    local nextErrorLog = 0

    local function log(message)
        print("[" .. settings.name .. "] " .. message .. "\n")
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

    local function applyMethod(method)
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
            local description = settings.methodLabel or command
            console:ExecuteConsoleCommand(controller, command, controller)
            assert(console:GetConsoleVariableFloatValue("r.AntiAliasingMethod") == method,
                "Could not apply " .. description)
            log("Applied " .. description)
        end
    end

    local function restoreMethod()
        applyMethod(settings.getMethod())
    end

    local function afterGameSettingsChange()
        safely(restoreMethod)
    end

    local function afterMenuChange(_, parameter)
        safely(function()
            if settings.getMenuMethod then
                applyMethod(settings.getMenuMethod(parameter))
            else
                restoreMethod()
            end
        end)
    end

    local hooks = {
        ["CPP_OptionFunctionHandler:HandleChangeAAType"] = afterMenuChange,
        ["CPP_MuchaSystemManager:HandleGameLoadOrReturnTitle"] = afterGameSettingsChange,
        ["CPP_TalkEventBlueprint:Header"] = afterGameSettingsChange,
        ["CPP_TalkEventBlueprint:Footer"] = afterGameSettingsChange,
        ["CPP_TalkEventSequenceDirector:ExecuteHeader"] = afterGameSettingsChange,
        ["CPP_TalkEventSequenceDirector:ExecuteFooter"] = afterGameSettingsChange,
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
        restoreMethod()
    end

    -- Hooks repair known resets immediately. Polling covers missed events and
    -- loading, and lets the menu mod discard previews that were not saved.
    LoopAsync(250, function()
        if pollPending then
            return false
        end

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
        return false
    end)
end

return AA

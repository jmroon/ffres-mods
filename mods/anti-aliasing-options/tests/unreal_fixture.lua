-- Minimal Unreal/UE4SS stand-ins for testing the production script.
-- Each Python test starts a fresh Lua runtime with this fixture.
values = {
    ["r.AntiAliasingMethod"] = 1,
    ["r.ScreenPercentage"] = 100,
    ["r.TSR.History.ScreenPercentage"] = 100,
    ["r.DepthOfFieldQuality"] = 2,
    ["sg.AntiAliasingQuality"] = 3,
    ["r.BloomQuality"] = 5,
}
saved = { Value = 3, IsDisplay = true, Type = 2 }
registered = {}
commands = {}
logs = {}
saveWrites = 0
controllerLookups = 0
validWorld = true
validOptions = true
failRead = false
failQueue = false
holdQueue = false

function print(message)
    logs[#logs + 1] = message
end

function FName(name)
    return name
end

world = {}
function world:IsValid()
    return validWorld
end

controller = {}
function controller:IsValid()
    return true
end
function controller:GetWorld()
    return world
end

console = {}
function console:IsValid()
    return true
end
function console:GetConsoleVariableFloatValue(name)
    return values[name]
end
function console:ExecuteConsoleCommand(context, command, playerController)
    local name, value = command:match("^(%S+) (%S+)$")
    assert(name == "r.AntiAliasingMethod", "Unexpected graphics mutation")
    commands[#commands + 1] = command
    values[name] = tonumber(value)
end

options = {}
function options:IsValid()
    return validOptions
end
function options:GetOptionSetting(tag)
    assert(tag.TagName == "Delegate.UI.System.Option.Root.GraphicSetting.AAType")
    if failRead then
        error("Save unavailable")
    end
    return { Value = saved.Value, IsDisplay = saved.IsDisplay, Type = saved.Type }
end
function options:SetOptionSettingData(tag, value)
    saveWrites = saveWrites + 1
    error("Script must not write option saves")
end

package.preload.UEHelpers = function()
    return {
        GetPlayerController = function()
            controllerLookups = controllerLookups + 1
            return controller
        end,
        GetKismetSystemLibrary = function()
            return console
        end,
    }
end

function StaticFindObject(path)
    if path == "/Script/mucha.Default__CPP_BFL_SaveDataOption" then
        return options
    end
    return { IsValid = function() return true end }
end

function RegisterHook(path, before, after)
    assert(not registered[path], "Hook registered twice: " .. path)
    registered[path] = { before, after }
    return 1, 2
end

function LoopAsync(interval, callback)
    assert(interval == 250)
    poll = callback
end

function ExecuteInGameThread(callback)
    if failQueue then
        error("Queue failure")
    end
    if holdQueue then
        assert(not queued, "More than one poll queued")
        queued = callback
    else
        callback()
    end
end

-- UE4SS wraps menu callback arguments in an object with a get() method.
function menuParameter(choice)
    local argument = { SelectedItem = choice }
    return { get = function() return argument end }
end

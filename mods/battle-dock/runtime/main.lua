-- The game owns tooltip text, icons and visibility. Only its content panel moves.
-- Its original canvas/SizeBox stay intact for the game's positioning callbacks.
local attached, queued, lastError

local function valid(object)
    return object ~= nil and object:IsValid()
end

local function liveWidget(class, accept)
    for _, widget in ipairs(FindAllOf(class) or {}) do
        if valid(widget) and widget:GetFullName():find('/Engine/Transient', 1, true)
            and accept(widget) then
            return widget
        end
    end
end

local function restore()
    if not attached then return end
    local saved = attached
    -- Keep the record until restoration succeeds; a transient failure retries.
    if valid(saved.content) and valid(saved.root) then
        saved.content:RemoveFromParent()
        saved.root:AddChild(saved.content)
        saved.content:SetVisibility(saved.visibility)
        if valid(saved.background) then saved.background:SetVisibility(saved.backgroundVisibility) end
    end
    attached = nil
    print('[Battle Dock] Restored normal tooltip layout.\n')
end

local function update()
    local battle = liveWidget('WBP_BattleLayout_C', function(widget)
        return widget:IsActivated() and widget:IsVisible()
    end)
    local tooltip = liveWidget('WBP_Tooltip_C', function() return true end)
    local scroll = battle and battle.BD_HelpScroll
    if not valid(scroll) or not valid(tooltip) then restore(); return end

    if attached and (attached.scroll ~= scroll or attached.tooltip ~= tooltip) then restore() end
    if not attached then
        local root = tooltip.SizeBox_0
        if not valid(root) then return end
        local content = root:GetChildAt(0)
        if not valid(content) then return end
        local background = content:GetChildAt(0)
        -- Capture everything before mutation so failures can be rolled back.
        attached = { root=root, content=content, tooltip=tooltip, scroll=scroll,
            visibility=content:GetVisibility(), background=background,
            backgroundVisibility=background:GetVisibility() }
        content:RemoveFromParent()
        local slot = scroll:AddChild(content)
        slot:SetHorizontalAlignment(0) -- Fill
        slot:SetPadding({Left=8, Top=8, Right=8, Bottom=8})
        background:SetVisibility(1) -- Collapsed; the dock supplies the background.
        print('[Battle Dock] Tooltip content attached to the battle help strip.\n')
    end
    -- Keep Show/Hide Tips and native empty-description handling intact.
    local visible = tooltip:IsVisible()
    if attached.shown ~= visible then
        attached.content:SetVisibility(visible and 0 or 1)
        scroll:ScrollToStart()
        attached.shown = visible
    end
end

-- Run on the game thread. This also restores the original parent on battle exit
-- and handles newly created widgets after a load without retaining stale objects.
LoopAsync(250, function()
    if queued then return false end
    queued = true
    local ok, err = pcall(function()
        ExecuteInGameThread(function()
            local success, message = pcall(update)
            if not success then
                pcall(restore)
                if message ~= lastError then print('[Battle Dock] ' .. tostring(message) .. '\n') end
                lastError = message
            else
                lastError = nil
            end
            queued = false
        end)
    end)
    if not ok then queued = false; print('[Battle Dock] ' .. tostring(err) .. '\n') end
    return false
end)

-- Create a logging wrapper for the function
function {baseFunctionName}_wrapper(...)
    -- Capture all arguments, including nils
    local args = {...}
    local argCount = select('#', ...)
    local logArgs = {}

    -- Detect if there are no arguments
    if argCount == 0 then
        logArgs[1] = '~no_args'
    else
        -- Process each argument
        for i = 1, argCount do
            local arg = args[i]

            -- For logging, convert nil to the string '~nil'
            if arg == nil then
                logArgs[i] = '~nil'
            elseif type(arg) == 'table' and next(arg) == nil then
                -- Detect if the argument is an empty table
                logArgs[i] = '~empty_table'
            else
                logArgs[i] = arg  -- Log other arguments as they are
            end
        end
    end

    -- Capture the line number where the wrapper was called
    local callingLineInfo = debug.getinfo(2, 'Sl')
    local callingLine = callingLineInfo.currentline

    -- Retrieve the line content from the stored Lua code, handling different line break types
    local codeLines = {}
    for line in _ExecutedLuaCode:gmatch('([^\r\n]*)[\r\n]?') do
        table.insert(codeLines, line)
    end

    local callingLineContent = codeLines[callingLine] or '<Line content unavailable>'

    -- Log the function call, its arguments, and the calling line and content
    _logAndStoreMethodCall('{baseFunctionName}', logArgs, callingLine, callingLineContent)

    -- Retrieve the latest log entry for this method from _MethodCallHistory
    local latestLogEntry = nil
    for i = #_MethodCallHistory, 1, -1 do
        if _MethodCallHistory[i].MethodName == '{baseFunctionName}' then
            latestLogEntry = _MethodCallHistory[i]
            break
        end
    end

    -- Modify ... using the latest log entry
    local modifiedArgs = {}
    if latestLogEntry then
        for i = 1, argCount do
            if latestLogEntry.Arguments[i].AutoConverted then
                modifiedArgs[i] = latestLogEntry.Arguments[i].ConvertedValue
            else
                modifiedArgs[i] = args[i]
            end
        end
    else
        modifiedArgs = args
    end

    -- Call the original function with the modified arguments using custom )_Table_unpack
    return Turtle['{baseFunctionName}_original'](_Table_unpack(modifiedArgs))
end

-- Store the original function and replace it with the wrapper
Turtle['{baseFunctionName}_original'] = Turtle.{baseFunctionName}
Turtle.{baseFunctionName} = {baseFunctionName}_wrapper

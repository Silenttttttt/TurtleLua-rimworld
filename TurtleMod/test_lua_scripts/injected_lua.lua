-- Function to perform type checking against the expected argument types
function _PerformTypeCheck(funcName, logEntry)
    local fullFuncName = funcName

    -- Retrieve the method info from the MethodsInfoTable using the full function name as the key
    local methodInfo = Turtle.MethodsInfoTable[fullFuncName]

    if not methodInfo then
        Turtle.LogWarning('Warning: Type check failed, No method info found for ' .. fullFuncName)
        return
    end

    -- Map C# types to Lua types
    local csharpToLuaTypeMap = {
        ['int32'] = 'number',
        ['single'] = 'number',  -- For float
        ['double'] = 'number',
        ['string'] = 'string',
        ['boolean'] = 'boolean',
        ['void'] = 'nil',        -- Map 'void' to 'nil' since there's no 'void' in Lua
        ['luatable'] = 'table',
        ['luafunction'] = 'function', -- Support for Lua functions
        -- Add more C# types as needed
    }

    -- Retrieve the arguments table from the method info
    local expectedArguments = methodInfo.Arguments

    -- Track whether the error line has been printed
    local errorLinePrinted = false

    -- Count the number of required arguments
    local requiredArgsCount = 0
    for i = 1, #expectedArguments do
        if not expectedArguments[i].IsOptional then
            requiredArgsCount = requiredArgsCount + 1
        end
    end

    -- Check if there are fewer arguments provided than required
    if #logEntry.Arguments < requiredArgsCount then
        if not errorLinePrinted then
            Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
            errorLinePrinted = true
        end
        Turtle.LogWarning('  in ' .. fullFuncName .. ': Expected ' .. requiredArgsCount .. ' arguments, got ' .. #logEntry.Arguments)
    end

    -- Iterate over the expected arguments and compare them with actual arguments
    for i = 1, #expectedArguments do
        local expectedArg = expectedArguments[i]
        local actualArg = logEntry.Arguments[i]

        -- If the actual argument doesn't exist but is required, print a warning
        if not actualArg and not expectedArg.IsOptional then
            if not errorLinePrinted then
                Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                errorLinePrinted = true
            end
            Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected ' .. (expectedArg.ArgType or 'unknown') .. ', but got nil (missing argument)')
            return
        elseif not actualArg and expectedArg.IsOptional then
            -- Skip optional arguments if not provided
            break
        end

        -- If the argument exists, check its type
        if actualArg then
            -- Skip type checking if the expected type is Object
            local mappedType = csharpToLuaTypeMap[expectedArg.ArgType:lower()]
            local expectedType = mappedType or expectedArg.ArgType  -- Use C# type if not mapped
            local actualType = actualArg.Type:lower()

            -- Special handling for userdata
            if actualType == 'userdata' then
                -- If C# type checking is enabled, get the actual C# type
                if _EnableCSharpTypeChecking then
                    Turtle.LogMessage('C# Type Checking Enabled: Checking type for userdata')
                    Turtle.LogMessage('Expected C# Type: ' .. expectedArg.ArgType)
                    
                    -- Retrieve the actual C# type
                    local actualCSharpType = Turtle.GetCSharpType(actualArg.OriginalValue)
                    Turtle.LogMessage('Actual C# Type Retrieved: ' .. actualCSharpType)

                    -- Compare the actual C# type with the expected C# type
                    if actualCSharpType ~= expectedArg.ArgType then
                        if not errorLinePrinted then
                            Turtle.LogWarning('C# Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                            errorLinePrinted = true
                        end
                        Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected ' .. expectedArg.ArgType .. ', got ' .. actualCSharpType)
                    else
                        Turtle.LogMessage('C# Type Match Successful: ' .. actualCSharpType)
                    end
                else
                    -- If C# type checking is disabled, just ensure it's `userdata`
                    Turtle.LogMessage('C# Type Checking Disabled: Expected Type is userdata')
                    if actualType ~= 'userdata' then
                        if not errorLinePrinted then
                            Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                            errorLinePrinted = true
                        end
                        Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected userdata, got ' .. actualType)
                    else
                        Turtle.LogMessage('Lua Type Match Successful: userdata')
                    end
                end
            else
                -- For other types, check if the actual type matches the expected type
                if actualType ~= expectedType:lower() then
                    if not errorLinePrinted then
                        Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                        errorLinePrinted = true
                    end
                    Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected ' .. expectedType .. ', got ' .. actualType)
                end
            end
        end
    end

    -- If there are more arguments provided than expected (even considering optional arguments)
    if #logEntry.Arguments > #expectedArguments then
        if not errorLinePrinted then
            Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
            errorLinePrinted = true
        end
        Turtle.LogWarning('  in ' .. fullFuncName .. ': Expected ' .. #expectedArguments .. ' arguments, got ' .. #logEntry.Arguments)
    end
end



-- Function to log and store the method call
function _logAndStoreMethodCall(funcName, args, callingLine, callingLineContent)
    local logEntry = {
        MethodName = funcName,
        Arguments = {},
        CallingLine = callingLine,  -- Store the line number where the wrapper was called
        CallingLineContent = callingLineContent -- Store the content of the line
    }

    -- Iterate over all arguments in the args table
    for i = 1, #args do
        local arg = args[i]
        local argType = type(arg)
        local argValue
        local argOriginalValue = arg  -- Keep the original value for userdata

        -- Check if the argument is the '~nil' placeholder
        if arg == '~nil' then
            argType = 'nil'
            argValue = 'nil'
        elseif argType == 'string' or argType == 'number' or argType == 'boolean' then
            argValue = tostring(arg)
        elseif argType == 'table' then
            -- Log that the argument is a table and its contents
            local tableContents = ''
            for key, value in pairs(arg) do
                tableContents = tableContents .. tostring(key) .. ' = ' .. tostring(value) .. ', '
            end
            argValue = '{' .. tableContents .. '}'
        elseif argType == 'userdata' then
            local success, result = pcall(function() return tostring(arg) end)
            if success then
                argValue = result
            else
                argValue = '<non-stringable>'
            end
            -- Keep the original userdata for later type checking
            argOriginalValue = arg
        else
            argValue = '<unsupported type>'
        end

        -- Truncate value if necessary
        if #argValue > 20 then
            argValue = string.sub(argValue, 1, 20) .. '...'
        end

        -- Store both the string value and the original value (for userdata)
        table.insert(logEntry.Arguments, { Type = argType, Value = argValue, OriginalValue = argOriginalValue })
    end

    -- Perform type checking against the expected argument types
    _PerformTypeCheck(funcName, logEntry)

    -- Store the log entry in the global method call history
    table.insert(_MethodCallHistory, logEntry)

    -- Ensure only the last 20 entries are stored
    if #_MethodCallHistory > 20 then
        table.remove(_MethodCallHistory, 1)
    end
end


_LineOffset = debug.getinfo(1, 'l').currentline + 1

-- Create a logging wrapper for the function
function GetThingsAt_wrapper(...)
    -- Capture all arguments, including nils
    local argCount = select('#', ...)
    local args = {{}}
    local logArgs = {{}}

    for i = 1, argCount do
        local arg = select(i, ...)
        args[i] = arg  -- Keep the original arguments intact

        -- For logging, convert nil to the string '~nil'
        if arg == nil then
            logArgs[i] = '~nil'
        else
            logArgs[i] = arg  -- Log other arguments as they are
            print(type(arg))
        end
    end

    -- Capture the line number where the wrapper was called
    local callingLineInfo = debug.getinfo(2, 'Sl')
    local callingLine = callingLineInfo.currentline

    -- Retrieve the line content from the stored Lua code, handling different line break types
    local codeLines = {{}}
    for line in _ExecutedLuaCode:gmatch('([^\r\n]*)[\r\n]?') do
        table.insert(codeLines, line)
    end

    local callingLineContent = codeLines[callingLine] or '<Line content unavailable>'

    -- Log the function call, its arguments, and the calling line and content
    _logAndStoreMethodCall('GetThingsAt', logArgs, callingLine, callingLineContent)

    -- Call the original function with the original arguments (including nils)
    return Turtle['GetThingsAt_original'](...)
end


-- Store the original function and replace it with the wrapper
Turtle['GetThingsAt_original'] = Turtle.GetThingsAt
Turtle.GetThingsAt = GetThingsAt_wrapper


position = Turtle.CreateIntVec3(1, 1, 1)
Turtle.GetThingsAt(position)
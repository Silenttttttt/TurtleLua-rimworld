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
                    local actualCSharpType = Turtle.GetCSharpType(actualArg.Value)
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

position = Turtle.CreateIntVec3(1, 1, 1)
Turtle.GetThingsAt(position)
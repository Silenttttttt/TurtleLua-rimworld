-- Override the default print function with Turtle.LogMessage
_G.print = function(...)
    local args = {...}
    for i = 1, #args do
        args[i] = tostring(args[i])  -- Convert each argument to a string
    end
    Turtle.LogMessage(table.concat(args, ' '))
end

-- Initialize the global method call history table
_MethodCallHistory = {}

-- Global variables to control behavior
-- _WarnOnSubclassMismatch = true
-- _EnableCSharpTypeChecking = true
-- _Auto_type_convert = true
-- _Log_type_convertions = true

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
    ['object'] = 'object'  -- Add a mapping for 'object' to 'object'
}


-- Map of expected types to conversion functions and their invalid equivalents
local typeConversionMap = {
    ["IntVec3"] = {
        convert = function(arg)
            return Turtle.CreateIntVec3(arg)
        end,
        invalid = IntVec3_Invalid
    },
    ["LocalTargetInfo"] = {
        convert = function(arg)
            return Turtle.ConvertToLocalTargetInfo(arg)
        end,
        invalid = LocalTargetInfo_Invalid
    },
    -- Add more mappings as needed
}


-- Function to attempt type conversion
function _AttemptTypeConversion(expectedType, arg)
    if not _Auto_type_convert then
        return arg, nil, false, false  -- No conversion if auto conversion is disabled
    end

    local trueCSharpType = nil
    if _EnableCSharpTypeChecking then
        trueCSharpType = Turtle.GetCSharpType(arg):lower()
    end

    -- Normalize the expected type to lowercase
    local normalizedExpectedType = expectedType:lower()

    -- Map the normalized expected type to Lua if possible
    local mappedExpectedType = csharpToLuaTypeMap[normalizedExpectedType] or normalizedExpectedType

    -- Try to map the true C# type to a Lua type if type checking is enabled
    local mappedLuaType = trueCSharpType and csharpToLuaTypeMap[trueCSharpType]

    -- If the argument is already of the expected type (either mapped or C#), return it without conversion
    if (mappedLuaType and mappedLuaType == mappedExpectedType) or 
       (trueCSharpType and trueCSharpType == mappedExpectedType) then
        return arg, trueCSharpType or mappedLuaType, false, false  -- No conversion needed and no failure
    end
    
    -- Try to get the conversion function and invalid equivalent for the expected type
    local conversionInfo = typeConversionMap[expectedType]
    local convertFunc = conversionInfo and conversionInfo.convert
    local invalidValue = conversionInfo and conversionInfo.invalid

    -- If a conversion function exists, attempt the conversion
    if convertFunc then
        local success, convertedArg = pcall(convertFunc, arg)
        if success and convertedArg ~= nil then
            -- Check if the converted value is invalid
            if convertedArg == invalidValue then
             --   Turtle.LogWarning('Conversion to ' .. mappedExpectedType .. ' resulted in an invalid value: nil, using original argument')
                return arg, trueCSharpType or mappedLuaType, false, true  -- Conversion failed due to invalid result
            end
            return convertedArg, mappedExpectedType, true, false  -- Conversion succeeded
        else
            Turtle.LogWarning('Failed to auto-convert argument to ' .. mappedExpectedType .. ': ' .. tostring(arg))
        end
    end

    -- Return the original argument and its type if conversion was not possible
    return arg, trueCSharpType or mappedLuaType or mappedExpectedType, false, true  -- Conversion failed
end




-- Function to perform type checking against the expected argument types
function _PerformTypeCheck(funcName, logEntry)
    local fullFuncName = funcName

    -- List of methods to skip type checking for
    local skipTypeCheckMethods = {
        "ConvertToLocalTargetInfo",
        "CreateIntVec3"
    }
    -- Check if this function should skip type checking
    for _, methodName in ipairs(skipTypeCheckMethods) do
        if fullFuncName == methodName then
            -- Skip type checking for this method
            return
        end
    end

    -- Retrieve the method info from the MethodsInfoTable using the full function name as the key
    local methodInfo = Turtle.MethodsInfoTable[fullFuncName]

    -- Retrieve the line content from the stored Lua code, handling different line break types
    local codeLines = {}
    for line in _ExecutedLuaCode:gmatch("[^\r\n]+") do
        table.insert(codeLines, line)
    end

    -- Adjust the calling line to ensure correct alignment
    local callingLine = logEntry.CallingLine
    local callingLineContent = codeLines[callingLine]

    -- Handle potential off-by-one errors by checking the previous line if the current one is empty
    if callingLineContent == '' and callingLine > 1 then
        callingLineContent = codeLines[callingLine - 1]
    end

    local skipAutoConversion = false
    -- Ensure callingLineContent is not nil and is a string
    if type(callingLineContent) == "string" then
        -- Check for the skip type check comment using plain matching
        if callingLineContent:find("--skip_type_check", 1, true) then
            return
        end

        -- Check for the skip auto conversion comment using plain matching
        skipAutoConversion = callingLineContent:find("--skip_auto_conversion", 1, true)
    end

    if not methodInfo then
        Turtle.LogWarning('Warning: Type check failed, No method info found for ' .. fullFuncName)
        return
    end

    -- Retrieve the arguments table from the method info
    local expectedArguments = methodInfo.Arguments

    -- If expectedArguments is nil, set it to an empty table
    if expectedArguments == nil then
        expectedArguments = {}
    end

    -- Track whether the error line has been printed
    local errorLinePrinted = false

    -- Create a table to store type information for logging
    logEntry.TypeInfo = {}

    -- Add a table to store the true C# types if type checking is enabled
    if _EnableCSharpTypeChecking then
        logEntry.TrueCSharpTypes = {}
    end

    -- If no arguments are passed, explicitly insert ~no_args
    if #logEntry.Arguments == 0 then
        table.insert(logEntry.Arguments, { Type = '~no_args', Value = nil, OriginalValue = nil })
    end

    -- Count the number of required arguments, but exclude ~no_args from the argument count
    local actualArgCount = 0
    for i, arg in ipairs(logEntry.Arguments) do
        if arg.Type ~= '~no_args' then
            actualArgCount = actualArgCount + 1
        end
    end

    local requiredArgsCount = 0
    for i = 1, #expectedArguments do
        if not expectedArguments[i].IsOptional then
            requiredArgsCount = requiredArgsCount + 1
        end
    end

    -- Check if there are fewer arguments provided than required
    if actualArgCount < requiredArgsCount then
        if not errorLinePrinted then
            Turtle.LogWarning('Argument mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
            errorLinePrinted = true
        end
        Turtle.LogWarning('  in ' .. fullFuncName .. ': Expected ' .. requiredArgsCount .. ' arguments, got ' .. actualArgCount)
    end

    -- Check if there are more arguments provided than expected (even considering optional arguments)
    if actualArgCount > #expectedArguments then
        if not errorLinePrinted then
            Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
            errorLinePrinted = true
        end
        Turtle.LogWarning('  in ' .. fullFuncName .. ': Expected ' .. #expectedArguments .. ' arguments, got ' .. actualArgCount)
    end

    -- Iterate over the expected arguments and compare them with actual arguments
    for i = 1, #expectedArguments do
        local expectedArg = expectedArguments[i]
        local actualArg = logEntry.Arguments[i]

        -- Skip type-checking logic for ~no_args
        if actualArg and actualArg.Type == '~no_args' then
            -- Skip further processing for ~no_args, it only counts for argument length checks
            break
        end

        -- Convert special case types to appropriate Lua types
        local argType = actualArg and actualArg.Type or nil
       
        if argType then
            -- Handle special cases like ~nil and ~empty_table
            if argType:lower() == '~nil' then
                argType = 'nil'
                actualArg = { Type = 'nil', Value = 'nil', OriginalValue = nil }
            elseif argType == '~empty_table' then
                argType = 'table'
                actualArg = { Type = 'table', Value = '{}', OriginalValue = {} }
            end

            -- Handle object type specifically
            if expectedArg.ArgType:lower() == 'object' then
                if _EnableCSharpTypeChecking then
                    local mappedType = csharpToLuaTypeMap[actualArg.Type:lower()]
                    if mappedType then
                        table.insert(logEntry.TypeInfo, { ExpectedType = 'object', ReceivedType = mappedType })
                    else
                        local actualCSharpType = Turtle.GetCSharpType(actualArg.OriginalValue)
                        table.insert(logEntry.TrueCSharpTypes, actualCSharpType)
                        table.insert(logEntry.TypeInfo, { ExpectedType = 'object', ReceivedType = actualCSharpType })
                    end
                else
                    table.insert(logEntry.TypeInfo, { ExpectedType = 'object', ReceivedType = argType })
                end
            else
                -- Attempt type conversion if necessary, skip if auto conversion is disabled for this line
                if _Auto_type_convert and not skipAutoConversion then
                    if actualArg.OriginalValue ~= nil then
                        local convertedArg, convertedType, autoConverted, conversionFailed = _AttemptTypeConversion(expectedArg.ArgType, actualArg.OriginalValue)

                        -- Store the initial type and value before conversion
                        logEntry.Arguments[i].InitialType = actualArg.Type
                        logEntry.Arguments[i].InitialValue = actualArg.Value
                        logEntry.Arguments[i].ConvertedType = convertedType
                        logEntry.Arguments[i].ConvertedValue = convertedArg
                        logEntry.Arguments[i].AutoConverted = autoConverted

                        -- Capture conversion failure
                        logEntry.Arguments[i].ConversionFailed = conversionFailed

                        if _Log_type_convertions and autoConverted then
                            Turtle.LogWarning(
                                'Auto-converted argument at line ' ..
                                logEntry.CallingLine ..
                                ': ' .. logEntry.CallingLineContent ..
                                ' from ' .. logEntry.Arguments[i].InitialType .. ' to ' .. convertedType
                            )
                        end
                        
                        -- If the conversion failed, use the original argument
                        if conversionFailed then
                            Turtle.LogWarning(
                                'Conversion to ' .. expectedArg.ArgType ..
                                ' resulted in an invalid value: nil, using original argument'
                            )
                            convertedArg = actualArg.OriginalValue
                            convertedType = actualArg.Type
                        end

                        -- Use the converted argument for further type checking
                        argType = convertedType

                        -- If type checking is enabled, update the TrueCSharpTypes with the converted type
                        if _EnableCSharpTypeChecking then
                            local actualCSharpType = Turtle.GetCSharpType(convertedArg)
                            table.insert(logEntry.TrueCSharpTypes, actualCSharpType)
                        end
                    end
                end


                
                -- Handle C# type checking for non-'Object' types
                if expectedArg.ArgType:lower() ~= 'object' then
                    if _EnableCSharpTypeChecking then
                        if argType:lower() == 'userdata' then
                            -- Get the true C# type for the 'Object' type
                            local actualCSharpType = Turtle.GetCSharpType(actualArg.OriginalValue)
                            table.insert(logEntry.TrueCSharpTypes, actualCSharpType)
                            table.insert(logEntry.TypeInfo, { ExpectedType = expectedArg.ArgType, ReceivedType = actualCSharpType })

                            -- Check for subclass relationship by matching if expectedArg.ArgType is in actualCSharpType
                            if actualCSharpType:lower() ~= expectedArg.ArgType:lower() and actualCSharpType:lower():find(expectedArg.ArgType:lower()) then
                                -- If it's a subclass, handle according to the flag
                                if _WarnOnSubclassMismatch then
                                    Turtle.LogWarning('Possible subclass mismatch at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                                    Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected ' .. expectedArg.ArgType .. ', got ' .. actualCSharpType)
                                end
                            elseif actualCSharpType:lower() ~= expectedArg.ArgType:lower() then
                                -- Full type mismatch warning
                                if not errorLinePrinted then
                                    Turtle.LogWarning('C# Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                                    errorLinePrinted = true
                                end
                                Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected ' .. expectedArg.ArgType .. ', got ' .. actualCSharpType)
                            end
                        else
                            -- For non-userdata types, map to Lua type if possible
                            local mappedType = csharpToLuaTypeMap[expectedArg.ArgType:lower()]
                            local expectedType = mappedType or expectedArg.ArgType  -- Use C# type if not mapped

                            -- Only log a mismatch if the expected type is not "object"
                            if expectedType:lower() ~= 'object' and argType:lower() ~= expectedType:lower() then
                                if not errorLinePrinted then
                                    Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                                    errorLinePrinted = true
                                end
                                
                                Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected ' .. expectedType .. ', got ' .. argType)
                            end
                            
                            table.insert(logEntry.TypeInfo, { ExpectedType = expectedType, ReceivedType = argType })
                        end
                    else
                        -- Skip further type checking if the expected type is object
                        table.insert(logEntry.TypeInfo, { ExpectedType = expectedArg.ArgType, ReceivedType = argType or 'nil' })
                    end
                elseif not actualArg or actualArg.Type:lower() == 'nil' then
                    -- If the argument is optional or the expected type is "object", consider it valid and continue
                    if expectedArg.IsOptional then
                        table.insert(logEntry.TypeInfo, { ExpectedType = expectedArg.ArgType, ReceivedType = 'nil (optional)' })
                    else
                        -- If the argument is required but not provided, log a warning
                        if not errorLinePrinted then
                            Turtle.LogWarning('Type mismatch detected at line ' .. logEntry.CallingLine .. ': ' .. logEntry.CallingLineContent)
                            errorLinePrinted = true
                        end
                        
                        Turtle.LogWarning('  in ' .. fullFuncName .. ' for argument ' .. i .. ': Expected ' .. (expectedArg.ArgType or 'unknown') .. ', but got nil (missing argument)')
                        table.insert(logEntry.TypeInfo, { ExpectedType = expectedArg.ArgType, ReceivedType = 'nil' })
                        return
                    end
                end
            end
        end
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
        local argOriginalValue = arg  -- Keep the original value for userdata and functions
        
        -- Check for special cases
        if arg == '~no_args' then
            argType = '~no_args'
            argValue = 'nil'
        elseif arg == '~nil' then
            argType = '~nil'
            argValue = 'nil'
        elseif arg == '~empty_table' then
            argType = '~empty_table'
            argValue = '{}'
        elseif argType == 'string' or argType == 'number' or argType == 'boolean' then
            argValue = tostring(arg)
        elseif argType == 'table' then
            -- Log that the argument is a table and its contents
            local tableContents = ''
            for key, value in pairs(arg) do
                tableContents = tableContents .. tostring(key) .. ' = ' .. tostring(value) .. ', '
            end
            argValue = '{' .. tableContents .. '}'
        elseif argType == 'function' then
            -- Handle function type
            argValue = '<function>'
            argOriginalValue = arg  -- Store the function reference
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

        -- Prepare the argument entry with all necessary fields
        local argumentEntry = {
            Type = argType,
            Value = argValue,
            OriginalValue = argOriginalValue,
            ConvertedValue = nil,  -- Initially, no conversion
            AutoConverted = false, -- Initially, no auto conversion
            InitialType = argType, -- Store the initial type
            InitialValue = argValue -- Store the initial value
        }

        -- Store the argument entry in the log
        table.insert(logEntry.Arguments, argumentEntry)
    end

    -- Perform type checking and potential conversion against the expected argument types
    _PerformTypeCheck(funcName, logEntry)

    -- Store the log entry in the global method call history
    table.insert(_MethodCallHistory, logEntry)

    -- Ensure only the last 20 entries are stored
    if #_MethodCallHistory > 20 then
        table.remove(_MethodCallHistory, 1)
    end
end





-- Custom table unpack function
function _Table_unpack(t, i, n)
    i = i or 1  
    n = n or #t
    if i > n then
        return
    else
        return t[i], _Table_unpack(t, i + 1, n)
    end
end


-- Function to recursively format a Lua table into a clean and clear string, showing method call details
function _LogFormattedTable(t, logAsError)
    local function GetFallbackLineContent(lineNumber)
        -- Retrieve the _ExecutedLuaCode (which should be stored globally) and get the specific line
        local executedCode = _ExecutedLuaCode or ""
        local lines = {}
        -- Split the code into lines, handling empty lines properly
        local linePattern = "([^\r\n]*)\r?\n?"  -- Matches each line, including empty ones
    
        -- Iterate through each line, including empty ones
        for line in string.gmatch(executedCode, linePattern) do
            -- Add each line to the lines table, replacing empty ones with "<~Empty Line>"
            table.insert(lines, line ~= "" and line or "<~Empty Line>")
        end
    
        -- Handle case where the last line does not end with a newline
        if executedCode:sub(-1) ~= "\n" and executedCode:match("[^\r\n]*$") ~= "" then
            table.insert(lines, executedCode:match("[^\r\n]*$"))
        end
    
        -- Check if lineNumber is valid and return the appropriate line
        if lineNumber > 0 and lineNumber <= #lines then
            return lines[lineNumber]
        else
            return "<Line content unavailable>"
        end
    end
    
    
    
    

    local function FormatTableToString(t, indent)
        indent = indent or 0
        local indentStr = string.rep("  ", indent)
        local resultStr = ""

        if type(t) ~= "table" then
            resultStr = resultStr .. indentStr .. tostring(t) .. "\n"
            return resultStr
        end

        -- First, print MethodName, CallingLine, and CallingLineContent
        if t.MethodName then
            resultStr = resultStr .. indentStr .. "- Method: " .. tostring(t.MethodName) .. "\n"
        end

        if t.CallingLine then
            resultStr = resultStr .. indentStr .. "- Line: " .. tostring(t.CallingLine) .. "\n"
        end

        -- Handle calling line content with a fallback to _ExecutedLuaCode
        if t.CallingLineContent and t.CallingLineContent ~= "" then
            resultStr = resultStr .. indentStr .. "- Code: " .. tostring(t.CallingLineContent) .. "\n"
        else
            -- Fallback if content is unavailable
            local fallbackContent = GetFallbackLineContent(t.CallingLine)
            resultStr = resultStr .. indentStr .. "- Code: " .. fallbackContent .. "\n"
        end

        -- Fetch method info for the arguments (if available)
        local methodInfo = Turtle.MethodsInfoTable[t.MethodName]
        local expectedArguments = methodInfo and methodInfo.Arguments or {}

        -- Add a blank line before "Arguments" section
        if t.Arguments then
            resultStr = resultStr .. "\n" -- Blank line before Arguments
            resultStr = resultStr .. indentStr .. "  Arguments:\n"

            local providedArgCount = #t.Arguments
            local expectedArgCount = #expectedArguments

            for i = 1, expectedArgCount do
                local expectedArg = expectedArguments[i]
                local argument = t.Arguments[i]

                local argName = expectedArg and expectedArg.ArgName or ("Arg " .. i)
                local argValue = argument and argument.Value or "nil"
                local luaType = argument and argument.Type or "~no_args"
                local expectedType = expectedArg and expectedArg.ArgType or "unknown"
                local isOptional = expectedArg and expectedArg.IsOptional

                -- Get True C# Type if available
                local trueCSharpType = t.TrueCSharpTypes and t.TrueCSharpTypes[i] or "None"

                -- Display argument information with related items grouped together
                resultStr = resultStr .. indentStr .. string.format("    %d. %s%s\n", i, argName, isOptional and " (Optional)" or "")
                resultStr = resultStr .. indentStr .. string.format("       - Value: %s (%s)\n", tostring(argValue), tostring(luaType))
                resultStr = resultStr .. indentStr .. string.format("       - Expected Type: %s\n", tostring(expectedType))
                
                -- If the argument was auto-converted, show the conversion details
                if argument and argument.AutoConverted then
                    local convertedValue = argument.ConvertedValue
                    resultStr = resultStr .. indentStr .. string.format("       - True C# Type: %s (Auto-Converted)\n", tostring(trueCSharpType))
                    resultStr = resultStr .. indentStr .. string.format("       - Converted Value: %s\n", tostring(convertedValue))
                else
                    resultStr = resultStr .. indentStr .. string.format("       - True C# Type: %s\n", tostring(trueCSharpType))
                end

                -- If the argument conversion failed, show that information
                if argument and argument.ConversionFailed then
                    resultStr = resultStr .. indentStr .. "       - Conversion Failed: Using original argument.\n"
                end

                -- Add an empty line after each argument for readability, but skip for the last argument
                if i < expectedArgCount then
                    resultStr = resultStr .. "\n"
                end
            end
        end

        -- Finally, iterate over any remaining keys
        for key, value in pairs(t) do
            if key ~= "MethodName" and key ~= "CallingLine" and key ~= "CallingLineContent" and key ~= "Arguments" and key ~= "TrueCSharpTypes" and key ~= "TypeInfo" then
                if type(value) == "table" then
                    resultStr = resultStr .. indentStr .. tostring(key) .. ":\n"
                    resultStr = resultStr .. FormatTableToString(value, indent + 1)
                else
                    resultStr = resultStr .. indentStr .. tostring(key) .. ": " .. tostring(value) .. "\n"
                end
            end
        end

        return resultStr
    end

    -- Invert the table order for logging by iterating in reverse order
    local function ReverseIterTable(tbl)
        local reversed = {}
        local n = #tbl
        for i = n, 1, -1 do
            reversed[n - i + 1] = tbl[i]
        end
        return reversed
    end

    -- Reverse the method call history for logging
    local reversedTable = ReverseIterTable(t)

    -- Add "Method Call #" before each method call in reverse order, with two blank lines between method calls
    local resultStr = ""
    for i, call in ipairs(reversedTable) do
        resultStr = resultStr .. "Method Call #" .. i .. ":\n"
        resultStr = resultStr .. FormatTableToString(call, 1)

        -- Add two blank lines between method calls for clarity
        if i < #reversedTable then
            resultStr = resultStr .. "\n\n"
        end
    end

    -- Log the formatted result
    if logAsError then
        Turtle.LogError(resultStr)
    else
        print(resultStr)
    end
end




-- Function to recursively print a Lua table with indentation
function _PrintTable(t, indent)
    indent = indent or 0
    local indentStr = string.rep("  ", indent)
    
    if type(t) ~= "table" then
        print(indentStr .. tostring(t))
        return
    end

    for key, value in pairs(t) do
        if type(value) == "table" then
            print(indentStr .. tostring(key) .. ":")
            PrintTable(value, indent + 1)
        else
            print(indentStr .. tostring(key) .. ": " .. tostring(value))
        end
    end
end





function _printStackTrace(depth)
    depth = depth or 10  -- Default to 10 levels deep if not provided
    local level = 2  -- Start at level 2 to skip the current function
    local trace = {}

    table.insert(trace, "Stack trace:")

    while true do
        -- Get info about the current function in the stack
        local info = debug.getinfo(level, "Slfn")
        if not info or level > depth + 1 then break end  -- Exit if there is no more info or we've reached the desired depth

        -- Format the information
        local source = info.short_src or "[C]"
        local line = info.currentline > 0 and info.currentline or "?"
        local name = info.name or "[anonymous function]"

        -- Retrieve the line content from the source if available
        local lineContent = "[source not available]"
        if info.currentline > 0 and info.short_src and io.open then
            local file = io.open(info.short_src, "r")
            if file then
                local fileLines = {}
                for fileLine in file:lines() do
                    table.insert(fileLines, fileLine)
                end
                file:close()
                if fileLines[info.currentline] then
                    lineContent = fileLines[info.currentline]
                end
            end
        end

        table.insert(trace, string.format("  [%d] %s:%s in %s - %s", level - 1, source, line, name, lineContent))

        level = level + 1  -- Move up the stack
    end

    -- Print the stack trace
    print(table.concat(trace, "\n"))
end

    
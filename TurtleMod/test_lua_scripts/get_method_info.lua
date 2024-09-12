


    -- Check if Turtle.MethodsInfoTable exists
    if Turtle.MethodsInfoTable then
        -- Access the method info for 'x method'
        local methodInfo = Turtle.MethodsInfoTable.CreateJob

        -- Check if the method info exists
        if methodInfo then
            -- Print the method name and return type
            Turtle.LogMessage("Method Name: " .. methodInfo.Name)
            Turtle.LogMessage("  Return Type: " .. methodInfo.ReturnType)

            -- Check if there are arguments and print their details
            if methodInfo.Arguments then
                for i, argInfo in ipairs(methodInfo.Arguments) do
                    Turtle.LogMessage("    Arg " .. i .. ": " .. argInfo.ArgName .. ", Type: " .. argInfo.ArgType)
                    Turtle.LogMessage("      IsOptional: " .. tostring(argInfo.IsOptional))

                    Turtle.LogMessage("      DefaultValue: " .. argInfo.DefaultValue)
                end
            else
                Turtle.LogMessage("  No arguments.")
            end
        else
            Turtle.LogMessage("No information found for method 'GetThingsAt'.")
        end
    else
        Turtle.LogMessage("No MethodsInfoTable found.")
    end

-- Function to find and extinguish fire within a 1-tile radius of the current position
function ExtinguishFireNearby()
    -- Get the current position
    local currentPosition = Turtle.GetCurrentPos()

    -- Calculate positions within a 1-tile radius
    local positionsToCheck = {
        Turtle.CreateIntVec3({currentPosition.x + 1, currentPosition.y, currentPosition.z}),
        Turtle.CreateIntVec3({currentPosition.x - 1, currentPosition.y, currentPosition.z}),
        Turtle.CreateIntVec3({currentPosition.x, currentPosition.y, currentPosition.z + 1}),
        Turtle.CreateIntVec3({currentPosition.x, currentPosition.y, currentPosition.z - 1}),
    }

    -- Loop through the positions and check for fire
    for _, position in ipairs(positionsToCheck) do
        local thingsAtPosition = Turtle.GetThingsAt(position, "", "Fire")

        -- Log the number of things found
        Turtle.LogMessage("Number of things found at position: " .. #thingsAtPosition)

        -- Check if fire was found and beat it out
        if #thingsAtPosition > 0 then
            for _, thing in ipairs(thingsAtPosition) do
                if thing.def.defName == "Fire" then
                    -- Create a job to extinguish the fire
                    local beatFireJob = Turtle.CreateJob(JobDefOf.BeatFire, thing)

                    -- Start the job
                    if Turtle.StartJob(beatFireJob, JobCondition.InterruptForced) then
                        Turtle.LogMessage("Started job to extinguish fire at position: " .. position.x .. ", " .. position.z)
                        return  -- Stop after finding the first fire
                    else
                        Turtle.LogMessage("Failed to start job to extinguish fire.")
                    end
                end
            end
        end
    end

    Turtle.LogMessage("No fire found within a 1-tile radius of the current position.")
end

-- Run the fire extinguishing script
ExtinguishFireNearby()

-- Function to beat fire at a specific position
function BeatFireAtPosition(position, onComplete)
    -- Convert the position to IntVec3
    local intVec3Position = Turtle.CreateIntVec3(position.x, position.y, position.z)

    -- Get all things at the position and filter for fire
    local thingsAtPosition = Turtle.GetThingsAt(intVec3Position, "", "Fire")
    
    if thingsAtPosition and #thingsAtPosition > 0 then
        for i, thing in ipairs(thingsAtPosition) do
            if thing.def.defName == "Fire" then
                -- Create a job to beat out the fire
                local beatFireJob = Turtle.CreateJob(JobDefOf.BeatFire, thing, nil)

                -- Start the job and wait for its completion
                if Turtle.StartJob(beatFireJob, JobCondition.InterruptForced) then
                    Turtle.LogMessage("Started beating fire at position: x=" .. position.x .. ", y=" .. position.y .. ", z=" .. position.z)
                    Turtle.WaitForJobCompletion(function()
                        Turtle.LogMessage("Finished beating fire at position: x=" .. position.x .. ", y=" .. position.y .. ", z=" .. position.z)
                        onComplete()  -- Callback to move to the next position
                    end)
                else
                    Turtle.LogMessage("Failed to start job to beat fire at position: x=" .. position.x .. ", y=" .. position.y .. ", z=" .. position.z)
                    onComplete()
                end
                return  -- Return after starting the job to avoid starting multiple jobs for the same position
            end
        end
    else
        
        onComplete()
    end
end

-- Recursive function to process all positions in the home area
function ProcessNextPosition(positions, index)
    if index > #positions then
        Turtle.LogMessage("Finished checking and beating fire in all home area positions.")
        return
    end

    -- Process the current position
    BeatFireAtPosition(positions[index], function()
        -- Move to the next position once the current one is done
        ProcessNextPosition(positions, index + 1)
    end)
end

-- Main function to beat fire in all positions of the home area
function BeatFireInHomeArea()
    -- Get all areas of the "Area_Home" type
    local homeAreas = Turtle.GetAreasOfType("Area_Home")

    -- Check if there are any home areas
    if not homeAreas or #homeAreas == 0 then
        Turtle.LogMessage("No 'Home' area found.")
        return
    end

    -- Get all positions within the first "Home" area
    local homeAreaPositions = Turtle.GetPositionsInArea(tostring(homeAreas[1].AreaID))

    -- Start processing positions one by one
    ProcessNextPosition(homeAreaPositions, 1)
end

-- Run the script to beat fire in all home area positions
BeatFireInHomeArea()

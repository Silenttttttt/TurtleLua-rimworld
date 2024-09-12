-- Function to find the soil at a given position
function GetSoilAtPosition(position)
    -- Assuming Turtle.GetTerrainAt returns the terrain at the given position
    local terrain = Turtle.GetTerrainAt(position)
    
    if terrain then
        -- Log the terrain found at the position
        Turtle.LogMessage("Terrain at position: " .. position[1] .. "," .. position[2] .. "," .. position[3] .. " is: " .. terrain.defName)
        
        -- Assuming the terrain is valid for planting (you might want to add checks here)
        return Turtle.ConvertToLocalTargetInfo(terrain)
    else
        Turtle.LogMessage("No valid terrain found at position: " .. position[1] .. "," .. position[2] .. "," .. position[3])
        return nil
    end
end

-- Target a specific position
local tilePosition = {83, 0, 55}

-- Get the soil or valid terrain at that position
local soilTarget = GetSoilAtPosition(tilePosition)

if soilTarget and soilTarget:IsValid() then
    -- Create the sow job targeting the soil
    local sowJob = Turtle.CreateJob(JobDefOf.Sow, soilTarget)
    sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")
    
    -- Start the job
    local success = Turtle.StartJob(sowJob, JobCondition.InterruptForced)
    if success then
        Turtle.LogMessage("Successfully started sowing job on soil at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
    else
        Turtle.LogMessage("Failed to start sowing job on soil at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
    end
else
    Turtle.LogMessage("No valid soil target found at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
end

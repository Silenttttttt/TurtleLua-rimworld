-- Function to cut a plant if needed and sow a potato plant at the pawn's current position
function CutAndSowAtPawnPosition()
    Turtle.LogMessage("Starting CutAndSowAtPawnPosition function.")

    -- Get the pawn's current position
    local pawnPosition = Turtle.GetCurrentPos()

    if not pawnPosition then
        Turtle.LogMessage("Pawn position could not be retrieved.")
        return
    end

    local tilePosition = {pawnPosition.x, pawnPosition.y, pawnPosition.z}
    Turtle.LogMessage("Pawn is at position: " .. pawnPosition.x .. "," .. pawnPosition.y .. "," .. pawnPosition.z)

    -- Get all things (e.g., plants) at the current position
    local thingsAtTile = Turtle.GetThingsAt(tilePosition)
    PrintTable(_MethodCallHistory)
    Turtle.MethodCallHistory()
    local function SowPotato()
        -- Sow a potato plant at the current tile
        local sowJob = Turtle.CreateJob(JobDefOf.Sow, tilePosition)
        sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")
        Turtle.LogMessage("Sowing potato at pawn's position.")
        Turtle.CancelCurrentJob()
        Turtle.StartJob(sowJob, JobCondition.InterruptForced)
    end

    if thingsAtTile and #thingsAtTile > 0 then
        local plant = thingsAtTile[1]

        -- If there is a plant, cut it
        local cutJob = Turtle.CreateJob(JobDefOf.Harvest, tilePosition)
        Turtle.LogMessage("Cutting plant at pawn's position.")
        Turtle.CancelCurrentJob()
        Turtle.StartJob(cutJob, JobCondition.InterruptForced)

        -- Wait for the cut job to complete before sowing the potato
        Turtle.WaitForJobCompletion(function()
            SowPotato()
        end)
    else
        -- No plant found, directly sow a new potato plant
        SowPotato()
    end
end

-- Execute the function to cut and sow at the pawn's current position
CutAndSowAtPawnPosition()



-- Get the pawn's current position
local pawnPosition = Turtle.GetCurrentPos()

if not pawnPosition then
    Turtle.LogMessage("Pawn position could not be retrieved.")
    return
end

local tilePosition = {83, 0, 55}
local sowJob = Turtle.CreateJob(JobDefOf.Sow, tilePosition)
sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")

Turtle.StartJob(sowJob, JobCondition.InterruptForced)



-- Get the pawn's current position
local pawnPosition = Turtle.GetCurrentPos()



Turtle.CutPlant(pawnPosition)

-- Function to mine a 10x10 area around the current position
function MineAreaAroundCurrentPosition()
    -- Log the start of the function
    Turtle.LogMessage("Starting to mine a 10x10 area around the current position.")

    -- Get the current position of the turtle
    local currentPos = Turtle.GetCurrentPos()

    if not currentPos then
        Turtle.LogMessage("Failed to get current position.")
        return
    end

    Turtle.LogMessage("Current position: " .. currentPos.x .. "," .. currentPos.y .. "," .. currentPos.z)

    -- Define the area to mine (10x10 centered on current position)
    local minX = currentPos.x - 5
    local maxX = currentPos.x + 4
    local minZ = currentPos.z - 5
    local maxZ = currentPos.z + 4

    local tilesToMine = {}

    -- Generate all positions within the 10x10 area
    for x = minX, maxX do
        for z = minZ, maxZ do
            table.insert(tilesToMine, {x = x, y = currentPos.y, z = z})
        end
    end

    local function ProcessNextTile(index)
        if index > #tilesToMine then
            Turtle.LogMessage("Finished mining the 10x10 area.")
            return
        end

        local tile = tilesToMine[index]

        -- Log the tile being mined
        Turtle.LogMessage("Mining tile at position: " .. tile.x .. "," .. tile.y .. "," .. tile.z)

        -- Create a mine job for this tile
        local mineJob = Turtle.CreateJob(JobDefOf.Mine, tile)

        -- Cancel any current job and start the new mine job
        Turtle.CancelCurrentJob()
        Turtle.StartJob(mineJob, JobCondition.InterruptForced)

        -- Wait for the job to complete before processing the next tile
        Turtle.WaitForJobCompletion(function()
            Turtle.WaitForTicks(10, function()  -- Add a small delay before moving to the next tile
                ProcessNextTile(index + 1)
            end)
        end)
    end

    -- Start processing the first tile
    ProcessNextTile(1)
end

-- Execute the function to mine the area
MineAreaAroundCurrentPosition()

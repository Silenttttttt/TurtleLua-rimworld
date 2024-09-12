-- Function to calculate the squared distance between two positions (avoiding square root for performance)
function GetSquaredDistance(pos1, pos2)
    return (pos1.x - pos2.x) ^ 2 + (pos1.z - pos2.z) ^ 2
end

-- Function to check if a chunk is in the stockpile zone
function IsChunkInStockpile(chunk, stockpilePositions)
    for _, pos in ipairs(stockpilePositions) do
        if chunk.Position.x == pos.x and chunk.Position.z == pos.z then
            return true
        end
    end
    return false
end

-- Function to check if a position in the stockpile zone is already occupied
function IsPositionOccupied(position, occupiedPositions)
    for _, pos in ipairs(occupiedPositions) do
        if position.x == pos.x and position.z == pos.z then
            return true
        end
    end
    return false
end

-- Function to haul a single chunk to a position in the stockpile zone
function HaulChunkToPosition(chunk, position, onComplete)
    -- Convert the chunk and the destination position to LocalTargetInfo
    local targetChunk = Turtle.ConvertToLocalTargetInfo(chunk)
    local targetPosition = Turtle.ConvertToLocalTargetInfo(position)

    -- Create the haul job using LocalTargetInfo
    local haulJob = Turtle.CreateJob(JobDefOf.HaulToCell, targetChunk, targetPosition)
    
    -- Set the count to be at least 1
    haulJob.count = math.max(1, chunk.stackCount or 1)

    -- Start the haul job using the StartJob helper method
    if Turtle.StartJob(haulJob, JobCondition.InterruptForced) then
        Turtle.LogMessage("Haul job started successfully.")
        -- Wait for job completion before calling the onComplete callback
        Turtle.WaitForJobCompletion(function()
            Turtle.LogMessage("Haul job completed.")
            if onComplete then
                onComplete(true)
            end
        end)
    else
        Turtle.LogMessage("Failed to start haul job.")
        if onComplete then
            onComplete(false)
        end
    end
end

-- Function to haul all stone chunks to the stockpile zone
function HaulAllChunksToStockpile()
    -- Get all stockpile zones
    local stockpileZones = Turtle.GetZonesOfType("Zone_Stockpile")

    -- Check if any stockpile zones were found
    if not stockpileZones or #stockpileZones == 0 then
        Turtle.LogMessage("No stockpile zones found.")
        return
    end

    -- Select the first stockpile zone's identifier
    local firstStockpileZoneIdentifier = stockpileZones[1].ZoneID

    -- Fetch all positions within the first stockpile zone
    local positionsInZone = Turtle.GetPositionsInZone(tostring(firstStockpileZoneIdentifier))

    -- Check if there are available positions in the zone
    if not positionsInZone or #positionsInZone == 0 then
        Turtle.LogMessage("No positions available in the first stockpile zone.")
        return
    end

    -- Get all stone chunks on the map
    local stoneChunks = Turtle.GetThingsOfDefCategory("StoneChunks")

    -- Check if there are any stone chunks to haul
    if not stoneChunks or #stoneChunks == 0 then
        Turtle.LogMessage("No stone chunks found to haul.")
        return
    end

    -- Filter out chunks that are already in the stockpile zone
    local chunksToHaul = {}
    for _, chunk in ipairs(stoneChunks) do
        if not IsChunkInStockpile(chunk, positionsInZone) then
            table.insert(chunksToHaul, chunk)
        end
    end

    -- Sort the remaining chunks by distance to the first position in the stockpile zone
    local firstPosition = positionsInZone[1]
    table.sort(chunksToHaul, function(a, b)
        return GetSquaredDistance(a.Position, firstPosition) < GetSquaredDistance(b.Position, firstPosition)
    end)

    local occupiedPositions = {}

    local index = 1

    -- Function to process the next chunk
    local function ProcessNextChunk()
        if index > #chunksToHaul then
            Turtle.LogMessage("All available stone chunks have been hauled.")
            return
        end

        local chunkToHaul = chunksToHaul[index]

        -- Find the first unoccupied position in the stockpile zone
        local targetPosition = nil
        for _, pos in ipairs(positionsInZone) do
            if not IsPositionOccupied(pos, occupiedPositions) then
                targetPosition = pos
                table.insert(occupiedPositions, pos)
                break
            end
        end

        if targetPosition then
            -- Convert position to IntVec3 and then to LocalTargetInfo
            local intVec3Position = {targetPosition.x, targetPosition.y, targetPosition.z}

            -- Haul the chunk to the position and wait for the job to complete before hauling the next chunk
            HaulChunkToPosition(chunkToHaul, intVec3Position, function(success)
                if success then
                    Turtle.WaitForTicks(10, function()
                        index = index + 1
                        ProcessNextChunk()
                    end)
                else
                    Turtle.LogMessage("Failed to haul chunk at index " .. index)
                    index = index + 1
                    ProcessNextChunk()
                end
            end)
        else
            Turtle.LogMessage("No unoccupied positions left in the stockpile zone.")
        end
    end

    -- Start processing the first chunk
    ProcessNextChunk()
end

-- Run the script to haul all stone chunks to the stockpile zone
HaulAllChunksToStockpile()

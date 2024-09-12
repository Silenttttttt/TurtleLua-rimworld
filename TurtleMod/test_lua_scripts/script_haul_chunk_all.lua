-- Function to haul a single chunk to a position in the stockpile zone
function HaulChunkToPosition(chunk, position, onComplete)
    -- Convert the chunk and the destination position to LocalTargetInfo
    local targetChunk = Turtle.ConvertToLocalTargetInfo(chunk)
    local targetPosition = Turtle.ConvertToLocalTargetInfo(position)



    Turtle.LogMessage("Reserved chunk and destination successfully.")
    
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

    local index = 1

    -- Function to process the next chunk
    local function ProcessNextChunk()
        if index > #positionsInZone or index > #stoneChunks then
            Turtle.LogMessage("All available stone chunks have been hauled.")
            return
        end

        local chunkToHaul = stoneChunks[index]
        local position = positionsInZone[index]

        -- Convert position to IntVec3 and then to LocalTargetInfo
        local intVec3Position = {position.x, position.y, position.z}

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
    end

    -- Start processing the first chunk
    ProcessNextChunk()
end

-- Run the script to haul all stone chunks to the stockpile zone
HaulAllChunksToStockpile()

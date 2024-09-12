-- Fetch a chunk to haul
local chunks = Turtle.GetThingsOfDefCategory("StoneChunks") -- Adjust this based on your actual implementation
if not chunks or #chunks == 0 then
    Turtle.LogMessage("No chunks found.")
    return
end
local chunkToHaul = chunks[1]  -- Pick the first chunk

-- Calculate the destination position (one cell to the left)
local currentPos = Turtle.GetCurrentPosition()
local destinationPos = {currentPos.x - 1, currentPos.y, currentPos.z}

-- Convert things and positions to LocalTargetInfo
local targetChunk = Turtle.ConvertToLocalTargetInfo(chunkToHaul)
local destination = Turtle.ConvertToLocalTargetInfo(destinationPos)


-- Create the haul job
local haulJob = Turtle.CreateJob(JobDefOf.HaulToCell, chunkToHaul, destination)
if not haulJob then
    Turtle.LogMessage("Failed to create haul job.")
    return
end

-- Set the count to be at least 1
haulJob.count = math.max(1, chunkToHaul.stackCount or 1)

-- Start the haul job with InterruptForced condition
if Turtle.StartJob(haulJob, JobCondition.InterruptForced) then
    Turtle.LogMessage("Haul job started successfully.")
else
    Turtle.LogMessage("Failed to start haul job.")
end

-- Optionally, release reservations if needed
-- Turtle.Release(targetChunk, JobDefOf.HaulToCell)
-- Turtle.Release(destination, JobDefOf.HaulToCell)

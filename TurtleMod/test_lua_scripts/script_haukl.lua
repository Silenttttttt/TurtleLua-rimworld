-- Fetch a chunk to haul
local chunks = Turtle.GetThingsOfDefCategory("StoneChunks") -- Adjust this based on your actual implementation
if not chunks or #chunks == 0 then
    Turtle.LogMessage("No chunks found.")
    return
end
local chunkToHaul = chunks[1]  -- Pick the first chunk

-- Calculate the destination position (one cell to the left)
local currentPos = Turtle.GetCurrentPosition()
local destinationPos = Turtle.CreateIntVec3(currentPos.x - 1, currentPos.y, currentPos.z)

-- Convert things and positions to LocalTargetInfo
local targetChunk = Turtle.ConvertToLocalTargetInfo(chunkToHaul)
local destination = Turtle.ConvertToLocalTargetInfo(destinationPos)

-- Reserve the chunk and the destination
if not Turtle.CanReserve(targetChunk, 1) or not Turtle.CanReserve(destination, 1) then
    Turtle.LogMessage("Reservation failed for chunk or destination.")
    return
end

if Turtle.Reserve(targetChunk, JobDefOf.HaulToCell, 1) and Turtle.Reserve(destination, JobDefOf.HaulToCell, 1) then
    Turtle.LogMessage("Both chunk and destination reserved successfully.")
else
    Turtle.LogMessage("Failed to reserve chunk or destination.")
    return
end

-- Create the haul job
local haulJob = Turtle.CreateJob(JobDefOf.HaulToCell, chunkToHaul, destination)
if not haulJob then
    Turtle.LogMessage("Failed to create haul job.")
    return
end

-- Start the haul job with InterruptForced condition
if Turtle.StartJob(haulJob, JobCondition.InterruptForced) then
    Turtle.LogMessage("Haul job started successfully.")
else
    Turtle.LogMessage("Failed to start haul job.")
end

-- Optionally, release reservations if needed
-- Turtle.Release(targetChunk, JobDefOf.HaulToCell)
-- Turtle.Release(destination, JobDefOf.HaulToCell)

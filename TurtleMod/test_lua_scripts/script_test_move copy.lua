function Move10TilesToRight()
    -- Get the current position of the TurtleBot
    local currentPosition = Turtle.GetCurrentPosition()

    -- Calculate the new position 10 tiles to the right (positive X direction)
    local newPosition = Turtle.CreateIntVec3(currentPosition.x + 10, currentPosition.y, currentPosition.z)

    -- Cancel any current job to ensure the TurtleBot is free to move
    Turtle.CancelCurrentJob()

    -- Create a job to move to the new position
    local moveJob = Turtle.CreateJob(JobDefOf.Goto, newPosition, "")

    -- Start the job to move to the new position
    TurtleBot.jobs:StartJob(moveJob, JobCondition.InterruptForced)
    Turtle.LogMessage("TurtleBot is moving 10 tiles to the right to position: " .. newPosition.x .. "," .. newPosition.y .. "," .. newPosition.z)
end

-- Execute the function to move the TurtleBot
Move10TilesToRight()

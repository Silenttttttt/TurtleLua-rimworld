function Move10TilesToRight()
    -- Get the current position of the TurtleBot
    local currentPosition = Turtle.GetCurrentPos()

    -- Calculate the new position 10 tiles to the right (positive X direction)
    local newPosition = {currentPosition.x + 10, currentPosition.y, currentPosition.z}

    -- Cancel any current job to ensure the TurtleBot is free to move
    Turtle.CancelCurrentJob()

    -- Create a job to move to the new position
    local moveJob = Turtle.CreateJob(JobDefOf.Goto, newPosition, nil)

    -- Start the job to move to the new position
    TurtleBot.jobs:StartJob(moveJob, JobCondition.InterruptForced)
    Turtle.LogMessage("TurtleBot is moving 10 tiles to the right")
end

-- Execute the function to move the TurtleBot
Move10TilesToRight()

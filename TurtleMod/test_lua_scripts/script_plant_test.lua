
-- Get the soil or valid terrain at that position
local soilTarget = Turtle.GetCurrentPos()

local sowJob = Turtle.CreateJob(JobDefOf.Sow, soilTarget)
sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")

-- Start the job
local success = Turtle.StartJob(sowJob, JobCondition.InterruptForced)
print(success)


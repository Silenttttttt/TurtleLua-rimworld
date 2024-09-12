-- Function to sow a plant or harvest a fully grown one in the first growing zone
function SowOrHarvestInFirstGrowingZone()
    -- Log the start of the function
    Turtle.LogMessage("Starting SowOrHarvestInFirstGrowingZone function.")

    -- Get the first growing zone type
    local growZones = Turtle.GetZonesOfType("Zone_Growing")
    
    if not growZones or #growZones == 0 then
        Turtle.LogMessage("No growing zones found.")
        return
    end

    Turtle.LogMessage("Found " .. #growZones .. " growing zone(s).")
    
    -- Get all tiles in the first growing zone
    local growZoneTiles = Turtle.GetPositionsInZone(tostring(1))

    local function ProcessNextTile(index)
        if index > #growZoneTiles then
            Turtle.LogMessage("Finished processing all tiles in the growing zone.")
            return
        end

        local tile = growZoneTiles[index]
        local tilePosition = {tile.x, tile.y, tile.z}

        -- Use the GetPlantAtPosition method to get the plant at this tile
        local plant = Turtle.GetPlantAtPosition(tilePosition)

        if plant then
            -- Check if the plant is fully grown and harvest it
            local growth = tonumber(plant.growth) or 0
            if growth >= 1 then
                local harvestJob = Turtle.CreateJob(JobDefOf.Harvest, plant, nil)
                Turtle.LogMessage("Harvesting plant at position: " .. tile.x .. "," .. tile.y .. "," .. tile.z)
                Turtle.CancelCurrentJob()
                Turtle.StartJob(harvestJob, JobCondition.InterruptForced)
                
                -- Wait for the job to complete before moving to the next tile
                Turtle.WaitForJobCompletion(function()
                    Turtle.WaitForTicks(10, function()  -- Add a small delay before processing the next tile
                        ProcessNextTile(index + 1)
                    end)
                end)
                return
            elseif plant.def.defName ~= "Plant_Potato" then
                -- If the plant is not the target plant, cut it
                local cutJob = Turtle.CreateJob(JobDefOf.CutPlant, plant)
                Turtle.LogMessage("Cutting plant at position: " .. tile.x .. "," .. tile.y .. "," .. tile.z)
                Turtle.CancelCurrentJob()
                Turtle.StartJob(cutJob, JobCondition.InterruptForced)
                
                -- Wait for the job to complete before moving to the next tile
                Turtle.WaitForJobCompletion(function()
                    Turtle.WaitForTicks(10, function()  -- Add a small delay before sowing
                        -- Now sow the correct plant
                        local sowJob = Turtle.CreateJob(JobDefOf.Sow, tilePosition)
                        sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")
                        Turtle.LogMessage("Sowing potato at position: " .. tile.x .. "," .. tile.y .. "," .. tile.z)
                        Turtle.CancelCurrentJob()
                        Turtle.StartJob(sowJob, JobCondition.InterruptForced)

                        -- Wait for the sow job to complete before moving to the next tile
                        Turtle.WaitForJobCompletion(function()
                            Turtle.WaitForTicks(10, function()  -- Add a small delay before processing the next tile
                                ProcessNextTile(index + 1)
                            end)
                        end)
                    end)
                end)
                return
            end
        else
            -- No plant found, so sow a new plant
            local sowJob = Turtle.CreateJob(JobDefOf.Sow, tilePosition)
            sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")
            Turtle.LogMessage("Sowing potato at position: " .. tile.x .. "," .. tile.y .. "," .. tile.z)
            Turtle.CancelCurrentJob()
            Turtle.StartJob(sowJob, JobCondition.InterruptForced)

            -- Wait for the job to complete before moving to the next tile
            Turtle.WaitForJobCompletion(function()
                Turtle.WaitForTicks(10, function()  -- Add a small delay before processing the next tile
                    ProcessNextTile(index + 1)
                end)
            end)
            return
        end

        -- If nothing to do, move to the next tile
        ProcessNextTile(index + 1)
    end

    -- Start processing the first tile
    ProcessNextTile(1)
end

-- Execute the function to sow or harvest in the first growing zone
SowOrHarvestInFirstGrowingZone()

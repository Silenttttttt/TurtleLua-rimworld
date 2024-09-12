-- Function to ensure every tile in the first growing zone has a potato planted
function EnsurePotatoInFirstGrowingZone()
    -- Log the start of the function
    Turtle.LogMessage("Starting EnsurePotatoInFirstGrowingZone function.")

    -- Get the first growing zone
    local growZones = Turtle.GetZonesOfType("Zone_Growing")
    
    if not growZones or #growZones == 0 then
        Turtle.LogMessage("No growing zones found.")
        return
    end

    Turtle.LogMessage("Found " .. #growZones .. " growing zone(s).")
    
    -- Get all tiles in the first growing zone
    local growZoneTiles = Turtle.GetPositionsInZone(tostring(1))
    local totalTiles = #growZoneTiles
    local potatoPlantCount = 0
    local processedTiles = {}

    local function ProcessTile(index)
        if index > totalTiles then
            if potatoPlantCount >= totalTiles then
                Turtle.LogMessage("All tiles have potato plants. Task complete.")
                return
            else
                index = 1  -- Restart processing from the first tile
            end
        end

        local tile = growZoneTiles[index]
        local tilePosition = {tile.x, tile.y, tile.z}

        -- Check if the tile can be reserved
        if not Turtle.CanReserve(tilePosition) then
            Turtle.LogMessage("Tile at position " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3] .. " is already reserved.")
            ProcessTile(index + 1)
            return
        end

        -- Reserve the tile
        Turtle.Reserve(tilePosition, 1)

        -- Use the GetPlantAtPosition method to get the plant at this tile
        local plant = Turtle.GetPlantAtPosition(tilePosition)

        -- Function to continue processing after a job is complete
        local function continueProcessing()
            -- Release the tile
            Turtle.Release(tilePosition)
            Turtle.WaitForTicks(10, function()  -- Add a small delay to avoid rapid-fire calls
                ProcessTile(index + 1)
            end)
        end

        if plant then
            -- Reserve the plant
            Turtle.Reserve(plant, 1)

            -- If the plant is a potato, mark the tile as processed and skip
            if plant.def.defName == "Plant_Potato" then
                if not processedTiles[index] then
                    Turtle.LogMessage("Potato plant already at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3] .. ". Counting.")
                    potatoPlantCount = potatoPlantCount + 1
                    processedTiles[index] = true
                end
                -- Release the plant
                Turtle.Release(plant)
                continueProcessing()
            else
                -- If the plant is not a potato, cut it
                Turtle.LogMessage("Cutting non-potato plant at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
                if Turtle.StartJob(Turtle.CreateJob(JobDefOf.CutPlant, plant), JobCondition.InterruptForced) then
                    Turtle.WaitForJobCompletion(function()
                        -- Recheck if the plant was successfully cut
                        local newPlant = Turtle.GetPlantAtPosition(tilePosition)
                        if newPlant and newPlant.def == plant.def then
                            Turtle.LogMessage("Second cut needed for plant at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
                            if Turtle.StartJob(Turtle.CreateJob(JobDefOf.CutPlant, newPlant), JobCondition.InterruptForced) then
                                Turtle.WaitForJobCompletion(continueProcessing)
                            else
                                -- Release the plant
                                Turtle.Release(plant)
                                continueProcessing()
                            end
                        else
                            -- Release the plant
                            Turtle.Release(plant)
                            -- After cutting, sow a potato
                            local sowJob = Turtle.CreateJob(JobDefOf.Sow, tilePosition)
                            sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")
                            Turtle.LogMessage("Sowing potato at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
                            if Turtle.StartJob(sowJob, JobCondition.InterruptForced) then
                                Turtle.WaitForJobCompletion(function()
                                    -- Recheck to ensure potato was planted
                                    local plantedPlant = Turtle.GetPlantAtPosition(tilePosition)
                                    if plantedPlant and plantedPlant.def.defName == "Plant_Potato" then
                                        Turtle.LogMessage("Successfully planted potato at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
                                        potatoPlantCount = potatoPlantCount + 1
                                        processedTiles[index] = true
                                        continueProcessing()
                                    else
                                        Turtle.LogMessage("Failed to sow potato at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3] .. ". Retrying.")
                                        ProcessTile(index)  -- Retry the same tile
                                    end
                                end)
                            else
                                continueProcessing()
                            end
                        end
                    end)
                else
                    -- Release the plant
                    Turtle.Release(plant)
                    continueProcessing()
                end
            end
        else
            -- No plant found, sow a new potato plant
            Turtle.LogMessage("Sowing potato at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
            local sowJob = Turtle.CreateJob(JobDefOf.Sow, tilePosition)
            sowJob.plantDefToSow = Turtle.GetThingDefByName("Plant_Potato")
            if Turtle.StartJob(sowJob, JobCondition.InterruptForced) then
                Turtle.WaitForJobCompletion(function()
                    -- Recheck to ensure potato was planted
                    local plantedPlant = Turtle.GetPlantAtPosition(tilePosition)
                    if plantedPlant and plantedPlant.def.defName == "Plant_Potato" then
                        Turtle.LogMessage("Successfully planted potato at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3])
                        potatoPlantCount = potatoPlantCount + 1
                        processedTiles[index] = true
                        continueProcessing()
                    else
                        Turtle.LogMessage("Failed to sow potato at position: " .. tilePosition[1] .. "," .. tilePosition[2] .. "," .. tilePosition[3] .. ". Retrying.")
                        ProcessTile(index)  -- Retry the same tile
                    end
                end)
            else
                continueProcessing()
            end
        end
    end

    -- Start processing the first tile
    ProcessTile(1)
end

-- Execute the function to ensure every tile has a potato planted in the first growing zone
EnsurePotatoInFirstGrowingZone()

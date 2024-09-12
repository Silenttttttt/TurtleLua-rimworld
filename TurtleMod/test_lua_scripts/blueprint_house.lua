-- Get the current position (assuming there's a function to get it)
local currentPosition = Turtle.GetCurrentPosition()

-- Define the dimensions of the house
local width = 4
local height = 4

-- Create the corners of the house based on the current position
local corners = {
    {x = currentPosition.x, y = currentPosition.y, z = currentPosition.z},              -- Bottom-left
    {x = currentPosition.x + width, y = currentPosition.y, z = currentPosition.z},      -- Bottom-right
    {x = currentPosition.x, y = currentPosition.y, z = currentPosition.z + height},     -- Top-left
    {x = currentPosition.x + width, y = currentPosition.y, z = currentPosition.z + height}  -- Top-right
}

-- Create blueprints for the walls
local function createWall(startPos, endPos)
    for z = startPos.z, endPos.z do
        for x = startPos.x, endPos.x do
            local wallPos = {x = x, y = startPos.y, z = z}
            local blueprint = Turtle.CreateBlueprint("Wall", wallPos, "Steel")
            Turtle.SetBlueprintPosition(blueprint, wallPos) --skip_type_check
        end
    end
end

-- Create the four walls using the corners
createWall(corners[1], {x = corners[2].x, y = corners[1].y, z = corners[1].z})  -- Bottom wall
createWall(corners[3], {x = corners[4].x, y = corners[3].y, z = corners[3].z})  -- Top wall
createWall(corners[1], {x = corners[1].x, y = corners[1].y, z = corners[3].z})  -- Left wall
createWall(corners[2], {x = corners[2].x, y = corners[2].y, z = corners[4].z})  -- Right wall

-- Create a door on the bottom wall
local doorPosition = {x = currentPosition.x + 2, y = currentPosition.y, z = currentPosition.z}
local doorBlueprint = Turtle.CreateBlueprint("Door", doorPosition, "Steel")
Turtle.SetBlueprintPosition(doorBlueprint, doorPosition)
Turtle.SetBlueprintRotation(doorBlueprint, 1)  -- Rotate the door if necessary

-- Create blueprints for the roof and floor
local function createRoofOrFloor(startPos, endPos, blueprintType)
    for z = startPos.z, endPos.z do
        for x = startPos.x, endPos.x do
            local pos = {x = x, y = startPos.y, z = z}
            local blueprint = Turtle.CreateBlueprint(blueprintType, pos, "Steel")
            Turtle.SetBlueprintPosition(blueprint, pos)
        end
    end
end

-- Create the floor at y = currentPosition.y - 1 (one level down)
createRoofOrFloor(corners[1], corners[4], "Floor")

-- Create the roof at y = currentPosition.y + 1 (one level up)
local roofPosition = {x = currentPosition.x, y = currentPosition.y + 1, z = currentPosition.z}
createRoofOrFloor(roofPosition, {x = roofPosition.x + width, y = roofPosition.y, z = roofPosition.z + height}, "Roof")

-- Convert the list of blueprints to a Lua table for iteration
local function convertListToTable(list)
    local luaTable = {}
    for i = 1, list.Count do
        luaTable[i] = list[i - 1]  -- Adjusting for Lua's 1-based indexing
    end
    return luaTable
end

-- List all blueprints
local blueprints = Turtle.GetAllBlueprints()
local blueprintTable = convertListToTable(blueprints)
for i, bp in ipairs(blueprintTable) do
    print(bp)
end

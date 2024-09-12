-- Get the current position
local currentPosition = Turtle.GetCurrentPosition()

-- Calculate the position 1 tile to the right
local positionToTheRight = Turtle.CreateIntVec3(currentPosition.x + 1, currentPosition.y, currentPosition.z)

-- Get the list of things at the position to the right
local thingsAtPosition = Turtle.GetThingsAt(positionToTheRight)

-- Log the type of the returned value
Turtle.LogMessage("Type of thingsAtPosition: " .. type(thingsAtPosition))

-- Check if any things were found and log their names
if type(thingsAtPosition) == "table" and #thingsAtPosition > 0 then
    for i, thing in ipairs(thingsAtPosition) do
        Turtle.LogMessage("Thing " .. i .. ": " .. thing.def.defName)
    end
else
    Turtle.LogMessage("No things found 1 tile to the right of the current position.")
end

-- Create a blueprint for a steel wall at position (0, 0, 0)
local blueprint = Turtle.CreateBlueprint("Wall", {x = 0, y = 0, z = 0}, "Steel")

-- Move the blueprint to a new position
Turtle.SetBlueprintPosition(blueprint, {x = 1, y = 0, z = 0})

-- Rotate the blueprint
Turtle.SetBlueprintRotation(blueprint, 1)

-- List all blueprints
local blueprints = Turtle.GetAllBlueprints()
for i, bp in ipairs(blueprints) do
    print(bp)
end

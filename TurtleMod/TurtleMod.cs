using UnityEngine;
using Verse;
using NLua;
using NLua.Exceptions;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Collections;  // Required for IEnumerator
using System.Linq;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;
using System.IO;
using Verse.AI;
using RimWorld;
using LudeonTK;


namespace TurtleMod
{



    public class CustomTurtleBot : Pawn
    {

        private Lua lua;


        private List<Tab> savedLuaCodeTabs = new List<Tab>();


        private Coroutine currentExecutionCoroutine;
        private bool continuousExecution = false;
        private bool stopExecutionRequested = false;

        private Dictionary<IntVec3, TileData> tileDataCache = new Dictionary<IntVec3, TileData>();



        private Queue<IntVec3> positionsToUpdate = new Queue<IntVec3>();  // Initialize the queue here


        private int tickCounter = 0;  // Counter to keep track of ticks
        private int customTickCounter = 0;
        private int resetThreshold = int.MaxValue - 10000;  // Arbitrary high value with buffer for safety

        private const int MaxHistorySize = 10;
        private Queue<MethodCallInfo> methodCallHistory = new Queue<MethodCallInfo>();

        private Coroutine jobCompletionCoroutine;

        public List<LuaMethodInfo> registeredMethods = new List<LuaMethodInfo>();

        private Queue<(int, LuaFunction)> waitRequests = new Queue<(int, LuaFunction)>();
        private Dictionary<string, MethodInfo> wrappedMethods = new Dictionary<string, MethodInfo>();

        // Global variables to control behavior
        public bool WarnOnSubclassMismatch { get; set; } = true;
        public bool EnableCSharpTypeChecking { get; set; } = true;
        public bool AutoTypeConvert { get; set; } = true;
        public bool LogTypeConversions { get; set; } = true;

        // These properties store the settings from the UI
        public bool PauseGameWhenOpen { get; set; } = true;
        public bool CloseOnExecute { get; set; } = true;       
        
         public string ErrorLine { get; set; } = ""; // Stores the line that caused the error
        public int ErrorLineNumber { get; set; } = -1; // Stores the line number where the error occurred
        public bool IsErrorDisplaying { get; set; } = false; // Indicates if an error is currently being displayed
        
        private List<bool> tabExecutionState = new List<bool>();  // List to store tab execution states


        public override void SpawnSetup(Map map, bool respawnAfterLoad)
        {
            base.SpawnSetup(map, respawnAfterLoad);

            // We no longer need to create the UnityTaskManager here, it is managed by TurtleMod
            if (TurtleMod.unityTaskManager == null)
            {
                Log.Warning("UnityTaskManager was not initialized properly.");
            }
            InitializeLuaEnvironment();
        }




        private void InitializeLuaEnvironment()
        {
            try
            {
                // Initialize the Lua interpreter
                lua = new Lua();

            
                // Apply global settings to the Lua environment
                lua["_WarnOnSubclassMismatch"] = WarnOnSubclassMismatch;
                lua["_EnableCSharpTypeChecking"] = EnableCSharpTypeChecking;
                lua["_Auto_type_convert"] = AutoTypeConvert;
                lua["_Log_type_convertions"] = LogTypeConversions;

                // Expose logging methods within Turtle table
                lua.NewTable("Turtle");
                // Expose IntVec3.Invalid directly as a field in Lua
                lua["IntVec3_Invalid"] = IntVec3.Invalid;

                // Expose IntVec3.Invalid directly as a field in Lua
                lua["LocalTargetInfo_Invalid"] = LocalTargetInfo.Invalid;

                InjectLuaFunctions();

                RegisterLuaMethod("Turtle.LogMessage", typeof(CustomTurtleBot).GetMethod(nameof(LogMessageWithLogging), BindingFlags.Instance | BindingFlags.NonPublic));
                RegisterLuaMethod("Turtle.LogWarning", typeof(CustomTurtleBot).GetMethod(nameof(LogWarningWithLogging), BindingFlags.Instance | BindingFlags.NonPublic));
                RegisterLuaMethod("Turtle.LogError", typeof(CustomTurtleBot).GetMethod(nameof(LogErrorWithLogging), BindingFlags.Instance | BindingFlags.NonPublic));
                // Register the GetCSharpType method but exclude it from logging
                RegisterLuaMethod("Turtle.GetCSharpType", typeof(CustomTurtleBot).GetMethod(nameof(GetCSharpType)), excludeFromLogging: true);

                // Expose JobCondition enum
                lua["JobCondition"] = new LuaJobCondition();

                // Expose JobDefOf
                lua["JobDefOf"] = new LuaJobDefOf();

                // Expose TurtleBot instance
                lua["TurtleBot"] = this;


                lua["_EnableCSharpTypeChecking"] = true;

                RegisterMapSize();


                // Register all methods you want to expose to Lua
                RegisterLuaMethod("Turtle.CreateJob", GetType().GetMethod("CreateJob"));
                RegisterLuaMethod("Turtle.GetCurrentPos", GetType().GetMethod("GetCurrentPos"));
                
                RegisterLuaMethod("Turtle.CancelCurrentJob", GetType().GetMethod("CancelCurrentJob"));
                RegisterLuaMethod("Turtle.MineAtPosition", GetType().GetMethod("MineAtPosition"));
                RegisterLuaMethod("Turtle.IsTilePassable", GetType().GetMethod("IsTilePassable"));
                RegisterLuaMethod("Turtle.GetCurrentJob", GetType().GetMethod("GetCurrentJob"));

                // Directly register these without extra logging
                RegisterLuaMethod("Turtle.GetTileInfo", GetType().GetMethod("GetTileInfo"));
                RegisterLuaMethod("Turtle.GetTileData", GetType().GetMethod("GetTileDataFromLua"));

                // Continue with safe registration for other functions
                RegisterLuaMethod("Turtle.WaitForTicks", GetType().GetMethod("WaitForTicks"));
                RegisterLuaMethod("Turtle.WaitForJobCompletion", GetType().GetMethod("WaitForJobCompletion"));

                RegisterLuaMethod("Turtle.GetThingAtold", GetType().GetMethod("GetThingAt"));
                RegisterLuaMethod("Turtle.GetThingsAt", GetType().GetMethod("GetThingsAt"));


                // Register the methods in Lua
                RegisterLuaMethod("Turtle.CreateOrModifyArea", GetType().GetMethod("CreateOrModifyArea"));
                RegisterLuaMethod("Turtle.DeleteArea", GetType().GetMethod("DeleteArea"));
                RegisterLuaMethod("Turtle.CreateOrModifyZone", GetType().GetMethod("CreateOrModifyZone"));
                RegisterLuaMethod("Turtle.DeleteZone", GetType().GetMethod("DeleteZone"));
                RegisterLuaMethod("Turtle.GetZonesOfType", GetType().GetMethod("GetZonesOfType"));
                RegisterLuaMethod("Turtle.GetPositionsInZone", GetType().GetMethod("GetPositionsInZone"));
                RegisterLuaMethod("Turtle.GetAllZoneTypes", GetType().GetMethod("GetAllZoneTypes"));
                RegisterLuaMethod("Turtle.GetZoneProperties", GetType().GetMethod("GetZoneProperties"));
                // Register Area-related methods in Lua
                RegisterLuaMethod("Turtle.GetAreasOfType", GetType().GetMethod("GetAreasOfType"));
                RegisterLuaMethod("Turtle.GetPositionsInArea", GetType().GetMethod("GetPositionsInArea"));
                RegisterLuaMethod("Turtle.GetAllAreaTypes", GetType().GetMethod("GetAllAreaTypes"));
                RegisterLuaMethod("Turtle.GetAreaProperties", GetType().GetMethod("GetAreaProperties"));

                RegisterLuaMethod("Turtle.GetAllJobDefs", GetType().GetMethod("GetAllJobDefs"));
                RegisterLuaMethod("Turtle.GetThingDefByName", GetType().GetMethod("GetThingDefByName"));
                RegisterLuaMethod("Turtle.SowPlantAtPosition", GetType().GetMethod("SowPlantAtPosition"));
                RegisterLuaMethod("Turtle.HarvestPlantAtPosition", GetType().GetMethod("HarvestPlantAtPosition"));
                RegisterLuaMethod("Turtle.GetThingsOfDefCategory", GetType().GetMethod("GetThingsOfDefCategory"));
                RegisterLuaMethod("Turtle.GetAllThingCategories", GetType().GetMethod("GetAllThingCategories"));
                RegisterLuaMethod("Turtle.MethodCallHistory", GetType().GetMethod("LogMethodCallHistory"));
                RegisterLuaMethod("Turtle.Reserve", GetType().GetMethod("Reserve"));
                RegisterLuaMethod("Turtle.Release", GetType().GetMethod("Release"));
                RegisterLuaMethod("Turtle.CanReserve", GetType().GetMethod("CanReserve"));
                RegisterLuaMethod("Turtle.MoveObjectToPosition", GetType().GetMethod("MoveObjectToPosition"));

                RegisterLuaMethod("Turtle.ConvertToLocalTargetInfo", GetType().GetMethod("ConvertToLocalTargetInfo"));
                RegisterLuaMethod("Turtle.CreateIntVec3", GetType().GetMethod("CreateIntVec3"));


                // Register the StartJob method for Lua
                RegisterLuaMethod("Turtle.StartJob", GetType().GetMethod("StartJob"));






                RegisterLuaMethod("Turtle.CreateBlueprint", GetType().GetMethod("CreateBlueprint"));
                RegisterLuaMethod("Turtle.RemoveBlueprint", GetType().GetMethod("RemoveBlueprint"));
                RegisterLuaMethod("Turtle.GetAllBlueprints", GetType().GetMethod("GetAllBlueprints"));
                RegisterLuaMethod("Turtle.SetBlueprintPosition", GetType().GetMethod("SetBlueprintPosition"));
                RegisterLuaMethod("Turtle.SetBlueprintRotation", GetType().GetMethod("SetBlueprintRotation"));


                RegisterLuaMethod("Turtle.ConvertListToLuaTable", GetType().GetMethod("ConvertListToLuaTable"));


                RegisterLuaMethod("Turtle.GetTerrainAt", GetType().GetMethod("GetTerrainAt"));
                RegisterLuaMethod("Turtle.SetTerrain", GetType().GetMethod("SetTerrain"));
                RegisterLuaMethod("Turtle.RemoveTerrain", GetType().GetMethod("RemoveTerrain"));
                RegisterLuaMethod("Turtle.GetAllTerrains", GetType().GetMethod("GetAllTerrains"));

                RegisterLuaMethod("Turtle.HarvestPlant", GetType().GetMethod("HarvestPlant"));
                RegisterLuaMethod("Turtle.CutPlant", GetType().GetMethod("CutPlant"));

                // Register the method for Lua
                RegisterLuaMethod("Turtle.GetPlantAtPosition", GetType().GetMethod("GetPlantAtPosition"));

                RegisterLuaMethod("Turtle.LocalTargetInfo_IsValid", GetType().GetMethod("LocalTargetInfo_IsValid"));

                RegisterLuaMethod("Turtle.TestMethod", GetType().GetMethod("TestMethod"));


                RegisterMethodsInfoTable();
                PopulateMethodsInfo();
              //  InjectCreateJobWrapper();
                

                Log.Message("Lua interpreter initialized, and essential classes and methods exposed to Lua.");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize Lua environment: " + ex);
            }
        }


        public void UpdateGlobalVariables(bool warnOnSubclassMismatch, bool enableCSharpTypeChecking, bool autoTypeConvert, bool logTypeConversions)
        {
            WarnOnSubclassMismatch = warnOnSubclassMismatch;
            EnableCSharpTypeChecking = enableCSharpTypeChecking;
            AutoTypeConvert = autoTypeConvert;
            LogTypeConversions = logTypeConversions;

        }

        private void InitializePositionQueue()
        {
            positionsToUpdate.Clear();

            // Define the area to update - let's say around the TurtleBot
            CellRect areaToUpdate = CellRect.CenteredOn(this.Position, 10);

            // Sort the positions by distance from the TurtleBot
            List<IntVec3> sortedPositions = areaToUpdate.Cells.ToList();
            sortedPositions.Sort((a, b) => a.DistanceTo(this.Position).CompareTo(b.DistanceTo(this.Position)));

            // Enqueue the positions
            foreach (var pos in sortedPositions)
            {
                positionsToUpdate.Enqueue(pos);
            }
        }


        public override void Tick()
        {
            base.Tick();

            // Increment the custom tick counter
            customTickCounter++;

            // Check if the counter needs to be reset
            if (customTickCounter >= resetThreshold)
            {
                ResetTickCounter();
            }

            // Process the wait requests
            if (waitRequests.Count > 0)
            {
                var currentRequest = waitRequests.Peek();

                // Check if the target tick has been reached
                if (customTickCounter >= currentRequest.Item1)
                {
                    // Call the Lua callback and remove the request from the queue
                    currentRequest.Item2.Call();
                    waitRequests.Dequeue();
                }
            }

            // Increment the tick counter and perform actions every few ticks
            tickCounter++;
            if (tickCounter >= 100)  // Adjust this value to change how often the rest is refreshed (e.g., every 100 ticks)
            {
                // // Refresh tile info batch if necessary
                // if (currentExecutionCoroutine != null && positionsToUpdate.Count > 0)
                // {
                //     RefreshTileInfoBatch();
                // }

                // Prevent the pawn from getting tired
                SetPawnMaxRest();

                // Reset the tick counter
                tickCounter = 0;
            }
            
            //old scheduled execution
            // if (scheduledExecutionInterval > 0 && !stopExecutionRequested)
            // {
            //     ticksUntilNextExecution--;
            //     if (ticksUntilNextExecution <= 0)
            //     {
            //         ExecuteLuaCode(savedLuaCode);
            //         ticksUntilNextExecution = scheduledExecutionInterval;
            //     }
            // }
        }
        private void SetPawnMaxRest()
        {
            // Get the "Rest" need from the pawn
            Need_Rest restNeed = this.needs?.TryGetNeed<Need_Rest>();

            if (restNeed != null)
            {
                // Set the current level of rest to maximum
                restNeed.CurLevel = restNeed.MaxLevel;
            }
        }





        private void ResetTickCounter()
        {
            int resetAmount = customTickCounter;

            // Adjust all pending wait requests
            Queue<(int, LuaFunction)> adjustedQueue = new Queue<(int, LuaFunction)>();
            while (waitRequests.Count > 0)
            {
                var request = waitRequests.Dequeue();
                adjustedQueue.Enqueue((request.Item1 - resetAmount, request.Item2));
            }

            waitRequests = adjustedQueue;

            // Reset the custom tick counter
            customTickCounter = 0;
        }


        // Method to register a Lua method and track its information
        private void RegisterLuaMethod(string luaFunctionName, MethodInfo methodInfo, bool excludeFromLogging = false)
        {
            try
            {
                // List of methods to exclude from logging
                var excludedMethods = new List<string>
                {
                    nameof(LogMessageWithLogging),
                    nameof(LogWarningWithLogging),
                    nameof(LogErrorWithLogging)
                };

                // Register the function with Lua
                lua.RegisterFunction(luaFunctionName, this, methodInfo);

                // Extract the base function name (e.g., "TestMethod" from "Turtle.TestMethod")
                string baseFunctionName = luaFunctionName.Contains(".")
                    ? luaFunctionName.Split('.').Last()
                    : luaFunctionName;

                // Check if the method should be excluded from logging and if it's not excluded explicitly
                if (!excludedMethods.Contains(methodInfo.Name) && !excludeFromLogging)
                {
                    // Read the Lua wrapper code from the file
                    string wrapperScriptPath = @"G:\SteamLibrary\steamapps\common\RimWorld\Mods\TurtleBotSimplified\TurtleMod\lua_scripts\injected_wrapper.lua";
                    string wrapperCode = File.ReadAllText(wrapperScriptPath);

                    // Inject the Lua wrapper code into the Lua environment
                    string injectedCode = wrapperCode
                        .Replace("{baseFunctionName}", baseFunctionName); // Replace the placeholder with the actual function name

                    lua.DoString(injectedCode);
                }

                // Collect method info
                var methodInfoDetails = new LuaMethodInfo
                {
                    Name = luaFunctionName,
                    ReturnType = methodInfo.ReturnType.Name,
                    Arguments = methodInfo.GetParameters()
                        .Select(p => new LuaMethodArgument
                        {
                            ArgName = p.Name,
                            ArgType = p.ParameterType.Name,
                            IsOptional = p.IsOptional,  // Check if the parameter is optional
                            DefaultValue = GetDefaultValueString(p)  // Use a helper method to get the default value as a string
                        })
                        .ToList()
                };

                // Store the method information
                registeredMethods.Add(methodInfoDetails);
            }
            catch (Exception ex)
            {
                Log.Error($"Error registering Lua method {luaFunctionName}: {ex.Message}");
            }
        }



        // Helper method to properly get the default value of a parameter and convert it to a string
        private string GetDefaultValueString(ParameterInfo parameter)
        {
            if (parameter.HasDefaultValue)
            {
                if (parameter.DefaultValue != DBNull.Value)
                {
                    return parameter.DefaultValue?.ToString() ?? "unknown";
                }
                else if (parameter.ParameterType.IsValueType)
                {
                    // For value types, get the default value (e.g., 0 for int, false for bool) and convert it to a string
                    var defaultValue = Activator.CreateInstance(parameter.ParameterType);
                    return defaultValue?.ToString() ?? "unknown";
                }
                else
                {
                    // For reference types with no explicit default, return "nil"
                    return "nil";
                }
            }

            return "nil"; // If not optional or no default value, return "nil"
        }



    // Helper function to handle registering "Turtle.MethodsInfoTable"
    private void RegisterMethodsInfoTable()
    {
        // Assuming we are registering this as a LuaTable with no arguments
        var methodInfoDetails = new LuaMethodInfo
        {
            Name = "Turtle.MethodsInfoTable",
            ReturnType = "LuaTable",
            Arguments = new List<LuaMethodArgument>() // No arguments
        };

        registeredMethods.Add(methodInfoDetails);
    }

    public void PopulateMethodsInfo()
    {
        try
        {
            // Create the MethodsInfoTable within the Turtle namespace
            lua.NewTable("Turtle.MethodsInfoTable");
            LuaTable methodsInfoTable = lua.GetTable("Turtle.MethodsInfoTable");

            // Check if MethodsInfoTable was successfully created and retrieved
            if (methodsInfoTable == null)
            {
                Log.Error("MethodsInfoTable could not be created or retrieved.");
                return;
            }

            // Register "Turtle.MethodsInfoTable" if it's not already in registeredMethods
            if (!registeredMethods.Any(m => m.Name == "Turtle.MethodsInfoTable"))
            {
                var methodsInfoTableEntry = new LuaMethodInfo
                {
                    Name = "Turtle.MethodsInfoTable",
                    ReturnType = "LuaTable",
                    Arguments = new List<LuaMethodArgument>() // No arguments
                };
                registeredMethods.Add(methodsInfoTableEntry);
            }

            for (int i = 0; i < registeredMethods.Count; i++)
            {
                var method = registeredMethods[i];
                string methodName = method.Name.StartsWith("Turtle.") ? method.Name.Substring(7) : method.Name;

                // Create a subtable for each method using NewTable
                lua.NewTable($"Turtle.MethodsInfoTable.{methodName}");
                LuaTable methodTable = lua.GetTable($"Turtle.MethodsInfoTable.{methodName}");

                // Ensure the method table was successfully created
                if (methodTable == null)
                {
                    Log.Error($"Failed to create or retrieve table for method: {methodName}");
                    continue; // Skip to the next method if this fails
                }

                // Set the method name and return type
                methodTable["Name"] = methodName;
                methodTable["ReturnType"] = method.ReturnType;

                // Create a subtable for arguments if there are any
                if (method.Arguments.Count > 0)
                {
                    lua.NewTable($"Turtle.MethodsInfoTable.{methodName}.Arguments");
                    LuaTable argsTable = lua.GetTable($"Turtle.MethodsInfoTable.{methodName}.Arguments");

                    for (int j = 0; j < method.Arguments.Count; j++)
                    {
                        var arg = method.Arguments[j];

                        lua.NewTable($"Turtle.MethodsInfoTable.{methodName}.Arguments[{j + 1}]");
                        LuaTable argTable = lua.GetTable($"Turtle.MethodsInfoTable.{methodName}.Arguments[{j + 1}]");

                        // Ensure the argument table was successfully created
                        if (argTable == null)
                        {
                            Log.Error($"Failed to create or retrieve table for argument {j + 1} of method: {methodName}");
                            continue; // Skip to the next argument if this fails
                        }

                        argTable["ArgName"] = arg.ArgName;
                        argTable["ArgType"] = arg.ArgType;
                        argTable["IsOptional"] = arg.IsOptional; // Assuming `IsOptional` is a boolean property
                        argTable["DefaultValue"] = arg.DefaultValue != null ? arg.DefaultValue.ToString() : "nil"; // Convert the default value to a string or "nil"

                        // Add the argument table to the arguments table
                        argsTable[j + 1] = argTable;
                    }

                    // Add the arguments table to the method table
                    methodTable["Arguments"] = argsTable;
                }
                else
                {
                    methodTable["Arguments"] = null; // No arguments
                }

                // Use the method name as the key to store the method table in MethodsInfoTable
                methodsInfoTable[methodName] = methodTable;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error populating MethodsInfo: {ex.Message}");
        }
    }





        public void PopulateMethodsInfoString()
        {
            try
            {
                lua["MethodsInfoString"] = "";
                // Initialize a StringBuilder to create the output string
                StringBuilder methodsInfoString = new StringBuilder();

                for (int i = 0; i < registeredMethods.Count; i++)
                {
                    var method = registeredMethods[i];

                    // Append method name and return type
                    methodsInfoString.AppendLine($"Method {i + 1}: {method.Name}");
                    methodsInfoString.AppendLine($"  Return Type: {method.ReturnType}");

                    // Append arguments
                    if (method.Arguments.Count > 0)
                    {
                        methodsInfoString.AppendLine("  Arguments:");
                        for (int j = 0; j < method.Arguments.Count; j++)
                        {
                            var arg = method.Arguments[j];
                            methodsInfoString.AppendLine($"    Arg {j + 1}: {arg.ArgName}, Type: {arg.ArgType}");
                        }
                    }
                    else
                    {
                        methodsInfoString.AppendLine("  No arguments.");
                    }

                    methodsInfoString.AppendLine(); // Add an extra line break between methods
                }

                // Set the resulting string to a Lua-accessible variable
                lua["MethodsInfoString"] = methodsInfoString.ToString();

                
            }
            catch (Exception ex)
            {
                Log.Error($"Error populating MethodsInfo: {ex.Message}");
            }
        }









        private void LogLuaMethodCall(params object[] args)
        {
            var methodName = new StackFrame(1).GetMethod().Name;
            var argTypes = GetArgumentTypes(args);

            // Create a new method call info object
            var methodCallInfo = new MethodCallInfo
            {
                MethodName = methodName,
                ArgumentTypes = argTypes
            };

            // Add the new method call to the history
            if (methodCallHistory.Count >= MaxHistorySize)
            {
                methodCallHistory.Dequeue(); // Remove the oldest item
            }
            methodCallHistory.Enqueue(methodCallInfo);
        }





        private string GetArgumentTypes(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "No arguments";
            }

            var argTypes = args.Select(arg => arg?.GetType().Name ?? "null").ToArray();
            return string.Join(", ", argTypes);
        }

        private void LogErrorWithStackTrace(string luaFunctionName, Exception ex)
        {
            Log.Error($"Error in Lua function '{luaFunctionName}': {ex.Message}");
            Log.Error($"Stack Trace:\n{ex.StackTrace}");

            var luaLineInfo = ExtractLuaLineInfo(ex);
            if (luaLineInfo != null)
            {
                Log.Error($"Lua Error occurred at: {luaLineInfo}");
            }
        }

        private string ExtractLuaLineInfo(Exception ex)
        {
            // Updated regex to match both '[string "chunk"]:50:' and '[string "chunk"]:50:'
            var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"\[string ""chunk""\]:\s*(\d+):");

            if (match.Success)
            {
                int lineNumber = int.Parse(match.Groups[1].Value);
                return $"Line {lineNumber}";
            }

            return null;
        }


        public void TestMethod(int someParam, string anotherParam)
        {
            // Your original method logic
            Log.Message($"TestMethod executed with parameters: {someParam}, {anotherParam}");
        }



        public string GetCSharpType(object userdata)
        {
            if (userdata == null)
            {
                return "nil";
            }
            return userdata.GetType().Name;
        }


        // Method for logging and delegating to Log.Message
        private void LogMessageWithLogging(object message)
        {
            string messageStr = ConvertObjectToString(message);
            Log.Message(messageStr);       // Delegate to the actual Log.Message method
        }

        // Method for logging and delegating to Log.Warning
        private void LogWarningWithLogging(object message)
        {
            string messageStr = ConvertObjectToString(message);
            Log.Warning(messageStr);       // Delegate to the actual Log.Warning method
        }

        // Method for logging and delegating to Log.Error
        private void LogErrorWithLogging(object message)
        {
            string messageStr = ConvertObjectToString(message);
            Log.Error(messageStr);         // Delegate to the actual Log.Error method
        }

        // Helper method to convert an object to a string and handle errors
        private string ConvertObjectToString(object obj)
        {
            try
            {
                return obj?.ToString() ?? "<null>";  // Attempt to convert the object to a string
            }
            catch (Exception ex)
            {
                // Log the error if conversion fails
                Log.Error($"Failed to convert object to string: {ex.Message}");
                return "<conversion failed>";  // Return a default value indicating conversion failure
            }
        }



        // Method to refresh a batch of tile information in the shared Lua table
        private void RefreshTileInfoBatch()
        {
            int batchSize = 10;  // Number of tiles to update in each batch

            for (int i = 0; i < batchSize && positionsToUpdate.Count > 0; i++)
            {
                IntVec3 position = positionsToUpdate.Dequeue();

                if (position.InBounds(this.Map))
                {
                    // Using the correct, efficient method for getting terrain and things at the position
                    TerrainDef terrain = this.Map.terrainGrid.TerrainAt(position);  // Use the regular method for terrain
                    List<Thing> things = this.Map.thingGrid.ThingsListAtFast(position);  // Fast access to things list

                    // Assume you care about the first thing at the position
                    Thing firstThing = things.Count > 0 ? things[0] : null;

                    TileData tileData = new TileData
                    {
                        Terrain = terrain?.defName ?? "None",
                        ThingDef = firstThing?.def.defName ?? "None",
                        IsPassable = IsTilePassable(position),
                        Position = position  // Include position in the TileData
                    };

                    tileDataCache[position] = tileData;

                    string luaCommand = $"TileDataCache['{position.x},{position.y},{position.z}'] = {{ Terrain = '{tileData.Terrain}', ThingDef = '{tileData.ThingDef}', IsPassable = {tileData.IsPassable.ToString().ToLower()}, Position = {{x = {position.x}, y = {position.y}, z = {position.z}}} }}";
                    lua.DoString(luaCommand);
                }
            }

            // If all positions have been updated, reset the queue
            if (positionsToUpdate.Count == 0)
            {
                InitializePositionQueue();  // Reinitialize the queue to start again
            }
        }
        // Method to initialize shared Lua table for tile data
        private void InitializeSharedTileDataTable()
        {
            lua.DoString("TileDataCache = {}");
        }


        public bool Reserve(LocalTargetInfo target, Job job, int maxPawns = 1)
        {
            LogLuaMethodCall(target, job, maxPawns);

            if (!target.IsValid)
            {
                Log.Warning("TurtleBot: Invalid target for reservation.");
                return false;
            }

            // Reserve the target
            bool reserved = this.Map.reservationManager.Reserve(this, job, target, maxPawns);
            if (reserved)
            {
                Log.Message($"TurtleBot: Successfully reserved {target} for job {job.def.defName}.");
            }
            else
            {
                Log.Warning($"TurtleBot: Failed to reserve {target} for job {job.def.defName}.");
            }

            return reserved;
        }

        public bool Release(LocalTargetInfo target, Job job)
        {
            LogLuaMethodCall(target, job);

            if (!target.IsValid)
            {
                Log.Warning("TurtleBot: Invalid target for release.");
                return false;
            }

            // Release the reservation
            if (this.Map.reservationManager.ReservedBy(target, this, job))
            {
                this.Map.reservationManager.Release(target, this, job);
                Log.Message($"TurtleBot: Successfully released the reservation on {target} for job {job.def.defName}.");
                return true;
            }
            else
            {
                Log.Warning($"TurtleBot: Attempted to release a reservation that was not held by this bot for {target}.");
                return false;
            }
        }

        public bool CanReserve(LocalTargetInfo target, int maxPawns = 1)
        {
            LogLuaMethodCall(target, maxPawns);

            if (!target.IsValid)
            {
                Log.Warning("TurtleBot: Invalid target for reservation check.");
                return false;
            }

            bool canReserve = this.Map.reservationManager.CanReserve(this, target, maxPawns, stackCount: -1, layer: null, ignoreOtherReservations: false);

            if (canReserve)
            {
                Log.Message($"TurtleBot: Target {target} is available for reservation.");
            }
            else
            {
                LogTurtleBotReserveError(this, new Job(JobDefOf.HaulToCell), target, maxPawns, stackCount: -1, layer: null);
            }

            return canReserve;
        }




        private void LogTurtleBotReserveError(Pawn claimant, Job job, LocalTargetInfo target, int maxPawns, int stackCount, ReservationLayerDef layer)
        {
            // Gather current job information
            string currentJobInfo = "TurtleBot: No current job";
            int currentToilIndex = -1;

            if (claimant.CurJob != null)
            {
                currentJobInfo = "TurtleBot: " + claimant.CurJob.ToStringSafe();
                if (claimant.jobs.curDriver != null)
                {
                    currentToilIndex = claimant.jobs.curDriver.CurToilIndex;
                }
            }

            // Prepare the error message
            string targetInfo = target.ToStringSafe();
            string stackInfo = (target.HasThing && target.Thing.def.stackLimit > 1) ? $" (current stack count: {target.Thing.stackCount})" : "";
            string errorMessage = $"TurtleBot: Could not reserve {targetInfo}{stackInfo} (layer: {layer?.ToStringSafe() ?? "None"}) for {claimant.ToStringSafe()} during job {job.ToStringSafe()} (current job: {currentJobInfo}, toil: {currentToilIndex}) for maxPawns {maxPawns} and stackCount {stackCount}.";

            // Log the error
            Log.Error(errorMessage);
        }


        public bool LocalTargetInfo_IsValid(LocalTargetInfo targetInfo)
        {
            return targetInfo.IsValid;
        }



        public bool MoveObjectToPosition(Thing thingToMove, IntVec3 destination)
        {
            LogLuaMethodCall(thingToMove, destination);
            try
            {
                // Ensure the target thing and destination position are valid
                if (thingToMove == null || !destination.IsValid)
                {
                    Log.Warning("Invalid target or destination for hauling.");
                    return false;
                }

                // Reserve the destination position and the object to move
                if (!this.Map.reservationManager.CanReserve(this, destination))
                {
                    Log.Warning($"Failed to reserve the destination position at {destination}.");
                    return false;
                }

                if (!this.Map.reservationManager.CanReserve(this, thingToMove))
                {
                    Log.Warning($"Failed to reserve the object {thingToMove.Label}.");
                    return false;
                }

                this.Map.reservationManager.Reserve(this, new Job(JobDefOf.HaulToCell), destination);
                this.Map.reservationManager.Reserve(this, new Job(JobDefOf.HaulToCell), thingToMove);

                // Create and start the hauling job
                Job haulJob = new Job(JobDefOf.HaulToCell, thingToMove, destination)
                {
                    count = Math.Max(1, thingToMove.stackCount) // Ensure count is at least 1
                };
                this.jobs.StartJob(haulJob, JobCondition.InterruptForced);

                Log.Message($"Started hauling {thingToMove.Label} to {destination}.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in MoveObjectToPosition: {ex.Message}");
                return false;
            }
        }



        public bool CutPlant(LocalTargetInfo plant)
        {
            if (plant == null || !plant.IsValid)
            {
                Log.Error("TurtleBot: Invalid plant target for cutting.");
                return false;
            }

            try
            {
                // Create the cut plant job
                Job cutJob = new Job(JobDefOf.CutPlant, plant);

                // Start the job
                bool jobStarted = StartJob(cutJob, JobCondition.InterruptForced, logWarnings: true);

                return jobStarted;
            }
            catch (Exception ex)
            {
                Log.Error($"TurtleBot: Failed to create or start cut plant job. Exception: {ex.Message}");
                return false;
            }
        }


        public bool HarvestPlant(LocalTargetInfo plant)
        {
            if (plant == null || !plant.IsValid)
            {
                Log.Error("TurtleBot: Invalid plant target for harvesting.");
                return false;
            }
            
            try
            {
                // Create the harvest job
                Job harvestJob = new Job(JobDefOf.Harvest, plant);

                // Start the job
                bool jobStarted = StartJob(harvestJob, JobCondition.InterruptForced, logWarnings: true);

                return jobStarted;
            }
            catch (Exception ex)
            {
                Log.Error($"TurtleBot: Failed to create or start harvest job. Exception: {ex.Message}");
                return false;
            }
        }

        public bool StartJob(Job job, JobCondition jobCondition, bool logWarnings = true)
        {
            try
            {
                string initial_job_def = job.def.defName;
                // Attempt to start the job
                this.jobs.StartJob(job, jobCondition);

                // Check if the job was immediately changed to "Wait" or "Wait_MaintainPosture"
                if (this.jobs.curJob.def == JobDefOf.Wait || this.jobs.curJob.def == JobDefOf.Wait_MaintainPosture)
                {
                    if (logWarnings)
                    {
                        Log.Warning($"TurtleBot: Job {initial_job_def} failed to start.");
                    }
                    return false; // Indicate that the job failed to start properly
                }

                return true; // Job started successfully
            }
            catch (Exception ex)
            {
                // Log detailed error information
                Log.Error($"TurtleBot: Failed to start job {job.def.defName}. Exception: {ex.Message}");

                return false; // Job failed to start
            }
        }





        // Helper method to convert object to LocalTargetInfo
        public LocalTargetInfo ConvertToLocalTargetInfo(object targetObj)
        {
            LogLuaMethodCall(targetObj);

            // Handle the case where targetObj is a Thing
            if (targetObj is Thing thing)
            {
                return new LocalTargetInfo(thing);
            }

            // Handle the case where targetObj is an IntVec3 (position)
            if (targetObj is IntVec3 position)
            {
                return new LocalTargetInfo(position);
            }

            // Handle the case where targetObj is a Lua table representing a position
            if (targetObj is LuaTable luaTable)
            {
                // Try to extract x, y, z components from the Lua table
                if (luaTable["x"] != null && luaTable["y"] != null && luaTable["z"] != null)
                {
                    int x = Convert.ToInt32(luaTable["x"]);
                    int y = Convert.ToInt32(luaTable["y"]); // Note: y is usually 0 in RimWorld
                    int z = Convert.ToInt32(luaTable["z"]);
                    return new LocalTargetInfo(new IntVec3(x, y, z));
                }

                // Alternative case: handle table with integer indices {0, 0, 0}
                if (luaTable[1] != null && luaTable[2] != null && luaTable[3] != null)
                {
                    int x = Convert.ToInt32(luaTable[1]);
                    int y = Convert.ToInt32(luaTable[2]); // Note: y is usually 0 in RimWorld
                    int z = Convert.ToInt32(luaTable[3]);
                    return new LocalTargetInfo(new IntVec3(x, y, z));
                }

                // Attempt to interpret it as a Thing or another recognizable object
                object luaThing = luaTable["Thing"];
                if (luaThing is Thing thingFromLua)
                {
                    return new LocalTargetInfo(thingFromLua);
                }
            }

            // Handle the case where targetObj is a LocalTargetInfo directly
            if (targetObj is LocalTargetInfo localTarget)
            {
                return localTarget;
            }

            // Handle the case where targetObj is a Vector3 or Vector2
            if (targetObj is Vector3 vec3)
            {
                IntVec3 intVec3 = IntVec3.FromVector3(vec3);
                return new LocalTargetInfo(intVec3);
            }
            if (targetObj is Vector2 vec2)
            {
                IntVec3 intVec3 = new IntVec3((int)vec2.x, 0, (int)vec2.y); // Assuming y = 0
                return new LocalTargetInfo(intVec3);
            }

            // Handle the case where targetObj is a string representing an object name
            if (targetObj is string objName)
            {
                ThingDef thingDef = GetThingDefByName(objName);
                if (thingDef != null)
                {
                    Thing createdThing = ThingMaker.MakeThing(thingDef); // Rename variable to avoid conflict
                    return new LocalTargetInfo(createdThing);
                }
            }

            // Handle null or invalid target
            if (targetObj == null)
            {
                Log.Warning("ConvertToLocalTargetInfo received a null targetObj.");
                return LocalTargetInfo.Invalid;
            }

            // If none of the above, return an invalid LocalTargetInfo
            Log.Warning($"ConvertToLocalTargetInfo received an unsupported or invalid target: {targetObj.GetType().Name}");
            return LocalTargetInfo.Invalid;
        }


        // Method to get all distinct categories of things on the map as a comma-separated string
        public string GetAllThingCategories()
        {
            LogLuaMethodCall();
            HashSet<string> categories = new HashSet<string>();

            // Iterate through all things on the map
            foreach (Thing thing in this.Map.listerThings.AllThings)
            {
                // If the thing has categories, add them to the set
                if (thing.def.thingCategories != null)
                {
                    foreach (var category in thing.def.thingCategories)
                    {
                        categories.Add(category.defName);
                    }
                }
            }

            // Join the categories into a comma-separated string
            return string.Join(", ", categories);
        }

        // Method to get all things of a specific def category and/or defName
        public LuaTable GetThingsOfDefCategory(string categoryFilter = "", string defNameFilter = "")
        {
            LogLuaMethodCall(categoryFilter, defNameFilter);
            List<Thing> result = new List<Thing>();

            // Iterate through all things on the map
            foreach (Thing thing in this.Map.listerThings.AllThings)
            {
                // Filter by category if provided
                bool matchesCategory = string.IsNullOrEmpty(categoryFilter) ||
                                    (thing.def.thingCategories != null &&
                                        thing.def.thingCategories.Any(tc => tc.defName.Equals(categoryFilter, System.StringComparison.OrdinalIgnoreCase)));

                // Filter by defName if provided
                bool matchesDefName = string.IsNullOrEmpty(defNameFilter) ||
                                    thing.def.defName.Equals(defNameFilter, System.StringComparison.OrdinalIgnoreCase);

                // Add the thing if it matches both filters
                if (matchesCategory && matchesDefName)
                {
                    result.Add(thing);
                }
            }

            // Create the LuaTable
            lua.NewTable("tempTable");
            LuaTable luaTable = lua.GetTable("tempTable");

            // Populate the LuaTable with the results
            for (int i = 0; i < result.Count; i++)
            {
                luaTable[i + 1] = result[i]; // Lua tables are 1-indexed
            }

            return luaTable;
        }


        // Method to query tile data from Lua
        public TileData GetTileDataFromLua(int x, int y, int z)
        {
            LogLuaMethodCall(x, y, z);

            IntVec3 position = new IntVec3(x, y, z);
            return GetTileData(position);
        }


        // Method to get detailed information about a tile at a specific position
        private TileData GetTileData(IntVec3 position)
        {
            LogLuaMethodCall(position);
            if (!position.InBounds(this.Map))
            {
                return new TileData();  // Return empty data if out of bounds
            }

            // Get data from cache if available
            if (tileDataCache.TryGetValue(position, out TileData cachedData))
            {
                return cachedData;
            }

            // Query the tile info
            Thing thing = this.Map.thingGrid.ThingAt<Thing>(position);
            TerrainDef terrain = this.Map.terrainGrid.TerrainAt(position);

            TileData tileData = new TileData
            {
                Terrain = terrain?.defName ?? "None",
                ThingDef = thing?.def.defName ?? "None",
                IsPassable = IsTilePassable(position)
            };

            return tileData;
        }


        public LuaTable ConvertListToLuaTable<T>(List<T> list)
        {
            // Generate a unique table name
            string tableName = Guid.NewGuid().ToString();

            // Create a new table in the Lua global environment
            lua.NewTable(tableName);

            // Retrieve the table we just created
            LuaTable luaTable = lua.GetTable(tableName);

            // Populate the LuaTable with elements from the list
            for (int i = 0; i < list.Count; i++)
            {
                luaTable[i + 1] = list[i]; // Lua tables are 1-indexed
            }

            // Optionally remove the global reference to keep the environment clean
            lua.DoString($"{tableName} = nil");

            return luaTable;
        }






        // Method to get areas of a specific type and return as Lua table
        public LuaTable GetAreasOfType(string areaType)
        {
            LogLuaMethodCall(areaType);
            lua.NewTable("areasOfType"); // Create a named table in the Lua environment
            LuaTable luaTable = lua.GetTable("areasOfType"); // Retrieve the Lua table by name

            

            // Gather areas of the specified type
            int index = 1;
            foreach (var area in this.Map.areaManager.AllAreas)
            {
                

                if (area.GetType().Name == areaType)
                {
                   

                    lua.NewTable($"areasOfType[{index}]"); // Create a sub-table for each area
                    LuaTable areaData = lua.GetTable($"areasOfType[{index}]");

                    // Add detailed area properties
                    areaData["AreaType"] = area.GetType().Name;
                    areaData["AreaName"] = area.Label;
                    areaData["AreaID"] = index; // Using the index as a simple identifier
                    areaData["CellCount"] = area.ActiveCells.Count();

                    // Add the position of the first cell as a representative position
                    if (area.ActiveCells.Any()) // Ensure there are cells in the area
                    {
                        var firstCell = area.ActiveCells.First();
                        areaData["x"] = firstCell.x;
                        areaData["y"] = firstCell.y;
                        areaData["z"] = firstCell.z;

                      
                    }
                    else
                    {
                        Log.Warning($"Area of type {areaType} has no cells.");
                    }

                    luaTable[index++] = areaData; // Add the area data to the top-level table
                }
            }

           

            return luaTable; // Return the top-level table
        }

        // Method to get all unique area types present on the map
        public LuaTable GetAllAreaTypes()
        {
            LogLuaMethodCall();
            lua.NewTable("areaTypes"); // Create a named table in the Lua environment
            LuaTable luaTable = lua.GetTable("areaTypes"); // Retrieve the Lua table by name

           

            // Retrieve the list of unique area types
            var areaTypes = this.Map.areaManager.AllAreas
                .Select(a => a.GetType().Name)
                .Distinct()
                .ToList();

            

            // Populate the Lua table with area types
            for (int i = 0; i < areaTypes.Count; i++)
            {
                luaTable[i + 1] = areaTypes[i]; // Lua tables are 1-indexed
                Log.Message($"Added area type {i + 1}: {areaTypes[i]}");
            }

           

            return luaTable;
        }

        // Method to get all positions within an area by its index or name
        public LuaTable GetPositionsInArea(string areaIdentifier)
        {
            LogLuaMethodCall(areaIdentifier);

            Area area = GetAreaByIdentifier(areaIdentifier);

            if (area == null)
            {
                Log.Error("GetPositionsInArea: Invalid area identifier. Could not find a matching area.");
                return null;
            }

            lua.NewTable("positionsInArea"); // Create a named table in the Lua environment
            LuaTable luaTable = lua.GetTable("positionsInArea"); // Retrieve the Lua table by name

            int index = 1;
            foreach (IntVec3 cell in area.ActiveCells)
            {
                lua.NewTable($"positionsInArea[{index}]"); // Create a sub-table for each position
                LuaTable positionTable = lua.GetTable($"positionsInArea[{index}]");

                positionTable["x"] = cell.x;
                positionTable["y"] = cell.y;
                positionTable["z"] = cell.z;

                luaTable[index++] = positionTable;
            }

            return luaTable;
        }

        // Method to get properties of an area by its index or name
        public LuaTable GetAreaProperties(string areaIdentifier)
        {
            LogLuaMethodCall(areaIdentifier);
            Area area = GetAreaByIdentifier(areaIdentifier);

            if (area == null)
            {
                Log.Error("GetAreaProperties: Invalid area identifier. Could not find a matching area.");
                return null;
            }

            lua.NewTable("areaProperties"); // Create a named table in the Lua environment
            LuaTable propertiesTable = lua.GetTable("areaProperties"); // Retrieve the Lua table by name

            propertiesTable["AreaType"] = area.GetType().Name;
            propertiesTable["CellCount"] = area.ActiveCells.Count();

            return propertiesTable;
        }

        // Helper method to retrieve an Area by index or name
        private Area GetAreaByIdentifier(string areaIdentifier)
        {
            Area area = null;

            // Try to parse the identifier as an integer (index)
            if (int.TryParse(areaIdentifier, out int index))
            {
                var allAreas = this.Map.areaManager.AllAreas;
                if (index > 0 && index <= allAreas.Count)
                {
                    area = allAreas[index - 1]; // Lua is 1-indexed
                }
            }

            // If parsing as an integer failed, treat the identifier as a name
            if (area == null)
            {
                area = this.Map.areaManager.AllAreas.FirstOrDefault(a => a.Label == areaIdentifier);
            }

            return area;
        }

        public Area CreateOrModifyArea(int areaIndex, string areaType, LuaTable positions, bool clearExisting = false)
        {
            LogLuaMethodCall(areaIndex, areaType, positions, clearExisting);


            // Convert LuaTable to List<IntVec3>
            List<IntVec3> positionList = new List<IntVec3>();
            foreach (var pos in positions.Values)
            {
                if (pos is IntVec3 intVec3)
                {
                    positionList.Add(intVec3);
                }
                else
                {
                    Log.Error("Invalid position type in positions table.");
                }
            }

            AreaManager areaManager = this.Map.areaManager;
            Area area = GetAreaByIdentifier(areaIndex.ToString());

            if (area == null)
            {
                // Create a new area if it doesn't exist
                area = CreateAreaByType(areaType, areaManager);
                if (area == null)
                {
                    Log.Error($"Failed to create area of type '{areaType}'.");
                    return null;
                }
                Log.Message($"Area '{area.Label}' created at index {areaIndex}.");
            }
            else if (clearExisting)
            {
                // Clear existing cells by setting them to false using the innerGrid
                foreach (var cell in area.ActiveCells.ToList())
                {
                    area.Map.areaManager.AllAreas.Remove(area);
                    area = CreateAreaByType(areaType, areaManager);
                    Log.Message($"Area '{area.Label}' at index {areaIndex} cleared.");
                }
            }

            // Add new positionList to the area
            foreach (var position in positionList)
            {
                int index = area.Map.cellIndices.CellToIndex(position);
                if (area != null)
                {
                    area[index] = true; // Modify the grid directly
                }
            }

            Log.Message($"Area '{area.Label}' at index {areaIndex} of type '{areaType}' modified with {positionList.Count} positions.");
            return area;
        }

        // Helper method to create areas by type
        private Area CreateAreaByType(string areaType, AreaManager areaManager)
        {
            LogLuaMethodCall(areaType);

            if (areaType == "Area_Allowed")
            {
                areaManager.TryMakeNewAllowed(out Area_Allowed newArea);
                return newArea;
            }
            else if (areaType == "Area_Home")
            {
                return areaManager.Home;
            }
            // Add logic for other area types if necessary
            return null;
        }

        public bool DeleteArea(int areaIndex)
        {
            LogLuaMethodCall(areaIndex);

            Area area = GetAreaByIdentifier(areaIndex.ToString());

            if (area != null)
            {
                area.Delete();  // Use the Delete method to remove the area
                Log.Message($"Area '{area.Label}' at index {areaIndex} deleted.");
                return true;
            }
            else
            {
                Log.Message($"DeleteArea: No area found at index {areaIndex}.");
                return false;
            }
        }


        public Zone CreateOrModifyZone(int zoneIndex, string zoneType, LuaTable positions, bool clearExisting = false)
        {
            LogLuaMethodCall(zoneIndex, zoneType, positions, clearExisting);

            ZoneManager zoneManager = this.Map.zoneManager;
            Zone zone = GetZoneByIdentifier(zoneIndex.ToString());

            if (zone == null)
            {
                // Create a new zone if it doesn't exist
                if (zoneType == "Zone_Stockpile")
                {
                    StorageSettingsPreset preset = StorageSettingsPreset.DefaultStockpile; // Assuming a default preset
                    zone = new Zone_Stockpile(preset, zoneManager);
                }
                else if (zoneType == "Zone_Growing")
                {
                    zone = new Zone_Growing(zoneManager);
                }
                // Add the new zone to the ZoneManager
                zoneManager.RegisterZone(zone);
                Log.Message($"Zone '{zone.label}' created at index {zoneIndex}.");
            }
            else if (clearExisting)
            {
                // Clear all cells in the zone by removing them individually
                while (zone.Cells.Count > 0)
                {
                    zone.RemoveCell(zone.Cells[0]);
                }
                Log.Message($"Zone '{zone.label}' at index {zoneIndex} cleared.");
            }

            // Add new positions to the zone
            foreach (var positionEntry in positions.Values)
            {
                if (positionEntry is LuaTable positionTable)
                {
                    IntVec3 position = new IntVec3(
                        positionTable["x"] != null ? (int)(double)positionTable["x"] : 0,
                        positionTable["y"] != null ? (int)(double)positionTable["y"] : 0,
                        positionTable["z"] != null ? (int)(double)positionTable["z"] : 0
                    );

                    zone.AddCell(position);
                }
                else
                {
                    Log.Warning("Invalid position entry in LuaTable. Skipping.");
                }
            }

            Log.Message($"Zone '{zone.label}' at index {zoneIndex} of type '{zoneType}' modified with {positions.Values.Count} positions.");
            return zone;
        }


        // Method to delete a zone by its index
        public bool DeleteZone(int zoneIndex)
        {
            LogLuaMethodCall(zoneIndex);

            Zone zone = GetZoneByIdentifier(zoneIndex.ToString());

            if (zone != null)
            {
                this.Map.zoneManager.DeregisterZone(zone);
                Log.Message($"Zone '{zone.label}' at index {zoneIndex} deleted.");
                return true;
            }
            else
            {
                Log.Message($"DeleteZone: No zone found at index {zoneIndex}.");
                return false;
            }
        }



        // Method to get zones of a specific type and return as Lua table
        public LuaTable GetZonesOfType(string zoneType)
        {
            LogLuaMethodCall(zoneType);
            lua.NewTable("zonesOfType"); // Create a named table in the Lua environment
            LuaTable luaTable = lua.GetTable("zonesOfType"); // Retrieve the Lua table by name


            // Gather zones of the specified type
            int index = 1;
            foreach (var zone in this.Map.zoneManager.AllZones)
            {
                

                if (zone.GetType().Name == zoneType)
                {
                   

                    lua.NewTable($"zonesOfType[{index}]"); // Create a sub-table for each zone
                    LuaTable zoneData = lua.GetTable($"zonesOfType[{index}]");

                    // Add detailed zone properties
                    zoneData["ZoneType"] = zone.GetType().Name;
                    zoneData["ZoneName"] = zone.label;
                    zoneData["ZoneID"] = index; // Using the index as a simple identifier
                    zoneData["CellCount"] = zone.Cells.Count;

                    // Add the position of the first cell as a representative position
                    if (zone.Cells.Any()) // Ensure there are cells in the zone
                    {
                        zoneData["x"] = zone.Cells.First().x;
                        zoneData["y"] = zone.Cells.First().y;
                        zoneData["z"] = zone.Cells.First().z;

                       
                    }
                    else
                    {
                        Log.Warning($"Zone of type {zoneType} has no cells.");
                    }

                    luaTable[index++] = zoneData; // Add the zone data to the top-level table
                }
            }

            

            return luaTable; // Return the top-level table
        }


        // Method to get all unique zone types present on the map
        public LuaTable GetAllZoneTypes()
        {
            LogLuaMethodCall();
            lua.NewTable("zoneTypes"); // Create a named table in the Lua environment
            LuaTable luaTable = lua.GetTable("zoneTypes"); // Retrieve the Lua table by name

            

            // Retrieve the list of unique zone types
            var zoneTypes = this.Map.zoneManager.AllZones
                .Select(z => z.GetType().Name)
                .Distinct()
                .ToList();

           

            // Populate the Lua table with zone types
            for (int i = 0; i < zoneTypes.Count; i++)
            {
                luaTable[i + 1] = zoneTypes[i]; // Lua tables are 1-indexed
                Log.Message($"Added zone type {i + 1}: {zoneTypes[i]}");
            }

           
            return luaTable;
        }

        // Method to get all positions within a zone by its index or name
        public LuaTable GetPositionsInZone(string zoneIdentifier)
        {
            LogLuaMethodCall(zoneIdentifier);

            Zone zone = GetZoneByIdentifier(zoneIdentifier);

            if (zone == null)
            {
                Log.Error("GetPositionsInZone: Invalid zone identifier. Could not find a matching zone.");
                return null;
            }

            lua.NewTable("positionsInZone"); // Create a named table in the Lua environment
            LuaTable luaTable = lua.GetTable("positionsInZone"); // Retrieve the Lua table by name

            int index = 1;
            foreach (IntVec3 cell in zone.Cells)
            {
                lua.NewTable($"positionsInZone[{index}]"); // Create a sub-table for each position
                LuaTable positionTable = lua.GetTable($"positionsInZone[{index}]");

                positionTable["x"] = cell.x;
                positionTable["y"] = cell.y;
                positionTable["z"] = cell.z;

                luaTable[index++] = positionTable;
            }

            return luaTable;
        }

        // Method to get properties of a zone by its index or name
        public LuaTable GetZoneProperties(string zoneIdentifier)
        {
            LogLuaMethodCall(zoneIdentifier);
            Zone zone = GetZoneByIdentifier(zoneIdentifier);

            if (zone == null)
            {
                Log.Error("GetZoneProperties: Invalid zone identifier. Could not find a matching zone.");
                return null;
            }

            lua.NewTable("zoneProperties"); // Create a named table in the Lua environment
            LuaTable propertiesTable = lua.GetTable("zoneProperties"); // Retrieve the Lua table by name

            propertiesTable["ZoneType"] = zone.GetType().Name;
            propertiesTable["CellCount"] = zone.Cells.Count;
            propertiesTable["IsForbidden"] = zone.AllContainedThings.Any(t => t.IsForbidden(this.Faction));

            if (zone is Zone_Growing growZone)
            {
                propertiesTable["PlantDef"] = growZone.GetPlantDefToGrow().defName;
            }

            return propertiesTable;
        }

        // Helper method to retrieve a Zone by index or name
        private Zone GetZoneByIdentifier(string zoneIdentifier)
        {

            Zone zone = null;

            // Try to parse the identifier as an integer (index)
            if (int.TryParse(zoneIdentifier, out int index))
            {
                var allZones = this.Map.zoneManager.AllZones;
                if (index > 0 && index <= allZones.Count)
                {
                    zone = allZones[index - 1]; // Lua is 1-indexed
                }
            }

            // If parsing as an integer failed, treat the identifier as a name
            if (zone == null)
            {
                zone = this.Map.zoneManager.AllZones.FirstOrDefault(z => z.label == zoneIdentifier);
            }

            return zone;
        }




        public Blueprint CreateBlueprint(string defName, object positionObj, object stuffObj = null)
        {
            LogLuaMethodCall(defName, positionObj, stuffObj);

            // Convert string defName to a ThingDef using the new method
            ThingDef thingDef = GetThingDefByName(defName);

            if (thingDef == null)
            {
                Log.Error($"Failed to create blueprint: ThingDef '{defName}' not found.");
                return null;
            }

            // Convert the position object to a LocalTargetInfo
            LocalTargetInfo position = ConvertToLocalTargetInfo(positionObj);

            // Optionally, convert stuff to ThingDef using the new method
            ThingDef stuff = stuffObj != null ? GetThingDefByName((string)stuffObj) : null;

            // Place the blueprint
            Blueprint blueprint = GenConstruct.PlaceBlueprintForBuild(thingDef, position.Cell, Find.CurrentMap, Rot4.North, Faction.OfPlayer, stuff);

            return blueprint;
        }


        public void RemoveBlueprint(Blueprint blueprint)
        {
            LogLuaMethodCall(blueprint);
            if (blueprint != null && !blueprint.Destroyed)
            {
                blueprint.Destroy(DestroyMode.Cancel);
            }
        }


        public List<Blueprint> GetAllBlueprints()
        {
            LogLuaMethodCall();

            List<Blueprint> blueprints = new List<Blueprint>();

            foreach (Thing thing in Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (thing is Blueprint blueprint)
                {
                    blueprints.Add(blueprint);
                }
            }

            return blueprints;
        }

        public void SetBlueprintPosition(Blueprint blueprint, object positionObj)
        {
            LogLuaMethodCall(blueprint, positionObj);
            LocalTargetInfo position = ConvertToLocalTargetInfo(positionObj);
            if (blueprint != null && position.IsValid)
            {
                blueprint.Position = position.Cell;
            }
        }

        public void SetBlueprintRotation(Blueprint blueprint, int rotation)
        {
            LogLuaMethodCall(blueprint, rotation);
            if (blueprint != null)
            {
                blueprint.Rotation = new Rot4(rotation % 4);
            }
        }



        public LuaTable GetAllJobDefs()
        {
            LogLuaMethodCall();
            lua.NewTable("jobDefsTable");  // Create a table with a specific name
            LuaTable jobDefsTable = lua.GetTable("jobDefsTable");  // Retrieve the table using the name

            int index = 1;
            foreach (var jobDef in DefDatabase<JobDef>.AllDefs)
            {
                jobDefsTable[index++] = jobDef.defName;
            }

            return jobDefsTable;
        }
        
        // Method to save Lua code tabs
        public void SaveLuaCodeTabs(List<Tab> codeTabs)
        {
            savedLuaCodeTabs = codeTabs.Select(tab => new Tab(tab.Name, tab.Content)).ToList(); // Make a copy to avoid reference issues
        }

        // Method to load Lua code tabs
        public List<Tab> LoadLuaCodeTabs()
        {
            // Ensure there is at least one tab
            if (savedLuaCodeTabs == null || savedLuaCodeTabs.Count == 0)
            {
                savedLuaCodeTabs = new List<Tab> { new Tab("Code_1.lua", "") };
            }
            return savedLuaCodeTabs;
        }

        
        // Get the current position of the TurtleBot
        public IntVec3 GetCurrentPos()
        {
            LogLuaMethodCall();
            return this.Position;
        }

        // Get the Thing (e.g., plant, resource) at a specific location, with optional filters for category or defName
        public Thing GetThingAt(IntVec3 position, string categoryFilter = null, string defNameFilter = null)
        {
            LogLuaMethodCall(position, categoryFilter, defNameFilter);
            // Retrieve all things at the position
            List<Thing> things = this.Map.thingGrid.ThingsListAt(position);

            // If both filters are empty, return the first thing found
            if (string.IsNullOrEmpty(categoryFilter) && string.IsNullOrEmpty(defNameFilter))
            {
                return things.FirstOrDefault();
            }

            // Convert categoryFilter string to the corresponding ThingCategory enum
            ThingCategory? categoryEnum = null;
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                if (Enum.TryParse(categoryFilter, true, out ThingCategory parsedCategory))
                {
                    categoryEnum = parsedCategory;
                }
                else
                {
                    Log.Warning($"Invalid category filter: {categoryFilter}");
                    return null;
                }
            }

            // Filter based on the category or defName if provided
            foreach (Thing thing in things)
            {
                // Check for a specific category or defName
                if ((categoryEnum.HasValue && thing.def.category == categoryEnum.Value) ||
                    (!string.IsNullOrEmpty(defNameFilter) && thing.def.defName == defNameFilter))
                {
                    return thing;
                }
            }

            return null;  // Return null if no matching Thing is found
        }


        // Get the list of Things (e.g., plants, resources) at a specific location, with optional filters for category or defName
        public LuaTable GetThingsAt(IntVec3 position, string categoryFilter = null, string defNameFilter = null)
        {
            LogLuaMethodCall(position, categoryFilter, defNameFilter);

            // Retrieve all things at the position using ThingsListAtFast for better performance
            List<Thing> thingsAtPosition = this.Map.thingGrid.ThingsListAtFast(position);
            List<Thing> filteredThings = new List<Thing>();

            // If both filters are empty, return all things at the position
            if (string.IsNullOrEmpty(categoryFilter) && string.IsNullOrEmpty(defNameFilter))
            {
                return ConvertThingsToLuaTable(thingsAtPosition);
            }

            // Convert categoryFilter string to the corresponding ThingCategory enum
            ThingCategory? categoryEnum = null;
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                if (Enum.TryParse(categoryFilter, true, out ThingCategory parsedCategory))
                {
                    categoryEnum = parsedCategory;
                }
                else
                {
                    Log.Warning($"Invalid category filter: {categoryFilter}");
                    return ConvertThingsToLuaTable(filteredThings);  // Return an empty Lua table if the category filter is invalid
                }
            }

            // Filter based on the category or defName if provided
            foreach (Thing thing in thingsAtPosition)
            {
                bool matchesCategory = categoryEnum.HasValue && thing.def.category == categoryEnum.Value;
                bool matchesDefName = !string.IsNullOrEmpty(defNameFilter) && thing.def.defName == defNameFilter;

                // Add to the filtered list if it matches either filter
                if ((categoryEnum.HasValue && matchesCategory) || (!string.IsNullOrEmpty(defNameFilter) && matchesDefName))
                {
                    filteredThings.Add(thing);
                }
            }

            return ConvertThingsToLuaTable(filteredThings);  // Return the list of filtered things as a Lua table
        }


        // Helper method to convert a list of Thing objects to a Lua table
        private LuaTable ConvertThingsToLuaTable(List<Thing> things)
        {
            lua.NewTable("thingsTable");
            LuaTable luaTable = lua.GetTable("thingsTable");

            for (int i = 0; i < things.Count; i++)
            {
                luaTable[i + 1] = things[i];  // Lua tables are 1-indexed
            }

            return luaTable;
        }



        // Get the terrain at a specific location
        public TerrainDef GetTerrainAt(IntVec3 position)
        {
            LogLuaMethodCall(position);
            
            TerrainDef terrain = this.Map.terrainGrid.TerrainAt(position);
            if (terrain == null)
            {
                Log.Warning("No terrain found at the specified position.");
                return null;
            }
            
            return terrain;
        }


        public void SetTerrain(string terrainDefName, IntVec3 position)
        {
            LogLuaMethodCall(terrainDefName, position);

            TerrainDef terrainDef = DefDatabase<TerrainDef>.GetNamed(terrainDefName, true);
            if (terrainDef == null)
            {
                Log.Error($"TerrainDef '{terrainDefName}' not found.");
                return;
            }

            this.Map.terrainGrid.SetTerrain(position, terrainDef);
        }


        public void RemoveTerrain(IntVec3 position)
        {
            LogLuaMethodCall(position);

            TerrainDef defaultTerrain = TerrainDefOf.Soil;  // Assuming Soil is the default terrain
            this.Map.terrainGrid.SetTerrain(position, defaultTerrain);
        }


        public List<TerrainDef> GetAllTerrains()
        {
            LogLuaMethodCall();

            List<TerrainDef> terrainList = new List<TerrainDef>();
            foreach (TerrainDef terrainDef in DefDatabase<TerrainDef>.AllDefs)
            {
                terrainList.Add(terrainDef);
            }

            return terrainList;
        }



        // Get the cleanliness of the room at the TurtleBot's current position
        public float GetCleanlinessAt()
        {
            LogLuaMethodCall();
            Room room = this.GetRoom();
            return room?.GetStat(RoomStatDefOf.Cleanliness) ?? 0f;
        }

        // Method to check if a tile is passable
        public bool IsTilePassable(IntVec3 position)
        {
            LogLuaMethodCall(position);
            if (position.InBounds(this.Map))
            {
                TerrainDef terrain = this.Map.terrainGrid.TerrainAt(position);
                if (terrain != null)
                {
                    return terrain.passability != Traversability.Impassable;
                }
            }
            return false; // Return false if the position is out of bounds or impassable
        }



        public void WaitForTicks(int ticks, LuaFunction luaCallback)
        {
            LogLuaMethodCall(ticks, luaCallback);
            int targetTick = customTickCounter + ticks;
            waitRequests.Enqueue((targetTick, luaCallback));
        }


        private IEnumerator WaitCoroutine(int ticks, Action callback)
        {
            int initialTick = Find.TickManager.TicksGame;
            while (Find.TickManager.TicksGame < initialTick + ticks)
            {
                yield return null;
            }
            callback?.Invoke();
        }


        public void RegisterMapSize()
        {
            try
            {
                // Create a new table in Lua
                lua.DoString("MapSize = {}");

                // Set the width and height fields of the table
                lua.DoString($"MapSize.width = {this.Map.Size.x}");
                lua.DoString($"MapSize.height = {this.Map.Size.z}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to create and register MapSize table: {ex.Message}");
            }
        }

        // C# Method Implementation
        public void HarvestPlantAtPosition(IntVec3 position)
        {
            LogLuaMethodCall(position);
            try
            {
                Log.Message($"HarvestPlantAtPosition called with position: {position}");

                // Check if the position is within bounds of the map
                if (!position.InBounds(this.Map))
                {
                    Log.Warning($"Position {position} is out of bounds.");
                    return;
                }

                // Get the plant at the specified position
                Thing plant = position.GetPlant(this.Map);

                if (plant == null || !plant.def.plant.Harvestable)
                {
                    Log.Warning($"No harvestable plant found at position {position}");
                    return;
                }

                // Cancel any current job to ensure the TurtleBot is free to take on the harvesting job
                CancelCurrentJob();
                Log.Message("Canceled any current job.");

                // Create a JobDef for harvesting
                JobDef harvestJobDef = JobDefOf.Harvest;

                // Create a harvesting job targeting the plant
                Job harvestingJob = new Job(harvestJobDef, plant);
                Log.Message("Harvesting job created.");

                // Start the harvesting job
                this.jobs.StartJob(harvestingJob, JobCondition.InterruptForced, null, false, true, null, null, false);
                Log.Message($"Started harvesting job at position: {position}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error while trying to harvest at position {position}: {ex.Message}");
            }
        }


        // C# Method Implementation
        public void SowPlantAtPosition(IntVec3 position, ThingDef plantDef)
        {
            LogLuaMethodCall(position, plantDef);
            try
            {
                Log.Message($"SowPlantAtPosition called with position: {position} and plantDef: {plantDef.defName}");

                // Check if the position is within bounds of the map
                if (!position.InBounds(this.Map))
                {
                    Log.Warning($"Position {position} is out of bounds.");
                    return;
                }

                // Cancel any current job to ensure the TurtleBot is free to take on the sowing job
                CancelCurrentJob();
                Log.Message("Canceled any current job.");

                // Create a JobDef for sowing
                JobDef sowJobDef = JobDefOf.Sow;

                // Create a sowing job at the specified position
                Job sowingJob = new Job(sowJobDef, position);
                Log.Message("Sowing job created.");

                // Assign the PlantDef to the job
                sowingJob.plantDefToSow = plantDef;
                Log.Message($"Assigned {plantDef.defName} to the sowing job.");

                // Start the sowing job
                this.jobs.StartJob(sowingJob, JobCondition.InterruptForced, null, false, true, null, null, false);
                Log.Message($"Started sowing job for {plantDef.defName} at position: {position}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error while trying to sow at position {position}: {ex.Message}");
            }
        }



        // Create and execute a mining job at the specified position
        public void MineAtPosition(IntVec3 position)
        {
            LogLuaMethodCall(position);
            try
            {
                // Find the mineable thing at the position, specifically looking for a mineable object
                Thing mineableThing = this.Map.thingGrid.ThingAt(position, ThingCategory.Building); // Only look for buildings, which mineable rocks are categorized as

                // Check if the thing is mineable
                if (mineableThing != null && mineableThing.def.mineable)
                {
                    // Cancel any current job to ensure the pawn is free to take on the mining job
                    CancelCurrentJob();

                    // Create a mining job
                    Job miningJob = new Job(JobDefOf.Mine, mineableThing);

                    // Start the mining job
                    this.jobs.StartJob(miningJob, JobCondition.InterruptForced, null, false, true, null, null, false);
                }
                else
                {
                    Log.Warning($"No mineable object found at position {position}. The object may not be a building or may not be mineable.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error while trying to mine at position {position}: {ex.Message}");
            }
        }


        // Get detailed information about the type of object at a position
        public string GetTileInfo(IntVec3 position)
        {
            LogLuaMethodCall(position);
            // Check if the position is within the bounds of the map
            if (!position.InBounds(this.Map))
            {
                return "Nothing";  // Return "Nothing" if the position is out of bounds
            }

            Thing thing = this.Map.thingGrid.ThingAt<Thing>(position);
            if (thing != null)
            {
                return thing.def.defName;  // Return the DefName of the object at the position
            }
            TerrainDef terrain = this.Map.terrainGrid.TerrainAt(position);
            if (terrain != null)
            {
                return terrain.defName;  // Return the DefName of the terrain at the position
            }
            return "Nothing";  // If nothing is found, return this message
        }

        // Utility method to create IntVec3 from various input types
        public IntVec3 CreateIntVec3(object input)
        {
            LogLuaMethodCall(input);

            // Handle the case where input is an IntVec3
            if (input is IntVec3 intVec)
            {
                return intVec;
            }

            // Handle the case where input is a LuaTable representing a position
            if (input is LuaTable luaTable)
            {
                // Try to extract x, y, z components from the Lua table
                if (luaTable["x"] != null && luaTable["y"] != null && luaTable["z"] != null)
                {
                    int x = Convert.ToInt32(luaTable["x"]);
                    int y = Convert.ToInt32(luaTable["y"]); // Note: y is usually 0 in RimWorld
                    int z = Convert.ToInt32(luaTable["z"]);
                    return new IntVec3(x, y, z);
                }

                // Alternative case: handle table with integer indices {0, 0, 0}
                if (luaTable[1] != null && luaTable[2] != null && luaTable[3] != null)
                {
                    int x = Convert.ToInt32(luaTable[1]);
                    int y = Convert.ToInt32(luaTable[2]); // Note: y is usually 0 in RimWorld
                    int z = Convert.ToInt32(luaTable[3]);
                    return new IntVec3(x, y, z);
                }
            }

            // Handle the case where input is a string representing coordinates, e.g., "0,0,0"
            if (input is string str)
            {
                var parts = str.Split(',');
                if (parts.Length == 3 && 
                    int.TryParse(parts[0], out int x) && 
                    int.TryParse(parts[1], out int y) && 
                    int.TryParse(parts[2], out int z))
                {
                    return new IntVec3(x, y, z);
                }
            }

            // Handle the case where input is a Vector3 or Vector2
            if (input is Vector3 vec3)
            {
                return IntVec3.FromVector3(vec3);
            }
            if (input is Vector2 vec2)
            {
                return new IntVec3((int)vec2.x, 0, (int)vec2.y); // Assuming y = 0
            }

            // Handle the case where input is an array of three integers
            if (input is int[] intArray && intArray.Length == 3)
            {
                return new IntVec3(intArray[0], intArray[1], intArray[2]);
            }

            // Handle the case where input is a tuple (int, int, int)
            if (input is Tuple<int, int, int> tuple)
            {
                return new IntVec3(tuple.Item1, tuple.Item2, tuple.Item3);
            }

            // Handle null or invalid input
            if (input == null)
            {
                Log.Warning("CreateIntVec3 received a null input.");
                return IntVec3.Invalid;
            }

            // If none of the above, return an invalid IntVec3
            Log.Warning($"CreateIntVec3 received an unsupported or invalid target: {input.GetType().Name}");
            return IntVec3.Invalid;
        }



        // Method to get a ThingDef by its defName with logging
        public ThingDef GetThingDefByName(string defName)
        {
            LogLuaMethodCall(defName);
            try
            {
                // Attempt to retrieve the ThingDef by its defName
                ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(defName, errorOnFail: true);

                // Log success with the retrieved ThingDef's name
                Log.Message($"Successfully retrieved ThingDef: {thingDef.defName}");

                return thingDef;
            }
            catch (Exception ex)
            {
                // Log the error if the ThingDef could not be found
                Log.Error($"Failed to retrieve ThingDef with defName: {defName}. Exception: {ex.Message}");
                return null;
            }
        }

        // Method to cancel the current job
        public void CancelCurrentJob()
        {
            LogLuaMethodCall();
            if (this.jobs.curJob != null)
            {
                this.jobs.EndCurrentJob(JobCondition.InterruptForced);
                Log.Message("TurtleBot's current job has been canceled.");
            }
            else
            {
                Log.Warning("TurtleBot has no current job to cancel.");
            }
        }


        public void InjectCreateJobWrapper()
        {
            string luaWrapperScript = @"
                -- Store the original Turtle.CreateJob function
                Turtle._CreateJob_original = Turtle.CreateJob

                -- Create a separate wrapper function
                function Turtle._CreateJob_wrapper(jobDefObj, targetObj, additionalArg)
                    -- If the additionalArg is not provided, pass nil explicitly
                    if additionalArg == nil then
                        return Turtle._CreateJob_original(jobDefObj, targetObj, nil)
                    else
                        return Turtle._CreateJob_original(jobDefObj, targetObj, additionalArg)
                    end
                end

                -- Redefine Turtle.CreateJob to use the wrapper
                Turtle.CreateJob = Turtle._CreateJob_wrapper
            ";

            try
            {
                lua.DoString(luaWrapperScript);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to inject and replace CreateJob: {ex.Message}");
            }
        }


        public Thing GetPlantAtPosition(IntVec3 position)
        {
            // Check if the position is within bounds of the map
            if (!position.InBounds(this.Map))
            {
                Log.Warning($"Position {position} is out of bounds.");
                return null;
            }

            // Return the plant at the specified position
            return position.GetPlant(this.Map);
        }



        public Job CreateJob(JobDef jobDef, object target, LocalTargetInfo? additionalArg = null)
        {
            // If target is an IntVec3, and the job is related to plants, find the plant at that position
            if (target is IntVec3 position)
            {
                if (jobDef == JobDefOf.Harvest || jobDef == JobDefOf.CutPlant)
                {
                    Thing plant = GetPlantAtPosition(position);
                    if (plant != null)
                    {
                        target = plant;  // Convert target to the specific plant (Thing)
                    }
                    else
                    {
                        target = new LocalTargetInfo(position);  // Fallback to using the position
                    }
                }
                else
                {
                    target = new LocalTargetInfo(position);
                }
            }

            // Convert target to LocalTargetInfo
            LocalTargetInfo primaryTarget = ConvertToLocalTargetInfo(target);

            // Create the job
            Job job = new Job(jobDef, primaryTarget);

            // Handle the additional argument if provided
            if (additionalArg != null)
            {
                job.targetB = additionalArg.Value;
            }

            return job;
        }



        // Example helper method to find a Thing of a specific ThingDef near a target location
        private Thing FindNearestThingOfDef(IntVec3 position, ThingDef thingDef)
        {
            // Search for the nearest Thing of the specified ThingDef near the position
            return GenClosest.ClosestThing_Global_Reachable(
                position,
                this.Map,
                this.Map.listerThings.ThingsOfDef(thingDef),
                PathEndMode.OnCell,
                TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false),
                9999f,
                t => t.def == thingDef
            );
        }








        public void WaitForJobCompletion(LuaFunction luaCallback)
        {
            LogLuaMethodCall(luaCallback);

            // Ensure any previous coroutine is stopped before starting a new one
            if (jobCompletionCoroutine != null)
            {
                TurtleMod.unityTaskManager.StopUnityCoroutine(jobCompletionCoroutine);
            }

            jobCompletionCoroutine = TurtleMod.unityTaskManager.StartUnityCoroutine(WaitForJobCompletionCoroutine(luaCallback));
        }

        private IEnumerator WaitForJobCompletionCoroutine(LuaFunction luaCallback)
        {
            // Get the current job of the TurtleBot
            Job currentJob = this.jobs.curJob;

            // Log the initial state of the job
            Log.Message($"Starting WaitForJobCompletionCoroutine for job: {currentJob?.def.defName}");

            // Loop to keep checking the job status
            while (this.jobs.curJob != null && this.jobs.curJob == currentJob)
            {
                // Check if the job has switched to "Wait", indicating it might be completed
                if (this.jobs.curJob.def.defName == "Wait")
                {
                    // Log that the job has switched to "Wait"
                    Log.Message("Job finished and switched to 'Wait'.");
                    break;
                }

                // Wait for 0.2 seconds before checking the job status again
                yield return new WaitForSeconds(0.2f);
            }

            // After the loop exits, the job is considered completed or has changed
            Log.Message($"Job {currentJob?.def.defName} considered completed or changed, calling Lua callback.");

            // Call the Lua callback function to signal job completion
            luaCallback.Call();

            // Clear the reference to the coroutine (optional cleanup step)
            jobCompletionCoroutine = null;
        }




        public Job GetCurrentJob()
        {
            LogLuaMethodCall();
            return this.jobs.curJob;
        }


        public void ExecuteLuaCode(string code)
        {
            
            // Stop any currently running Lua execution
            //StopLuaExecution();

            // Execute immediately
            currentExecutionCoroutine = TurtleMod.unityTaskManager.StartUnityCoroutine(ExecuteLuaCodeCoroutine(code));
        }

        // public void ScheduleLuaCodeExecution(string code, int intervalTicks)
        // {
        //     // Stop any currently running Lua execution
        //     //StopLuaExecution();

        //     // Set up the scheduled execution
        //     scheduledExecutionInterval = intervalTicks;
        //     ticksUntilNextExecution = scheduledExecutionInterval;
        //     SaveLuaCode(code);  // Save the code to be executed repeatedly
        // }

        public void ExecuteLuaCodeContinuously(string code)
        {
            // Stop any currently running Lua execution
            //  StopLuaExecution();

            continuousExecution = true;
            stopExecutionRequested = false;
            currentExecutionCoroutine = TurtleMod.unityTaskManager.StartUnityCoroutine(ExecuteLuaCodeContinuouslyCoroutine(code));
        }

        public void StopLuaExecution()
        {
            // Stop the main Lua execution coroutine if it's running
            if (currentExecutionCoroutine != null)
            {
                TurtleMod.unityTaskManager.StopUnityCoroutine(currentExecutionCoroutine);
                currentExecutionCoroutine = null;
            }

            // Stop the job completion coroutine if it's running
            if (jobCompletionCoroutine != null)
            {
                TurtleMod.unityTaskManager.StopUnityCoroutine(jobCompletionCoroutine);
                jobCompletionCoroutine = null;
            }

            // Clear the wait requests queue to prevent any pending waits from continuing
            waitRequests.Clear();

            // Optionally clear and reinitialize the Lua environment
            lua.Close(); // Close the current Lua state
            InitializeLuaEnvironment(); // Reinitialize the Lua environment for a clean state

            Log.Message("Lua execution stopped successfully.");
        }


        public void InjectLuaFunctions()
        {
            string luaScriptPath = @"G:\SteamLibrary\steamapps\common\RimWorld\Mods\TurtleBotSimplified\TurtleMod\lua_scripts\injected_lua_functions.lua";
            
            try
            {
                // Read the Lua script from the file
                string injectedCode = File.ReadAllText(luaScriptPath);

                // Inject the Lua script into the Lua environment
                lua.DoString(injectedCode);
            }
            catch (Exception ex)
            {
                Log.Error("Error injecting Lua functions: " + ex.Message);
            }
        }






        private IEnumerator ExecuteLuaCodeCoroutine(string code)
        {
            InitializeLuaEnvironment();
            
            // Clear the Lua _MethodCallHistory table before execution
            lua.DoString(@"
                _MethodCallHistory = {}
            ");
            yield return null;

            try
            {
                // Store the code string in a global Lua variable
                lua.DoString($"_ExecutedLuaCode = \"{code.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r")}\"");


                lua.DoString(code);

                Log.Message("Lua code executed successfully.");
            }
            catch (LuaScriptException luaEx)
            {
                LogMethodCallHistory(true);
                // Log the Lua error message
                Log.Error($"Lua error: {luaEx.Message}");
                

                try
                {
                    // Attempt to parse the line number from the Lua error message
                    string lineNumber = string.Empty;
                    string errorLine = string.Empty;
                    var lineMatch = System.Text.RegularExpressions.Regex.Match(luaEx.Message, @"\[string ""chunk""\]:(\d+):");

                    if (lineMatch.Success)
                    {
                        lineNumber = lineMatch.Groups[1].Value;
                        int originalLineNumber = int.Parse(lineNumber);

                        // If the line number is valid, attempt to log the specific line that caused the error
                        if (originalLineNumber > 0)
                        {
                            // Split lines by both \r\n and \n to handle different line endings
                            string[] lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                            if (originalLineNumber <= lines.Length)
                            {
                                errorLine = lines[originalLineNumber - 1]; // Lua uses 1-based index

                                // Store the error details in the CustomTurtleBot instance
                                ErrorLineNumber = originalLineNumber;
                                ErrorLine = errorLine;
                                IsErrorDisplaying = true;

                                Log.Message($"Identified error line: {errorLine}");
                            }
                            else
                            {
                                Log.Warning($"Line number {originalLineNumber} is out of range for the provided Lua code.");
                            }
                        }
                        else
                        {
                            Log.Warning($"Error at line {originalLineNumber}, but line number is out of range.");
                        }
                    }

                    // Conditionally log the line number and error line if they exist
                    if (!string.IsNullOrEmpty(lineNumber) && !string.IsNullOrEmpty(errorLine))
                    {
                        Log.Error($"Error occurred at line {lineNumber}: {errorLine}");
                    }
                }
                catch (Exception innerEx)
                {
                    Log.Error($"Failed to process Lua error line information: {innerEx.Message}");
                }

                // Log the history of Lua method calls
                LogMethodCallHistory(true);

                // Log the real stack trace and other details
                Log.Error($"Real Stack Trace:\n{luaEx.StackTrace}");



                if (luaEx.InnerException != null)
                {
                    Log.Error($"Inner .NET Exception: {luaEx.InnerException.Message}\nStack Trace:\n{luaEx.InnerException.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unexpected .NET exception caught during Lua code execution.");
                Log.Error($"Exception Message: {ex.Message}");
                Log.Error($"Stack Trace:\n{ex.StackTrace}");
            }
        }


        public void LogMethodCallHistory(bool logAsError = false)
        {
            // Prepare the Lua string command to execute
            // Pass the logAsError flag directly into the Lua function as true or false
            string luaCommand = $"_LogFormattedTable(_MethodCallHistory, {(logAsError ? "true" : "false")})";
            
            // Execute the Lua command
            lua.DoString(luaCommand);
        }


        // Helper function to format Lua values based on their type
        private string FormatLuaValue(object value)
        {
            if (value is LuaTable table)
            {
                // If it's a LuaTable, format its contents recursively
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                foreach (var key in table.Keys)
                {
                    sb.AppendLine($"    {key}: {FormatLuaValue(table[key])}");
                }
                sb.Append("  }");
                return sb.ToString();
            }
            else if (value == null)
            {
                return "null";
            }
            else
            {
                return value.ToString();
            }
        }



        private IEnumerator ExecuteLuaCodeContinuouslyCoroutine(string code)
        {
            while (continuousExecution && !stopExecutionRequested)
            {
                yield return ExecuteLuaCodeCoroutine(code);
                yield return new WaitForSeconds(1f);  // Adjust the delay as needed
            }
        }



        public override IEnumerable<Gizmo> GetGizmos()
        {
            // Yield all base gizmos first
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }



            // Add your existing Lua console command gizmo
            yield return new Command_Action
            {
                defaultLabel = "Open Lua Console",
                defaultDesc = "Opens a console to input and execute Lua code.",
                icon = ContentFinder<Texture2D>.Get("TurtleBot", true),
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_LuaConsole(this));
                }
            };
        }

        // Method to get the execution state of a specific tab
        public bool GetTabExecutionState(int tabIndex)
        {
            // Ensure the list is long enough to include the requested tabIndex
            if (tabExecutionState.Count <= tabIndex)
            {
                while (tabExecutionState.Count <= tabIndex)
                    tabExecutionState.Add(true);  // Default to true for new tabs
            }

            return tabExecutionState[tabIndex];
        }

        // Method to set the execution state of a specific tab
        public void SetTabExecutionState(int tabIndex, bool state)
        {
            if (tabExecutionState.Count <= tabIndex)
            {
                while (tabExecutionState.Count <= tabIndex)
                    tabExecutionState.Add(true);
            }

            tabExecutionState[tabIndex] = state;
        }


        // Helper class to store method call information
        private class MethodCallInfo
        {
            public string MethodName { get; set; }
            public string ArgumentTypes { get; set; }
        }


    }


    // Wrapper class to expose JobDefs
    public class LuaJobDefOf
    {
        public JobDef Clean => JobDefOf.Clean;
        public JobDef Mine => JobDefOf.Mine;  // Expose the mining job
        public JobDef Goto => JobDefOf.Goto;  // Expose the Goto job for movement
        public JobDef Sow => JobDefOf.Sow;    // Expose the Sow job for planting
        public JobDef HaulToCell => JobDefOf.HaulToCell; // Expose the HaulToCell job
        public JobDef HaulToContainer => JobDefOf.HaulToContainer; // Expose the HaulToContainer job
        public JobDef Harvest => JobDefOf.Harvest;  // Expose the Harvest job for harvesting plants
        public JobDef CutPlant => JobDefOf.CutPlant;  // Expose the CutPlant job for cutting plants
        public JobDef BuildRoof => JobDefOf.BuildRoof; // Expose the BuildRoof job
        public JobDef RemoveRoof => JobDefOf.RemoveRoof; // Expose the RemoveRoof job

        // Additional JobDefs
        public JobDef ConstructFinishFrame => JobDefOf.FinishFrame; // Finish construction frame
        public JobDef Deconstruct => JobDefOf.Deconstruct; // Deconstruct structures
        public JobDef Repair => JobDefOf.Repair; // Repair structures
        public JobDef Flick => JobDefOf.Flick; // Toggle power or other switches
        public JobDef Hunt => JobDefOf.Hunt; // Hunt wild animals
        public JobDef Train => JobDefOf.Train; // Train animals
        public JobDef Tame => JobDefOf.Tame; // Tame wild animals
        public JobDef Slaughter => JobDefOf.Slaughter; // Slaughter tamed animals
        public JobDef Rescue => JobDefOf.Rescue; // Rescue downed pawns
        public JobDef Capture => JobDefOf.Capture; // Capture prisoners
        public JobDef FeedPatient => JobDefOf.FeedPatient; // Feed patients or prisoners
        public JobDef TendPatient => JobDefOf.TendPatient; // Tend to patients' wounds
        public JobDef DeliverFood => JobDefOf.DeliverFood; // Deliver food to patients or prisoners
        public JobDef Refuel => JobDefOf.Refuel; // Refuel buildings or machines
        public JobDef UnloadInventory => JobDefOf.UnloadInventory; // Unload a pawn's inventory
        public JobDef Strip => JobDefOf.Strip; // Strip dead or unconscious pawns of items
        public JobDef Equip => JobDefOf.Equip; // Equip weapons or items
        public JobDef ConsumeMeal => JobDefOf.Ingest; // Consume food or meals
        public JobDef Wear => JobDefOf.Wear; // Wear clothing or armor
        public JobDef RemoveApparel => JobDefOf.RemoveApparel; // Remove worn apparel
        public JobDef OperateDeepDrill => JobDefOf.OperateDeepDrill; // Operate deep drilling machines
        public JobDef Milk => JobDefOf.Milk; // Milk animals
        public JobDef Shear => JobDefOf.Shear; // Shear wool from animals
        public JobDef Meditate => JobDefOf.Meditate; // Meditate for psycasters
        public JobDef Research => JobDefOf.Research; // Conduct research
        public JobDef Open => JobDefOf.Open; // Open containers or cryptosleep caskets
        public JobDef BeatFire => JobDefOf.BeatFire; // Extinguish fire

        public JobDef Wait => JobDefOf.Wait;
        public JobDef Wait_MaintainPosture => JobDefOf.Wait_MaintainPosture;
    }



    // Wrapper class to expose JobCondition enums
    public class LuaJobCondition
    {
        public JobCondition Succeeded => JobCondition.Succeeded;
        public JobCondition Errored => JobCondition.Errored;
        public JobCondition Incompletable => JobCondition.Incompletable;
        public JobCondition InterruptForced => JobCondition.InterruptForced;
        public JobCondition Ongoing => JobCondition.Ongoing;
    }

    // Class to store Lua method information
    public class LuaMethodInfo
    {
        public string Name { get; set; }  // Name of the method
        public string ReturnType { get; set; }  // Return type of the method
        public List<LuaMethodArgument> Arguments { get; set; }  // List of arguments
    }
    // Class to store argument information
    public class LuaMethodArgument
    {
        public string ArgName { get; set; }
        public string ArgType { get; set; }
        public bool IsOptional { get; set; }  // Indicates if the argument is optional
        public object DefaultValue { get; set; }  // Stores the default value if the argument is optional
    }
    public class TileData
    {
        public string Terrain { get; set; }
        public string ThingDef { get; set; }
        public bool IsPassable { get; set; }
        public IntVec3 Position { get; set; }  // Add Position to TileData
    }



    public class UnityTaskManager : MonoBehaviour
    {
        public static UnityTaskManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public Coroutine StartUnityCoroutine(IEnumerator coroutine)
        {
            return StartCoroutine(coroutine);
        }

        public void StopUnityCoroutine(Coroutine coroutine)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
    }



public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private static readonly Queue<Action> actions = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            // Find the existing dispatcher or create a new one
            _instance = FindObjectOfType<UnityMainThreadDispatcher>();
            if (_instance == null)
            {
                GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
                _instance = dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(dispatcherObject); // Ensure it persists across scenes
            }
        }
        return _instance;
    }

    public static void Enqueue(Action action)
    {
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }

    void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
            {
                actions.Dequeue().Invoke();
            }
        }
    }
}



}


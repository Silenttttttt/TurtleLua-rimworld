using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System;

namespace TurtleMod
{

public class TurtleMod : Mod
{
    public static TurtleModSettings settings;
    public static UnityTaskManager unityTaskManager;  // Static instance of the UnityTaskManager

    public TurtleMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<TurtleModSettings>();
        ModThemeConfig.LoadFromXML();

        // Use ExecuteWhenFinished to ensure this runs after mod loading is complete, on the main thread
        LongEventHandler.ExecuteWhenFinished(InitializeUnityTaskManager);
    }

    private void InitializeUnityTaskManager()
    {
        if (unityTaskManager == null)
        {
            try
            {
                // This is executed on the main thread, after RimWorld has completed loading tasks
                GameObject taskManagerObject = new GameObject("UnityTaskManager");
                unityTaskManager = taskManagerObject.AddComponent<UnityTaskManager>();

                // Prevent the task manager from being destroyed when switching scenes or maps
                GameObject.DontDestroyOnLoad(taskManagerObject);

            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize UnityTaskManager: " + ex);
            }
        }
    }


    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);

        if (listingStandard.ButtonText("Customize Theme"))
        {
            var initialColors = new Dictionary<string, Color>
            {
                { "Keyword Color", ParseColor(settings.KeywordColor) },
                { "Comment Color", ParseColor(settings.CommentColor) },
                { "String Color", ParseColor(settings.StringColor) },
                { "Number Color", ParseColor(settings.NumberColor) },
                { "Boolean Color", ParseColor(settings.BooleanColor) },
                { "Nil Color", ParseColor(settings.NilColor) },
                { "Operator Color", ParseColor(settings.OperatorColor) },
                { "Function Color", ParseColor(settings.FunctionColor) },
                { "Variable Color", ParseColor(settings.VariableColor) },
                { "Error Color", ParseColor(settings.ErrorColor) },
                { "Selection Color", ParseColor(settings.SelectionColor) }, 
                { "Turtle Color", ParseColor(TurtleMod.settings.TurtleColor) }
            };

            Find.WindowStack.Add(new Dialog_ColorPicker(
                initialColors,
                (selectedElement, selectedColor) =>
                {
                    var colorHex = ColorUtility.ToHtmlStringRGB(selectedColor);
                    switch (selectedElement)
                    {
                        case "Keyword Color": settings.KeywordColor = colorHex; break;
                        case "Comment Color": settings.CommentColor = colorHex; break;
                        case "String Color": settings.StringColor = colorHex; break;
                        case "Number Color": settings.NumberColor = colorHex; break;
                        case "Boolean Color": settings.BooleanColor = colorHex; break;
                        case "Nil Color": settings.NilColor = colorHex; break;
                        case "Operator Color": settings.OperatorColor = colorHex; break;
                        case "Function Color": settings.FunctionColor = colorHex; break;
                        case "Variable Color": settings.VariableColor = colorHex; break;
                        case "Error Color": settings.ErrorColor = colorHex; break;
                        case "Selection Color": settings.SelectionColor = colorHex; break;
                        case "Turtle Color": settings.TurtleColor = colorHex; break;
                    }
                }
            ));
        }

        if (listingStandard.ButtonText("Reset to Default"))
        {
            settings = new TurtleModSettings(); // Reset settings to default
        }

        listingStandard.End();
    }


    private Color ParseColor(string colorString)
    {
        if (!colorString.StartsWith("#"))
        {
            colorString = "#" + colorString;
        }
        
        if (ColorUtility.TryParseHtmlString(colorString, out var color))
        {
            return color;
        }

        return Color.white;
    }


    public override string SettingsCategory() => "TurtleBot Mod";

    public override void WriteSettings()
    {
        base.WriteSettings();
        ModThemeConfig.KeywordColor = settings.KeywordColor;
        ModThemeConfig.CommentColor = settings.CommentColor;
        ModThemeConfig.StringColor = settings.StringColor;
        ModThemeConfig.NumberColor = settings.NumberColor;
        ModThemeConfig.BooleanColor = settings.BooleanColor;
        ModThemeConfig.NilColor = settings.NilColor;
        ModThemeConfig.OperatorColor = settings.OperatorColor;
        ModThemeConfig.FunctionColor = settings.FunctionColor;
        ModThemeConfig.VariableColor = settings.VariableColor;
        ModThemeConfig.ErrorColor = settings.ErrorColor;
        ModThemeConfig.SelectionColor = settings.SelectionColor;
        ModThemeConfig.TurtleColor = settings.TurtleColor;

        ModThemeConfig.SaveToXML(); // Save settings to XML
    }
}


public class TurtleModSettings : ModSettings
{
    public string KeywordColor = ModThemeConfig.KeywordColor;
    public string CommentColor = ModThemeConfig.CommentColor;
    public string StringColor = ModThemeConfig.StringColor;
    public string NumberColor = ModThemeConfig.NumberColor;
    public string BooleanColor = ModThemeConfig.BooleanColor;
    public string NilColor = ModThemeConfig.NilColor;
    public string OperatorColor = ModThemeConfig.OperatorColor;
    public string FunctionColor = ModThemeConfig.FunctionColor;
    public string VariableColor = ModThemeConfig.VariableColor;
    public string ErrorColor = ModThemeConfig.ErrorColor;
    public string SelectionColor = ModThemeConfig.SelectionColor;
    public string TurtleColor = ModThemeConfig.TurtleColor;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref KeywordColor, "KeywordColor", "#B45EFF");
        Scribe_Values.Look(ref CommentColor, "CommentColor", "#4E5F75EB");
        Scribe_Values.Look(ref StringColor, "StringColor", "#64FE2E");
        Scribe_Values.Look(ref NumberColor, "NumberColor", "#FFCC00");
        Scribe_Values.Look(ref BooleanColor, "BooleanColor", "#FE7000");
        Scribe_Values.Look(ref NilColor, "NilColor", "#A8BDD5");
        Scribe_Values.Look(ref OperatorColor, "OperatorColor", "#A8BDD5");
        Scribe_Values.Look(ref FunctionColor, "FunctionColor", "#FFEB00");
        Scribe_Values.Look(ref VariableColor, "VariableColor", "#00CCFF");
        Scribe_Values.Look(ref ErrorColor, "ErrorColor", "#FF0000");
        Scribe_Values.Look(ref SelectionColor, "SelectionColor", "#3399FF");
        Scribe_Values.Look(ref TurtleColor, "TurtleColor", "#FF69B4");

        base.ExposeData();
    }
}


public static class ModThemeConfig
{
    public static string KeywordColor = "#B45EFF";
    public static string CommentColor = "#4E5F75";
    public static string StringColor = "#64FE2E";
    public static string NumberColor = "#FFCC00";
    public static string BooleanColor = "#FE7000";
    public static string NilColor = "#A8BDD5";
    public static string OperatorColor = "#A8BDD5";
    public static string FunctionColor = "#FFEB00";
    public static string VariableColor = "#00CCFF";
    public static string ErrorColor = "#FF0000";
    public static string SelectionColor = "#3399FF"; 

    public static string TurtleColor = "#FF69B4"; 

    private static string ConfigFilePath => Path.Combine(GenFilePaths.ConfigFolderPath, "TurtleModThemeConfig.xml");

    public static void LoadFromXML()
    {
        if (!File.Exists(ConfigFilePath)) return;

        XDocument doc = XDocument.Load(ConfigFilePath);
        foreach (var element in doc.Root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "KeywordColor": KeywordColor = element.Value; break;
                case "CommentColor": CommentColor = element.Value; break;
                case "StringColor": StringColor = element.Value; break;
                case "NumberColor": NumberColor = element.Value; break;
                case "BooleanColor": BooleanColor = element.Value; break;
                case "NilColor": NilColor = element.Value; break;
                case "OperatorColor": OperatorColor = element.Value; break;
                case "FunctionColor": FunctionColor = element.Value; break;
                case "VariableColor": VariableColor = element.Value; break;
                case "ErrorColor": ErrorColor = element.Value; break;
                case "SelectionColor": SelectionColor = element.Value; break;
                case "TurtleColor": TurtleColor = element.Value; break;
            }
        }
    }

    public static void SaveToXML()
    {
        XDocument doc = new XDocument(
            new XElement("TurtleModThemeConfig",
                new XElement("KeywordColor", KeywordColor),
                new XElement("CommentColor", CommentColor),
                new XElement("StringColor", StringColor),
                new XElement("NumberColor", NumberColor),
                new XElement("BooleanColor", BooleanColor),
                new XElement("NilColor", NilColor),
                new XElement("OperatorColor", OperatorColor),
                new XElement("FunctionColor", FunctionColor),
                new XElement("VariableColor", VariableColor),
                new XElement("ErrorColor", ErrorColor),
                new XElement("SelectionColor", SelectionColor),
                new XElement("TurtleColor", TurtleColor) 
            )
        );

        doc.Save(ConfigFilePath);
    }
}


}

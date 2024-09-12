using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;
using RimWorld;

namespace TurtleMod
{






public class SyntaxHighlighter
{
    private readonly Dictionary<string, string> _syntaxColors;
    private readonly List<LuaMethodInfo> _registeredMethods;

    public SyntaxHighlighter(List<LuaMethodInfo> registeredMethods)
    {
        _registeredMethods = registeredMethods;
        _syntaxColors = new Dictionary<string, string>
        {
            { @"--.*", "COMMENT" }, // Comments first
            { "\"[^\"]*\"|'[^']*'", "STRING" }, // Match both double-quoted and single-quoted strings
            { @"Turtle\.\w+", "TURTLE" }, // Turtle object and its members
            { @"\b\d+(\.\d+)?\b", "NUMBER" }, // Numbers
            { @"\b(true|false)\b", "BOOLEAN" }, // Boolean values true/false
            { @"\bnil\b", "NIL" }, // Nil value
            { @"\b(function|end|if|then|else|elseif|for|while|do|return|local)\b", "KEYWORD" }, // Keywords
            { @"\b(and|or|not)\b", "OPERATOR" }, // Logical operators
            { @"\+|-|\*|\/|%|=|==|~=|‹|›", "OPERATOR" }, // Other operators (including special ‹ and ›)
            { @"\b\w+\s*(?=\()", "FUNCTION" }, // Function calls
            { @"(?<![\w\d_])\b([a-zA-Z_]\w*)\b(?![\w\d_])", "VARIABLE" } // Variables and default color
        };
    }

    public string ApplySyntaxHighlighting(string code)
    {
        var matchesAndReplacements = new List<(int Index, int Length, string Replacement)>();

        foreach (Match match in Regex.Matches(code, @"Turtle\.\w+"))
        {
            string turtleColor = GetColorForType("TURTLE");
            string methodOrVarColor;
            string methodName = match.Value.Substring(7); // Extract the part after "Turtle."

            if (_registeredMethods.Any(m => m.Name == "Turtle." + methodName))
            {
                // Check if the match is followed by `()`
                bool isMethodCall = Regex.IsMatch(code.Substring(match.Index + match.Length), @"\s*\(");
                methodOrVarColor = isMethodCall ? GetColorForType("FUNCTION") : GetColorForType("VARIABLE");
            }
            else
            {
                // Invalid method; use error color
                methodOrVarColor = GetColorForType("ERROR");
            }

            // Split the match into "Turtle." and the method/variable part
            string turtlePart = $"<color={turtleColor}>Turtle.</color>";
            string methodOrVarPart = $"<color={methodOrVarColor}>{methodName}</color>";

            string replacement = turtlePart + methodOrVarPart;

            matchesAndReplacements.Add((match.Index, match.Length, replacement));
        }

        // Handle the rest of the syntax highlighting
        foreach (var entry in _syntaxColors.Where(e => e.Key != @"Turtle\.\w+"))
        {
            string colorCode = GetColorForType(entry.Value);

            foreach (Match match in Regex.Matches(code, entry.Key))
            {
                if (!IsOverlappingWithExistingMatches(matchesAndReplacements, match.Index, match.Length))
                {
                    string replacement = $"<color={colorCode}>{match.Value}</color>";
                    matchesAndReplacements.Add((match.Index, match.Length, replacement));
                }
            }
        }

        matchesAndReplacements = matchesAndReplacements.OrderByDescending(m => m.Index).ToList();

        foreach (var (index, length, replacement) in matchesAndReplacements)
        {
            code = code.Remove(index, length).Insert(index, replacement);
        }

        return code;
    }

    private bool IsOverlappingWithExistingMatches(List<(int Index, int Length, string Replacement)> matches, int index, int length)
    {
        return matches.Any(match => index < match.Index + match.Length && match.Index < index + length);
    }

    private string GetColorForType(string type)
    {
        return type switch
        {
            "KEYWORD" => "#" + TurtleMod.settings.KeywordColor,
            "COMMENT" => "#" + TurtleMod.settings.CommentColor,
            "STRING" => "#" + TurtleMod.settings.StringColor,
            "NUMBER" => "#" + TurtleMod.settings.NumberColor,
            "BOOLEAN" => "#" + TurtleMod.settings.BooleanColor,
            "NIL" => "#" + TurtleMod.settings.NilColor,
            "OPERATOR" => "#" + TurtleMod.settings.OperatorColor,
            "FUNCTION" => "#" + TurtleMod.settings.FunctionColor,
            "VARIABLE" => "#" + TurtleMod.settings.VariableColor,
            "TURTLE" => "#" + TurtleMod.settings.TurtleColor,
            "ERROR" => "#" + TurtleMod.settings.ErrorColor,
            _ => "#FFFFFF", // Default color if type not found
        };
    }
}


public class Dialog_RenameTab : Window
{
    private string tabName;
    private Action<string> onRename;
    private const int MaxTabNameLength = 30;  // Define the max tab name length

    public Dialog_RenameTab(Tab tab)
    {
        this.tabName = tab.Name;
        this.onRename = (newName) => tab.Name = newName;

        this.doCloseX = true;
        this.closeOnClickedOutside = true;
        this.absorbInputAroundWindow = true;
        this.forcePause = true;
    }

    public override Vector2 InitialSize => new Vector2(300f, 150f);

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Rename Tab");

        // Allow input and apply truncation visually in the text field
        tabName = Widgets.TextField(new Rect(0f, 40f, inRect.width - 20f, 30f), tabName);

        if (Widgets.ButtonText(new Rect(0f, 80f, inRect.width / 2f - 10f, 30f), "Cancel"))
        {
            Close();
        }

        if (Widgets.ButtonText(new Rect(inRect.width / 2f + 10f, 80f, inRect.width / 2f - 10f, 30f), "OK"))
        {
            RenameTab();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        RenameTab();  // Handle the Enter key by renaming the tab
    }

    private void RenameTab()
    {
        // Truncate the name to the max length before saving
        string truncatedName = tabName.Length > MaxTabNameLength ? tabName.Substring(0, MaxTabNameLength) : tabName;
        onRename(truncatedName);
        Close();
    }
}



public class Dialog_ContextMenu : Window
{
    private int tabIndex;
    private Action<int> onRename;
    private Action<int> onCloseTab;
    private Action<int> onSaveTab;
    private Rect tabRect;  // The position of the tab for correct alignment
    private Rect parentWindowRect; // The parent window's position
    private Func<int, string> getTabContent;  // Function to get the content of the tab

    public Dialog_ContextMenu(int tabIndex, Action<int> onRename, Action<int> onCloseTab, Action<int> onSaveTab, Rect tabRect, Rect parentWindowRect, Func<int, string> getTabContent)
    {
        this.tabIndex = tabIndex;
        this.onRename = onRename;
        this.onCloseTab = onCloseTab;
        this.onSaveTab = onSaveTab;
        this.tabRect = tabRect;
        this.parentWindowRect = parentWindowRect;
        this.getTabContent = getTabContent;

        this.doCloseX = false;
        this.closeOnClickedOutside = true;  // Close when clicking outside
        this.absorbInputAroundWindow = true;  // Absorb input around the window
        this.forcePause = false;
    }

    public override Vector2 InitialSize => new Vector2(150f, 100f);


    protected override void SetInitialSizeAndPosition()
    {
        // Calculate the center X position for horizontal alignment, then shift left by 20 pixels
        float xPos = parentWindowRect.x + tabRect.x + (tabRect.width / 2) - (InitialSize.x / 2) + 20f;
        
        // Calculate Y position to place the context menu just below the tab, then shift down by 45 pixels
        float yPos = parentWindowRect.y + tabRect.yMax + 52f;

        // Ensure the context menu appears directly below the tab and centered horizontally
        windowRect = new Rect(xPos, yPos, InitialSize.x, InitialSize.y);
    }


    public override void DoWindowContents(Rect inRect)
    {
        if (Widgets.ButtonText(new Rect(0f, 0f, inRect.width, 30f), "Rename Tab"))
        {
            onRename(tabIndex);  // Trigger the rename action
            Close();
        }

        if (Widgets.ButtonText(new Rect(0f, 35f, inRect.width, 30f), "Close Tab"))
        {
            HandleCloseTab(tabIndex);  // Handle closing with confirmation if necessary
            Close();
        }

        if (Widgets.ButtonText(new Rect(0f, 70f, inRect.width, 30f), "Save Tab"))
        {
            onSaveTab(tabIndex);  // Trigger the save tab action
            Close();
        }
    }

    // Method to handle tab closing with confirmation
    private void HandleCloseTab(int tabIndex)
    {
        // Get the content of the tab and check if it's not empty
        string trimmedContent = getTabContent(tabIndex)?.Trim();
        
        if (!string.IsNullOrEmpty(trimmedContent))
        {
            // If the tab contains code, trigger the confirmation dialog
            Dialog_MessageBox confirmationDialog = Dialog_MessageBox.CreateConfirmation(
                "This tab contains code. Are you sure you want to close it?",
                () => onCloseTab(tabIndex),  // Close the tab if confirmed
                true, // Centered alignment
                "Confirm Close"
            );

            // Show the confirmation dialog
            Find.WindowStack.Add(confirmationDialog);
        }
        else
        {
            // Close the tab directly if there's no content
            onCloseTab(tabIndex);
        }
    }
}


// Custom warning dialog subclass for the character limit
public class Dialog_CharacterLimitWarning : Window
{
    private int tabIndex;
    private Action<int> onConfirm;

    public Dialog_CharacterLimitWarning(int tabIndex, Action<int> onConfirm)
    {
        this.tabIndex = tabIndex;
        this.onConfirm = onConfirm;

        this.doCloseX = false; // Disable the X button
        this.closeOnClickedOutside = true; // Close when clicking outside
        this.absorbInputAroundWindow = true; // Absorb input around the window
        this.forcePause = true; // Pause input
        this.draggable = true;
    }

    public override Vector2 InitialSize => new Vector2(400f, 200f);

    public override void DoWindowContents(Rect inRect)
    {
        // Customize text: make it bold, larger, and centered
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;

        // Create label rect for the message
        Rect labelRect = new Rect(0f, 0f, inRect.width, 100f);
        Widgets.Label(labelRect, "The code in this tab has reached the 15,000 character limit. Please split your code into multiple tabs to avoid issues.");

        // Reset text settings to default
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        // Draw the confirmation button
        if (Widgets.ButtonText(new Rect(inRect.width / 2 - 60f, inRect.height - 50f, 120f, 35f), "Confirm"))
        {
            onConfirm?.Invoke(tabIndex);  // Trigger the confirmation action
            Close();  // Close the dialog
        }
    }

    protected override void SetInitialSizeAndPosition()
    {
        base.SetInitialSizeAndPosition();

        // Center the dialog window on the screen
        windowRect.x = UI.screenWidth / 2f - InitialSize.x / 2f;
        windowRect.y = UI.screenHeight / 2f - InitialSize.y / 2f;
    }
}



public class Dialog_ExecuteSpecificTabs : Window
{
    private CustomTurtleBot pawn;
    private List<Tab> codeTabs;
    private List<bool> tabSelections;  // Boolean list to track selected tabs
    private Vector2 scrollPosition = Vector2.zero; // Store scroll position

    // Set a max height for the window
    private const float MaxWindowHeight = 600f;
    private const float MinWindowHeight = 200f;

    public Dialog_ExecuteSpecificTabs(CustomTurtleBot pawn, List<Tab> codeTabs)
    {
        this.pawn = pawn;
        this.codeTabs = codeTabs;

        // Initialize the tabSelections list based on the number of tabs and pawn's saved selections
        tabSelections = new List<bool>();
        for (int i = 0; i < codeTabs.Count; i++)
        {
            // Load the saved selection for each tab from the pawn, or default to true
            tabSelections.Add(pawn.GetTabExecutionState(i));
        }

        this.doCloseX = true;
        this.closeOnClickedOutside = true;
        this.absorbInputAroundWindow = true;
        this.forcePause = true;
    }

    public override Vector2 InitialSize
    {
        get
        {
            // Dynamically calculate the window height based on the number of tabs
            float height = codeTabs.Count * 35f + 100f; // 35px per row + space for buttons
            height = Mathf.Clamp(height, MinWindowHeight, MaxWindowHeight); // Clamp between min and max heights
            return new Vector2(400f, height);
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        float y = 0f;
        Widgets.Label(new Rect(0f, y, inRect.width, 30f), "Select Tabs to Execute:");
        y += 35f;

        // Adjust y for scroll view, we subtract the label height (35f)
        float scrollViewHeight = inRect.height - 90f;

        // If there are too many tabs to fit in the window, allow scrolling
        float contentHeight = codeTabs.Count * 35f;
        Rect scrollViewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

        // Begin scrolling view, setting the position to after the label
        Widgets.BeginScrollView(new Rect(0f, y, inRect.width, scrollViewHeight), ref scrollPosition, scrollViewRect);
        
        // Display each tab name with a checkbox
        for (int i = 0; i < codeTabs.Count; i++)
        {
            Tab tab = codeTabs[i];
            Rect rowRect = new Rect(0f, i * 35f, scrollViewRect.width - 20f, 30f);
            
            // Use a local variable to store the checkbox state
            bool selected = tabSelections[i];
            Widgets.CheckboxLabeled(rowRect, tab.Name, ref selected);
            
            // Update the list with the modified value
            tabSelections[i] = selected;
        }

        Widgets.EndScrollView();

        // Execute button
        if (Widgets.ButtonText(new Rect(0f, inRect.height - 50f, inRect.width / 2f - 10f, 40f), "Execute"))
        {
            ExecuteSelectedTabs();
        }

        // Cancel button
        if (Widgets.ButtonText(new Rect(inRect.width / 2f + 10f, inRect.height - 50f, inRect.width / 2f - 10f, 40f), "Cancel"))
        {
            Close();
        }
    }


    private void ExecuteSelectedTabs()
    {
        // Save the tab selections back to the pawn
        for (int i = 0; i < tabSelections.Count; i++)
        {
            pawn.SetTabExecutionState(i, tabSelections[i]);
        }

        // Concatenate and execute code from selected tabs
        string concatenatedCode = string.Join("\n", codeTabs
            .Where((tab, index) => tabSelections[index])  // Only selected tabs
            .Select(tab => tab.Content.Replace("‹", "<").Replace("›", ">")));

        if (!string.IsNullOrEmpty(concatenatedCode))
        {
            pawn.ExecuteLuaCode(concatenatedCode);
        }

        Close();
    }
}



public class Tab
{
    public string Name { get; set; }
    public string Content { get; set; }

    public Tab(string name, string content)
    {
        Name = name;
        Content = content;
    }
}


public class RichTextInput
{
    private List<Tab> codeTabs = new List<Tab>(); // List to store tabs (name and content)
    private int activeTabIndex = 0; // Index of the currently active tab

    private Vector2 scrollPosition = Vector2.zero;
    private float maxTextWidth = 0f; // Track the maximum width of the text for horizontal scrolling
    private GUIStyle textFieldStyle;
    private GUIStyle lineNumberStyle;
    private GUIStyle overlayStyle;
    private GUIStyle tabButtonStyle;
    private GUIStyle tabButtonActiveStyle;
    private GUIStyle plusButtonStyle;
    private GUIStyle closeButtonStyle;
    private GUIStyle contextMenuStyle;
  //  private GUIStyle closeButtonHoverStyle;
    private float lineNumberOffset = 0.15f; // Offset for aligning line numbers
    private const float fixedLineHeight = 15f; // Fixed line height for line numbers
    private const float cornerRadius = 10f; // Radius for rounded corners
    private const float horizontalScrollExtraSpace = 50f; // Extra space for horizontal scrolling
    private const int emptyLinesEnd = 50; 
    private readonly Color outerBackgroundColor = new Color(24f / 255f, 32f / 255f, 43f / 255f); // Outer container color
    private readonly Color innerBackgroundColor = new Color(18f / 255f, 24f / 255f, 32f / 255f); // Inner container color
    private readonly Color errorHighlightColor = new Color(1f, 0.5f, 0f); // Orange color for error highlighting

    private readonly Color selectionColor = new Color(0.5f, 0.7f, 1f, 0.5f); // Light blue selection color

    private readonly Color tabActiveColor = new Color(30f / 255f, 144f / 255f, 255f / 255f); // Active tab color
    private readonly Color tabInactiveColor = new Color(60f / 255f, 63f / 255f, 65f / 255f); // Inactive tab color
    private readonly Color closeButtonColor = new Color(0.8f, 0.3f, 0.3f); // Red color for the close button
    private readonly Color closeButtonHoverColor = new Color(1f, 0.5f, 0.5f); // Lighter red for hover


    private Color outlineColor = new Color(24f / 255f, 32f / 255f, 43f / 255f);


    private CustomTurtleBot pawn; // Reference to the pawn instance

    private Stack<string> undoStack = new Stack<string>();
    private Stack<string> redoStack = new Stack<string>();

    private Dictionary<int, string> cachedHighlightedLines = new Dictionary<int, string>(); // Cache for highlighted lines
    private string lastInputText = ""; // To track changes in the input text

    private SyntaxHighlighter syntaxHighlighter;


    // Cached textures for rounded rectangle backgrounds
    private Dictionary<Color, Texture2D> cachedRoundedRectangleTextures = new Dictionary<Color, Texture2D>();


    private bool showConfirmationDialog = false;
    private int tabToClose = -1;

    private float tabScrollOffset = 0f; // Scroll offset for the tabs
    private float totalTabWidth = 0f; // Total width of all tabs combined

    private const int CharacterLimit = 15000;
    private Dictionary<int, bool> tabWarningShown = new Dictionary<int, bool>(); // Track warnings shown per tab



    // private bool isDraggingTab = false;
    // private int draggingTabIndex = -1;
    // private float dragOffsetX;
    // private float currentX; // This should be declared at the class level




 //   private bool showContextMenu = false;
    //private Vector2 contextMenuPosition;
   // private int contextMenuTabIndex = -1;   

    // Cached textures to avoid repeated creation
    private Texture2D backgroundTexture;
    private Texture2D selectionTexture;
    private Texture2D errorHighlightTexture;
    private Texture2D closeButtonTexture;
    private Texture2D closeButtonHoverTexture;
    private Texture2D separatorTexture;

    private Texture2D plusButtonNormalTexture;
    private Texture2D plusButtonHoverTexture;
    private Texture2D plusButtonActiveTexture;

    private Texture2D luaIconTexture; // Cache the Lua icon texture here

    private Dialog_LuaConsole parentDialog; // Reference to the parent dialog window

    public string Text
    {
        get => codeTabs[activeTabIndex].Content;
        set
        {
            codeTabs[activeTabIndex].Content = value.Replace("<", "‹").Replace(">", "›");
        }
    }

        // Constructor accepting a reference to the parent dialog
        public RichTextInput(CustomTurtleBot pawn, Dialog_LuaConsole parentDialog)
        {
        this.pawn = pawn;
        this.parentDialog = parentDialog;

        syntaxHighlighter = new SyntaxHighlighter(pawn.registeredMethods);

        // Load saved tabs from the pawn instance
        codeTabs = pawn.LoadLuaCodeTabs();
        activeTabIndex = codeTabs.Count > 0 ? 0 : -1; // Set the first tab as active, if available

        // Initialize styles
        InitializeStyles();

        // Load and cache the Lua icon texture
        luaIconTexture = ContentFinder<Texture2D>.Get("LuaIcon");


        // Create cached textures
        backgroundTexture = MakeTex(2, 2, innerBackgroundColor);
        selectionTexture = MakeTex(2, 2, selectionColor);
        errorHighlightTexture = MakeTex(1, 1, errorHighlightColor);
        closeButtonTexture = MakeTex(1, 1, closeButtonColor);
        closeButtonHoverTexture = MakeTex(1, 1, closeButtonHoverColor);

        // Initialize the separator texture
        separatorTexture = MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 0.8f)); // Gray color for the separator

        // Cache textures for the "+" button
        plusButtonNormalTexture = MakeTex(2, 2, new Color(60f / 255f, 63f / 255f, 65f / 255f));
        plusButtonHoverTexture = MakeTex(2, 2, new Color(80f / 255f, 83f / 255f, 85f / 255f));
        plusButtonActiveTexture = MakeTex(2, 2, new Color(100f / 255f, 103f / 255f, 105f / 255f));
    }

    


private void InitializeStyles()
{
    // Cache textures at initialization to avoid repetitive creation
    Texture2D tabInactiveTexture = MakeTex(1, 1, tabInactiveColor);
    Texture2D tabActiveTexture = MakeTex(1, 1, tabActiveColor);
    Texture2D tabHoverTexture = MakeTex(1, 1, new Color(0.3f, 0.5f, 0.7f));

    textFieldStyle = new GUIStyle(GUI.skin.textArea)
    {
        normal = { textColor = Color.clear, background = backgroundTexture },
        hover = { textColor = Color.clear, background = backgroundTexture },
        active = { textColor = Color.clear, background = backgroundTexture },
        focused = { textColor = Color.clear, background = backgroundTexture },
        richText = false,
        wordWrap = false,
        alignment = TextAnchor.UpperLeft,
        padding = new RectOffset(5, 5, 5, 5),
    };

    lineNumberStyle = new GUIStyle(GUI.skin.label)
    {
        normal = { textColor = Color.gray },
        alignment = TextAnchor.UpperRight,
        padding = new RectOffset(0, 5, 5, 5),
        clipping = TextClipping.Overflow,
    };

    overlayStyle = new GUIStyle(GUI.skin.label)
    {
        richText = true,
        wordWrap = false,
        alignment = TextAnchor.UpperLeft,
        padding = new RectOffset(5, 5, 5, 5),
    };

    tabButtonStyle = new GUIStyle(GUI.skin.button)
    {
        normal = { textColor = Color.white, background = tabInactiveTexture },
        hover = { textColor = Color.white, background = tabHoverTexture },
        active = { textColor = Color.white, background = tabHoverTexture },
        alignment = TextAnchor.MiddleLeft,
        fixedHeight = 25,
        border = new RectOffset(1, 1, 1, 1),
        padding = new RectOffset(25, 10, 2, 2),  // Moved text left by 25 pixels and adjusted vertical padding
        margin = new RectOffset(0, 0, 0, 0)
    };

    tabButtonActiveStyle = new GUIStyle(tabButtonStyle)
    {
        normal = { textColor = Color.white, background = tabActiveTexture },
    };

    plusButtonStyle = new GUIStyle(GUI.skin.button)
    {
        fontSize = 20,
        alignment = TextAnchor.MiddleCenter,
        normal = { textColor = Color.white, background = plusButtonNormalTexture },
        hover = { textColor = Color.white, background = plusButtonHoverTexture },
        active = { textColor = Color.white, background = plusButtonActiveTexture },
        border = new RectOffset(1, 1, 1, 1),
        padding = new RectOffset(2, 2, 2, 2),
        margin = new RectOffset(0, 0, 0, 0),
        fixedWidth = 25,
        fixedHeight = 25,
    };

    closeButtonStyle = new GUIStyle(GUI.skin.button)
    {
        normal = { textColor = Color.white, background = closeButtonTexture },
        hover = { textColor = Color.white, background = closeButtonHoverTexture },
        alignment = TextAnchor.MiddleCenter,
        fixedWidth = 15,
        fixedHeight = 15,
        border = new RectOffset(1, 1, 1, 1),
        padding = new RectOffset(0, 0, 0, 0),
        margin = new RectOffset(0, 0, 0, 0)
    };
    // Right-click tab context menu style
    contextMenuStyle = new GUIStyle(GUI.skin.box)
    {
        padding = new RectOffset(10, 10, 10, 10),
        normal = { background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f)) }
    };

}



    public List<Tab> GetCodeTabs()
    {
        return codeTabs;
    }




    private void EnsureEmptyLines(int numberOfEmptyLines)
    {
        var lines = codeTabs[activeTabIndex].Content.Split('\n');
        int existingEmptyLines = 0;

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                existingEmptyLines++;
            else
                break;
        }

        int linesToAdd = numberOfEmptyLines - existingEmptyLines;
        if (linesToAdd > 0)
        {
            codeTabs[activeTabIndex].Content += new string('\n', linesToAdd);
        }
        else if (linesToAdd < 0)
        {
            codeTabs[activeTabIndex].Content = string.Join("\n", lines.Take(lines.Length + linesToAdd));
        }
    }



    private void HighlightLine(int lineNumber, float lineHeight, Rect rect, float padding)
    {
        float yPos = (lineNumber * lineHeight) - scrollPosition.y;

        if (yPos >= 0 && yPos < rect.height)
        {
            float highlightX = padding;
            float highlightY = rect.y + yPos;
            float highlightWidth = Mathf.Min(rect.width - padding * 2, maxTextWidth);

            Rect highlightRect = new Rect(highlightX, highlightY, highlightWidth, lineHeight);

            GUI.DrawTexture(highlightRect, errorHighlightTexture);
        }
    }



    public void Draw(Rect rect)
    {
        float tabHeight = 25f; // Height of the tab buttons
        float tabYOffset = -tabHeight; // Offset to move the tabs down

        // Check character count and show warning if limit is exceeded for the active tab
        CheckCharacterLimitForTab(activeTabIndex);

        // Draw the code area without moving it down
        DrawCodeArea(rect);

        // Manually handle the tabs and the "+" button layout
        DrawTabs(new Rect(rect.x, rect.y + tabYOffset, rect.width, tabHeight));

        // Draw confirmation dialog if triggered
        if (showConfirmationDialog)
        {
            DrawConfirmationDialog();
        }
    }


    // Method to check if the active tab's character count exceeds the limit
    private void CheckCharacterLimitForTab(int tabIndex)
    {
        string activeTabContent = codeTabs[tabIndex].Content;

        // Initialize tracking for this tab if not present
        if (!tabWarningShown.ContainsKey(tabIndex))
        {
            tabWarningShown[tabIndex] = false;
        }

        // Show warning if the tab exceeds character limit and the warning has not been shown
        if (activeTabContent.Length > CharacterLimit && !tabWarningShown[tabIndex])
        {
            ShowCharacterLimitWarning(tabIndex);
            tabWarningShown[tabIndex] = true;
        }
    }

// Show a warning dialog about the character limit
private void ShowCharacterLimitWarning(int tabIndex)
{
    // Create and show the custom warning dialog
    Find.WindowStack.Add(new Dialog_CharacterLimitWarning(tabIndex, (index) =>
    {
        // Set warning shown for this tab
        tabWarningShown[index] = true;
    }));
}

private void OpenContextMenu(int tabIndex, Rect tabRect)
{
    // Get the parent window's position
    Rect parentWindowRect = parentDialog.GetWindowRect();

    // Create the context menu and pass the necessary information, including getTabContent
    Dialog_ContextMenu contextMenu = new Dialog_ContextMenu(
        tabIndex,
        OpenRenameDialog,  // Pass the rename function
        CloseTab,          // Pass the close tab function
        SaveTab,           // Pass the save tab function
        tabRect,           // Pass the tab's rectangle for positioning
        parentWindowRect,  // Pass the parent window's position for correct placement
        (index) => codeTabs[index].Content  // Function to get the tab's content by index
    );

    Find.WindowStack.Add(contextMenu);
}





private void DrawTabs(Rect rect)
{
    totalTabWidth = 0f;

    // Calculate total width of all tabs plus the "+" button
    for (int i = 0; i < codeTabs.Count; i++)
    {
        var currentStyle = (i == activeTabIndex) ? tabButtonActiveStyle : tabButtonStyle;
        totalTabWidth += currentStyle.CalcSize(new GUIContent(codeTabs[i].Name)).x + 40;
    }

    // Calculate the position of the "+" button
    totalTabWidth += plusButtonStyle.fixedWidth + 5;

    bool showLeftArrow = tabScrollOffset > 0;
    bool showRightArrow = totalTabWidth > rect.width - 2 * rect.height;

    // Automatically reset scroll offset if all tabs fit within the available space
    if (!showRightArrow && tabScrollOffset > 0)
    {
        tabScrollOffset = 0;
    }

    var arrowButtonStyle = new GUIStyle(plusButtonStyle)
    {
        alignment = TextAnchor.MiddleCenter,
        padding = new RectOffset(2, 2, 2, 2)
    };

    if (showLeftArrow)
    {
        Rect leftArrowRect = new Rect(rect.x, rect.y, rect.height, rect.height);
        bool isLeftHovered = leftArrowRect.Contains(Event.current.mousePosition);
        arrowButtonStyle.normal.textColor = isLeftHovered ? Color.green : Color.white;

        if (GUI.Button(leftArrowRect, "<", arrowButtonStyle))
        {
            tabScrollOffset = Mathf.Max(0, tabScrollOffset - 100);
        }

        DrawOutline(leftArrowRect, outlineColor, true, true);
    }

    if (showRightArrow)
    {
        Rect rightArrowRect = new Rect(rect.x + rect.width - rect.height, rect.y, rect.height, rect.height);
        bool isRightHovered = rightArrowRect.Contains(Event.current.mousePosition);
        arrowButtonStyle.normal.textColor = isRightHovered ? Color.green : Color.white;

        if (GUI.Button(rightArrowRect, ">", arrowButtonStyle))
        {
            tabScrollOffset = Mathf.Min(totalTabWidth - (rect.width - 2 * rect.height), tabScrollOffset + 100);
        }

        DrawOutline(rightArrowRect, outlineColor, true, true);
    }

    Rect tabsAreaRect = new Rect(rect.x + (showLeftArrow ? rect.height : 0), rect.y, rect.width - (showLeftArrow ? rect.height : 0) - (showRightArrow ? rect.height : 0), rect.height);

    GUI.BeginClip(tabsAreaRect);
    float currentX = -tabScrollOffset;

    for (int i = 0; i < codeTabs.Count; i++)
    {
        bool isActive = (i == activeTabIndex);
        var currentStyle = isActive ? tabButtonActiveStyle : tabButtonStyle;

        float tabWidth = currentStyle.CalcSize(new GUIContent(codeTabs[i].Name)).x + 40;
        Rect tabRect = new Rect(currentX, 0, tabWidth, rect.height);

        // Handle close button click
        Rect closeRect = new Rect(tabRect.xMax - 20, 5, 15, 15);
        if (HandleCloseButtonClick(closeRect, i))
        {
            Event.current.Use();
            GUI.EndClip();
            return;
        }

        // Process the tab button click
        if (GUI.Button(tabRect, codeTabs[i].Name, currentStyle))
        {
            if (Event.current.button == 1) // Right-click
            {
                OpenContextMenu(i, tabRect);  // Pass the tabRect to OpenContextMenu
                Event.current.Use();
            }
            else if (Event.current.button == 0) // Left-click
            {
                activeTabIndex = i;
            }
        }

        // Draw the Lua icon using the cached texture
        Rect luaIconRect = new Rect(tabRect.x + 5, (tabRect.height - 15) / 2, 15, 15);
        GUI.DrawTexture(luaIconRect, luaIconTexture);

        DrawCloseButton(closeRect);
        DrawOutline(tabRect, outlineColor, i == 0, true);

        currentX += tabWidth;
    }

    DrawPlusButton(new Rect(currentX, 0, plusButtonStyle.fixedWidth, rect.height), outlineColor);

    GUI.EndClip();
}








    private void SaveTab(int tabIndex)
    {
        // Logic to save the tab content (Placeholder for now)
    }

    




    private void OpenRenameDialog(int tabIndex)
    {
        Find.WindowStack.Add(new Dialog_RenameTab(codeTabs[tabIndex]));
    }



    private void DrawPlusButton(Rect rect, Color outlineColor)
    {
        bool isHovered = rect.Contains(Event.current.mousePosition);
        plusButtonStyle.normal.textColor = isHovered ? Color.green : Color.white;

        if (GUI.Button(rect, "+", plusButtonStyle))
        {
            int tabNumber = codeTabs.Count + 1;
            string newTabName;
            do
            {
                newTabName = $"Code_{tabNumber}.lua";
                tabNumber++;
            } while (codeTabs.Any(tab => tab.Name == newTabName));

            codeTabs.Add(new Tab(newTabName, string.Empty));
            activeTabIndex = codeTabs.Count - 1;
        }

        plusButtonStyle.normal.textColor = Color.white;
        DrawOutline(rect, outlineColor, true, true);
        Verse.Text.Font = GameFont.Small;
    }








    private void DrawOutline(Rect rect, Color color, bool drawLeft, bool drawRight = true)
    {
        // Save the original color
        Color originalColor = GUI.color;

        // Set the GUI color to the outline color
        GUI.color = color;

        // Draw top line
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Texture2D.whiteTexture);

        // Draw bottom line
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);

        // Draw left line only if drawLeft is true
        if (drawLeft)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
        }

        // Draw right line only if drawRight is true
        if (drawRight)
        {
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Texture2D.whiteTexture);
        }

        // Restore the original color
        GUI.color = originalColor;
    }




    private void DrawSeparator(Rect rect)
    {
        // Ensure the separator is exactly 1 pixel wide
        rect.width = 1f;

        GUIStyle separatorStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = separatorTexture },
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            fixedWidth = 1f, // Set fixed width to 1 pixel
        };
        GUI.Box(rect, GUIContent.none, separatorStyle);
    }



    private void DrawCloseButton(Rect closeRect)
    {
        Color textColor = closeRect.Contains(Event.current.mousePosition) ? Color.red : Color.white;
        GUIStyle closeButtonStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = textColor }
        };

        GUI.Label(closeRect, "✕", closeButtonStyle);
    }

    private bool HandleCloseButtonClick(Rect closeRect, int tabIndex)
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && closeRect.Contains(Event.current.mousePosition))
        {
            // Trim the content of the tab to check if it only contains whitespace
            string trimmedContent = codeTabs[tabIndex].Content.Trim();

            if (!string.IsNullOrEmpty(trimmedContent))
            {
                // If the tab contains actual content, trigger the confirmation dialog
                showConfirmationDialog = true;
                tabToClose = tabIndex;
            }
            else
            {
                // If the tab is empty or contains only whitespace, close it directly
                CloseTab(tabIndex);
            }
            return true; // Event was handled
        }
        return false; // Event was not handled, allow further processing
    }



    // private void HandleTabDragging(Rect rect)
    // {
    //     currentX = 0f; // Reset currentX at the start of the method

    //     if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
    //     {
    //         for (int i = 0; i < codeTabs.Count; i++)
    //         {
    //             var currentStyle = (i == activeTabIndex) ? tabButtonActiveStyle : tabButtonStyle;
    //             float tabWidth = currentStyle.CalcSize(new GUIContent(codeTabs[i])).x + 40;
    //             Rect tabRect = new Rect(currentX, rect.y, tabWidth, rect.height);

    //             if (tabRect.Contains(Event.current.mousePosition))
    //             {
    //                 isDraggingTab = true;
    //                 draggingTabIndex = i;
    //                 dragOffsetX = Event.current.mousePosition.x - tabRect.x;
    //                 Event.current.Use();
    //                 break;
    //             }
    //             currentX += tabWidth;
    //         }
    //     }

    //     if (isDraggingTab)
    //     {
    //         if (Event.current.type == EventType.MouseDrag)
    //         {
    //             float newX = Event.current.mousePosition.x - dragOffsetX;
    //             int newIndex = Mathf.Clamp(Mathf.FloorToInt(newX / totalTabWidth * codeTabs.Count), 0, codeTabs.Count - 1);

    //             if (newIndex != draggingTabIndex)
    //             {
    //                 string temp = codeTabs[draggingTabIndex];
    //                 codeTabs.RemoveAt(draggingTabIndex);
    //                 codeTabs.Insert(newIndex, temp);
    //                 draggingTabIndex = newIndex;
    //             }
    //             Event.current.Use();
    //         }

    //         if (Event.current.type == EventType.MouseUp)
    //         {
    //             isDraggingTab = false;
    //             draggingTabIndex = -1;
    //             Event.current.Use();
    //         }
    //     }
    // }


    private void DrawCodeArea(Rect rect)
    {
        const float padding = 40f;

        EnsureEmptyLines(emptyLinesEnd);

        Color selectionColor = ColorUtility.TryParseHtmlString("#" + TurtleMod.settings.SelectionColor, out var parsedColor)
            ? parsedColor
            : new Color(0.5f, 0.75f, 1f, 1f);

        if (GUI.skin.settings.selectionColor != selectionColor)
        {
            GUI.skin.settings.selectionColor = selectionColor;
        }

        GUI.BeginGroup(rect);
        DrawRoundedRectangle(new Rect(0, 0, rect.width, rect.height), outerBackgroundColor, cornerRadius);
        GUI.EndGroup();

        float lineHeight = fixedLineHeight;
        string[] lines = codeTabs[activeTabIndex].Content.Split('\n');
        float contentHeight = lineHeight * lines.Length;
        float requiredContentWidth = textFieldStyle.CalcSize(new GUIContent(codeTabs[activeTabIndex].Content)).x + horizontalScrollExtraSpace;

        bool showHorizontalScrollbar = requiredContentWidth > rect.width - padding + 20;
        float contentWidth = showHorizontalScrollbar ? requiredContentWidth : rect.width - padding - 20;

        Rect viewRect = new Rect(0, 0, contentWidth, contentHeight);

        Vector2 previousScrollPosition = scrollPosition;

        scrollPosition = GUI.BeginScrollView(new Rect(rect.x + padding, rect.y, rect.width - padding, rect.height), scrollPosition, viewRect, showHorizontalScrollbar, true);

        GUI.SetNextControlName("RichTextInputField");
        string newInputText = GUI.TextArea(new Rect(0, 0, contentWidth, contentHeight), codeTabs[activeTabIndex].Content, textFieldStyle);

        TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
        if (editor != null && editor.scrollOffset.y > 0)
        {
            editor.scrollOffset.y = 0;
        }

        if (newInputText != codeTabs[activeTabIndex].Content || codeTabs[activeTabIndex].Content != lastInputText || Event.current.type == EventType.Repaint)
        {
            lastInputText = newInputText.Replace("<", "‹").Replace(">", "›");
            cachedHighlightedLines.Clear();

            codeTabs[activeTabIndex].Content = lastInputText;
            requiredContentWidth = textFieldStyle.CalcSize(new GUIContent(codeTabs[activeTabIndex].Content)).x + horizontalScrollExtraSpace;
        }

        int firstVisibleLine = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / lineHeight) - 10);
        int lastVisibleLine = Mathf.Min(lines.Length, firstVisibleLine + Mathf.CeilToInt(rect.height / lineHeight) + 10);

        StringBuilder visibleText = new StringBuilder();
        for (int i = 0; i < firstVisibleLine; i++) visibleText.AppendLine();
        for (int i = firstVisibleLine; i < lastVisibleLine; i++)
        {
            if (!cachedHighlightedLines.TryGetValue(i, out string highlightedLine))
            {
                highlightedLine = syntaxHighlighter.ApplySyntaxHighlighting(lines[i]);
                cachedHighlightedLines[i] = highlightedLine;
            }
            visibleText.AppendLine(highlightedLine);
        }
        for (int i = lastVisibleLine; i < lines.Length; i++) visibleText.AppendLine();

        Rect overlayRect = new Rect(0, 0, contentWidth, contentHeight);
        GUI.Label(overlayRect, visibleText.ToString(), overlayStyle);

        GUI.EndScrollView();

        scrollPosition.x = Mathf.Clamp(scrollPosition.x, 0, Mathf.Max(0, requiredContentWidth - rect.width + padding + 2));
        scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0, Mathf.Max(0, contentHeight - rect.height + padding));

        DrawLineNumbers(rect, lines, lineHeight, contentHeight);

        HandleClipboardPostProcess();
        HandleUndoRedo();
    }



    private void DrawConfirmationDialog()
    {
        // Create the confirmation dialog using RimWorld's Dialog_MessageBox
        Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
            "This tab contains code. Are you sure you want to close it?", 
            () =>
            {
                CloseTab(tabToClose);
                showConfirmationDialog = false;
                tabToClose = -1;
            },
            true, // Use centered alignment
            "Confirm Close"
        );

        Find.WindowStack.Add(dialog);

        // Reset the dialog flag
        showConfirmationDialog = false;
    }


    private void CloseTab(int index)
    {
        // Remove the tab at the specified index
        codeTabs.RemoveAt(index);

        // Adjust the activeTabIndex if necessary
        if (activeTabIndex >= codeTabs.Count)
        {
            activeTabIndex = codeTabs.Count - 1;
        }

        // Ensure there is always at least one tab
        if (codeTabs.Count == 0)
        {
            codeTabs.Add(new Tab("Code_1.lua", string.Empty));
            activeTabIndex = 0;
        }
    }


    private Vector2 GetCursorPosition(Rect rect, string text, GUIStyle style, int controlID)
    {
        TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), controlID);
        int cursorIndex = editor.cursorIndex;
        Vector2 cursorPixelPos = style.GetCursorPixelPosition(rect, new GUIContent(text), cursorIndex);
        return cursorPixelPos;
    }

    private void HandleClipboardPostProcess()
    {
        if (Event.current.type == EventType.KeyUp && Event.current.control &&
            (Event.current.keyCode == KeyCode.C || Event.current.keyCode == KeyCode.X))
        {
            string selectedText = GUIUtility.systemCopyBuffer;
            string processedText = selectedText.Replace("‹", "<").Replace("›", ">");
            processedText = RemoveTrailingEmptyLines(processedText);
            GUIUtility.systemCopyBuffer = processedText;
            Event.current.Use();
        }
    }

    private void RecordUndoState(string text)
    {
        undoStack.Push(text);
        redoStack.Clear(); // Clear the redo stack whenever a new action is performed
    }

    private void HandleUndoRedo()
    {
        if (Event.current.type == EventType.KeyUp && Event.current.control)
        {
            if (Event.current.keyCode == KeyCode.Z && undoStack.Count > 0)
            {
                // Undo operation
                redoStack.Push(codeTabs[activeTabIndex].Content);
                codeTabs[activeTabIndex].Content = undoStack.Pop();
                Event.current.Use(); // Prevent the event from propagating further
            }
            else if (Event.current.keyCode == KeyCode.Y && redoStack.Count > 0)
            {
                // Redo operation
                undoStack.Push(codeTabs[activeTabIndex].Content);
                codeTabs[activeTabIndex].Content = redoStack.Pop();
                Event.current.Use(); // Prevent the event from propagating further
            }
        }
    }


    private string RemoveTrailingEmptyLines(string text)
    {
        string[] lines = text.Split('\n');
        int lastNonEmptyLineIndex = lines.Length - 1;
        while (lastNonEmptyLineIndex >= 0 && string.IsNullOrWhiteSpace(lines[lastNonEmptyLineIndex]))
        {
            lastNonEmptyLineIndex--;
        }
        return string.Join("\n", lines.Take(lastNonEmptyLineIndex + 1));
    }

    private (int firstVisibleLine, int lastVisibleLine) CalculateVisibleLineNumbers(Rect rect, string[] lines, float lineHeight)
    {
        int firstVisibleLine = -1;
        int lastVisibleLine = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            float yPos = rect.y + (i * lineHeight) - scrollPosition.y + lineNumberOffset;

            if (yPos >= rect.y - lineHeight / 2 && yPos < rect.y + rect.height - lineHeight)
            {
                if (firstVisibleLine == -1) firstVisibleLine = i;
                lastVisibleLine = i;
            }
        }

        if (firstVisibleLine == -1) firstVisibleLine = 0;
        if (lastVisibleLine == -1) lastVisibleLine = lines.Length - 1;

        return (firstVisibleLine, lastVisibleLine);
    }

    private void DrawLineNumbers(Rect rect, string[] lines, float lineHeight, float contentHeight)
    {
        var (firstVisibleLine, lastVisibleLine) = CalculateVisibleLineNumbers(rect, lines, lineHeight);

        for (int i = firstVisibleLine; i <= lastVisibleLine; i++)
        {
            float yPos = rect.y + (i * lineHeight) - scrollPosition.y + lineNumberOffset;
            Rect lineNumberRect = new Rect(rect.x + 5, yPos, 35f, lineHeight);
            GUI.Label(lineNumberRect, (i + 1).ToString(), lineNumberStyle);
        }
    }

    private void DrawRoundedRectangle(Rect rect, Color color, float radius)
    {
        // Use a cached texture to avoid creating new textures repeatedly
        if (!cachedRoundedRectangleTextures.TryGetValue(color, out Texture2D roundedRectTexture))
        {
            roundedRectTexture = MakeTex(1, 1, color);
            cachedRoundedRectangleTextures[color] = roundedRectTexture;
        }

        GUI.DrawTexture(rect, roundedRectTexture);
    }
    
    public void SaveTabs()
    {
        // Directly save the current state of all tabs (which are already of type List<Tab>)
        pawn.SaveLuaCodeTabs(codeTabs);
    }


    public List<string> GetAllTabsContent()
    {
        // Return a list of all tabs' content
        return codeTabs.Select(tab => tab.Content).ToList();
    }


    public string GetActiveTabContent()
    {
        return codeTabs[activeTabIndex].Content; // Return the content of the active tab
    }



    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col;
        }

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();

        return result;
    }

    ~RichTextInput()
    {

        // Clean up cached textures to prevent memory leaks
        if (plusButtonNormalTexture != null)
            UnityEngine.Object.Destroy(plusButtonNormalTexture);
        if (plusButtonHoverTexture != null)
            UnityEngine.Object.Destroy(plusButtonHoverTexture);
        if (plusButtonActiveTexture != null)
            UnityEngine.Object.Destroy(plusButtonActiveTexture);

        if (backgroundTexture != null)
            UnityEngine.Object.Destroy(backgroundTexture);

        if (selectionTexture != null)
            UnityEngine.Object.Destroy(selectionTexture);

        if (errorHighlightTexture != null)
            UnityEngine.Object.Destroy(errorHighlightTexture);

        if (closeButtonTexture != null)
            UnityEngine.Object.Destroy(closeButtonTexture);

        if (closeButtonHoverTexture != null)
            UnityEngine.Object.Destroy(closeButtonHoverTexture);

        if (separatorTexture != null)
            UnityEngine.Object.Destroy(separatorTexture);
    }


}




public class Dialog_LuaConsole : Window
{
    private CustomTurtleBot pawn;
    private RichTextInput richTextInput;

    private bool warnOnSubclassMismatch;
    private bool enableCSharpTypeChecking;
    private bool autoTypeConvert;
    private bool logTypeConversions;
    private bool pauseGameWhenOpen;
    private bool closeOnExecute;

    public Dialog_LuaConsole(CustomTurtleBot pawn)
    {
        this.pawn = pawn;

        // Load settings from the pawn instance
        warnOnSubclassMismatch = pawn.WarnOnSubclassMismatch;
        enableCSharpTypeChecking = pawn.EnableCSharpTypeChecking;
        autoTypeConvert = pawn.AutoTypeConvert;
        logTypeConversions = pawn.LogTypeConversions;
        pauseGameWhenOpen = pawn.PauseGameWhenOpen;
        closeOnExecute = pawn.CloseOnExecute;

        // Set forcePause based on the loaded setting
        this.forcePause = pauseGameWhenOpen;

        // Apply the pause/unpause immediately if the window is open
        ApplyPauseState();

        this.absorbInputAroundWindow = true;
        this.closeOnClickedOutside = false;
        this.doCloseX = true;
        this.draggable = true;

        // Initialize RichTextInput with loaded tabs
        richTextInput = new RichTextInput(pawn, this);
    }

    public override Vector2 InitialSize => new Vector2(1000f, 700f); // Increased height

    public Rect GetWindowRect()
    {
        return this.windowRect;
    }


    public override void DoWindowContents(Rect inRect)
    {

        // Draw a draggable area at the top
        Rect draggableArea = new Rect(0f, 0f, inRect.width, 30f);
        GUI.DragWindow(draggableArea);
        
        // Draw the TurtleBot icon and title
        Rect iconRect = new Rect(0f, 0f, 30f, 30f);
        Rect titleRect = new Rect(40f, 0f, inRect.width - 40f, 30f);
        GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("TurtleBot"));
        Widgets.Label(titleRect, "Lua Code Console");

        // Draw the rich text input
        Rect richTextInputRect = new Rect(0f, 45f + 15f, inRect.width, inRect.height - 260f); // Increased height
        richTextInput.Draw(richTextInputRect);

        // Draw checkboxes
        float toggleY = inRect.height - 120f; // Adjusted toggle Y position
        float toggleWidth = inRect.width / 2f - 20f;
        float toggleDistance = toggleWidth + 20f;

        Widgets.CheckboxLabeled(new Rect(0f, toggleY, toggleWidth, 30f), "Warn on Subclass Mismatch", ref warnOnSubclassMismatch);
        Widgets.CheckboxLabeled(new Rect(toggleDistance, toggleY, toggleWidth, 30f), "Enable C# Type Checking", ref enableCSharpTypeChecking);
        Widgets.CheckboxLabeled(new Rect(0f, toggleY + 40f, toggleWidth, 30f), "Auto Type Convert", ref autoTypeConvert);
        Widgets.CheckboxLabeled(new Rect(toggleDistance, toggleY + 40f, toggleWidth, 30f), "Log Type Conversions", ref logTypeConversions);

        bool previousPauseGameWhenOpen = pauseGameWhenOpen;
        Widgets.CheckboxLabeled(new Rect(0f, toggleY + 80f, toggleWidth, 30f), "Pause Game When Open", ref pauseGameWhenOpen);

        // Update the forcePause dynamically if the user changes the "Pause Game When Open" setting
        if (pauseGameWhenOpen != previousPauseGameWhenOpen)
        {
            this.forcePause = pauseGameWhenOpen;
            ApplyPauseState(); // Apply the pause/unpause immediately
        }

        Widgets.CheckboxLabeled(new Rect(toggleDistance, toggleY + 80f, toggleWidth, 30f), "Close on Execute", ref closeOnExecute);

        // Buttons
        float buttonY = inRect.height - 200f; // Adjusted button Y position
        float buttonWidth = (inRect.width - 50f) / 4f;  // Adjusted for 4 buttons

        // Execute the code in the current tab
        if (Widgets.ButtonText(new Rect(0f, buttonY, buttonWidth, 40f), "Execute Current Tab"))
        {
            ExecuteCurrentTab();
        }

        // Execute the code in all tabs sequentially
        if (Widgets.ButtonText(new Rect(buttonWidth + 10f, buttonY, buttonWidth, 40f), "Execute All Tabs"))
        {
            ExecuteAllTabs();
        }

        // Execute specific tabs
        if (Widgets.ButtonText(new Rect(2 * (buttonWidth + 10f), buttonY, buttonWidth, 40f), "Execute Specific Tabs"))
        {
            Find.WindowStack.Add(new Dialog_ExecuteSpecificTabs(pawn, richTextInput.GetCodeTabs()));
        }

        // Stop execution
        if (Widgets.ButtonText(new Rect(3 * (buttonWidth + 10f), buttonY, buttonWidth, 40f), "Stop Execution"))
        {
            pawn.StopLuaExecution();

            if (closeOnExecute) Close();
        }
    }


    private void ApplyPauseState()
    {
        // Get the current pause state
        bool isGamePaused = Find.TickManager.CurTimeSpeed == TimeSpeed.Paused;

        // Apply the pause state if needed
        if (pauseGameWhenOpen && !isGamePaused)
        {
            Find.TickManager.TogglePaused(); // Pause the game
        }
        else if (!pauseGameWhenOpen && isGamePaused)
        {
            Find.TickManager.TogglePaused(); // Unpause the game
        }
    }

    private void ExecuteCurrentTab()
    {
        SaveSettings(); // Save the settings before executing

        // Get the code from the currently active tab
        string codeToExecute = richTextInput.GetActiveTabContent().Replace("‹", "<").Replace("›", ">");

        pawn.ExecuteLuaCode(codeToExecute);
        SaveTabs();

        if (closeOnExecute) Close();
    }

    private void ExecuteAllTabs()
    {
        SaveSettings(); // Save the settings before executing

        // Concatenate code from all tabs into one string
        string concatenatedCode = string.Join("\n", richTextInput.GetAllTabsContent());

        // Replace ‹ and › back to < and > before executing the Lua code
        string codeToExecute = concatenatedCode.Replace("‹", "<").Replace("›", ">");

        // Execute the concatenated code
        pawn.ExecuteLuaCode(codeToExecute);

        SaveTabs();

        if (closeOnExecute) Close();
    }

    private void SaveTabs()
    {
       richTextInput.SaveTabs(); // Save the current state of all tabs
    }

    private void SaveSettings()
    {
        // Save the checkbox settings back to the pawn instance
        pawn.WarnOnSubclassMismatch = warnOnSubclassMismatch;
        pawn.EnableCSharpTypeChecking = enableCSharpTypeChecking;
        pawn.AutoTypeConvert = autoTypeConvert;
        pawn.LogTypeConversions = logTypeConversions;
        pawn.PauseGameWhenOpen = pauseGameWhenOpen;
        pawn.CloseOnExecute = closeOnExecute;

        // Adjust forcePause based on the updated setting
        this.forcePause = pauseGameWhenOpen;

        // Apply the pause/unpause immediately
        ApplyPauseState();
    }

    public override void PreClose()
    {
        base.PreClose();
        SaveTabs(); // Ensure tabs are saved when the window is closed
        SaveSettings(); // Save settings when the window is closed
    }

    // Prevent Enter and Esc from closing the dialog
    public override void OnAcceptKeyPressed()
    {
        // Prevent the default behavior of closing the window on Enter
    }

    public override void OnCancelKeyPressed()
    {
        SaveTabs();
        SaveSettings();
        Close();
    }
}


}

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;

namespace TurtleMod
{
    public class DialogColorPickerSyntaxHighlighter
    {
        private readonly Dictionary<string, string> _syntaxColors;

        public DialogColorPickerSyntaxHighlighter(Dictionary<string, Color> initialColors)
        {
            _syntaxColors = new Dictionary<string, string>
            {
                { @"\bTurtle\.", ColorUtility.ToHtmlStringRGB(initialColors["Turtle Color"]) }, // Turtle. handling
                { @"Turtle\.(InvalidMethod)\b", ColorUtility.ToHtmlStringRGB(initialColors["Error Color"]) }, // Invalid method
                { @"--.*", ColorUtility.ToHtmlStringRGB(initialColors["Comment Color"]) }, // Comments
                { "\"[^\"]*\"|'[^']*'", ColorUtility.ToHtmlStringRGB(initialColors["String Color"]) }, // Strings
                { @"\b\d+(\.\d+)?\b", ColorUtility.ToHtmlStringRGB(initialColors["Number Color"]) }, // Numbers
                { @"\b(true|false)\b", ColorUtility.ToHtmlStringRGB(initialColors["Boolean Color"]) }, // Boolean values true/false
                { @"\bnil\b", ColorUtility.ToHtmlStringRGB(initialColors["Nil Color"]) }, // Nil value
                { @"\b(function|end|if|then|else|elseif|for|while|do|return|local)\b", ColorUtility.ToHtmlStringRGB(initialColors["Keyword Color"]) }, // Keywords
                { @"\b(and|or|not)\b", ColorUtility.ToHtmlStringRGB(initialColors["Operator Color"]) }, // Logical operators
                { @"\+|-|\*|\/|%|=|==|~=|‹|›", ColorUtility.ToHtmlStringRGB(initialColors["Operator Color"]) }, // Other operators
                { @"\b\w+\s*(?=\()", ColorUtility.ToHtmlStringRGB(initialColors["Function Color"]) }, // Function calls
                { @"(?<![\w\d_])\b([a-zA-Z_]\w*)\b(?![\w\d_])", ColorUtility.ToHtmlStringRGB(initialColors["Variable Color"]) } // Variables
            };
        }

        public string ApplySyntaxHighlighting(string code)
        {
            var matchesAndReplacements = new List<(int Index, int Length, string Replacement)>();

            // First, apply the Turtle. highlighting separately
            foreach (Match match in Regex.Matches(code, @"\bTurtle\."))
            {
                if (!IsOverlappingWithExistingMatches(matchesAndReplacements, match.Index, match.Length))
                {
                    string colorCode = $"#{_syntaxColors[@"\bTurtle\."]}";
                    string replacement = $"<color={colorCode}>{match.Value}</color>";
                    matchesAndReplacements.Add((match.Index, match.Length, replacement));
                }
            }

            // Now, handle method names, including invalid methods
            foreach (var entry in _syntaxColors.Where(e => e.Key != @"\bTurtle\."))
            {
                string colorCode = $"#{entry.Value}";

                foreach (Match match in Regex.Matches(code, entry.Key))
                {
                    if (!IsOverlappingWithExistingMatches(matchesAndReplacements, match.Index, match.Length))
                    {
                        string replacement = match.Groups[1].Success
                            ? match.Value.Replace(match.Groups[1].Value, $"<color={colorCode}>{match.Groups[1].Value}</color>")
                            : $"<color={colorCode}>{match.Value}</color>";

                        matchesAndReplacements.Add((match.Index, match.Length, replacement));
                    }
                }
            }

            // Sort matches by index in descending order to apply them correctly
            matchesAndReplacements = matchesAndReplacements.OrderByDescending(m => m.Index).ToList();

            // Apply the replacements to the code
            foreach (var (index, length, replacement) in matchesAndReplacements)
            {
                code = code.Remove(index, length).Insert(index, replacement);
            }

            return code;
        }

        private bool IsOverlappingWithExistingMatches(List<(int Index, int Length, string Replacement)> matches, int index, int length)
        {
            foreach (var match in matches)
            {
                if (index < match.Index + match.Length && match.Index < index + length)
                {
                    return true;
                }
            }
            return false;
        }
    }




    
    public class Dialog_ColorPicker : Window
    {
            
        private readonly List<string> _themeElements = new List<string>
        {
            "Keyword Color", "Comment Color", "String Color", "Number Color",
            "Boolean Color", "Nil Color", "Operator Color", "Function Color", 
            "Variable Color", "Error Color", "Selection Color", "Turtle Color" 
        };


        private readonly Dictionary<string, Color> _initialColors;
        private readonly Dictionary<string, Color> _originalColors; // Store original colors
        private readonly Action<string, Color> _onSelect;
        private string _selectedElement;
        private Color _selectedColor;

        private bool _hsvColorWheelDragging;
        private string _previousFocusedControlName;

        private Color _textfieldColorBuffer;

        private readonly string[] _textfieldBuffers = new string[3];

        private string _themeStringInput = string.Empty;
        private string _currentThemeString;



        public Dialog_ColorPicker(Dictionary<string, Color> initialColors, Action<string, Color> onSelect)
        {
                // Load initial colors from ModThemeConfig
            _initialColors = new Dictionary<string, Color>
            {
                { "Keyword Color", ParseColor(ModThemeConfig.KeywordColor) },
                { "Comment Color", ParseColor(ModThemeConfig.CommentColor) },
                { "String Color", ParseColor(ModThemeConfig.StringColor) },
                { "Number Color", ParseColor(ModThemeConfig.NumberColor) },
                { "Boolean Color", ParseColor(ModThemeConfig.BooleanColor) },
                { "Nil Color", ParseColor(ModThemeConfig.NilColor) },
                { "Operator Color", ParseColor(ModThemeConfig.OperatorColor) },
                { "Function Color", ParseColor(ModThemeConfig.FunctionColor) },
                { "Variable Color", ParseColor(ModThemeConfig.VariableColor) },
                { "Error Color", ParseColor(ModThemeConfig.ErrorColor) },
                { "Selection Color", ParseColor(ModThemeConfig.SelectionColor) },
                { "Turtle Color", ParseColor(ModThemeConfig.TurtleColor) }
            };

            _onSelect = onSelect;
            _selectedElement = _themeElements.First();
            _selectedColor = _initialColors[_selectedElement];
           
            // Create a copy of the original colors to revert to on cancel
            _originalColors = new Dictionary<string, Color>(_initialColors);


            this.doCloseX = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
        }


        public override Vector2 InitialSize => new(700f, 708f);




        public override void DoWindowContents(Rect inRect)
        {
            using (TextBlock.Default())
            {
                var layout = new RectDivider(inRect, 91185);
                HeaderRow(ref layout);
                ThemeElementSelection(ref layout);

                var rectDivider = layout.NewRow(150f);
                Rect colorWheelRect = new Rect(rectDivider.Rect.x + 80f, rectDivider.Rect.y, 140f, 140f); // Move the wheel 40 pixels to the right

                // Always draw the color wheel
                Widgets.HSVColorWheel(colorWheelRect, ref _selectedColor, ref _hsvColorWheelDragging, 1f);

                if (Event.current.type == EventType.MouseDown && Mouse.IsOver(colorWheelRect))
                {
                    _hsvColorWheelDragging = true;
                }

                if (Event.current.type == EventType.MouseUp)
                {
                    _hsvColorWheelDragging = false;
                }

                if (_hsvColorWheelDragging)
                {
                    if (!inRect.Contains(Event.current.mousePosition))
                    {
                        _hsvColorWheelDragging = false;
                    }
                    else
                    {
                        Widgets.HSVColorWheel(colorWheelRect, ref _selectedColor, ref _hsvColorWheelDragging, 1f);
                    }
                }

                ColorTextFields(ref layout);
                
                // Ensure that the ColorReadback is immediately below ColorTextFields
                ColorReadback(ref layout, _selectedColor, _originalColors[_selectedElement]);

                // Ensure that the BottomButtons are directly below the ColorReadback
                BottomButtons(ref layout);

                // Add Lua code display with a nice background and syntax highlighting
                LuaCodeDisplay(ref layout);

                if (Event.current.type != EventType.Layout)
                    return;

                _previousFocusedControlName = GUI.GetNameOfFocusedControl();
            }
        }


    private void LuaCodeDisplay(ref RectDivider layout)
    {
        // Define the background color
        Color backgroundColor = new Color(18f / 255f, 24f / 255f, 32f / 255f);

        // Adjust the overall Rect position and height to accommodate full code display
        float codeDisplayHeight = 280f; // Adjust height as needed
        var rectDivider = layout.NewRow(codeDisplayHeight); // Use the adjusted height
        Rect luaDisplayRect = new Rect(rectDivider.Rect.x, rectDivider.Rect.y - 156f, rectDivider.Rect.width, codeDisplayHeight); // Adjust height and move up

        // Draw the custom background box
        Widgets.DrawBoxSolid(luaDisplayRect, backgroundColor);

        // Sample Lua code covering all syntax types
    string luaCode = @"
local x = 5
local active = true
local result = nil

local function add(a, b)
    return a + b
end

result = add(x, 10)
print('Result: ' .. result)

-- Returns IntVec3
local pos = Turtle.GetCurrentPos()

-- Invalid method call
Turtle.InvalidMethod()";
        // Apply syntax highlighting using current selected colors
        var syntaxHighlighter = new DialogColorPickerSyntaxHighlighter(_initialColors);
        string highlightedCode = syntaxHighlighter.ApplySyntaxHighlighting(luaCode);

        // Adjust margins to fix alignment
        float marginLeft = 10f;  // Positive margins to move the text inside the container
        float marginTop = 5f;   
        float marginRight = 10f;  
        float marginBottom = 5f;

        // Set the font to small
        Text.Font = GameFont.Small;

        // Set the global selection color to the user's chosen color
        GUI.skin.settings.selectionColor = ParseColor(ColorUtility.ToHtmlStringRGB(_initialColors["Selection Color"]));

        // Create a custom GUIStyle for the text area that allows selection but not editing
        GUIStyle textAreaStyle = new GUIStyle(GUI.skin.textArea)
        {
            richText = true,
            normal = { textColor = Color.white, background = null }, // Visible text with no background
            hover = { textColor = Color.white, background = null },  // Disable hover effects
            active = { textColor = Color.white, background = null }, // Disable active state
            focused = { textColor = Color.white, background = null }, // Disable focused state
            border = new RectOffset(0, 0, 0, 0), // No border
            padding = new RectOffset(5, 5, 5, 5), // Add padding
            fontSize = (int)Text.CurFontStyle.fontSize,
            wordWrap = true
        };

        // Adjust the Rect for the text area
        var codeRect = new Rect(
            luaDisplayRect.x + marginLeft,
            luaDisplayRect.y + marginTop,
            luaDisplayRect.width - marginLeft - marginRight,
            luaDisplayRect.height - marginTop - marginBottom
        );

        // Capture keyboard input events
        if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp)
        {
            Event.current.Use(); // Consume the event so it doesn't affect the TextArea
        }

        // Display the code in a selectable TextField, ensuring it is properly aligned and only rendered once
        GUI.TextArea(codeRect, highlightedCode, textAreaStyle);

        // Reset the font back to its default state after rendering
        Text.Font = GameFont.Small;
    }









        private static void HeaderRow(ref RectDivider layout)
        {
            using (new TextBlock(GameFont.Medium))
            {
                var taggedString = "ChooseAColor".Translate().CapitalizeFirst();
                var rectDivider = layout.NewRow(Text.CalcHeight(taggedString, layout.Rect.width));
                Widgets.Label(rectDivider, taggedString);
            }
        }


        private void ThemeElementSelection(ref RectDivider layout)
        {
            var rectDivider = layout.NewRow(30f);
            float dropdownWidth = 300f;
            float labelWidth = 200f;
            float totalWidth = labelWidth + dropdownWidth;
            float startX = rectDivider.Rect.xMax - totalWidth;

            //Widgets.Label(new Rect(startX, rectDivider.Rect.y, labelWidth, rectDivider.Rect.height), "");
            startX += labelWidth;

            if (Widgets.ButtonText(new Rect(startX, rectDivider.Rect.y, dropdownWidth, rectDivider.Rect.height), _selectedElement))
            {
                var options = new List<FloatMenuOption>();
                foreach (var element in _themeElements)
                {
                    options.Add(new FloatMenuOption(element, () =>
                    {
                        _selectedElement = element;
                        _selectedColor = _initialColors[_selectedElement];
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            var squaresRow = layout.NewRow(30f);
            const float squareSize = 25f; 
            const float spacing = 5f;
            totalWidth = (_themeElements.Count * squareSize) + ((_themeElements.Count - 1) * spacing);
            float currentX = squaresRow.Rect.xMax - totalWidth;

            foreach (var element in _themeElements)
            {
                Rect colorRect = new Rect(currentX, squaresRow.Rect.y, squareSize, squareSize);
                Widgets.DrawBoxSolid(colorRect, _initialColors[element]);

                if (element == _selectedElement)
                {
                    Widgets.DrawBox(colorRect, 2); // Draw a black outline with a thickness of 2
                }

                if (Widgets.ButtonInvisible(colorRect))
                {
                    _selectedElement = element;
                    _selectedColor = _initialColors[_selectedElement];
                }

                currentX += squareSize + spacing;
            }


        }



        private void BottomButtons(ref RectDivider layout)
        {
            Text.Font = GameFont.Small;
            var rectDivider = layout.NewRow(UIUtility.BottomButtonSize.y, VerticalJustification.Bottom);

            // Align buttons to the left
            float startX = rectDivider.Rect.x; // Start from the leftmost edge
            float buttonWidth = UIUtility.BottomButtonSize.x;
            float spacing = 10f; // Optional spacing between buttons

            if (Widgets.ButtonText(new Rect(startX, rectDivider.Rect.y, buttonWidth, rectDivider.Rect.height), "Cancel".Translate()))
            {
                ResetChanges(); // Revert to original colors
                Close();
            }

            // Move startX to the right for the next button
            startX += buttonWidth + spacing;

            if (Widgets.ButtonText(new Rect(startX, rectDivider.Rect.y, buttonWidth, rectDivider.Rect.height), "Apply"))
            {
                ApplyChangesAndSave(); // Apply the changes and save them
                Close(); // Close the dialog
            }

            // Add the Reset Theme button under the Apply button
            var rectDivider2 = layout.NewRow(UIUtility.BottomButtonSize.y, VerticalJustification.Bottom);
            if (Widgets.ButtonText(new Rect(rectDivider2.Rect.x, rectDivider2.Rect.y, buttonWidth, rectDivider2.Rect.height), "Reset Theme"))
            {
                ResetToDefaultTheme(); // Reset to the default theme
            }
        }


        // Method to reset the theme to the default
        private void ResetToDefaultTheme()
        {
            string defaultThemeString = "#B45EFF,#4E5F75,#64FE2E,#FFCC00,#FE7000,#A8BDD5,#A8BDD5,#FFEB00,#00CCFF,#FF0000,#3399FF,#FF69B4";
            PreviewThemeFromString(defaultThemeString);
            _themeStringInput = string.Empty; // Clear the input field
        }

        // Apply changes and then save to config
        private void ApplyChangesAndSave()
        {
            // Apply changes to the settings when "Apply" is clicked
            foreach (var element in _themeElements)
            {
                _onSelect?.Invoke(element, _initialColors[element]);
            }

            // Update and save to the config
            ApplyChangesToConfig();
        }

        private void ResetChanges()
        {
            // Revert all changes to the original state
            foreach (var key in _originalColors.Keys.ToList())
            {
                _initialColors[key] = _originalColors[key];
            }
        }

        private void ColorReadback(ref RectDivider layout, Color color, Color oldColor)
        {
            var rectDivider1 = layout.NewRow(Text.LineHeightOf(GameFont.Small) * 2 + 26f + 8f, VerticalJustification.Bottom);

            var label1 = "CurrentColor".Translate().CapitalizeFirst();
            var label2 = "OldColor".Translate().CapitalizeFirst();

            var width = Mathf.Max(60f, label1.GetWidthCached(), label2.GetWidthCached());

            // Align elements to the left
            float startX = rectDivider1.Rect.x; // Start from the leftmost edge
            float spacing = 10f; // Space between label and color box
            Text.Font = GameFont.Small; 
            
            var rectDivider2 = rectDivider1.NewRow(Text.LineHeight);
            Widgets.Label(new Rect(startX, rectDivider2.Rect.y, width, Text.LineHeight), label1);

            Rect currentColorRect = new Rect(startX + width + spacing, rectDivider2.Rect.y, 60f, Text.LineHeight);
            Widgets.DrawBoxSolid(currentColorRect, color);

            var rectDivider3 = rectDivider1.NewRow(Text.LineHeight);
            Widgets.Label(new Rect(startX, rectDivider3.Rect.y, width, Text.LineHeight), label2);

            Rect oldColorRect = new Rect(startX + width + spacing, rectDivider3.Rect.y, 60f, Text.LineHeight);
            Widgets.DrawBoxSolid(oldColorRect, oldColor);
        }

        private void ColorTextFields(ref RectDivider layout)
        {
            var rectDivider1 = layout.NewCol(layout.Rect.width / 2);

            var label1 = "Hue";
            var label2 = "Saturation";
            var label3 = "Hex";
            var label4 = "Current";
            var label5 = "Load";

            float extraPadding = 10f;

            const string controlName = "ColorTextfields";
            var focusedPrev = _previousFocusedControlName != null && _previousFocusedControlName.StartsWith(controlName);
            var focusedNow = GUI.GetNameOfFocusedControl().StartsWith(controlName);

            var width = Mathf.Max(40, label1.GetWidthCached(), label2.GetWidthCached(), label3.GetWidthCached());

            Color.RGBToHSV(_selectedColor, out var hue, out var sat, out var val);

            // First row: Hue
            var rectDivider2 = rectDivider1.NewRow(Text.LineHeight + extraPadding); // Add 3 pixels of padding
            Widgets.Label(rectDivider2.NewCol(width), label1);
            var hueText = ToIntegerRange(hue, 0, 360).ToString();
            var newHueText = Widgets.DelayedTextField(rectDivider2, hueText, ref _textfieldBuffers[0], _previousFocusedControlName, controlName + "_hue");
            if (hueText != newHueText && int.TryParse(newHueText, out var newHue))
                _textfieldColorBuffer = Color.HSVToRGB(newHue / 360f, sat, val);

            // Second row: Saturation
            var rectDivider3 = rectDivider1.NewRow(Text.LineHeight + extraPadding); // Add 3 pixels of padding
            Widgets.Label(rectDivider3.NewCol(width), label2);
            var satText = ToIntegerRange(sat, 0, 100).ToString();
            var newSatText = Widgets.DelayedTextField(rectDivider3, satText, ref _textfieldBuffers[1], _previousFocusedControlName, controlName + "_sat");
            if (satText != newSatText && int.TryParse(newSatText, out var newSat))
                _textfieldColorBuffer = Color.HSVToRGB(hue, newSat / 100f, val);

            // Third row: Hex
            var rectDivider4 = rectDivider1.NewRow(Text.LineHeight + extraPadding); // Add 3 pixels of padding
            Widgets.Label(rectDivider4.NewCol(width), label3);
            var hex = ColorUtility.ToHtmlStringRGB(_selectedColor);
            var newHex = Widgets.DelayedTextField(rectDivider4, hex, ref _textfieldBuffers[2], _previousFocusedControlName, controlName + "_hex");
            if (hex != newHex)
            {
                if (!newHex.StartsWith("#"))
                    newHex = "#" + newHex;
                ColorUtility.TryParseHtmlString(newHex, out _textfieldColorBuffer);
            }

            if (focusedPrev)
            {
                if (!focusedNow)
                    _selectedColor = _textfieldColorBuffer;
            }
            else
            {
                _textfieldColorBuffer = _selectedColor;
            }

            // Fourth row: Random button
            var rectDivider5 = rectDivider1.NewRow(UIUtility.RegularButtonHeight + extraPadding); // Add 3 pixels of padding
            if (Widgets.ButtonText(rectDivider5, "Random".Translate()))
                _selectedColor = Random.ColorHSV();

            // Apply the change immediately for visual feedback
            _initialColors[_selectedElement] = _selectedColor;

            // Fifth row: Current Theme String
            _currentThemeString = GetCurrentThemeString();
            var rectDivider6 = rectDivider1.NewRow(Text.LineHeight + extraPadding); // Add 3 pixels of padding
            Widgets.Label(rectDivider6.NewCol(width), label4);
            Text.Font = GameFont.Tiny;  // Set the font to a very small size
            _currentThemeString = Widgets.TextField(new Rect(rectDivider6.Rect.x + width + 10f, rectDivider6.Rect.y, rectDivider6.Rect.width - width - 10f, rectDivider6.Rect.height), _currentThemeString);

            // Sixth row: Load Theme String
            Text.Font = GameFont.Small;  // Set font size to medium for the Load Theme label
            var rectDivider7 = rectDivider1.NewRow(Text.LineHeight + extraPadding); // Add 3 pixels of padding
            Widgets.Label(rectDivider7.NewCol(width), label5);
            Text.Font = GameFont.Tiny;  // Set font size to small for the Load Theme input field
            _themeStringInput = Widgets.TextField(new Rect(rectDivider7.Rect.x + width + 10f, rectDivider7.Rect.y, rectDivider7.Rect.width - width - 10f, rectDivider7.Rect.height), _themeStringInput);

            // If Enter is pressed or the field is unfocused, try to load the theme
            if ((Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) || !GUI.GetNameOfFocusedControl().StartsWith(controlName))
            {
                PreviewThemeFromString(_themeStringInput);
            }

        }




        private string GetCurrentThemeString()
        {
            return string.Join(",", _themeElements.Select(element => $"#{ColorUtility.ToHtmlStringRGB(_initialColors[element])}"));
        }


        private void PreviewThemeFromString(string themeString)
        {
            // Clean up the theme string by removing any new lines, spaces, etc.
            themeString = themeString.Replace("\n", "").Replace("\r", "").Replace(" ", "").Trim();

            var colors = themeString.Split(',').Select(c => c.Trim()).ToArray();

            if (colors.Length == _themeElements.Count)
            {
                bool validTheme = true;
                for (int i = 0; i < _themeElements.Count; i++)
                {
                    var colorString = colors[i];
                    if (!colorString.StartsWith("#"))
                        colorString = "#" + colorString;

                    if (ColorUtility.TryParseHtmlString(colorString, out var color))
                    {
                        _initialColors[_themeElements[i]] = color;

                        // Update the selected color to the first element's color if it's the first time
                        if (_themeElements[i] == _selectedElement)
                        {
                            _selectedColor = color;
                        }
                    }
                    else
                    {
                        validTheme = false;
                        break;
                    }
                }

                if (validTheme)
                {
                    _themeStringInput = string.Empty; // Clear the input field after loading the theme
                }
            }
        }



        private int ToIntegerRange(float value, int min, int max)
        {
            return Mathf.Clamp(Mathf.RoundToInt(value * max), min, max);
        }

        private void Accept()
        {
            _onSelect?.Invoke(_selectedElement, _selectedColor);
            Close();
        }



        // Apply changes to ModThemeConfig and save them to XML
        private void ApplyChangesToConfig()
        {
            foreach (var element in _themeElements)
            {
                // Update ModThemeConfig static variables based on element name
                switch (element)
                {
                    case "Keyword Color": ModThemeConfig.KeywordColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Comment Color": ModThemeConfig.CommentColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "String Color": ModThemeConfig.StringColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Number Color": ModThemeConfig.NumberColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Boolean Color": ModThemeConfig.BooleanColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Nil Color": ModThemeConfig.NilColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Operator Color": ModThemeConfig.OperatorColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Function Color": ModThemeConfig.FunctionColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Variable Color": ModThemeConfig.VariableColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Error Color": ModThemeConfig.ErrorColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Selection Color": ModThemeConfig.SelectionColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                    case "Turtle Color": ModThemeConfig.TurtleColor = ColorUtility.ToHtmlStringRGB(_initialColors[element]); break;
                }
            }

            // Save the updated configuration to the XML file
            ModThemeConfig.SaveToXML();
        }

        private Color ParseColor(string hex)
        {
            // Check if the string already starts with '#'
            if (!hex.StartsWith("#"))
            {
                hex = "#" + hex;
            }

            // Try to parse the color and return it, or default to white if parsing fails
            if (ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return color;
            }

            return Color.white; // Default to white if parsing fails
        }


    }
}

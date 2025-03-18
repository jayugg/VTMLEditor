using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace VTMLEditor.TextHighlighting;

/// <summary>
/// Provides a highlight theme for VTML syntax highlighting.
/// </summary>
public class VtmlEditorTheme
{
    [JsonProperty]
    public string Code = "Default";
    
    [JsonProperty]
    public string FontName = GuiStyle.StandardFontName; // "monospace", "sans-serif", "menlo

    [JsonProperty]
    public int FontSize = 14;
    
    [JsonProperty]
    public Dictionary<VtmlTokenType, string?> TokenColors { get; set; } = new()
    {
        // Gray for tag delimiters.
        { VtmlTokenType.TagDelimiter, ColorUtil.Doubles2Hex(new []{0.56,0.56,0.56,1.0}) },
        // Blue for tag names.
        { VtmlTokenType.TagName, ColorUtil.Doubles2Hex(new []{0.56,0.56,0.56,1.0}) },
        // Brownish for attribute names.
        { VtmlTokenType.AttributeName, ColorUtil.Doubles2Hex(new []{0.8,0.6,0.4,1.0}) },
        // Light gray for equals signs.
        { VtmlTokenType.EqualsSign, ColorUtil.Doubles2Hex(new []{0.56,0.56,0.56,1.0})  },
        // Green for attribute values.
        { VtmlTokenType.AttributeValue, ColorUtil.Doubles2Hex(new []{0.42,0.65,0.81,1.0}) },
        // Use the font color for normal text.
        { VtmlTokenType.Text, null }
    };
    
    public static double[]? GetColor(Dictionary<VtmlTokenType, string?> tokenColors, VtmlTokenType tokenType)
    {
        if (!tokenColors.TryGetValue(tokenType, out string? color) ||
            color?.Length < 8)
        {
            return null;
        }
        return color == null ? null : ColorUtil.Hex2Doubles(color);
    }
    
    public static VtmlEditorTheme Default => new VtmlEditorTheme();
}
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using VTMLEditor.TextHighlighting;

namespace VTMLEditor.GuiElements;

public static class TextUtilExtensions
{
    public static void DrawMultilineTextHighlightedAt(
        this TextDrawUtil util,
        Context ctx,
        CairoFont font,
        TextLine[] lines,
        double posX,
        double posY,
        double boxWidth,
        EnumTextOrientation orientation = EnumTextOrientation.Left)
    {
        ctx.Save();
        Matrix matrix = ctx.Matrix;
        matrix.Translate(posX, posY);
        ctx.Matrix = matrix;
        font.SetupContext(ctx);
        util.DrawMultilineTextHighlighted(ctx, font, lines, orientation);
        ctx.Restore();
    }
    
    
    
    public static void DrawMultilineTextHighlighted(
        this TextDrawUtil util,
        Context ctx,
        CairoFont font,
        TextLine[] lines,
        EnumTextOrientation orientation = EnumTextOrientation.Left)
    {
        font.SetupContext(ctx);
        var tokens = VtmlTokenizer.Tokenize(lines);
        double offsetX = 0.0;
        for (int index = 0; index < tokens.Count; ++index)
        {
            TextLine line = lines[index];
            List<VtmlToken> lineTokens = tokens[index];
            if (line.Text.Length != 0)
            {
                if (orientation == EnumTextOrientation.Center)
                    offsetX = (line.LeftSpace + line.RightSpace) / 2.0;
                if (orientation == EnumTextOrientation.Right)
                    offsetX = line.LeftSpace + line.RightSpace;
                util.DrawTextLineHighlighted(ctx, font, lineTokens, offsetX + line.Bounds.X, line.Bounds.Y);
            }
        }
    }
    
    public static void DrawTextLineHighlighted(
            this TextDrawUtil util,
            Context ctx,
            CairoFont font,
            List<VtmlToken> tokens,
            double offsetX = 0.0,
            double offsetY = 0.0,
            bool textPathMode = false)
        {
            ctx.Save();
            font.SetupContext(ctx);
            double baselineY = offsetY + ctx.FontExtents.Ascent;
            double currentX = offsetX;
            foreach (var token in tokens)
            {
                var color = VtmleSystem.Theme.GetColor(token.TokenType);
                if (color != null)
                {
                    ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);
                }
                else
                {
                    ctx.SetSourceRGBA(font.Color);
                }

                string content = token.Content;
                
                double tokenWidth = font.GetTextExtents(content).XAdvance;
                ctx.MoveTo(currentX, baselineY);
                if (textPathMode)
                {
                    ctx.TextPath(content);
                }
                else
                {
                    ctx.ShowText(content);
                    if (font.RenderTwice)
                    {
                        ctx.ShowText(content);
                    }
                }

                // Advance the x position by the measured width plus the extra for each trailing space.
                currentX += tokenWidth;
            }

            ctx.Restore();
        }

    public static void DrawTextLineHighlighted(
        this TextDrawUtil util,
        Context ctx,
        CairoFont font,
        string text,
        double offsetX = 0.0,
        double offsetY = 0.0,
        bool textPathMode = false)
    {
        var tokens = VtmlTokenizer.Tokenize(text);
        util.DrawTextLineHighlighted(ctx, font, tokens, offsetX, offsetY, textPathMode);
    }
    
    public static CairoFont EditorFont(string fontName, double fontSize = 14)
    {
        if (string.IsNullOrEmpty(fontName))
        {
            fontName = CairoFont.WhiteSmallText().Fontname;
        }
        CairoFont cairoFont = new CairoFont();
        cairoFont.Color = (double[]) GuiStyle.DialogDefaultTextColor.Clone();
        cairoFont.Fontname = fontName;
        cairoFont.UnscaledFontsize = fontSize;
        return cairoFont;
    }
}
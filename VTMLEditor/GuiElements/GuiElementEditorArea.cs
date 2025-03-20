using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using VTMLEditor.EditorFeatures;

namespace VTMLEditor.GuiElements;

public class GuiElementEditorArea : Vanilla.GuiElementTextArea
    {
        public Dictionary<VtmlTokenType, string?> ThemeColors { get; set; }

        /// <summary>
        /// Creates a new text area.
        /// </summary>
        /// <param name="capi">The client API.</param>
        /// <param name="bounds">The bounds of the text area.</param>
        /// <param name="OnTextChanged">The event fired when the text is changed.</param>
        /// <param name="font">The font of the text.</param>
        /// <param name="themeColors">The syntax highlighting theme colors</param>
        public GuiElementEditorArea(
            ICoreClientAPI capi,
            ElementBounds bounds,
            Action<string> OnTextChanged,
            CairoFont? font,
            Dictionary<VtmlTokenType, string?> themeColors
            )
            : base(capi, bounds, OnTextChanged, font)
        {
            this.OnTextChanged = OnTextChanged;
            this.ThemeColors = themeColors;
        }

        public override void DrawTextLineAt(Context ctx, string textIn, double posX, double posY, bool textPathModeIn = false)
        {
            textUtil.DrawTextLineHighlighted(ThemeColors, ctx, Font, textIn, posX, posY, textPathModeIn);
        }

        public override void RenderMultilineText(Context ctx, double paddingX, double paddingY, double fontHeight, double width)
        {
            var textlines = new TextLine[lines.Count];
            for (int i = 0; i < textlines.Length; i++)
            {
                textlines[i] = new TextLine()
                {
                    Text = lines[i].Replace("\r\n", "").Replace("\n", ""),
                    Bounds = new LineRectangled(0, i*fontHeight, Bounds.InnerWidth, fontHeight)
                };
            }
            this.textUtil.DrawMultilineTextHighligtedAt(ThemeColors, ctx, this.Font, textlines, this.Bounds.absPaddingX + this.leftPadding, this.Bounds.absPaddingY, width);
        }
    }
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
            this.ThemeColors = themeColors;
            this.OnTextChanged = OnTextChanged;
        }
        
        // Change highlight compared to normal text area
        public override void ComposeTextElements(Context ctx, ImageSurface surface)
        {
            EmbossRoundRectangleElement(ctx, Bounds, true, 3);
            ctx.SetSourceRGBA(0, 0, 0, 0.2f);
            ElementRoundRectangle(ctx, Bounds, true, 3);
            ctx.Fill();
            GenerateHighlight();
            RecomposeText();
        }

        private void GenerateHighlight()
        {
            ImageSurface surfaceHighlight = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
            Context ctxHighlight = genContext(surfaceHighlight);

            ctxHighlight.SetSourceRGBA(1, 1, 1, 0.0);
            ctxHighlight.Paint();
            generateTexture(surfaceHighlight, ref highlightTexture);

            ctxHighlight.Dispose();
            surfaceHighlight.Dispose();

            highlightBounds = Bounds.FlatCopy();
            highlightBounds.CalcWorldBounds();
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

        public override void OnKeyDown(ICoreClientAPI capi, KeyEvent args)
        {
            base.OnKeyDown(capi, args);
            var selection = GetSelectedText();
            switch (args.KeyCode)
            {
                case (int)GlKeys.I when args.CtrlPressed || args.CommandPressed:
                    InsertTextAtCursor($"<i>{selection}</i>");
                    break;
                case (int)GlKeys.B when args.CtrlPressed || args.CommandPressed:
                    InsertTextAtCursor($"<strong>{selection}</strong>");
                    break;
            }
        }
        
        // Don't clear selection on focus lost so tag can work
        public override void OnFocusLost()
        {
            this.hasFocus = false;
            OnLostFocus?.Invoke();
        }
        
        public void SetFocused(bool focus)
        {
            this.hasFocus = focus;
        }
    }
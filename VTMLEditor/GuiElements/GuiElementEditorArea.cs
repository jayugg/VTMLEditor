using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using VTMLEditor.TextHighlighting;

namespace VTMLEditor.GuiElements;

public class GuiElementEditorArea : GuiElementEditorAreaBase
    {
        private double minHeight;
        private LoadedTexture highlightTexture;
        private ElementBounds highlightBounds;
        public bool Autoheight = true;

        /// <summary>
        /// Creates a new text area.
        /// </summary>
        /// <param name="capi">The client API.</param>
        /// <param name="bounds">The bounds of the text area.</param>
        /// <param name="OnTextChanged">The event fired when the text is changed.</param>
        /// <param name="font">The font of the text.</param>
        public GuiElementEditorArea(
            ICoreClientAPI capi,
            ElementBounds bounds,
            Action<string> OnTextChanged,
            CairoFont font,
            Dictionary<VtmlTokenType, string?> themeColors
            )
            : base(capi, font, bounds, themeColors)
        {
            highlightTexture = new LoadedTexture(capi);
            multilineMode = true;
            minHeight = bounds.fixedHeight;
            this.OnTextChanged = OnTextChanged;
        }

        internal override void TextChanged()
        {
            if (Autoheight)
            {
                string text = string.Join("\n", (IEnumerable<string>)lines);
                Bounds.fixedHeight = Math.Max(minHeight, textUtil.GetMultilineTextHeight(Font, text, Bounds.InnerWidth));
            }
            Bounds.CalcWorldBounds();
            base.TextChanged();
        }

        public override void ComposeTextElements(Context ctx, ImageSurface surface)
        {
            EmbossRoundRectangleElement(ctx, Bounds, true, 3);
            ctx.SetSourceRGBA(0.0, 0.0, 0.0, 0.20000000298023224);
            ElementRoundRectangle(ctx, Bounds, true, 3.0);
            ctx.Fill();
            GenerateHighlight();
            RecomposeText();
        }

        private void GenerateHighlight()
        {
            using (var surface = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight))
            {
                using (var context = genContext(surface))
                {
                    context.SetSourceRGBA(1.0, 1.0, 1.0, 0.1);
                    context.Paint();
                }
                generateTexture(surface, ref highlightTexture);
            }
            highlightBounds = Bounds.FlatCopy();
            highlightBounds.CalcWorldBounds();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (HasFocus)
                api.Render.Render2DTexturePremultipliedAlpha(highlightTexture.TextureId, highlightBounds);
            api.Render.Render2DTexturePremultipliedAlpha(textTexture.TextureId, Bounds);
            base.RenderInteractiveElements(deltaTime);
        }

        public override void Dispose()
        {
            base.Dispose();
            highlightTexture.Dispose();
        }

        public void SetFont(CairoFont cairoFont)
        {
            Font = cairoFont;
            caretHeight = cairoFont.GetFontExtents().Height;
        }
    }
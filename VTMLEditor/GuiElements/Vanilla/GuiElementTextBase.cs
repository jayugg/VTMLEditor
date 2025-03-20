using Cairo;
using Vintagestory.API.Client;

namespace VTMLEditor.GuiElements.Vanilla;

public class GuiElementTextBase : GuiElementControl
    {
        public TextDrawUtil textUtil;

        protected string text;
        public string Text { get { return text; } set { text = value; } }

        /// <summary>
        /// Whether or not the text path mode is active.
        /// </summary>
        public bool textPathMode = false;

        /// <summary>
        /// The font of the Text Element.
        /// </summary>
        public CairoFont Font;

        protected float RightPadding = 0f;

        /// <summary>
        /// Creates a new text based element.
        /// </summary>
        /// <param name="capi">The Client API</param>
        /// <param name="text">The text of this element.</param>
        /// <param name="font">The font of the text.</param>
        /// <param name="bounds">The bounds of the element.</param>
        public GuiElementTextBase(ICoreClientAPI capi, string text, CairoFont font, ElementBounds bounds) : base(capi, bounds)
        {
            Font = font;
            textUtil = new TextDrawUtil();
            this.text = text;
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Font.SetupContext(ctx);
            Bounds.CalcWorldBounds();
            ComposeTextElements(ctx, surface);
        }

        public virtual void ComposeTextElements(Context ctx, ImageSurface surface) { }


        public double GetMultilineTextHeight()
        {
            return textUtil.GetMultilineTextHeight(Font, text, Bounds.InnerWidth - RightPadding);
        }
        
        public double DrawMultilineTextAt(Context ctx, double posX, double posY, EnumTextOrientation orientation = EnumTextOrientation.Left)
        {
            Font.SetupContext(ctx);

            TextLine[] lines = textUtil.Lineize(Font, text, Bounds.InnerWidth - RightPadding);

            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(posX, posY);
            ctx.Matrix = m;
            
            textUtil.DrawMultilineText(ctx, Font, lines, orientation);
            ctx.Restore();

            return lines.Length == 0 ? 0 : (lines[^1].Bounds.Y + lines[^1].Bounds.Height);
        }


        // Use this instead of the raw TextDrawUtil method to allow for child classes to override the behavior.
        /// <summary>
        /// Draws the line of text on a component.
        /// </summary>
        /// <param name="ctx">The context of the text</param>
        /// <param name="textIn">The text of the text.</param>
        /// <param name="posX">The X Position of the text.</param>
        /// <param name="posY">The Y position of the text.</param>
        /// <param name="textPathModeIn">The pathing mode.</param>
        public virtual void DrawTextLineAt(Context ctx, string textIn, double posX, double posY, bool textPathModeIn = false)
        {
            textUtil.DrawTextLine(ctx, Font, textIn, posX, posY, textPathModeIn);
        }

        /// <summary>
        /// Gets the text on the element.
        /// </summary>
        /// <returns>The text of the element.</returns>
        public virtual string GetText()
        {
            return text;
        }

        internal virtual void setFont(CairoFont font)
        {
            Font = font;
        }

    }
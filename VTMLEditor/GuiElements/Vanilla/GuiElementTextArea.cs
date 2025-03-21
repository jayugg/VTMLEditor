using System;
using Cairo;
using Vintagestory.API.Client;

namespace VTMLEditor.GuiElements.Vanilla;

#nullable disable
public class GuiElementTextArea : GuiElementEditableTextBase
{
    double minHeight;
    
    // j: Expose to child classes
    protected LoadedTexture highlightTexture;
    protected ElementBounds highlightBounds;

    public bool Autoheight = true;

    /// <summary>
    /// Creates a new text area.
    /// </summary>
    /// <param name="capi">The client API</param>
    /// <param name="bounds">The bounds of the text area.</param>
    /// <param name="OnTextChanged">The event fired when the text is changed.</param>
    /// <param name="font">The font of the text.</param>
    public GuiElementTextArea(
        ICoreClientAPI capi,
        ElementBounds bounds, 
        Action<string> OnTextChanged,
        CairoFont font) : 
        base(capi, font, bounds)
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
            Bounds.fixedHeight = Math.Max(minHeight, textUtil.GetMultilineTextHeight(Font, string.Join("\n", lines), Bounds.InnerWidth));
        }
        Bounds.CalcWorldBounds();
        base.TextChanged();
    }

    public override void ComposeTextElements(Context ctx, ImageSurface surface)
    {
        EmbossRoundRectangleElement(ctx, Bounds, true, 3);
        ctx.SetSourceRGBA(0, 0, 0, 0.2f);
        ElementRoundRectangle(ctx, Bounds, true, 3);
        ctx.Fill();

        GenerateHighlight();

        RecomposeText();
    }

    void GenerateHighlight()
    {
        ImageSurface surfaceHighlight = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        Context ctxHighlight = genContext(surfaceHighlight);

        ctxHighlight.SetSourceRGBA(1, 1, 1, 0.1);
        ctxHighlight.Paint();

        generateTexture(surfaceHighlight, ref highlightTexture);

        ctxHighlight.Dispose();
        surfaceHighlight.Dispose();

        highlightBounds = Bounds.FlatCopy();
        highlightBounds.CalcWorldBounds();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (HasFocus)
        {
            api.Render.Render2DTexturePremultipliedAlpha(highlightTexture.TextureId, highlightBounds);
        }

        api.Render.Render2DTexturePremultipliedAlpha(textTexture.TextureId, Bounds);

        base.RenderInteractiveElements(deltaTime);
    }


    public override void Dispose()
    {
        base.Dispose();
        highlightTexture?.Dispose();
    }

    public void SetFont(CairoFont cairoFont)
    {
        this.Font = cairoFont;
        caretHeight = cairoFont.GetFontExtents().Height;
    }
}
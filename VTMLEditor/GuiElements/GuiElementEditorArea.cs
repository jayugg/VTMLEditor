using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;

namespace VTMLEditor.GuiElements;

public class GuiElementEditorArea : GuiElementEditorAreaBase
{
    private double minHeight;
    private LoadedTexture highlightTexture;
    private ElementBounds highlightBounds;
    public bool Autoheight = true;

    /// <summary>Creates a new text area.</summary>
    /// <param name="capi">The client API</param>
    /// <param name="bounds">The bounds of the text area.</param>
    /// <param name="OnTextChanged">The event fired when the text is changed.</param>
    /// <param name="font">The font of the text.</param>
    public GuiElementEditorArea(
      ICoreClientAPI capi,
      ElementBounds bounds,
      Action<string> OnTextChanged,
      CairoFont font)
      : base(capi, font, bounds)
    {
      this.highlightTexture = new LoadedTexture(capi);
      this.multilineMode = true;
      this.minHeight = bounds.fixedHeight;
      this.OnTextChanged = OnTextChanged;
    }

    internal override void TextChanged()
    {
      if (this.Autoheight)
        this.Bounds.fixedHeight = Math.Max(this.minHeight, this.textUtil.GetMultilineTextHeight(this.Font, string.Join("\n", (IEnumerable<string>) this.lines), this.Bounds.InnerWidth));
      this.Bounds.CalcWorldBounds();
      base.TextChanged();
    }

    public override void ComposeTextElements(Context ctx, ImageSurface surface)
    {
      this.EmbossRoundRectangleElement(ctx, this.Bounds, true, 3);
      ctx.SetSourceRGBA(0.0, 0.0, 0.0, 0.20000000298023224);
      this.ElementRoundRectangle(ctx, this.Bounds, true, 3.0);
      ctx.Fill();
      this.GenerateHighlight();
      this.RecomposeText();
    }

    private void GenerateHighlight()
    {
      ImageSurface surface = new ImageSurface(Format.Argb32, (int) this.Bounds.OuterWidth, (int) this.Bounds.OuterHeight);
      Context context = this.genContext(surface);
      context.SetSourceRGBA(1.0, 1.0, 1.0, 0.1);
      context.Paint();
      this.generateTexture(surface, ref this.highlightTexture);
      context.Dispose();
      surface.Dispose();
      this.highlightBounds = this.Bounds.FlatCopy();
      this.highlightBounds.CalcWorldBounds();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
      if (this.HasFocus)
        this.api.Render.Render2DTexturePremultipliedAlpha(this.highlightTexture.TextureId, this.highlightBounds);
      this.api.Render.Render2DTexturePremultipliedAlpha(this.textTexture.TextureId, this.Bounds);
      base.RenderInteractiveElements(deltaTime);
    }

    public override void Dispose()
    {
      base.Dispose();
      this.highlightTexture.Dispose();
    }

    public void SetFont(CairoFont cairoFont)
    {
      this.Font = cairoFont;
      this.caretHeight = cairoFont.GetFontExtents().Height;
    }

    internal override void RecomposeText()
    {
      base.RecomposeText();
    }
}
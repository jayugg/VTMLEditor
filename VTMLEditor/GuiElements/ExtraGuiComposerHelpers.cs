using System;
using Vintagestory.API.Client;

namespace VTMLEditor.GuiElements;

public static class ExtraGuiComposerHelpers
{
    /// <summary>Adds a text area with syntax highlighting to the GUI.</summary>
    /// <param name="composer"></param>
    /// <param name="bounds">The bounds of the Text Area</param>
    /// <param name="onTextChanged">The event fired when the text is changed.</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="key">The name of the text area.</param>
    public static GuiComposer AddVtmlEditorArea(
        this GuiComposer composer,
        ElementBounds bounds,
        Action<string> onTextChanged,
        CairoFont font = null,
        string key = null)
    {
        if (font == null)
            font = CairoFont.SmallTextInput();
        if (!composer.Composed)
            composer.AddInteractiveElement((GuiElement) new GuiElementEditorArea(composer.Api, bounds, onTextChanged, font), key);
        return composer;
    }
    
    /// <summary>Gets the vtml editor area by name.</summary>
    /// <param name="composer"></param>
    /// <param name="key">The name of the vtml editor area.</param>
    /// <returns>The named Text Area.</returns>
    public static GuiElementEditorArea GetVtmlEditorArea(this GuiComposer composer, string key)
    {
        return (GuiElementEditorArea) composer.GetElement(key);
    }
}
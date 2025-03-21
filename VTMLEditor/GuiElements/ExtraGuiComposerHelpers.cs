using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using VTMLEditor.EditorFeatures;

namespace VTMLEditor.GuiElements;

public static class ExtraGuiComposerHelpers
{
    /// <summary>Adds a text area with syntax highlighting to the GUI.</summary>
    /// <param name="composer"></param>
    /// <param name="bounds">The bounds of the Text Area</param>
    /// <param name="onTextChanged">The event fired when the text is changed.</param>
    /// <param name="themeColors">The theme colors for syntax highlighting.</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="key">The name of the text area.</param>
    public static GuiComposer AddVtmlEditorArea(
        this GuiComposer composer,
        ElementBounds bounds,
        Action<string> onTextChanged,
        Dictionary<VtmlTokenType, string?> themeColors,
        CairoFont? font = null,
        string? key = null)
    {
        font ??= CairoFont.SmallTextInput();
        if (!composer.Composed)
            composer.AddInteractiveElement(new GuiElementEditorArea(composer.Api, bounds, onTextChanged, font, themeColors), key);
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
    
    /// <summary>
    /// Adds a text area to the GUI.  
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="bounds">The bounds of the Text Area</param>
    /// <param name="onTextChanged">The event fired when the text is changed.</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="key">The name of the text area.</param>
    public static GuiComposer AddNewTextArea(
        this GuiComposer composer,
        ElementBounds bounds,
        Action<string> onTextChanged,
        CairoFont? font = null,
        string key = null)
    {
        font ??= CairoFont.SmallTextInput();

        if (!composer.Composed)
        {
            composer.AddInteractiveElement(new Vanilla.GuiElementTextArea(composer.Api, bounds, onTextChanged, font), key);
        }

        return composer;
    }

    /// <summary>
    /// Gets the text area by name.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="key">The name of the text area.</param>
    /// <returns>The named Text Area.</returns>
    public static Vanilla.GuiElementTextArea GetNewTextArea(this GuiComposer composer, string key)
    {
        return (Vanilla.GuiElementTextArea)composer.GetElement(key);
    }
    
    /// <summary>
    /// Adds a text input to the current GUI.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="bounds">The bounds of the text input.</param>
    /// <param name="onTextChanged">The event fired when the text is changed.</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="key">The name of this text component.</param>
    public static GuiComposer AddNewTextInput(this GuiComposer composer, ElementBounds bounds, Action<string> onTextChanged, CairoFont? font = null, string key = null)
    {
        font ??= CairoFont.TextInput();

        if (!composer.Composed)
        {
            composer.AddInteractiveElement(new Vanilla.GuiElementTextInput(composer.Api, bounds, onTextChanged, font), key);
        }

        return composer;
    }

    /// <summary>
    /// Gets the text input by input name.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="key">The name of the text input to get.</param>
    /// <returns>The named text input</returns>
    public static Vanilla.GuiElementTextInput GetNewTextInput(this GuiComposer composer, string key)
    {
        return (Vanilla.GuiElementTextInput)composer.GetElement(key);
    }
}
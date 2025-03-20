using System;
using System.Collections.Generic;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VTMLEditor.GuiElements.Vanilla;

#nullable disable
public abstract class GuiElementEditableTextBase : GuiElementTextBase
{
    public delegate bool OnTryTextChangeDelegate(List<string> lines);

    internal float[] caretColor = { 1, 1, 1, 1 };
    internal float[] selectionColor = { 217f/255, 131f/255, 36f/255, 0.5f };

    internal bool hideCharacters;
    internal bool multilineMode;
    internal int maxlines = 99999;

    internal double caretX, caretY;
    internal double topPadding;
    internal double leftPadding = 3;
    internal double rightSpacing;
    internal double bottomSpacing;

    internal LoadedTexture caretTexture;
    internal LoadedTexture textTexture;
    internal LoadedTexture selectionTexture;

    public Action<int, int> OnCaretPositionChanged;
    public Action<string> OnTextChanged;
    public OnTryTextChangeDelegate OnTryTextChangeText;
    public Action<double, double> OnCursorMoved;
    public Action<int, int, int, int> OnSelectionChanged;

    internal Action OnFocused = null;
    internal Action OnLostFocus = null;

    /// <summary>
    /// Called when a keyboard key was pressed, received and handled
    /// </summary>
    public Action OnKeyPressed;

    internal long caretBlinkMilliseconds;
    internal bool caretDisplayed;
    internal double caretHeight;

    internal double renderLeftOffset;
    internal Vec2i textSize = new Vec2i();

    protected List<string> lines;
    /// <summary>
    /// Contains the same as Lines, but may momentarily have different values when an edit is being made
    /// </summary>
    protected List<string> linesStaging;

    public bool WordWrap = true;

    public List<string> GetLines() => new(lines);

    public int TextLengthWithoutLineBreaks {
        get {
            int length = 0;
            for (int i = 0; i < lines.Count; i++) length += lines[i].Length;
            return length;
        }
    }

    public int CaretPosWithoutLineBreaks
    {
        get
        {
            int pos = 0;
            for (int i = 0; i < CaretPosLine; i++) pos += lines[i].Length;
            return pos + CaretPosInLine;
        }
        set
        {
            if (value < 0) { SetCaretPos(0, 0); return; }

            int sum = 0, i = 0;
            for (; i < lines.Count && sum + lines[i].Length <= value; i++)
            {
                sum += lines[i].Length;
            }

            if (i < lines.Count) SetCaretPos(value - sum, i);
            else SetCaretPos(sum, !multilineMode ? 0 : lines.Count);
        }
    }

    protected int pcaretPosLine;
    protected int pcaretPosInLine;
    public int CaretPosLine {
        get => pcaretPosLine;
        set => pcaretPosLine = value;
    }
    public int CaretPosInLine
    {
        get => pcaretPosInLine;
        set => pcaretPosInLine = Math.Min(value, lines[CaretPosLine].Length);
        /* j: why crash, when you can just clamp it to the line length?
        set
        {
            if (value > lines[CaretPosLine].Length) throw new IndexOutOfRangeException($"Caret @ {value} cannot be beyond current line length of {pcaretPosInLine}");
            pcaretPosInLine = Math.Min(value, lines[CaretPosLine].Length);
        }
        */
    }
    
    #region Selection
    
    protected int selectionStartLine;
    protected int selectionStartInLine;
    protected int selectionEndLine;
    protected int selectionEndInLine;

    public int SelectionStartLine
    {
        // If there is no selection, the start is the same as the end and is at the caret position
        get => HasSelection ? selectionStartLine : CaretPosLine;
        set => selectionStartLine = value;
    }

    public int SelectionStartInLine
    {
        // If there is no selection, the end is the same as the start and is at the caret position
        get => HasSelection ? selectionStartInLine : CaretPosInLine;
        // Clamp the value to the current line length
        set => selectionStartInLine = Math.Min(value, lines[CaretPosLine].Length);
    }

    public int SelectionEndLine
    {
        // If there is no selection, the end is the same as the start and is at the caret position
        get => HasSelection ? selectionEndLine : CaretPosLine;
        set => selectionEndLine = value;
    }

    public int SelectionEndInLine
    {
        // If there is no selection, the end is the same as the start and is at the caret position
        get => HasSelection ? selectionEndInLine : CaretPosInLine;
        // Clamp the value to the current line length
        set => selectionEndInLine = Math.Min(value, lines[CaretPosLine].Length);
    }
    
    // Helper method that calculates the global index given a line and position in that line.
    private int GetGlobalIndex(int line, int posInLine)
    {
        int index = 0;
        for (int i = 0; i < line; i++) index += lines[i].Length;
        return index + posInLine;
    }

    // Read-only property for selection start index without line breaks.
    public int SelectionStartWithoutLineBreaks => GetGlobalIndex(SelectionStartLine, SelectionStartInLine);

    // Read-only property for selection end index without line breaks.
    public int SelectionEndWithoutLineBreaks => GetGlobalIndex(SelectionEndLine, SelectionEndInLine);
    
    public int TrueSelectionStartWithoutLineBreaks => Math.Min(SelectionStartWithoutLineBreaks, SelectionEndWithoutLineBreaks);
    public int TrueSelectionEndWithoutLineBreaks => Math.Max(SelectionStartWithoutLineBreaks, SelectionEndWithoutLineBreaks);
    
    public ( (int line, int col) trueStart, (int line, int col) trueEnd ) GetTrueSelectionPositions()
    {
        int startLine = SelectionStartLine;
        int startCol = SelectionStartInLine;
        int endLine = SelectionEndLine;
        int endCol = SelectionEndInLine;

        if (startLine > endLine || (startLine == endLine && startCol > endCol))
        {
            // Swap the start with the end if the values are in reverse order.
            return ((endLine, endCol), (startLine, startCol));
        }
        return ((startLine, startCol), (endLine, endCol));
    }

    public bool HasSelection => selectionStartLine != selectionEndLine || selectionStartInLine != selectionEndInLine;

    public void ClearSelection()
    {
        selectionStartLine = selectionEndLine = CaretPosLine;
        selectionStartInLine = selectionEndInLine = CaretPosInLine;
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
    }
    
    public string GetSelectedText()
    {
        if (!HasSelection) return "";
        var fullText = GetText();
        return fullText.Substring(TrueSelectionStartWithoutLineBreaks, TrueSelectionEndWithoutLineBreaks - TrueSelectionStartWithoutLineBreaks);
    }

    #endregion

    public override bool Focusable
    {
        get { return true; }
    }

    /// <summary>
    /// Initializes the text component.
    /// </summary>
    /// <param name="capi">The Client API</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="bounds">The bounds of the component.</param>
    public GuiElementEditableTextBase(ICoreClientAPI capi, CairoFont font, ElementBounds bounds) : base(capi, "", font, bounds)
    {
        caretTexture = new LoadedTexture(capi);
        textTexture = new LoadedTexture(capi);
        selectionTexture = new LoadedTexture(capi);

        lines = new List<string> { "" };
        linesStaging = new List<string> { "" };
    }

    public override void OnFocusGained()
    {
        base.OnFocusGained();
        SetCaretPos(TextLengthWithoutLineBreaks);
        OnFocused?.Invoke();
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        ClearSelection();
        OnLostFocus?.Invoke();
    }

    /// <summary>
    /// Sets the position of the cursor at a given point.
    /// </summary>
    /// <param name="x">X position of the cursor.</param>
    /// <param name="y">Y position of the cursor.</param>
    public void SetCaretPos(double x, double y)
    {
        CaretPosLine = 0;

        ImageSurface surface = new ImageSurface(Format.Argb32, 1, 1);
        Context ctx = genContext(surface);
        Font.SetupContext(ctx);

        if (multilineMode)
        {
            double lineY = y / ctx.FontExtents.Height;
            if (lineY >= lines.Count)
            {
                CaretPosLine = lines.Count - 1;
                CaretPosInLine = lines[CaretPosLine].Length;

                ctx.Dispose();
                surface.Dispose();
                return;
            }

            CaretPosLine = Math.Max(0, (int)lineY);
        }

        string line = lines[CaretPosLine].TrimEnd('\r', '\n');
        CaretPosInLine = line.Length;

        for (int i = 0; i < line.Length; i++)
        {
            double posx = ctx.TextExtents(line.Substring(0, i+1)).XAdvance;

            if (x - posx <= 0)
            {
                CaretPosInLine = i;
                break;
            }
        }

        ctx.Dispose();
        surface.Dispose();

        SetCaretPos(CaretPosInLine, CaretPosLine);
    }


   
    /// <summary>
    /// Sets the position of the cursor to a specific character.
    /// </summary>
    /// <param name="posInLine">The position in the line.</param>
    /// <param name="posLine">The line of the text.</param>
    public void SetCaretPos(int posInLine, int posLine = 0)
    {
        caretBlinkMilliseconds = api.ElapsedMilliseconds;
        caretDisplayed = true;

        CaretPosLine = GameMath.Clamp(posLine, 0, lines.Count - 1);
        CaretPosInLine = GameMath.Clamp(posInLine, 0, lines[CaretPosLine].TrimEnd('\r', '\n').Length);


        if (multilineMode)
        {
            caretX = Font.GetTextExtents(lines[CaretPosLine].Substring(0, CaretPosInLine)).XAdvance;
            caretY = Font.GetFontExtents().Height * CaretPosLine;
        }
        else
        {
            string displayedText = lines[0];

            if (hideCharacters)
            {
                displayedText = new StringBuilder(lines[0]).Insert(0, "•", displayedText.Length).ToString();
            }

            caretX = Font.GetTextExtents(displayedText.Substring(0, CaretPosInLine)).XAdvance;
            caretY = 0;
        }

        OnCursorMoved?.Invoke(caretX, caretY);

        renderLeftOffset = Math.Max(0, caretX - Bounds.InnerWidth + rightSpacing);

        OnCaretPositionChanged?.Invoke(posLine, posInLine);
    }

    /// <summary>
    /// Sets a numerical value to the text, appending it to the end of the text.
    /// </summary>
    /// <param name="value">The value to add to the text.</param>
    public void SetValue(float value)
    {
        SetValue(value.ToString(GlobalConstants.DefaultCultureInfo));
    }

    /// <summary>
    /// Sets a numerical value to the text, appending it to the end of the text.
    /// </summary>
    /// <param name="value">The value to add to the text.</param>
    public void SetValue(double value)
    {
        SetValue(value.ToString(GlobalConstants.DefaultCultureInfo));
    }

    /// <summary>
    /// Sets given text, sets the cursor to the end of the text
    /// </summary>
    /// <param name="text"></param>
    /// <param name="setCaretPosToEnd"></param>
    public void SetValue(string text, bool setCaretPosToEnd = true)
    {
        LoadValue(Lineize(text));

        if (setCaretPosToEnd)
        {
            var endLine = lines[lines.Count - 1];
            var endPos = endLine.Length;
            SetCaretPos(endPos, lines.Count - 1);
        }
    }

    /// <summary>
    /// Sets given texts, leaves cursor position unchanged
    /// </summary>
    /// <param name="newLines"></param>
    public void LoadValue(List<string> newLines)
    {
        // Disallow edit if prevent by event or if it adds another line beyond max lines
        if (OnTryTextChangeText?.Invoke(newLines) == false || (newLines.Count > maxlines && newLines.Count >= lines.Count))
        {
            // Revert edits
            linesStaging = new List<string>(lines);
            return;
        }

        lines = new List<string>(newLines);
        linesStaging = new List<string>(lines);
        
        TextChanged();
    }

    public List<string> Lineize(string text)
    {
        if (text == null) text = "";

        List<string> lines = new List<string>();

        // We only allow Linux style newlines (only \n)
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        if (multilineMode)
        {
            double boxWidth = Bounds.InnerWidth - 2 * Bounds.absPaddingX;
            if (!WordWrap) boxWidth = 999999;

            TextLine[] textlines = textUtil.Lineize(Font, text, boxWidth, EnumLinebreakBehavior.Default, true);
            foreach (var val in textlines) lines.Add(val.Text);

            if (lines.Count == 0)
            {
                lines.Add("");
            }
        }
        else
        {
            lines.Add(text);
        }

        return lines;
    }


    internal virtual void TextChanged()
    {
        OnTextChanged?.Invoke(string.Join("", lines));
        RecomposeText();
    }

    internal virtual void RecomposeText()
    {
        Bounds.CalcWorldBounds();

        string displayedText = null;
        ImageSurface surface;

        if (multilineMode) {
            textSize.X = (int)(Bounds.OuterWidth - rightSpacing);
            textSize.Y = (int)(Bounds.OuterHeight - bottomSpacing);
            
        } else {
            displayedText = lines[0];

            if (hideCharacters)
            {
                displayedText = new StringBuilder(displayedText.Length).Insert(0, "•", displayedText.Length).ToString();
            }

            textSize.X = (int)Math.Max(Bounds.InnerWidth - rightSpacing, Font.GetTextExtents(displayedText).Width);
            textSize.Y = (int)(Bounds.InnerHeight - bottomSpacing);
        }

        surface = new ImageSurface(Format.Argb32, textSize.X, textSize.Y);

        Context ctx = genContext(surface);
        Font.SetupContext(ctx);

        double fontHeight = ctx.FontExtents.Height;
        
        if (multilineMode)
        {
            double width = Bounds.InnerWidth - 2 * Bounds.absPaddingX - rightSpacing;

            TextLine[] textlines = new TextLine[lines.Count];
            for (int i = 0; i < textlines.Length; i++)
            {
                textlines[i] = new TextLine()
                {
                    Text = lines[i].Replace("\r\n", "").Replace("\n", ""),
                    Bounds = new LineRectangled(0, i*fontHeight, Bounds.InnerWidth, fontHeight)
                };
            }

            textUtil.DrawMultilineTextAt(ctx, Font, textlines, Bounds.absPaddingX + leftPadding, Bounds.absPaddingY, width, EnumTextOrientation.Left);
        } else
        {
            this.topPadding = Math.Max(0, Bounds.OuterHeight - bottomSpacing - ctx.FontExtents.Height) / 2;
            textUtil.DrawTextLine(ctx, Font, displayedText, Bounds.absPaddingX + leftPadding, Bounds.absPaddingY + this.topPadding);
        }


        generateTexture(surface, ref textTexture);
        ctx.Dispose();
        surface.Dispose();

        if (caretTexture.TextureId == 0)
        {
            caretHeight = fontHeight;
            surface = new ImageSurface(Format.Argb32, (int)3.0, (int)fontHeight);
            ctx = genContext(surface);
            Font.SetupContext(ctx);

            ctx.SetSourceRGBA(caretColor[0], caretColor[1], caretColor[2], caretColor[3]);
            ctx.LineWidth = 1;
            ctx.NewPath();
            ctx.MoveTo(2, 0);
            ctx.LineTo(2, fontHeight);
            ctx.ClosePath();
            ctx.Stroke();

            generateTexture(surface, ref caretTexture.TextureId);

            ctx.Dispose();
            surface.Dispose();
        }
        if (selectionTexture.TextureId == 0)
        {
            surface = new ImageSurface(Format.Argb32, 1, 1);
            ctx = genContext(surface);
            Font.SetupContext(ctx);
            ctx.SetSourceRGBA(selectionColor[0], selectionColor[1], selectionColor[2], selectionColor[3]);
            ctx.Rectangle(0, 0, 1, 1);
            ctx.Fill();
            generateTexture(surface, ref selectionTexture.TextureId);
            ctx.Dispose();
            surface.Dispose();
        }
    }


    #region Mouse, Keyboard

    public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseDownOnElement(capi, args);
        SetCaretPos(args.X - Bounds.absX, args.Y - Bounds.absY);
        // Only reset selection anchor if shift is NOT pressed.
        if (!capi.Input.IsHotKeyPressed("shift"))
        {
            SelectionStartLine = CaretPosLine;
            SelectionStartInLine = CaretPosInLine;
        }
        // Always update the selection end.
        SelectionEndLine = CaretPosLine;
        SelectionEndInLine = CaretPosInLine;
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
    }

    public override void OnMouseUpOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseUpOnElement(capi, args);
        SetCaretPos(args.X - Bounds.absX, args.Y - Bounds.absY);
        // Always update the selection end on mouse up.
        SelectionEndLine = CaretPosLine;
        SelectionEndInLine = CaretPosInLine;
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
    }
    
    public override void OnMouseMove(ICoreClientAPI capi, MouseEvent args)
    {
        // Use capi.Input.MouseButton.Left because args.Button does not seem to work
        if (args.Handled || !HasFocus || !capi.Input.MouseButton.Left ) return;
        SetCaretPos(args.X - Bounds.absX, args.Y - Bounds.absY);
        SelectionEndLine = CaretPosLine;
        SelectionEndInLine = CaretPosInLine;
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
        args.Handled = true;
    }

    public override void OnKeyDown(ICoreClientAPI capi, KeyEvent args)
    {
        if (!HasFocus) return;
        
        bool handled = multilineMode || args.KeyCode != (int)GlKeys.Tab;

        switch (args.KeyCode)
        {
            case (int)GlKeys.LShift:
                handled = false;
                break;
            case (int)GlKeys.BackSpace:
                if (args.CtrlPressed) OnCtrlBackspace();
                else if (CaretPosWithoutLineBreaks > 0 || HasSelection) OnKeyBackSpace();
                break;
            case (int)GlKeys.Delete:
                if (CaretPosWithoutLineBreaks < TextLengthWithoutLineBreaks) OnKeyDelete();
                break;
            case (int)GlKeys.End:
                if (args.CtrlPressed) SetCaretPos(lines[^1].TrimEnd('\r', '\n').Length, lines.Count - 1);
                else SetCaretPos(lines[CaretPosLine].TrimEnd('\r', '\n').Length, CaretPosLine);
                UpdateSelectionOnKeyDown(args);
                capi.Gui.PlaySound("tick");
                break;
            case (int)GlKeys.Home:
                if (args.CtrlPressed) SetCaretPos(0);
                else SetCaretPos(0, CaretPosLine);
                UpdateSelectionOnKeyDown(args);
                capi.Gui.PlaySound("tick");
                break;
            case (int)GlKeys.Left:
                MoveCursor(-1, args.CtrlPressed);
                UpdateSelectionOnKeyDown(args);
                break;
            case (int)GlKeys.Right:
                MoveCursor(1, args.CtrlPressed);
                UpdateSelectionOnKeyDown(args);
                break;
            case (int)GlKeys.Down when CaretPosLine < lines.Count - 1:
                SetCaretPos(CaretPosInLine, CaretPosLine + 1);
                UpdateSelectionOnKeyDown(args);
                capi.Gui.PlaySound("tick");
                break;
            case (int)GlKeys.Up when CaretPosLine > 0:
                SetCaretPos(CaretPosInLine, CaretPosLine - 1);
                UpdateSelectionOnKeyDown(args);
                capi.Gui.PlaySound("tick");
                break;
            case (int)GlKeys.V when (args.CtrlPressed || args.CommandPressed):
                OnPaste(capi);
                break;
            case (int)GlKeys.C when (args.CtrlPressed || args.CommandPressed):
                OnCopy(capi);
                break;
            case (int)GlKeys.X when (args.CtrlPressed || args.CommandPressed):
                OnCopy(capi);
                DeleteSelection();
                break;
            case (int)GlKeys.Tab:
                InsertTextAtCurrentSel("    ");
                break;
            case (int)GlKeys.Enter:
            case (int)GlKeys.KeypadEnter:
                if (multilineMode) OnKeyEnter();
                else handled = false;
                break;
            case (int)GlKeys.Escape:
                handled = false;
                break;
        }
        args.Handled = handled;
    }

    private void OnCopy(ICoreClientAPI capi)
    {
        // Copy selected text if there is a selection; otherwise copy the full text.
        string copiedText = HasSelection ? GetSelectedText() : GetText();
        capi.Input.ClipboardText = copiedText;
    }

    private void OnPaste(ICoreClientAPI capi)
    {
        string insert = capi.Forms.GetClipboardText();
        insert = insert.Replace("\uFEFF", ""); // Remove UTF-8 BOM if present
        InsertTextAtCurrentSel(insert);
        capi.Gui.PlaySound("tick");
    }

    private void InsertTextAtCurrentSel(string insert)
    {
        string fulltext = GetText();
        int newCaretPos;

        // If there is a selection, replace the selected text
        if (HasSelection)
        {
            int start = SelectionStartWithoutLineBreaks;
            int end = SelectionEndWithoutLineBreaks;
            fulltext = fulltext.Substring(0, start) + insert + fulltext.Substring(end);
            newCaretPos = start + insert.Length;
        }
        else
        {
            int caretPos = CaretPosWithoutLineBreaks;
            fulltext = fulltext.Substring(0, caretPos) + insert + fulltext.Substring(caretPos);
            newCaretPos = caretPos + insert.Length;
        }

        // Update the text and move the caret to the new position
        SetValue(fulltext);
        CaretPosWithoutLineBreaks = newCaretPos;
        ClearSelection();
    }

    private void UpdateSelectionOnKeyDown(KeyEvent args)
    {
        if (args.ShiftPressed)
        {
            // When shift is pressed, extend the selection to the current caret position.
            SelectionEndLine = CaretPosLine;
            SelectionEndInLine = CaretPosInLine;
        }
        else
        {
            // Otherwise, collapse any active selection.
            ClearSelection();
        }
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
    }

    public override string GetText()
    {
        return string.Join("", lines);
    }

    private void OnKeyEnter()
    {
        // If there is an active selection, remove it first.
        if (HasSelection) DeleteSelection();

        if (lines.Count >= maxlines) return;

        string leftText = linesStaging[CaretPosLine].Substring(0, CaretPosInLine);
        string rightText = linesStaging[CaretPosLine].Substring(CaretPosInLine);

        linesStaging[CaretPosLine] = leftText + "\n";
        linesStaging.Insert(CaretPosLine + 1, rightText);

        if (OnTryTextChangeText?.Invoke(linesStaging) == false) return;

        lines = new List<string>(linesStaging);

        TextChanged();
        SetCaretPos(0, CaretPosLine + 1);
        api.Gui.PlaySound("tick");
    }

    private void DeleteSelection()
    {
        if (!HasSelection) return;
        int start = TrueSelectionStartWithoutLineBreaks;
        int end = TrueSelectionEndWithoutLineBreaks;
        string fulltext = GetText();
        fulltext = fulltext.Substring(0, start) + fulltext.Substring(end);
        LoadValue(Lineize(fulltext));
        // Set caret to the selection start position after deletion.
        CaretPosWithoutLineBreaks = start;
        ClearSelection();
    }

    private void OnKeyDelete()
    {
        if (HasSelection) DeleteSelection();
        else
        {
            string alltext = GetText();
            var caret = CaretPosWithoutLineBreaks;
            if (alltext.Length == caret) return;

            alltext = alltext.Substring(0, caret) + alltext.Substring(caret + 1, alltext.Length - caret - 1);
            LoadValue(Lineize(alltext));
        }
        api.Gui.PlaySound("tick");
    }
    
    private void OnCtrlBackspace()
    {
        string text = GetText();
        int caret = CaretPosWithoutLineBreaks;
        if (caret == 0) return;
        int start = caret - 1;
        // If the character immediately before the caret is whitespace,
        // move backward until non-whitespace is found.
        if (char.IsWhiteSpace(text[start]))
        {
            while (start >= 0 && char.IsWhiteSpace(text[start]))
            {
                start--;
            }
        }
        else
        {
            // Otherwise, move backward until whitespace is found.
            while (start >= 0 && !char.IsWhiteSpace(text[start]))
            {
                start--;
            }
        }
        int deleteStart = start + 1; // word boundary found
        // Remove the word preceding the caret.
        string newText = text.Substring(0, deleteStart) + text.Substring(caret);
        LoadValue(Lineize(newText));
        CaretPosWithoutLineBreaks = deleteStart;
        api.Gui.PlaySound("tick");
    }

    private void OnKeyBackSpace()
    {
        // Check if there is a selection and delete it.
        if (HasSelection) DeleteSelection();
        else
        {
            var caret = CaretPosWithoutLineBreaks;
            if (caret == 0) return;

            string alltext = GetText();
            alltext = alltext.Substring(0, caret - 1) + alltext.Substring(caret, alltext.Length - caret);
            LoadValue(Lineize(alltext));
            if (caret > 0)
            {
                CaretPosWithoutLineBreaks = caret - 1;
            }
        }
        api.Gui.PlaySound("tick");
    }

    public override void OnKeyPress(ICoreClientAPI capi, KeyEvent args)
    {
        if (!HasFocus) return;
        // If there's an active selection, delete it first.
        if (HasSelection) DeleteSelection();
        string newline = lines[CaretPosLine].Substring(0, CaretPosInLine) + args.KeyChar + lines[CaretPosLine].Substring(CaretPosInLine, lines[CaretPosLine].Length - CaretPosInLine);
        double width = Bounds.InnerWidth - 2 * Bounds.absPaddingX - rightSpacing;
        linesStaging[CaretPosLine] = newline;

        if (multilineMode)
        {
            var textExts = Font.GetTextExtents(newline.TrimEnd('\r', '\n'));
            bool lineOverFlow = textExts.Width >= width;
            if (lineOverFlow)
            {
                StringBuilder newLines = new StringBuilder();
                for (int i = 0; i < lines.Count; i++) newLines.Append(i == CaretPosLine ? newline : lines[i]);

                linesStaging = Lineize(newLines.ToString());

                if (lines.Count >= maxlines && linesStaging.Count >= maxlines) return;
            }
        }

        var cpos = CaretPosWithoutLineBreaks;
        LoadValue(linesStaging); // Ensures word wrapping
        CaretPosWithoutLineBreaks = cpos + 1;

        args.Handled = true;
        capi.Gui.PlaySound("tick");
        OnKeyPressed?.Invoke();
    }

    #endregion


    public override void RenderInteractiveElements(float deltaTime)
    {
        if (!HasFocus) return;
        // Render the selection box.
        if (HasSelection)
        {
            var ((selStartLine, selStartInLine), (selEndLine, selEndInLine)) = GetTrueSelectionPositions();
            for (int i = selStartLine; i <= selEndLine && i < lines.Count; i++)
            {
                double lineY = Bounds.renderY + topPadding + i * Font.GetFontExtents().Height;
                double lineHeight = Font.GetFontExtents().Ascent + Font.GetFontExtents().Descent;
                string lineText = lines[i];

                double selStartX, selEndX;
                // For the first line, start from the Caret selection position.
                if (i == selStartLine)
                {
                    int startIdx = Math.Min(selStartInLine, lineText.Length);
                    selStartX = Bounds.renderX + Bounds.absPaddingX + leftPadding +
                                Font.GetTextExtents(lineText.Substring(0, startIdx)).XAdvance;
                }
                else
                {
                    // For subsequent lines, start at left padding.
                    selStartX = Bounds.renderX + Bounds.absPaddingX + leftPadding;
                }

                // For the last line, use the selection end position.
                if (i == selEndLine)
                {
                    int endIdx = Math.Min(selEndInLine, lineText.Length);
                    selEndX = Bounds.renderX + Bounds.absPaddingX + leftPadding +
                              Font.GetTextExtents(lineText.Substring(0, endIdx)).XAdvance;
                }
                else
                {
                    // Give middle lines the full width of the component.
                    selEndX = Bounds.renderX + Bounds.absPaddingX + leftPadding + Bounds.InnerWidth;
                }

                double selWidth = selEndX - selStartX;
                api.Render.Render2DTexturePremultipliedAlpha(selectionTexture.TextureId, selStartX, lineY, selWidth, lineHeight);
            }
        }
        
        if (api.ElapsedMilliseconds - caretBlinkMilliseconds > 900)
        {
            caretBlinkMilliseconds = api.ElapsedMilliseconds;
            caretDisplayed = !caretDisplayed;
        }

        if (caretDisplayed && caretX - renderLeftOffset < Bounds.InnerWidth)
        {
            api.Render.Render2DTexturePremultipliedAlpha(caretTexture.TextureId, Bounds.renderX + caretX + scaled(1.5) - renderLeftOffset, Bounds.renderY + caretY + topPadding, 2, caretHeight);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        caretTexture.Dispose();
        textTexture.Dispose();
        selectionTexture.Dispose();
    }


    /// <summary>
    /// Moves the cursor forward and backward by an amount.
    /// </summary>
    /// <param name="dir">The direction to move the cursor.</param>
    /// <param name="wholeWord">Whether or not we skip entire words moving it.</param>
    public void MoveCursor(int dir, bool wholeWord = false)
    {
        bool done = false;
        bool moved = 
            ((CaretPosInLine > 0 || CaretPosLine > 0) && dir < 0) ||
            ((CaretPosInLine < lines[CaretPosLine].Length || CaretPosLine < lines.Count-1) && dir > 0)
        ;

        int newPos = CaretPosInLine;
        int newLine = CaretPosLine;

        while (!done) {
            newPos += dir;

            if (newPos < 0)
            {
                if (newLine <= 0) break;
                newLine--;
                newPos = lines[newLine].TrimEnd('\r', '\n').Length;
            } 

            if (newPos > lines[newLine].TrimEnd('\r', '\n').Length)
            {
                if (newLine >= lines.Count - 1) break;
                newPos = 0;
                newLine++;
            }

            done = !wholeWord || (newPos > 0 && lines[newLine][newPos - 1] == ' ');
        }

        if (moved)
        {
            SetCaretPos(newPos, newLine);
            SelectionEndLine = CaretPosLine;
            SelectionEndInLine = CaretPosInLine;
            OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
            api.Gui.PlaySound("tick");
        }
    }

    /// <summary>
    /// Sets the number of lines in the Text Area.
    /// </summary>
    /// <param name="maxlines">The maximum number of lines.</param>
    public void SetMaxLines(int maxlines)
    {
        this.maxlines = maxlines;
    }
    
    public void SetMaxHeight(int maxheight)
    {
        var fontExt = Font.GetFontExtents();
        this.maxlines = (int)Math.Floor(maxheight / fontExt.Height);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VTMLEditor.GuiElements.Vanilla;

#nullable disable
public abstract class GuiElementEditableTextBase : GuiElementTextBase
{
    public delegate bool OnTryTextChangeDelegate(List<string> lines);

    internal readonly float[] caretColor = { 1, 1, 1, 1 };
    internal readonly float[] selectionColor = { 217f/255, 131f/255, 36f/255, 0.5f };

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
    private int offscreenSurfaceTexId;

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
    
    private long mouseClickMilliseconds;
    private int mouseClickCount;

    internal double renderLeftOffset;
    internal Vec2i textSize = new Vec2i();

    protected List<string> lines;
    /// <summary>
    /// Contains the same as Lines, but may momentarily have different values when an edit is being made
    /// </summary>
    protected List<string> linesStaging;
    // Cached text to avoid joining lines every time GetText is called. Seems to improve performance.
    private string? cachedText;
    private bool isTextDirty = true;
    
    private UndoRedoManager undoRedoManager = new();

    public bool WordWrap = true;

    public List<string> GetLines() => new(lines);

    public int TextLengthWithoutLineBreaks => lines.Sum(line => line.Length);

    public int CaretPosWithoutLineBreaks
    {
        get => GetGlobalIndex(CaretPosLine, CaretPosInLine);
        set
        {
            if (value < 0) { SetCaretPos(0); return; }

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

    // Read-only property for selection start index without line breaks.
    private int SelectionStartWithoutLineBreaks => GetGlobalIndex(SelectionStartLine, SelectionStartInLine);

    // Read-only property for selection end index without line breaks.
    private int SelectionEndWithoutLineBreaks => GetGlobalIndex(SelectionEndLine, SelectionEndInLine);

    public int TrueSelectionStartWithoutLineBreaks => Math.Min(SelectionStartWithoutLineBreaks, SelectionEndWithoutLineBreaks);
    public int TrueSelectionEndWithoutLineBreaks => Math.Max(SelectionStartWithoutLineBreaks, SelectionEndWithoutLineBreaks);

    private ( (int line, int col) trueStart, (int line, int col) trueEnd ) GetTrueSelectionPositions()
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

    #endregion

    public override bool Focusable => true;

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
    
    #region Undo/Redo
    
    protected void SaveSnapshot()
    {
        var delta = UndoRedoManager.ComputeDelta(GetText(), string.Join("", linesStaging));
        undoRedoManager.SaveDelta(delta);
    }

    public void Undo()
    {
        var delta = undoRedoManager.Undo();
        if (delta != null)
        {
            ApplyDelta(delta, true);
        }
    }

    public void Redo()
    {
        var delta = undoRedoManager.Redo();
        if (delta != null)
        {
            ApplyDelta(delta, false);
        }
    }
    
    private void ApplyDelta(TextDelta delta, bool isUndo)
    {
        string allText = GetText();
        int start = Math.Max(0, Math.Min(delta.StartIndex, allText.Length));
        string expectedText = isUndo ? delta.AddedText : delta.RemovedText;
        int deleteLength = Math.Max(0, Math.Min(expectedText.Length, allText.Length - start));
        string insertText = isUndo ? delta.RemovedText : delta.AddedText;

        string newText = allText.Remove(start, deleteLength).Insert(start, insertText);
        if (newText == allText) return;

        List<string> newLines = Lineize(newText);
        if (OnTryTextChangeText?.Invoke(newLines) == false) return;
        lines = new List<string>(newLines);
        linesStaging = new List<string>(newLines);

        CaretPosWithoutLineBreaks = start + insertText.Length;
        SelectionStartInLine = SelectionEndInLine = CaretPosInLine;
        SelectionStartLine = SelectionEndLine = CaretPosLine;
        TextChanged();
    }

    #endregion
    
    private void InvalidateCachedText()
    {
        isTextDirty = true;
    }
    
    public override string GetText()
    {
        if (isTextDirty || cachedText == null)
        {
            // Joining lines only when necessary.
            cachedText = string.Join("", lines);
            isTextDirty = false;
        }
        return cachedText;
    }
    
    public string GetSelectedText()
    {
        if (!HasSelection) return "";
        var fullText = GetText();
        return fullText.Substring(TrueSelectionStartWithoutLineBreaks, TrueSelectionEndWithoutLineBreaks - TrueSelectionStartWithoutLineBreaks);
    }
    
    // Helper method that calculates the global index given a line and position in that line.
    private int GetGlobalIndex(int line, int posInLine)
    {
        int index = 0;
        for (int i = 0; i < line; i++) index += lines[i].Length;
        return index + posInLine;
    }

    public List<string> Lineize(string textIn)
    {
        textIn ??= "";

        List<string> textLines = new List<string>();

        // We only allow Linux style newlines (only \n)
        textIn = textIn.Replace("\r\n", "\n").Replace('\r', '\n');

        if (multilineMode)
        {
            double boxWidth = Bounds.InnerWidth - 2 * Bounds.absPaddingX;
            if (!WordWrap) boxWidth = 999999;

            TextLine[] textlines = textUtil.Lineize(Font, textIn, boxWidth, EnumLinebreakBehavior.Default, true);
            foreach (var val in textlines) textLines.Add(val.Text);

            if (textLines.Count == 0)
            {
                textLines.Add("");
            }
        }
        else
        {
            textLines.Add(textIn);
        }

        return textLines;
    }
    
    // Reworked to set caret to the nearest valid position
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
        double validY = Math.Max(0, Math.Min(y, (lines.Count - 1) * ctx.FontExtents.Height));
        int lineIndex = Math.Min(lines.Count - 1, (int)(validY / ctx.FontExtents.Height));
        string line = lines[lineIndex].TrimEnd('\r', '\n');

        double validX = Math.Max(0, x);
        double currentWidth = ctx.TextExtents(line).XAdvance;
        if (validX > currentWidth) validX = currentWidth;

        int charIndex = line.Length;
        for (int i = 0; i < line.Length; i++)
        {
            double widthUpToChar = ctx.TextExtents(line[..(i+1)]).XAdvance;
            if (validX < widthUpToChar)
            {
                charIndex = i;
                break;
            }
        }
        ctx.Dispose();
        surface.Dispose();
        SetCaretPos(charIndex, lineIndex);
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
    /// <param name="textIn"></param>
    /// <param name="setCaretPosToEnd"></param>
    public void SetValue(string textIn, bool setCaretPosToEnd = true)
    {
        LoadValue(Lineize(textIn));

        if (setCaretPosToEnd)
        {
            var endLine = lines[^1];
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
        linesStaging = new List<string>(newLines);
        SaveSnapshot();
        lines = new List<string>(linesStaging);
        TextChanged();
    }
    
    public void ClearSelection()
    {
        selectionStartLine = selectionEndLine = CaretPosLine;
        selectionStartInLine = selectionEndInLine = CaretPosInLine;
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
    }
    
    /// <summary>
    /// Inserts text at the current caret position. If there is a selection, the selected text is replaced.
    /// </summary>
    /// <param name="insert"> The text to insert. </param>
    public void InsertTextAtCursor(string insert)
    {
        string fulltext = GetText();
        int newCaretPos;

        // If there is a selection, replace the selected text
        if (HasSelection)
        {
            int start = SelectionStartWithoutLineBreaks;
            int end = SelectionEndWithoutLineBreaks;
            fulltext = fulltext[..start] + insert + fulltext[end..];
            newCaretPos = start + insert.Length;
        }
        else
        {
            int caretPos = CaretPosWithoutLineBreaks;
            fulltext = fulltext[..caretPos] + insert + fulltext[caretPos..];
            newCaretPos = caretPos + insert.Length;
        }

        // Update the text and move the caret to the new position
        SetValue(fulltext);
        CaretPosWithoutLineBreaks = newCaretPos;
        ClearSelection();
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
    
    /// <summary>
    /// Sets the number of lines in the Text Area.
    /// </summary>
    /// <param name="maxLines">The maximum number of lines.</param>
    public void SetMaxLines(int maxLines)
    {
        this.maxlines = maxLines;
    }
    
    public void SetMaxHeight(int maxheight)
    {
        var fontExt = Font.GetFontExtents();
        this.maxlines = (int)Math.Floor(maxheight / fontExt.Height);
    }

    /// <summary>
    /// Moves the cursor forward and backward by an amount.
    /// </summary>
    /// <param name="dir">The direction to move the cursor.</param>
    /// <param name="wholeWord">Whether we skip entire words moving it.</param>
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

        if (!moved) return;
        SetCaretPos(newPos, newLine);
        SelectionEndLine = CaretPosLine;
        SelectionEndInLine = CaretPosInLine;
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
        api.Gui.PlaySound("tick");
    }
    
    private bool IsWordChar(char c)
    {
        // Adjust the conditions to suit your needs.
        return !char.IsPunctuation(c) && !char.IsWhiteSpace(c) && !char.IsSeparator(c) && !char.IsControl(c) && !char.IsSymbol(c);
    }
    
    internal virtual void TextChanged()
    {
        InvalidateCachedText();
        OnTextChanged?.Invoke(GetText());
        RecomposeText();
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

    #region Mouse, Keyboard

    public override void OnMouseDownOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseDownOnElement(capi, args);
        SetCaretPos(args.X - Bounds.absX, args.Y - Bounds.absY);
        if (capi.Input.IsHotKeyPressed("shift"))
        {
            SelectionEndLine = CaretPosLine;
            SelectionEndInLine = CaretPosInLine;
            OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
            return;
        }
        long now = api.ElapsedMilliseconds;
        if (now - mouseClickMilliseconds < 250 && hasFocus) mouseClickCount++;
        else mouseClickCount = 0;
        mouseClickMilliseconds = now;
        switch (mouseClickCount)
        {
            case 2: OnMouseTripleClick(); break;
            case 1: OnMouseDoubleClick(); break;
            default:
                SelectionStartLine = SelectionEndLine = CaretPosLine;
                SelectionStartInLine = SelectionEndInLine = CaretPosInLine;
                break;
        }
        OnSelectionChanged?.Invoke(SelectionStartLine, SelectionStartInLine, SelectionEndLine, SelectionEndInLine);
    }
    
    public override void OnMouseUpOnElement(ICoreClientAPI capi, MouseEvent args)
    {
        base.OnMouseUpOnElement(capi, args);
    }
    
    private CancellationTokenSource mouseDoubleClickCTS;

    private void OnMouseDoubleClick()
    {
        // Find word boundaries around the caret position
        string line = lines[CaretPosLine];
        int start = CaretPosInLine;
        while (start > 0 && IsWordChar(line[start - 1])) start--;
        int end = CaretPosInLine;
        while (end < line.Length && IsWordChar(line[end])) end++;
        SelectionStartLine = CaretPosLine;
        SelectionStartInLine = start;
        SelectionEndLine = CaretPosLine;
        SelectionEndInLine = end;
        SetCaretPos(SelectionEndInLine, SelectionEndLine);
    }

    private void OnMouseTripleClick()
    {
        // Select the entire line
        SelectionStartLine = CaretPosLine;
        SelectionStartInLine = 0;
        SelectionEndLine = CaretPosLine;
        SelectionEndInLine = lines[CaretPosLine].Length;
        SetCaretPos(SelectionEndInLine, SelectionEndLine);
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
                if (args.CtrlPressed) OnCtrlDelete();
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
                if (args.CtrlPressed) SetCaretPos(caretX, caretY + Font.GetFontExtents().Height);
                else SetCaretPos(CaretPosInLine, CaretPosLine + 1);
                UpdateSelectionOnKeyDown(args);
                capi.Gui.PlaySound("tick");
                break;
            case (int)GlKeys.Up when CaretPosLine > 0:
                if (args.CtrlPressed) SetCaretPos(caretX, caretY - Font.GetFontExtents().Height);
                else SetCaretPos(CaretPosInLine, CaretPosLine - 1);
                UpdateSelectionOnKeyDown(args);
                capi.Gui.PlaySound("tick");
                break;
            case (int)GlKeys.A when args.CtrlPressed || args.CommandPressed:
                SetCaretPos(lines[^1].Length, lines.Count - 1);
                SelectionStartLine = 0;
                SelectionStartInLine = 0;
                SelectionEndLine = CaretPosLine;
                SelectionEndInLine = CaretPosInLine;
                break;
            case (int)GlKeys.V when args.CtrlPressed || args.CommandPressed:
                OnPaste(capi);
                break;
            case (int)GlKeys.C when args.CtrlPressed || args.CommandPressed:
                OnCopy(capi);
                break;
            case (int)GlKeys.X when args.CtrlPressed || args.CommandPressed:
                OnCopy(capi);
                DeleteSelection();
                break;
            case (int)GlKeys.Y when args.CtrlPressed || args.CommandPressed:
            case (int)GlKeys.Z when (args.CtrlPressed || args.CommandPressed) && args.ShiftPressed:
                Redo();
                break;
            case (int)GlKeys.Z when args.CtrlPressed || args.CommandPressed:
                Undo();
                break;
            case (int)GlKeys.Tab:
                InsertTextAtCursor("    ");
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
        InsertTextAtCursor(insert);
        capi.Gui.PlaySound("tick");
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

    private void OnKeyEnter()
    {
        // If there is an active selection, remove it first.
        if (HasSelection) DeleteSelection();
        if (lines.Count >= maxlines) return;

        string leftText = linesStaging[CaretPosLine][..CaretPosInLine];
        string rightText = linesStaging[CaretPosLine][CaretPosInLine..];

        linesStaging[CaretPosLine] = leftText + "\n";
        linesStaging.Insert(CaretPosLine + 1, rightText);
        
        LoadValue(linesStaging);
        SetCaretPos(0, CaretPosLine + 1);
        api.Gui.PlaySound("tick");
    }

    private void OnKeyDelete()
    {
        if (HasSelection) DeleteSelection();
        else
        {
            string alltext = GetText();
            var caret = CaretPosWithoutLineBreaks;
            if (alltext.Length == caret) return;

            alltext = alltext[..caret] + alltext.Substring(caret + 1, alltext.Length - caret - 1);
            var newLines = Lineize(alltext);
            LoadValue(newLines);
        }
        api.Gui.PlaySound("tick");
    }
    
    private void OnCtrlBackspace()
    {
        if (HasSelection) DeleteSelection();
        string allText = GetText();
        int caret = CaretPosWithoutLineBreaks;
        if (caret == 0) return;
        int start = caret - 1;
        // If the character immediately before the caret is whitespace,
        // move backward until non-whitespace is found.
        if (char.IsWhiteSpace(allText[start]))
        {
            while (start >= 0 && char.IsWhiteSpace(allText[start]))
            {
                start--;
            }
        }
        else
        {
            // Otherwise, move backward until whitespace is found.
            while (start >= 0 && !char.IsWhiteSpace(allText[start]))
            {
                start--;
            }
        }
        int deleteStart = start + 1; // word boundary found
        // Remove the word preceding the caret.
        string newText = allText[..deleteStart] + allText[caret..];
        LoadValue(Lineize(newText));
        CaretPosWithoutLineBreaks = deleteStart;
        api.Gui.PlaySound("tick");
    }

    private void OnCtrlDelete()
    {
        if (HasSelection) DeleteSelection();
        string allText = GetText();
        int caret = CaretPosWithoutLineBreaks;
        if (caret == allText.Length) return;
        int start = caret;
        // If the character immediately after the caret is whitespace,
        // move forward until non-whitespace is found.
        if (char.IsWhiteSpace(allText[start]))
        {
            while (start < allText.Length && char.IsWhiteSpace(allText[start]))
            {
                start++;
            }
        }
        else
        {
            // Otherwise, move forward until whitespace is found.
            while (start < allText.Length && !char.IsWhiteSpace(allText[start]))
            {
                start++;
            }
        }
        int deleteEnd = start; // word boundary found
        // Remove the word following the caret.
        string newText = allText.Substring(0, caret) + allText.Substring(deleteEnd);
        LoadValue(Lineize(newText));
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

    internal virtual void RecomposeText()
    {
        Bounds.CalcWorldBounds();

        string displayedText = "";

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

        var surface = new ImageSurface(Format.Argb32, textSize.X, textSize.Y);

        Context ctx = genContext(surface);
        Font.SetupContext(ctx);

        double fontHeight = ctx.FontExtents.Height;
        
        if (multilineMode)
        {
            double width = Bounds.InnerWidth - 2 * Bounds.absPaddingX - rightSpacing;
            this.RenderMultilineText(ctx, this.Bounds.absPaddingX + this.leftPadding, this.Bounds.absPaddingY, fontHeight, width);
        } else
        {
            this.topPadding = Math.Max(0, Bounds.OuterHeight - bottomSpacing - fontHeight) / 2;
            this.DrawTextLineAt(ctx, displayedText, Bounds.absPaddingX + leftPadding, Bounds.absPaddingY + this.topPadding);
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
    
    /// <summary>
    /// Renders the text in a multiline format. Virtual to allow for overriding. (e.g. for custom text rendering)
    /// </summary>
    /// <param name="ctx">The context of the text</param>
    /// <param name="paddingX">X padding</param>
    /// <param name="paddingY">Y padding</param>
    /// <param name="fontHeight">Font height</param>
    /// <param name="width">Line width</param>
    public virtual void RenderMultilineText(Context ctx, double paddingX, double paddingY, double fontHeight, double width)
    {
        TextLine[] textlines = new TextLine[lines.Count];
        for (int i = 0; i < textlines.Length; i++)
        {
            textlines[i] = new TextLine()
            {
                Text = lines[i].Replace("\r\n", "").Replace("\n", ""),
                Bounds = new LineRectangled(0, i*fontHeight, Bounds.InnerWidth, fontHeight)
            };
        }
        this.textUtil.DrawMultilineTextAt(ctx, this.Font, textlines, paddingX, paddingY, width);
    }
    
    // Rendering with one single surface will prevent overlaps/empty gaps between selection lines
    private void RenderOffscreenSelection(ICoreClientAPI api)
    {
        int offWidth = (int)Math.Ceiling(Bounds.InnerWidth);
        int offHeight = (int)Math.Ceiling(Bounds.OuterHeight);
        using (var offscreen = new ImageSurface(Format.Argb32, offWidth, offHeight))
        using (var offCtx = new Context(offscreen))
        {
            // Clear the offscreen surface.
            offCtx.Operator = Operator.Source;
            offCtx.SetSourceRGBA(0, 0, 0, 0);
            offCtx.Rectangle(0, 0, offWidth, offHeight);
            offCtx.Fill();

            double fontHeight = Font.GetFontExtents().Height;
            var ((selStartLine, selStartInLine), (selEndLine, selEndInLine)) = GetTrueSelectionPositions();
            for (int i = selStartLine; i <= selEndLine && i < lines.Count; i++)
            {
                double lineY = topPadding + i * fontHeight;
                string lineText = lines[i];
                double selStartX = i == selStartLine
                    ? leftPadding + Font.GetTextExtents(lineText.Substring(0, Math.Min(selStartInLine, lineText.Length))).XAdvance
                    : leftPadding;
                double selEndX = i == selEndLine
                    ? leftPadding + Font.GetTextExtents(lineText.Substring(0, Math.Min(selEndInLine, lineText.Length))).XAdvance
                    : leftPadding + Bounds.InnerWidth;
                offCtx.Rectangle(selStartX, lineY, selEndX - selStartX, fontHeight);
            }
            offCtx.SetSourceRGBA(selectionColor[0], selectionColor[1], selectionColor[2], selectionColor[3]);
            offCtx.Fill();
            generateTexture(offscreen, ref offscreenSurfaceTexId);
        }
        // Render the composed offscreen selection.
        api.Render.Render2DTexturePremultipliedAlpha(offscreenSurfaceTexId, Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.OuterHeight);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (!HasFocus) return;
        if (HasSelection)
        {
            RenderOffscreenSelection(api);
        }

        if (api.ElapsedMilliseconds - caretBlinkMilliseconds > 900)
        {
            caretBlinkMilliseconds = api.ElapsedMilliseconds;
            caretDisplayed = !caretDisplayed;
        }

        if (caretDisplayed && caretX - renderLeftOffset < Bounds.InnerWidth)
        {
            api.Render.Render2DTexturePremultipliedAlpha(caretTexture.TextureId,
                Bounds.renderX + caretX + scaled(1.5) - renderLeftOffset,
                Bounds.renderY + caretY + topPadding, 2, caretHeight);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        caretTexture.Dispose();
        textTexture.Dispose();
        selectionTexture.Dispose();
        undoRedoManager.Dispose();
    }
}
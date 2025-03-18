using System;
using System.Collections.Generic;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using VTMLEditor.TextHighlighting;

namespace VTMLEditor.GuiElements;

// This class is a flattened copy of GuiElementEditableTextBase and GuiElementTextBase
public class GuiElementEditorAreaBase : GuiElementControl
{
    public TextDrawUtil textUtil;
    protected string text;
    /// <summary>The font of the Text Element.</summary>
    public CairoFont Font;
    public Dictionary<VtmlTokenType, string?>  ThemeColors { get; set; }

    public string Text
    {
    get => this.text;
    set => this.text = value;
    }

    /// <summary>Creates a new text based element.</summary>
    /// <param name="capi">The Client API</param>
    /// <param name="text">The text of this element.</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="bounds">The bounds of the element.</param>
    public GuiElementEditorAreaBase(
    ICoreClientAPI capi,
    string text,
    CairoFont font,
    ElementBounds bounds)
    : base(capi, bounds)
    {
    this.Font = font;
    this.textUtil = new TextDrawUtil();
    this.text = text;
    }

    /// <summary>Initializes the text component.</summary>
    /// <param name="capi">The Client API</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="bounds">The bounds of the component.</param>
    public GuiElementEditorAreaBase(
      ICoreClientAPI capi,
      CairoFont font,
      ElementBounds bounds,
      Dictionary<VtmlTokenType, string?>  themeColors)
    : this(capi, "", font, bounds)
    {
    this.caretTexture = new LoadedTexture(capi);
    this.textTexture = new LoadedTexture(capi);
    this.lines = new List<string>() { "" };
    this.linesStaging = new List<string>() { "" };
    this.ThemeColors = themeColors;
    }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
    this.Font.SetupContext(ctx);
    this.Bounds.CalcWorldBounds();
    this.ComposeTextElements(ctx, surface);
    }

    public virtual void ComposeTextElements(Context ctx, ImageSurface surface)
    {
    }
    
    /// <summary>Gets the text on the element.</summary>
    /// <returns>The text of the element.</returns>
    public string GetText() => string.Join("", this.lines);
    

    internal float[] caretColor = new float[]
    {
      1f,
      1f,
      1f,
      1f
    };
    internal bool hideCharacters;
    internal bool multilineMode;
    internal int maxlines = 99999;
    internal double caretX;
    internal double caretY;
    internal double topPadding;
    internal double leftPadding = 3.0;
    internal double rightSpacing;
    internal double bottomSpacing;
    internal LoadedTexture caretTexture;
    internal LoadedTexture textTexture;
    public Action<int, int> OnCaretPositionChanged;
    public Action<string> OnTextChanged;
    public GuiElementEditableTextBase.OnTryTextChangeDelegate OnTryTextChangeText;
    public Action<double, double> OnCursorMoved;
    internal Action OnFocused;
    internal Action OnLostFocus;
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
    protected int pcaretPosLine;
    protected int pcaretPosInLine;

    public int TextLengthWithoutLineBreaks
    {
      get
      {
        int withoutLineBreaks = 0;
        for (int index = 0; index < this.lines.Count; ++index)
          withoutLineBreaks += this.lines[index].Length;
        return withoutLineBreaks;
      }
    }

    public int CaretPosWithoutLineBreaks
    {
      get
      {
        int num = 0;
        for (int index = 0; index < this.CaretPosLine; ++index)
          num += this.lines[index].Length;
        return num + this.CaretPosInLine;
      }
      set
      {
        int posInLine = 0;
        for (int index = 0; index < this.lines.Count; ++index)
        {
          int length = this.lines[index].Length;
          if (posInLine + length > value)
          {
            this.SetCaretPos(value - posInLine, index);
            return;
          }
          posInLine += length;
        }
        if (!this.multilineMode)
          this.SetCaretPos(posInLine);
        else
          this.SetCaretPos(posInLine, this.lines.Count);
      }
    }

    public int CaretPosLine
    {
      get => this.pcaretPosLine;
      set => this.pcaretPosLine = value;
    }

    public int CaretPosInLine
    {
      get => this.pcaretPosInLine;
      set
      {
        if (value > this.lines[this.CaretPosLine].Length)
          throw new IndexOutOfRangeException("Caret @" + value.ToString() + ", cannot beyond current line length of " + this.pcaretPosInLine.ToString());
        this.pcaretPosInLine = value;
      }
    }

    public override bool Focusable => true;

    public override void OnFocusGained()
    {
      base.OnFocusGained();
      this.SetCaretPos(this.TextLengthWithoutLineBreaks);
      Action onFocused = this.OnFocused;
      if (onFocused == null)
        return;
      onFocused();
    }

    public override void OnFocusLost()
    {
      base.OnFocusLost();
      Action onLostFocus = this.OnLostFocus;
      if (onLostFocus == null)
        return;
      onLostFocus();
    }

    /// <summary>Sets the position of the cursor at a given point.</summary>
    /// <param name="x">X position of the cursor.</param>
    /// <param name="y">Y position of the cursor.</param>
    public void SetCaretPos(double x, double y)
    {
      this.CaretPosLine = 0;
      ImageSurface surface = new ImageSurface(Format.Argb32, 1, 1);
      Context ctx = this.genContext(surface);
      this.Font.SetupContext(ctx);
      if (this.multilineMode)
      {
        double val2 = y / ctx.FontExtents.Height;
        if (val2 > this.lines.Count)
        {
          this.CaretPosLine = this.lines.Count - 1;
          this.CaretPosInLine = this.lines[this.CaretPosLine].Length;
          ctx.Dispose();
          surface.Dispose();
          return;
        }
        this.CaretPosLine = Math.Max(0, (int) val2);
      }
      string str = this.lines[this.CaretPosLine].TrimEnd('\r', '\n');
      this.CaretPosInLine = str.Length;
      for (int index = 0; index < str.Length; ++index)
      {
        double xadvance = ctx.TextExtents(str.Substring(0, index + 1)).XAdvance;
        if (x - xadvance <= 0.0)
        {
          this.CaretPosInLine = index;
          break;
        }
      }
      ctx.Dispose();
      surface.Dispose();
      this.SetCaretPos(this.CaretPosInLine, this.CaretPosLine);
    }

    /// <summary>
    /// Sets the position of the cursor to a specific character.
    /// </summary>
    /// <param name="posInLine">The position in the line.</param>
    /// <param name="posLine">The line of the text.</param>
    public void SetCaretPos(int posInLine, int posLine = 0)
    {
      this.caretBlinkMilliseconds = this.api.ElapsedMilliseconds;
      this.caretDisplayed = true;
      this.CaretPosLine = GameMath.Clamp(posLine, 0, this.lines.Count - 1);
      this.CaretPosInLine = GameMath.Clamp(posInLine, 0, this.lines[this.CaretPosLine].TrimEnd('\r', '\n').Length);
      if (this.multilineMode)
      {
        this.caretX = this.Font.GetTextExtents(this.lines[this.CaretPosLine].Substring(0, this.CaretPosInLine)).XAdvance;
        this.caretY = this.Font.GetFontExtents().Height * this.CaretPosLine;
      }
      else
      {
        string line = this.lines[0];
        if (this.hideCharacters)
          line = new StringBuilder(this.lines[0]).Insert(0, "•", line.Length).ToString();
        this.caretX = this.Font.GetTextExtents(line.Substring(0, this.CaretPosInLine)).XAdvance;
        this.caretY = 0.0;
      }
      Action<double, double> onCursorMoved = this.OnCursorMoved;
      if (onCursorMoved != null)
        onCursorMoved(this.caretX, this.caretY);
      this.renderLeftOffset = Math.Max(0.0, this.caretX - this.Bounds.InnerWidth + this.rightSpacing);
      Action<int, int> caretPositionChanged = this.OnCaretPositionChanged;
      if (caretPositionChanged == null)
        return;
      caretPositionChanged(posLine, posInLine);
    }

    /// <summary>
    /// Sets given text, sets the cursor to the end of the text
    /// </summary>
    /// <param name="text"></param>
    /// <param name="setCaretPosToEnd"></param>
    public void SetValue(string text, bool setCaretPosToEnd = true)
    {
      this.LoadValue(this.Lineize(text));
      if (!setCaretPosToEnd)
        return;
      this.SetCaretPos(this.lines[this.lines.Count - 1].Length, this.lines.Count - 1);
    }

    /// <summary>Sets given texts, leaves cursor position unchanged</summary>
    /// <param name="newLines"></param>
    public void LoadValue(List<string> newLines)
    {
      GuiElementEditableTextBase.OnTryTextChangeDelegate tryTextChangeText = this.OnTryTextChangeText;
      if ((tryTextChangeText != null ? (!tryTextChangeText(newLines) ? 1 : 0) : 0) != 0 || newLines.Count > this.maxlines && newLines.Count >= this.lines.Count)
      {
        this.linesStaging = new List<string>(this.lines);
      }
      else
      {
        this.lines = new List<string>(newLines);
        this.linesStaging = new List<string>(this.lines);
        this.TextChanged();
      }
    }

    public List<string> Lineize(string text)
    {
      if (text == null)
        text = "";
      List<string> stringList = new List<string>();
      text = text.Replace("\r\n", "\n").Replace('\r', '\n');
      if (this.multilineMode)
      {
        double boxWidth = this.Bounds.InnerWidth - 2.0 * this.Bounds.absPaddingX;
        if (!this.WordWrap)
          boxWidth = 999999.0;
        foreach (TextLine textLine in this.textUtil.Lineize(this.Font, text, boxWidth, keepLinebreakChar: true))
          stringList.Add(textLine.Text);
        if (stringList.Count == 0)
          stringList.Add("");
      }
      else
        stringList.Add(text);
      return stringList;
    }

    internal virtual void TextChanged()
    {
      Action<string> onTextChanged = this.OnTextChanged;
      if (onTextChanged != null)
        onTextChanged(string.Join("", this.lines));
      this.RecomposeText();
    }

    internal virtual void RecomposeText()
    {
      this.Bounds.CalcWorldBounds();
      string text = null;
      if (this.multilineMode)
      {
        this.textSize.X = (int) (this.Bounds.OuterWidth - this.rightSpacing);
        this.textSize.Y = (int) (this.Bounds.OuterHeight - this.bottomSpacing);
      }
      else
      {
        text = this.lines[0];
        if (this.hideCharacters)
          text = new StringBuilder(text.Length).Insert(0, "•", text.Length).ToString();
        this.textSize.X = (int) Math.Max(this.Bounds.InnerWidth - this.rightSpacing, this.Font.GetTextExtents(text).Width);
        this.textSize.Y = (int) (this.Bounds.InnerHeight - this.bottomSpacing);
      }
      ImageSurface surface1 = new ImageSurface(Format.Argb32, this.textSize.X, this.textSize.Y);
      Context ctx1 = this.genContext(surface1);
      this.Font.SetupContext(ctx1);
      FontExtents fontExtents = ctx1.FontExtents;
      double height1 = fontExtents.Height;
      if (this.multilineMode)
      {
        double boxWidth = this.Bounds.InnerWidth - 2.0 * this.Bounds.absPaddingX - this.rightSpacing;
        TextLine[] lines = new TextLine[this.lines.Count];
        for (int index = 0; index < lines.Length; ++index)
          lines[index] = new TextLine()
          {
            Text = this.lines[index].Replace("\r\n", "").Replace("\n", ""),
            Bounds = new LineRectangled(0.0, index * height1, this.Bounds.InnerWidth, height1)
          };
        this.textUtil.DrawMultilineTextHighlightedAt(ThemeColors, ctx1, this.Font, lines, this.Bounds.absPaddingX + this.leftPadding, this.Bounds.absPaddingY, boxWidth);
      }
      else
      {
        double num = this.Bounds.OuterHeight - this.bottomSpacing;
        fontExtents = ctx1.FontExtents;
        double height2 = fontExtents.Height;
        this.topPadding = Math.Max(0.0, num - height2) / 2.0;
        this.textUtil.DrawTextLineHighlighted(ThemeColors, ctx1, this.Font, text, this.Bounds.absPaddingX + this.leftPadding, this.Bounds.absPaddingY + this.topPadding);
      }
      this.generateTexture(surface1, ref this.textTexture);
      ctx1.Dispose();
      surface1.Dispose();
      if (this.caretTexture.TextureId != 0)
        return;
      this.caretHeight = height1;
      ImageSurface surface2 = new ImageSurface(Format.Argb32, 3, (int) height1);
      Context ctx2 = this.genContext(surface2);
      this.Font.SetupContext(ctx2);
      ctx2.SetSourceRGBA(this.caretColor[0], this.caretColor[1], this.caretColor[2], this.caretColor[3]);
      ctx2.LineWidth = 1.0;
      ctx2.NewPath();
      ctx2.MoveTo(2.0, 0.0);
      ctx2.LineTo(2.0, height1);
      ctx2.ClosePath();
      ctx2.Stroke();
      this.generateTexture(surface2, ref this.caretTexture.TextureId);
      ctx2.Dispose();
      surface2.Dispose();
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
      base.OnMouseDownOnElement(api, args);
      this.SetCaretPos(args.X - this.Bounds.absX, args.Y - this.Bounds.absY);
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
      if (!this.HasFocus)
        return;
      bool flag = this.multilineMode || args.KeyCode != 52;
      if (args.KeyCode == 53 && this.CaretPosWithoutLineBreaks > 0)
        this.OnKeyBackSpace();
      if (args.KeyCode == 55 && this.CaretPosWithoutLineBreaks < this.TextLengthWithoutLineBreaks)
        this.OnKeyDelete();
      if (args.KeyCode == 59)
      {
        if (args.CtrlPressed)
          this.SetCaretPos(this.lines[this.lines.Count - 1].TrimEnd('\r', '\n').Length, this.lines.Count - 1);
        else
          this.SetCaretPos(this.lines[this.CaretPosLine].TrimEnd('\r', '\n').Length, this.CaretPosLine);
        api.Gui.PlaySound("tick");
      }
      if (args.KeyCode == 58)
      {
        if (args.CtrlPressed)
          this.SetCaretPos(0);
        else
          this.SetCaretPos(0, this.CaretPosLine);
        api.Gui.PlaySound("tick");
      }
      if (args.KeyCode == 47)
        this.MoveCursor(-1, args.CtrlPressed);
      if (args.KeyCode == 48)
        this.MoveCursor(1, args.CtrlPressed);
      if (args.KeyCode == 104 && (args.CtrlPressed || args.CommandPressed))
      {
        string str1 = api.Forms.GetClipboardText().Replace("\uFEFF", "");
        string str2 = string.Join("", this.lines);
        int caretPosInLine = this.CaretPosInLine;
        for (int index = 0; index < this.CaretPosLine; ++index)
          caretPosInLine += this.lines[index].Length;
        this.SetValue(str2.Substring(0, caretPosInLine) + str1 + str2.Substring(caretPosInLine, str2.Length - caretPosInLine));
        api.Gui.PlaySound("tick");
      }
      if (args.KeyCode == 46 && this.CaretPosLine < this.lines.Count - 1)
      {
        this.SetCaretPos(this.CaretPosInLine, this.CaretPosLine + 1);
        api.Gui.PlaySound("tick");
      }
      if (args.KeyCode == 45 && this.CaretPosLine > 0)
      {
        this.SetCaretPos(this.CaretPosInLine, this.CaretPosLine - 1);
        api.Gui.PlaySound("tick");
      }
      if (args.KeyCode == 49 || args.KeyCode == 82)
      {
        if (this.multilineMode)
          this.OnKeyEnter();
        else
          flag = false;
      }
      if (args.KeyCode == 50)
        flag = false;
      args.Handled = flag;
    }

    private void OnKeyEnter()
    {
      if (this.lines.Count >= this.maxlines)
        return;
      string str1 = this.linesStaging[this.CaretPosLine].Substring(0, this.CaretPosInLine);
      string str2 = this.linesStaging[this.CaretPosLine].Substring(this.CaretPosInLine);
      this.linesStaging[this.CaretPosLine] = str1 + "\n";
      this.linesStaging.Insert(this.CaretPosLine + 1, str2);
      GuiElementEditableTextBase.OnTryTextChangeDelegate tryTextChangeText = this.OnTryTextChangeText;
      if ((tryTextChangeText != null ? (!tryTextChangeText(this.linesStaging) ? 1 : 0) : 0) != 0)
        return;
      this.lines = new List<string>(this.linesStaging);
      this.TextChanged();
      this.SetCaretPos(0, this.CaretPosLine + 1);
      this.api.Gui.PlaySound("tick");
    }

    private void OnKeyDelete()
    {
      string text = this.GetText();
      int withoutLineBreaks = this.CaretPosWithoutLineBreaks;
      if (text.Length == withoutLineBreaks)
        return;
      this.LoadValue(this.Lineize(text.Substring(0, withoutLineBreaks) + text.Substring(withoutLineBreaks + 1, text.Length - withoutLineBreaks - 1)));
      this.api.Gui.PlaySound("tick");
    }

    private void OnKeyBackSpace()
    {
      int withoutLineBreaks1 = this.CaretPosWithoutLineBreaks;
      if (withoutLineBreaks1 == 0)
        return;
      string text1 = this.GetText();
      string text2 = text1.Substring(0, withoutLineBreaks1 - 1) + text1.Substring(withoutLineBreaks1, text1.Length - withoutLineBreaks1);
      int withoutLineBreaks2 = this.CaretPosWithoutLineBreaks;
      this.LoadValue(this.Lineize(text2));
      if (withoutLineBreaks2 > 0)
        this.CaretPosWithoutLineBreaks = withoutLineBreaks2 - 1;
      this.api.Gui.PlaySound("tick");
    }

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
      if (!this.HasFocus)
        return;
      string prefix = this.lines[this.CaretPosLine].Substring(0, this.CaretPosInLine);
      string suffix = this.lines[this.CaretPosLine].Substring(this.CaretPosInLine);
      string str = prefix + args.KeyChar + suffix;
      double num = this.Bounds.InnerWidth - 2.0 * this.Bounds.absPaddingX - this.rightSpacing;
      this.linesStaging[this.CaretPosLine] = str;
      if (this.multilineMode)
      {
        if (this.Font.GetTextExtents(str.TrimEnd('\r', '\n')).Width >= num)
        {
          StringBuilder stringBuilder = new StringBuilder();
          for (int index = 0; index < this.lines.Count; ++index)
            stringBuilder.Append(index == this.CaretPosLine ? str : this.lines[index]);
          this.linesStaging = this.Lineize(stringBuilder.ToString());
          if (this.lines.Count >= this.maxlines && this.linesStaging.Count >= this.maxlines)
            return;
        }
      }
      int withoutLineBreaks = this.CaretPosWithoutLineBreaks;
      this.LoadValue(this.linesStaging);
      this.CaretPosWithoutLineBreaks = withoutLineBreaks + 1;
      args.Handled = true;
      api.Gui.PlaySound("tick");
      Action onKeyPressed = this.OnKeyPressed;
      if (onKeyPressed == null)
        return;
      onKeyPressed();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
      if (!this.HasFocus)
        return;
      if (this.api.ElapsedMilliseconds - this.caretBlinkMilliseconds > 900L)
      {
        this.caretBlinkMilliseconds = this.api.ElapsedMilliseconds;
        this.caretDisplayed = !this.caretDisplayed;
      }
      if (!this.caretDisplayed || this.caretX - this.renderLeftOffset >= this.Bounds.InnerWidth)
        return;
      this.api.Render.Render2DTexturePremultipliedAlpha(this.caretTexture.TextureId, this.Bounds.renderX + this.caretX + GuiElement.scaled(1.5) - this.renderLeftOffset, this.Bounds.renderY + this.caretY + this.topPadding, 2.0, this.caretHeight);
    }

    public override void Dispose()
    {
      base.Dispose();
      this.caretTexture.Dispose();
      this.textTexture.Dispose();
    }

    /// <summary>Moves the cursor forward and backward by an amount.</summary>
    /// <param name="dir">The direction to move the cursor.</param>
    /// <param name="wholeWord">Whether or not we skip entire words moving it.</param>
    public void MoveCursor(int dir, bool wholeWord = false)
    {
      bool flag1 = false;
      bool flag2 = (this.CaretPosInLine > 0 || this.CaretPosLine > 0) && dir < 0 ||
                   (this.CaretPosInLine < this.lines[this.CaretPosLine].Length ||
                    this.CaretPosLine < this.lines.Count - 1) && dir > 0;
      int posInLine = this.CaretPosInLine;
      int caretPosLine = this.CaretPosLine;
      for (; !flag1; flag1 = !wholeWord || posInLine > 0 && this.lines[caretPosLine][posInLine - 1] == ' ')
      {
        posInLine += dir;
        if (posInLine < 0)
        {
          if (caretPosLine > 0)
          {
            --caretPosLine;
            posInLine = this.lines[caretPosLine].TrimEnd('\r', '\n').Length;
          }
          else
            break;
        }

        if (posInLine > this.lines[caretPosLine].TrimEnd('\r', '\n').Length)
        {
          if (caretPosLine < this.lines.Count - 1)
          {
            posInLine = 0;
            ++caretPosLine;
          }
          else
            break;
        }
      }

      if (!flag2)
        return;
      this.SetCaretPos(posInLine, caretPosLine);
      this.api.Gui.PlaySound("tick");
    }
}
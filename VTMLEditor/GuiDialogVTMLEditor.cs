using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using VTMLEditor.EditorFeatures;
using VTMLEditor.GuiElements;
using AssetLocation = Vintagestory.API.Common.AssetLocation;

namespace VTMLEditor;

public class GuiDialogVTMLEditor : GuiDialog
{
  public override double DrawOrder => 0.2;
  public string DialogTitle;
  private string searchLangKey = Lang.DefaultLocale;
  private string? SearchTextInput { get; set; }
  private GuiDialogVTMLViewer? viewerDialog;
  public AssetLocation? SelectedThemeLoc { get; set; } = VtmleSystem.Themes.Keys.FirstOrDefault();
  public VtmlEditorTheme SelectedTheme => 
    SelectedThemeLoc == null ?
      VtmlEditorTheme.Default : 
    VtmleSystem.Themes[this.SelectedThemeLoc];

  public GuiDialogVTMLEditor(ICoreClientAPI? capi, string DialogTitle, GuiDialogVTMLViewer? viewerDialog) : base(capi)
  {
    this.DialogTitle = DialogTitle;
    this.viewerDialog = viewerDialog;
  }

  public override string ToggleKeyCombinationCode => VtmleSystem.UiKeyCode;

  private void ComposeDialog()
{
    // Auto-sized dialog at the center of the screen
    ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle).WithFixedPosition(0, -30);;
    ElementBounds searchInputBounds = ElementBounds.Fixed(0, 30, 420, 30);
    ElementBounds searchButtonBounds = ElementBounds.Fixed(424, 30, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds previewButtonBounds = ElementBounds.Fixed(342, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds dropDownBounds = searchButtonBounds.RightCopy(194)
      .WithSizing(ElementBounds.Fixed(0, 70, 78.0, 25.0).WithFixedPadding(2).verticalSizing);
    ElementBounds hotkeyDropDownBounds = ElementBounds.Fixed(0, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds hotkeyButtonBounds = searchButtonBounds.BelowCopy(10).WithFixedPosition(90, 70).WithFixedWidth(110);
    ElementBounds clipBounds = ElementBounds.Fixed(0, 110, 1000, 720);
    ElementBounds textBounds = clipBounds.ForkContainingChild();
    ElementBounds scrollbarBounds = textBounds.RightCopy(4).WithFixedWidth(20);

    // Background boundaries. Again, just make it fit it's child elements, then add the text as a child element
    ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
    bgBounds.BothSizing = ElementSizing.FitToChildren;
    bgBounds.WithChildren(searchInputBounds, searchButtonBounds, previewButtonBounds, dropDownBounds,  hotkeyDropDownBounds, hotkeyButtonBounds)
      .WithChildren(scrollbarBounds, clipBounds);

    SingleComposer = capi.Gui.CreateCompo(DialogTitle, dialogBounds)
      .AddShadedDialogBG(bgBounds)
      .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
      .BeginChildElements()
      .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
      .AddNewTextInput(searchInputBounds, t => SearchTextInput = t, CairoFont.WhiteSmallText(), "searchInput")
      .AddSmallButton(Lang.Get("Search"), this.OnPressSearch, searchButtonBounds, EnumButtonStyle.Small, "search")
      .AddSmallButton(Lang.Get("Copy Text"), this.OnPressCopy, searchButtonBounds.RightCopy(3).WithFixedWidth(98), EnumButtonStyle.Small, "copy")
      .AddSmallButton(Lang.Get("Preview"), this.OnTogglePreview, searchButtonBounds.RightCopy(108), EnumButtonStyle.Small, "showViewer")
      .AddDropDown(Lang.AvailableLanguages.Keys.ToArray(),
        Lang.AvailableLanguages.Keys.ToArray(),
        Lang.AvailableLanguages.Keys.IndexOf(k => k == searchLangKey),
        OnLangSelectionChanged, dropDownBounds);
    if (VtmleSystem.Themes.Count > 1)
    {
      SingleComposer.AddDropDown(VtmleSystem.Themes.Keys.Select(a => a.ToString()).ToArray(),
        VtmleSystem.Themes.Values.Select(t => t.Code).ToArray(),
        VtmleSystem.Themes.Keys.IndexOf(a => a == SelectedThemeLoc),
        OnThemeSelectionChanged, dropDownBounds.RightCopy(3));
    }
    SingleComposer
      .AddDropDown(capi.Input.HotKeys.Keys.ToArray(), capi.Input.HotKeys.Keys.ToArray(), 0, null, hotkeyDropDownBounds, "hotkeyDropDown")
      .AddSmallButton(Lang.Get("Insert Hotkey"), this.OnAddHotkey, hotkeyButtonBounds, EnumButtonStyle.Small, "insertHotkey")
      .AddSmallButton(Lang.Get("Insert Link"), this.OnAddLink, hotkeyButtonBounds.RightCopy(3).WithFixedWidth(96), EnumButtonStyle.Small, "insertLink")
      .BeginClip(clipBounds)
      .AddVtmlEditorArea(textBounds, OnTextChanged, SelectedTheme.TokenColors,
        TextUtilExtensions.EditorFont(SelectedTheme.FontName, SelectedTheme.FontSize).WithFontSize(18), "textArea")
      .EndClip()
      .EndChildElements()
      .Compose();
    
    SingleComposer.GetNewTextInput("searchInput").SetPlaceHolderText($"{Lang.Get("Search for Lang Key")} ({Lang.Get("@ for wildcard search")})");
    if (SearchTextInput is null || SearchTextInput?.Length > 0)SingleComposer.GetNewTextInput("searchInput").SetValue(SearchTextInput);
    
    if (viewerDialog?.Text != null) SingleComposer.GetVtmlEditorArea("textArea").SetValue(viewerDialog.Text);
    
    // After composing dialog, need to set the scrolling area heights to enable scroll behavior
    float scrollVisibleHeight = (float)clipBounds.fixedHeight;
    float scrollTotalHeight = (float)SingleComposer.GetVtmlEditorArea("textArea").Bounds.fixedHeight;
    SingleComposer.GetScrollbar("scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);
}

  private bool OnAddHotkey()
  {
    var editorArea = SingleComposer.GetVtmlEditorArea("textArea");
    var selectedHotkey = SingleComposer.GetDropDown("hotkeyDropDown").SelectedValue;
    if (selectedHotkey == null) return false;
    var hotkeyTag = $"<hk>{selectedHotkey}</hk>";
    editorArea.InsertTextAtCursor(hotkeyTag);
    editorArea.SetFocused(true);
    return true;
  }

  private bool OnAddLink()
  {
    var editorArea = SingleComposer.GetVtmlEditorArea("textArea");
    var selection = editorArea.GetSelectedText();
    var leftSideString = "<a href=\"";
    var rightSideString = $"\">{selection}</a>";
    editorArea.InsertTextAtCursor(leftSideString + rightSideString);
    editorArea.SetCaretPos(editorArea.CaretPosInLine - rightSideString.Length, editorArea.CaretPosLine);
    editorArea.SetFocused(true);
    return true;
  }

  private void OnNewScrollbarValue(float value)
  {
    ElementBounds bounds = SingleComposer.GetVtmlEditorArea("textArea").Bounds;
    bounds.fixedY = 5 - value;
    bounds.CalcWorldBounds();
  }

  private bool OnTogglePreview()
  {
    if (this.viewerDialog != null && this.viewerDialog.IsOpened())
    {
      this.viewerDialog.TryClose();
    }
    else
    {
      this.viewerDialog?.TryOpen();
    }
    return viewerDialog?.IsOpened() ?? false;
  }

  private void OnLangSelectionChanged(string code, bool selected)
  {
    this.searchLangKey = code;
  }
  
  private void OnThemeSelectionChanged(string code, bool selected)
  {
    this.SelectedThemeLoc = new AssetLocation(code);
    this.ComposeDialog();
  }

  private bool OnPressCopy()
  {
    var text = SingleComposer.GetVtmlEditorArea("textArea").GetText();
    capi.Input.ClipboardText = text;
    return true;
  }

  private bool OnPressSearch()
  {
    string searchText = SingleComposer.GetNewTextInput("searchInput").GetText();
    var langText = searchLangKey != Lang.DefaultLocale ? Lang.GetL(searchLangKey, searchText) : Lang.Get(searchText);
    if (langText == searchText)
    {
      if (searchText.StartsWith("@"))
      {
        var wildcardMatchedKeys = Lang.GetAllEntries()?.Keys.Where(
          key => WildcardUtil.Match(searchText, key)).Take(30).ToArray();
        langText = Lang.Get("Matched entries:\n") + string.Join("\n", wildcardMatchedKeys ?? Array.Empty<string>());
      }
      else
      {
        var allKeys = Lang.GetAllEntries()?.Keys.Where(
          key => key.ToLower().Contains(searchText.ToLower())).Take(30);
        langText = Lang.Get("Available entries:\n") + string.Join("\n", allKeys ?? Array.Empty<string>());
      }
    }
    SingleComposer.GetVtmlEditorArea("textArea").SetValue(langText);
    OnTextChanged(langText);
    return true;
  }

  private void OnTextChanged(string newText)
  {
    var guiDialogVtmlViewer = this.viewerDialog;
    if (guiDialogVtmlViewer == null) return;
    guiDialogVtmlViewer.Text = newText;
    guiDialogVtmlViewer.RefreshDialog();
  }

  private void OnTitleBarClose()
  {
    TryClose();
  }
  
  public override void OnGuiOpened()
  {
    ComposeDialog();
    base.OnGuiOpened();
  }
  
  public override bool PrefersUngrabbedMouse => true;
  
}
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using VTMLEditor.EditorFeatures;
using VTMLEditor.GuiElements;
using VTMLEditor.GuiElements.Vanilla;
using AssetLocation = Vintagestory.API.Common.AssetLocation;

namespace VTMLEditor;

public class GuiDialogVTMLEditor : GuiDialog
{
  public override double DrawOrder => 0.2;
  public string DialogTitle;
  private string searchLangKey = Lang.DefaultLocale;
  private string? SearchTextInput { get; set; }
  private GuiDialogVTMLViewer? viewerDialog;
  
  public AssetLocation? SelectedThemeLoc { get; set; }
  public VtmlEditorTheme SelectedTheme =>
    (this.SelectedThemeLoc == null ?
      new VtmlEditorTheme() :
      VtmleSystem.Themes[this.SelectedThemeLoc]);

  public GuiDialogVTMLEditor(ICoreClientAPI? capi, string DialogTitle, GuiDialogVTMLViewer? viewerDialog) : base(capi)
  {
    this.DialogTitle = DialogTitle;
    this.viewerDialog = viewerDialog;
  }

  public override string ToggleKeyCombinationCode => VtmleSystem.UiKeyCode;

  private void ComposeDialog()
{
    // Auto-sized dialog at the center of the screen
    ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle);
    ElementBounds searchInputBounds = ElementBounds.Fixed(0, 30, 420, 30);
    ElementBounds searchButtonBounds = ElementBounds.Fixed(422, 30, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds copyButtonBounds = ElementBounds.Fixed(422, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds previewButtonBounds = ElementBounds.Fixed(342, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds dropDownBounds = ElementBounds.Fixed(0, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds themeDropDownBounds = dropDownBounds.RightCopy(20);
    ElementBounds clipBounds = ElementBounds.Fixed(0, 110, 500, 600);
    ElementBounds textBounds = clipBounds.ForkContainingChild();
    ElementBounds scrollbarBounds = textBounds.RightCopy().WithFixedWidth(20);

    // Background boundaries. Again, just make it fit it's child elements, then add the text as a child element
    ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
    bgBounds.BothSizing = ElementSizing.FitToChildren;
    bgBounds.WithChildren(searchInputBounds, searchButtonBounds, copyButtonBounds, previewButtonBounds, dropDownBounds, themeDropDownBounds)
      .WithChildren(scrollbarBounds, clipBounds);

    SingleComposer = capi.Gui.CreateCompo(DialogTitle, dialogBounds)
      .AddShadedDialogBG(bgBounds)
      .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
      .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
      .AddTextInput(searchInputBounds, t => SearchTextInput = t, CairoFont.WhiteSmallText(), "searchInput")
      .AddSmallButton(Lang.Get("Search"), this.OnPressSearch, searchButtonBounds, EnumButtonStyle.Small, "search")
      .AddSmallButton(Lang.Get("Copy"), this.OnPressCopy, copyButtonBounds, EnumButtonStyle.Small, "copy")
      .AddToggleButton(Lang.Get("Preview"), CairoFont.SmallButtonText(), OnTogglePreview, previewButtonBounds,
        "showViewer")
      .AddDropDown(Lang.AvailableLanguages.Keys.ToArray(),
        Lang.AvailableLanguages.Keys.ToArray(),
        Lang.AvailableLanguages.Keys.IndexOf(k => k == searchLangKey),
        OnLangSelectionChanged, dropDownBounds);
    if (VtmleSystem.Themes.Count > 1)
    {
      SingleComposer.AddDropDown(VtmleSystem.Themes.Keys.Select(a => a.ToString()).ToArray(),
        VtmleSystem.Themes.Values.Select(t => t.Code).ToArray(),
        SelectedThemeLoc == null ? 0 : VtmleSystem.Themes.Keys.IndexOf(a => a == SelectedThemeLoc),
        OnThemeSelectionChanged, themeDropDownBounds);
    }
    SingleComposer
      .BeginClip(clipBounds)
      .AddVtmlEditorArea(textBounds, OnTextChanged, SelectedTheme.TokenColors,
        TextUtilExtensions.EditorFont(SelectedTheme.FontName, SelectedTheme.FontSize), "textArea")
      .EndClip()
      .Compose();
    VtmleSystem.Logger?.Warning($"Loaded themes: {VtmleSystem.Themes.Count}");
    
    SingleComposer.GetTextInput("searchInput").SetPlaceHolderText($"{Lang.Get("Search for Lang Key")} ({Lang.Get("@ for wildcard search")})");
    if (SearchTextInput is null || SearchTextInput?.Length > 0)SingleComposer.GetTextInput("searchInput").SetValue(SearchTextInput);
    
    if (viewerDialog?.Text != null) SingleComposer.GetVtmlEditorArea("textArea").SetValue(viewerDialog.Text);
    
    // After composing dialog, need to set the scrolling area heights to enable scroll behavior
    float scrollVisibleHeight = (float)clipBounds.fixedHeight;
    float scrollTotalHeight = (float)SingleComposer.GetVtmlEditorArea("textArea").Bounds.OuterHeight;
    SingleComposer.GetScrollbar("scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);
}

  private void OnNewScrollbarValue(float value)
  {
    ElementBounds bounds = SingleComposer.GetVtmlEditorArea("textArea").Bounds;
    bounds.fixedY = 5 - value;
    bounds.CalcWorldBounds();
  }

  private void OnTogglePreview(bool toggled)
  {
    if (this.viewerDialog != null && this.viewerDialog.IsOpened())
    {
      this.viewerDialog.TryClose();
    }
    else
    {
      this.viewerDialog?.TryOpen();
    }
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
    string searchText = SingleComposer.GetTextInput("searchInput").GetText();
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
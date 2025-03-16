using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace VTMLEditor;

public class GuiDialogVTMLEditor : GuiDialog
{
  public override double DrawOrder => 0.2;
  public string DialogTitle;
  private string searchLangKey = Lang.DefaultLocale;
  private GuiDialogVTMLViewer viewerDialog;

  public GuiDialogVTMLEditor(ICoreClientAPI capi, string DialogTitle, GuiDialogVTMLViewer viewerDialog) : base(capi)
  {
    this.DialogTitle = DialogTitle;
    this.viewerDialog = viewerDialog;
  }

  public override string ToggleKeyCombinationCode => VTMLESystem.UIKeyCode;

  private void ComposeDialog()
{
    // Auto-sized dialog at the center of the screen
    ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle);
    var baseShift = 0;
    ElementBounds searchBounds = ElementBounds.Fixed(0, 30, 420, 30);
    ElementBounds buttonBounds = ElementBounds.Fixed(422, 30, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds buttonBounds2 = ElementBounds.Fixed(422, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds buttonBounds3 = ElementBounds.Fixed(342, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds dropDownBounds = ElementBounds.Fixed(0, 70, 78.0, 25.0).WithFixedPadding(2);
    ElementBounds textBounds = ElementBounds.Fixed(0, 110, 500, 600);

    // Background boundaries. Again, just make it fit it's child elements, then add the text as a child element
    ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
    bgBounds.BothSizing = ElementSizing.FitToChildren;
    bgBounds.WithChildren(searchBounds);
    bgBounds.WithChildren(buttonBounds);
    bgBounds.WithChildren(buttonBounds2);
    bgBounds.WithChildren(buttonBounds3);
    bgBounds.WithChildren(dropDownBounds);
    bgBounds.WithChildren(textBounds);

    SingleComposer = capi.Gui.CreateCompo(DialogTitle, dialogBounds)
        .AddShadedDialogBG(bgBounds)
        .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
        .AddTextArea(textBounds, OnTextChanged, CairoFont.WhiteSmallText(), "textArea")
        .AddTextInput(searchBounds, null, CairoFont.WhiteSmallText(), "searchInput")
        .AddSmallButton(Lang.Get("Search"), this.OnPressSearch, buttonBounds, EnumButtonStyle.Small, "search")
        .AddSmallButton(Lang.Get("Copy"), this.OnPressCopy, buttonBounds2, EnumButtonStyle.Small, "copy")
        .AddToggleButton(Lang.Get("Preview"), CairoFont.SmallButtonText(), OnTogglePreview, buttonBounds3, "showViewer")
        .AddDropDown(Lang.AvailableLanguages.Keys.ToArray(),
          Lang.AvailableLanguages.Keys.ToArray(),
          Lang.AvailableLanguages.Keys.IndexOf(k=> k == searchLangKey),
          OnLangSelectionChanged, dropDownBounds)
        .Compose();
    SingleComposer.GetToggleButton("showViewer").SetValue(true);
    SingleComposer.GetTextInput("searchInput").SetPlaceHolderText($"{Lang.Get("Search for Lang Key")} ({Lang.Get("@ for wildcard search")})");
    SetText(viewerDialog.Text);
}

  private void OnTogglePreview(bool toggled)
  {
    if (toggled)
    {
      this.viewerDialog.Text = SingleComposer.GetTextArea("textArea").GetText();
      if (!viewerDialog.TryOpen())
        this.viewerDialog.RefreshDialog();
    }
    else
    {
      viewerDialog.TryClose();
    }
  }

  private void OnLangSelectionChanged(string code, bool selected)
  {
    this.searchLangKey = code;
  }

  private bool OnPressCopy()
  {
    // Copy using the system clipboard
    var text = SingleComposer.GetTextArea("textArea").GetText();
    capi.Input.ClipboardText = text;
    return true;
  }

  private void SetText(string newText)
  {
    SingleComposer.GetTextArea("textArea").SetValue(newText);
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
    SetText(langText);
    OnTextChanged(langText);
    return true;
  }

  private void OnTextChanged(string newText)
  {
    this.viewerDialog.Text = newText;
    this.viewerDialog.RefreshDialog();
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

  public override bool TryOpen()
  {
    this.viewerDialog.ignoreNextKeyPress = true;
    return base.TryOpen() & this.viewerDialog.TryOpen();
  }

  public override bool TryClose()
  {
    return base.TryClose() & this.viewerDialog.TryClose();
  }

  public override bool PrefersUngrabbedMouse => true;
  
}
using Vintagestory.API.Client;

namespace VTMLEditor;

public class GuiDialogVTMLViewer : GuiDialog
{
    public override double DrawOrder => 0.2;
    public string DialogTitle;
    private string text = "";
    public string Text { get => text; set => text = value; }

    public GuiDialogVTMLViewer(ICoreClientAPI? capi, string DialogTitle) : base(capi)
    {
        this.DialogTitle = DialogTitle;
    }

    public override string ToggleKeyCombinationCode => VtmleSystem.UiKeyCode;

    private void ComposeDialog()
    {
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        
        ElementBounds insetBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 510, 678);
        ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(20);

        ElementBounds textBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds);
        
        ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);

        SingleComposer = capi.Gui.CreateCompo(DialogTitle, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements()
                .AddInset(insetBounds)
                .BeginClip(clipBounds)
                .AddRichtext(text, CairoFont.WhiteSmallText(), textBounds, "text")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .Compose();
        
        // After composing dialog, need to set the scrolling area heights to enable scroll behavior
        float scrollVisibleHeight = (float)clipBounds.fixedHeight;
        float scrollTotalHeight = (float)SingleComposer.GetRichtext("text").TotalHeight;
        SingleComposer.GetScrollbar("scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);
    }

    private void OnNewScrollbarValue(float value)
    {
        ElementBounds bounds = SingleComposer.GetRichtext("text").Bounds;
        bounds.fixedY = 5 - value;
        bounds.CalcWorldBounds();
    }

    public void RefreshDialog()
    {
        SingleComposer?.Dispose();
        ComposeDialog();
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
}
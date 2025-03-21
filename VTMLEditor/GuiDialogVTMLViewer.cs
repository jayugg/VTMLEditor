using Vintagestory.API.Client;

namespace VTMLEditor;

public class GuiDialogVTMLViewer : GuiDialog
{
    public override double DrawOrder => 0.2;
    public string DialogTitle;
    private string text = "";
    private double listHeight = 500; // Emulate hardcoded value from GuiDialogHandbook
    public string Text { get => text; set => text = value; }

    public GuiDialogVTMLViewer(ICoreClientAPI? capi, string DialogTitle) : base(capi)
    {
        this.DialogTitle = DialogTitle;
    }

    public override string ToggleKeyCombinationCode => VtmleSystem.UiKeyCode;

    private void ComposeDialog()
    {
        ElementBounds textBounds = ElementBounds.Fixed(9, 45, 500, listHeight + 30 + 17);
        ElementBounds clipBounds = textBounds.ForkBoundingParent();
        ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
        ElementBounds scrollbarBounds = clipBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7, -6, 0, 6).WithFixedWidth(20);
        

        ElementBounds bgBounds = insetBounds.ForkBoundingParent(5, 40, 36, 52).WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);
        bgBounds.WithChildren(insetBounds, textBounds, scrollbarBounds);

        ElementBounds dialogBounds = bgBounds.ForkBoundingParent().WithAlignment(EnumDialogArea.None).WithAlignment(EnumDialogArea.RightMiddle);

        SingleComposer = capi.Gui.CreateCompo(DialogTitle, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .BeginClip(clipBounds)
                .AddInset(insetBounds, 3)
                .AddRichtext(text, CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2), textBounds, "text")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .EndChildElements()
                .Compose();
        
        float scrollTotalHeight = (float)SingleComposer.GetRichtext("text").Bounds.fixedHeight;
        SingleComposer.GetScrollbar("scrollbar").SetHeights((float)listHeight, scrollTotalHeight);
    }

    private void OnNewScrollbarValue(float value)
    {
        GuiElementRichtext richtextElem = SingleComposer.GetRichtext("text");
        richtextElem.Bounds.fixedY = 3 - value;
        richtextElem.Bounds.CalcWorldBounds();
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
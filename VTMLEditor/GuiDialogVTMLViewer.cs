using Vintagestory.API.Client;

namespace VTMLEditor;

public class GuiDialogVTMLViewer : GuiDialog
{
    public override double DrawOrder => 0.2;
    public string DialogTitle;
    private string text = "";
    public string Text { get => text; set => text = value; }

    public GuiDialogVTMLViewer(ICoreClientAPI capi, string DialogTitle) : base(capi)
    {
        this.DialogTitle = DialogTitle;
    }

    public override string ToggleKeyCombinationCode => VTMLESystem.UIKeyCode;

    private void ComposeDialog()
    {
        // Auto-sized dialog at the center of the screen
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle);

        ElementBounds textBounds = ElementBounds.Fixed(0, 30, 500, 600);

        // Background boundaries. Again, just make it fit it's child elements, then add the text as a child element
        ElementBounds insetBounds = ElementBounds.Fixed(-5, 25, 510, 610);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding); 
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        bgBounds.WithChildren(textBounds);
        bgBounds.WithChildren(insetBounds);

        SingleComposer = capi.Gui.CreateCompo(DialogTitle, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddInset(insetBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddRichtext(text, CairoFont.WhiteSmallText(), textBounds)
                .Compose();
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
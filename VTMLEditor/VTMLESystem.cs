using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VTMLEditor;

public class VtmleSystem : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;
    public static ILogger Logger;
    public static string Modid;
    public static ICoreClientAPI Capi;
    private GuiDialogVTMLEditor _editorDialog;
    private GuiDialogVTMLViewer _viewerDialog;
    public static string UiKeyCode => Modid + ":hotkey-vtml-editor";
    
    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        
        Logger = api.Logger;
        Modid = Mod.Info.ModID;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        Capi = api;
        api.Input.RegisterHotKey(UiKeyCode, Lang.Get(UiKeyCode), GlKeys.Y, HotkeyType.DevTool);
        api.Input.SetHotKeyHandler(UiKeyCode, this.OnEditorHotkey);
        api.Event.LevelFinalize += this.Event_LevelFinalize;
        api.ChatCommands
            .Create("vtmle")
            .WithAlias("vtmleditor")
            .WithDescription("Open VTML Editor")
            .HandleWith(_ => OnOpenDialogCommand(api));
    }

    private TextCommandResult OnOpenDialogCommand(ICoreClientAPI api)
    {
        ToggleDialog();
        return TextCommandResult.Success();
    }

    private void Event_LevelFinalize()
    {
        this._viewerDialog = new GuiDialogVTMLViewer(Capi, Lang.Get("VTMLViewer"));
        this._editorDialog = new GuiDialogVTMLEditor(Capi, Lang.Get("VTMLEditor"), this._viewerDialog);
    }

    private bool OnEditorHotkey(KeyCombination key)
    {
        return ToggleDialog();
    }

    private bool ToggleDialog()
    {
        bool result;
        if (this._editorDialog.IsOpened())
        {
            result = this._editorDialog.TryClose();
        }
        else
        {
            result = this._editorDialog.TryOpen();
            this._editorDialog.ignoreNextKeyPress = true;
        }
        return result;
    }

    public override void Dispose()
    {
        Logger = null;
        Modid = null;
        base.Dispose();
    }
}
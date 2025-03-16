using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VTMLEditor;

public class VTMLESystem : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;
    public static string UIKeyCode = Modid + ":hotkey-pee";
    public static ILogger Logger;
    public static string Modid;
    public static ICoreClientAPI Capi;
    private GuiDialogVTMLEditor _editorDialog;
    private GuiDialogVTMLViewer _viewerDialog;
    
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
        api.Input.RegisterHotKey(UIKeyCode, Lang.Get(UIKeyCode), GlKeys.Y, HotkeyType.DevTool);
        api.Input.SetHotKeyHandler(UIKeyCode, OnEditorHotkey);
        api.Event.LevelFinalize += new Action(this.Event_LevelFinalize);
    }

    private void Event_LevelFinalize()
    {
        this._viewerDialog = new GuiDialogVTMLViewer(Capi, "VTMLViewer");
        this._editorDialog = new GuiDialogVTMLEditor(Capi, "VTMLEditor", this._viewerDialog);
    }

    private bool OnEditorHotkey(KeyCombination key)
    {
        if (this._editorDialog.IsOpened())
        {
            this._editorDialog.TryClose();
        }
        else
        {
            this._editorDialog.TryOpen();
            this._editorDialog.ignoreNextKeyPress = true;
        }
        return true;
    }
    
    public override void Dispose()
    {
        Logger = null;
        Modid = null;
        UIKeyCode = null;
        base.Dispose();
    }
}
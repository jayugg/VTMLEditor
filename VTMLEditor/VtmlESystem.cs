using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using VTMLEditor.EditorFeatures;

namespace VTMLEditor;

public class VtmleSystem : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;
    public static ILogger? Logger;
    public static string? Modid;
    public static ICoreClientAPI? Capi;
    public static Harmony? HarmonyInstance;
    private GuiDialogVTMLEditor? _editorDialog;
    private GuiDialogVTMLViewer? _viewerDialog;
    public static string UiKeyCode => Modid + ":hotkey-vtml-editor";
    public static Dictionary<AssetLocation, VtmlEditorTheme> Themes = new();

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        
        Logger = api.Logger;
        Modid = Mod.Info.ModID;
        HarmonyInstance = new Harmony(Modid);
        HarmonyInstance.PatchAll();
    }

    public override void StartClientSide(ICoreClientAPI? api)
    {
        base.StartClientSide(api);
        Capi = api;
        api?.Input.RegisterHotKey(UiKeyCode, Lang.Get(UiKeyCode), GlKeys.U, HotkeyType.DevTool);
        api?.Input.SetHotKeyHandler(UiKeyCode, this.OnEditorHotkey);
        if (api == null) return;
        api.Event.LevelFinalize += this.Event_LevelFinalize;
        api.ChatCommands
            .Create("vtmle")
            .WithAlias("vtmleditor")
            .WithDescription("Open VTML Editor")
            .HandleWith(_ => OnOpenDialogCommand(api));
    }
    
    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        var loadedThemes = api.Assets.GetMany<VtmlEditorTheme[]>(Logger, $"config/{Modid}/themes.json");
        Logger?.Notification($"Found {loadedThemes.Count} theme locations");
        foreach (var themeLocation in loadedThemes.Keys)
        {
            var index = 0;
            foreach (var theme in loadedThemes[themeLocation])
            {
                if (theme is { FontName.Length: > 0, FontSize: > 0, Code.Length: > 0})
                {
                    Themes.Add(new AssetLocation(themeLocation.Domain, $"theme-{theme.Code}"), theme);
                    Logger?.Notification($"Loaded theme: {theme.Code} from {themeLocation}");
                }
                else
                {
                    Logger?.Warning($"Failed to load theme at index {index} from location {themeLocation}");
                }
                index++;
            }
        }
    }

    private TextCommandResult OnOpenDialogCommand(ICoreClientAPI? api)
    {
        ToggleDialog();
        return TextCommandResult.Success();
    }

    private void Event_LevelFinalize()
    {
        this._viewerDialog = new GuiDialogVTMLViewer(Capi, Lang.Get("VTML Viewer"));
        this._editorDialog = new GuiDialogVTMLEditor(Capi, Lang.Get("VTML Editor"), this._viewerDialog);
    }

    private bool OnEditorHotkey(KeyCombination key)
    {
        return ToggleDialog();
    }

    private bool ToggleDialog()
    {
        bool result;
        if (this._editorDialog != null && this._editorDialog.IsOpened())
        {
            result = this._editorDialog.TryClose();
        }
        else
        {
            result = this._editorDialog?.TryOpen() ?? false;
            if (this._editorDialog != null) this._editorDialog.ignoreNextKeyPress = true;
        }
        return result;
    }

    public override void Dispose()
    {
        Logger = null;
        Modid = null;
        Capi = null;
        HarmonyInstance?.UnpatchAll(Modid);
        HarmonyInstance = null;
        _editorDialog?.Dispose();
        _viewerDialog?.Dispose();
        Themes.Clear();
        base.Dispose();
    }
}
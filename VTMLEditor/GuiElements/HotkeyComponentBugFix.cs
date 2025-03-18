using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VTMLEditor.GuiElements;

[HarmonyPatch(typeof(VtmlUtil))]
public class HotkeyComponentBugFix
{
    static MethodBase TargetMethod()
    {
        return typeof(VtmlUtil).GetMethod("Richtextify", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 
            null, new Type[] {
                typeof(ICoreClientAPI),
                typeof(VtmlToken),
                typeof(List<RichTextComponentBase>).MakeByRefType(),
                typeof(Stack<CairoFont>),
                typeof(Action<LinkTextComponent>)
            }, null) ?? throw new InvalidOperationException("Cannot find method 'Richtextify' in VtmlUtil");
    }
    
    [HarmonyPrefix]
    public static bool Prefix_Richtextify(
        ICoreClientAPI capi,
        VtmlToken token,
        ref List<RichTextComponentBase> elems,
        Stack<CairoFont> fontStack,
        Action<LinkTextComponent> didClickLink)
    {
        if (token is not VtmlTagToken vtmlTagToken) return true;
        if (vtmlTagToken.Name is not "hotkey" and not "hk") return true;
        return !(string.IsNullOrEmpty(vtmlTagToken.ContentText) || vtmlTagToken.ContentText.All(char.IsWhiteSpace));
    }
}
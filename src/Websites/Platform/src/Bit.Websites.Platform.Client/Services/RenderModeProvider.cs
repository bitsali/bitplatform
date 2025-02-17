﻿using Microsoft.AspNetCore.Components.Web;

namespace Bit.Websites.Platform.Client.Services;

public static class RenderModeProvider
{
    static IComponentRenderMode PrerenderEnabledAuto = RenderMode.InteractiveAuto;
    static IComponentRenderMode PrerenderEnabledBlazorWasm = RenderMode.InteractiveWebAssembly;
    static IComponentRenderMode PrerenderEnabledBlazorServer = RenderMode.InteractiveServer;

    static IComponentRenderMode Auto = new InteractiveAutoRenderMode(prerender: false);
    static IComponentRenderMode BlazorWasm = new InteractiveWebAssemblyRenderMode(prerender: false);
    static IComponentRenderMode BlazorServer = new InteractiveServerRenderMode(prerender: false);

    // PrerenderOnly: In order to have prerender only mode, simply remove @rendermode usages from App.razor

    public static IComponentRenderMode Current => PrerenderEnabledAuto;
}

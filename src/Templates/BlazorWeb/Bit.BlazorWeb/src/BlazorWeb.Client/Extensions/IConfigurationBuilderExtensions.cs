﻿using System.Reflection;

namespace Microsoft.Extensions.Configuration;

public static class IConfigurationBuilderExtensions
{
    public static void AddClientConfigurations(this IConfigurationBuilder builder)
    {
        var assembly = Assembly.Load("BlazorWeb.Client");
        builder.AddJsonStream(assembly.GetManifestResourceStream("BlazorWeb.Client.appsettings.json")!);
    }
}

﻿namespace Bit.Websites.Platform.Client.Shared;

public partial class PageNotFound
{
    [AutoInject] public NavigationManager NavigationManager { get; set; }

    private void BackToHome()
    {
        NavigationManager.NavigateTo("/");
    }
}

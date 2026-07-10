// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Windows.AppNotifications;
using Snap.Hutao.Remastered.Core;
using Snap.Hutao.Remastered.Core.LifeCycle.InterProcess.Model;
using System.Diagnostics;

namespace Snap.Hutao.Remastered.Service.Notification;

[Service(ServiceLifetime.Singleton)]
public sealed partial class ToastNotificationService
{
    public void Show(string rawXml)
    {
        if (HutaoRuntime.IsProcessElevated)
        {
            LaunchToastHelper(rawXml);
        }
        else
        {
            AppNotificationManager.Default.Show(new AppNotification(rawXml));
        }
    }

    public void ShowText(string text)
    {
        string encoded = System.Net.WebUtility.HtmlEncode(text);
        string rawXml = $"""<toast><visual><binding template="ToastGeneric"><text>{encoded}</text></binding></visual></toast>""";
        Show(rawXml);
    }

    private static void LaunchToastHelper(string rawXml)
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        try
        {
            ToastNotificationRequest request = new(rawXml);
            using ToastNotificationPipeServer pipeServer = new();

            // Launch helper via explorer.exe so it runs non-elevated
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{exePath}\"",
                UseShellExecute = true,
            });

            pipeServer.TrySendRequest(request);
        }
        catch
        {
            // Fire-and-forget: silently ignore if helper fails
        }
    }
}

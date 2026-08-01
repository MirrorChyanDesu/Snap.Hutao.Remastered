// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Remastered.Core.LifeCycle;
using Snap.Hutao.Remastered.Core.Setting;
using Snap.Hutao.Remastered.Service.Game.Launching.Context;
using Snap.Hutao.Remastered.UI.Windowing;
using Snap.Hutao.Remastered.UI.Xaml.View.Window;

namespace Snap.Hutao.Remastered.Service.Game.Launching.Handler;

public sealed class LaunchExecutionCloseWindowHandler : AbstractLaunchExecutionHandler
{
    public override async ValueTask ExecuteAsync(LaunchExecutionContext context)
    {
        if (LocalSetting.Get(SettingKeys.LaunchIsCloseWhenGameStarted, false))
        {
            XamlApplicationLifetime.WindowClosedForGameLaunch = true;

            await context.TaskContext.SwitchToMainThreadAsync();
            MainWindow.Instance.Close();
        }
    }

    public override async ValueTask AfterAsync(AfterLaunchExecutionContext context)
    {
        if (XamlApplicationLifetime.WindowClosedForGameLaunch)
        {
            XamlApplicationLifetime.WindowClosedForGameLaunch = false;

            ICurrentXamlWindowReference currentXamlWindowReference = context.ServiceProvider.GetRequiredService<ICurrentXamlWindowReference>();
            await context.TaskContext.SwitchToMainThreadAsync();

            // If the user has already manually reopened a window (e.g. via tray icon
            // or by launching a new instance), don't create a duplicate.
            if (currentXamlWindowReference.Window is not null)
            {
                return;
            }

            // Game has exited, recreate and show the main window
            MainWindow mainWindow = context.ServiceProvider.GetRequiredService<MainWindow>();
            currentXamlWindowReference.Window = mainWindow;
            mainWindow.SwitchTo();
            mainWindow.AppWindow.MoveInZOrderAtTop();
        }
    }
}

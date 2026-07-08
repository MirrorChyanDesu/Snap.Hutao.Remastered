// Copyright (c) Snap HuTao RP. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Snap.Hutao.Remastered.Core.Logging;
using Snap.Hutao.Remastered.Core.Property;
using Microsoft.Extensions.DependencyInjection;
using Snap.Hutao.Remastered.Service;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading;
using Snap.Hutao.Remastered.Service.BackgroundMediaPlayer;
using Snap.Hutao.Remastered.UI.Content;
using Snap.Hutao.Remastered.UI.Xaml.Control.Theme;
using Snap.Hutao.Remastered.UI.Xaml.Media.Animation;
using System.Runtime.InteropServices;

namespace Snap.Hutao.Remastered.UI.Xaml.Behavior;

public sealed partial class ServiceRecipientMediaPlayerElementPresenterBehavior : BehaviorBase<MediaPlayerElement>, IDisposable, IRecipient<Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage>
{
    private readonly CancellationTokenSource unloadCts = new();

    private IBackgroundMediaPlayerService? backgroundMediaPlayerService;

    public void Dispose()
    {
        unloadCts.Dispose();
    }

    protected override void OnAssociatedObjectLoaded()
    {
        if (AssociatedObject.XamlRoot.XamlContext()?.ServiceProvider is { } serviceProvider)
        {
            backgroundMediaPlayerService = serviceProvider.GetRequiredService<IBackgroundMediaPlayerService>();
            IMessenger messenger = serviceProvider.GetRequiredService<IMessenger>();
            messenger.Register<Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage>(this);
            PrivateUpdateMediaPlayerElementAsync(unloadCts.Token).SafeForget();
        }
    }

    protected override bool Uninitialize()
    {
        unloadCts.Cancel();
        if (AssociatedObject.XamlRoot.XamlContext()?.ServiceProvider is { } serviceProvider)
        {
            IMessenger messenger = serviceProvider.GetRequiredService<IMessenger>();
            messenger.UnregisterAll(this);
        }

        return base.Uninitialize();
    }

    [Command("UpdateMediaPlayerElementCommand")]
    private void UpdateMediaPlayerElement()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Update media element", "ServiceRecipientMediaPlayerElementPresenterBehavior.Command"));
        PrivateUpdateMediaPlayerElementAsync(unloadCts.Token).SafeForget();
    }

    private async ValueTask PrivateUpdateMediaPlayerElementAsync(CancellationToken token = default)
    {
        if (AssociatedObject is not { } mediaElement || backgroundMediaPlayerService is null)
        {
            return;
        }

        ITaskContext taskContext = TaskContext.GetForDependencyObject(mediaElement);

        token.ThrowIfCancellationRequested();

        try
        {
            await AnimationBuilder
                .Create()
                .Opacity(
                    to: 0D,
                    duration: Constants.ImageOpacityFadeInOut,
                    easingType: EasingType.Quartic,
                    easingMode: EasingMode.EaseInOut)
                .StartAsync(mediaElement, token)
                .ConfigureAwait(false);

            if (XamlApplicationLifetime.Exiting)
            {
                return;
            }

            await backgroundMediaPlayerService.UpdateMediaPlayerElementAsync(mediaElement, token).ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();

            double targetOpacity = mediaElement.Source is null ? 0 : 1;

            await AnimationBuilder
                .Create()
                .Opacity(
                    to: targetOpacity,
                    duration: Constants.ImageOpacityFadeInOut,
                    easingType: EasingType.Quartic,
                    easingMode: EasingMode.EaseInOut)
                .StartAsync(mediaElement, token)
                .ConfigureAwait(false);
        }
        catch (COMException)
        {
            // ignore
        }
    }

    public void Receive(Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage message)
    {
        PrivateUpdateMediaPlayerElementAsync(unloadCts.Token).SafeForget();
    }
}

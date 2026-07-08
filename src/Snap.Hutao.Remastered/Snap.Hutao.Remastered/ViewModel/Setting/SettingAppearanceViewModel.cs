// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Snap.Hutao.Remastered.Factory.Picker;
using System.Collections.Immutable;
using Snap.Hutao.Remastered.Model;
using Snap.Hutao.Remastered.Service;
using Snap.Hutao.Remastered.Service.BackgroundImage;
using Snap.Hutao.Remastered.Service.BackgroundMediaPlayer;
using Snap.Hutao.Remastered.UI.Xaml;
using Snap.Hutao.Remastered.UI.Xaml.Control.Theme;
using Snap.Hutao.Remastered.UI.Xaml.Media.Backdrop;

namespace Snap.Hutao.Remastered.ViewModel.Setting;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class SettingAppearanceViewModel : Abstraction.ViewModel
{
    [GeneratedConstructor]
    public partial SettingAppearanceViewModel(IServiceProvider serviceProvider);

    public partial CultureOptions CultureOptions { get; }

    public partial AppOptions AppOptions { get; }

    public partial BackgroundImageOptions BackgroundImageOptions { get; }

    public partial BackgroundMediaPlayerOptions BackgroundMediaPlayerOptions { get; }

    public partial IMessenger Messenger { get; }

    // Background media UI bindings
    public ImmutableArray<NameValue<BackgroundMediaType>> BackgroundMediaTypes => [
        new NameValue<BackgroundMediaType>("None", BackgroundMediaType.None),
            new NameValue<BackgroundMediaType>("LocalFolder", BackgroundMediaType.LocalFolder),
            new NameValue<BackgroundMediaType>("HutaoWeb", BackgroundMediaType.HutaoWeb)
    ];

    // TODO: Replace with IObservableProperty
    public NameValue<BackgroundMediaType>? SelectedBackgroundMediaType
    {
        get => field ??= Selection.Initialize(BackgroundMediaTypes, BackgroundMediaPlayerOptions.BackgroundMediaType);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                BackgroundMediaPlayerOptions.BackgroundMediaType = value.Value;
                Messenger.Send(new Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage());
            }
        }
    }

    public string? BackgroundMediaPath
    {
        get => BackgroundMediaPlayerOptions.BackgroundMediaPath;
        set
        {
            if (BackgroundMediaPlayerOptions.BackgroundMediaPath == value)
            {
                return;
            }

            BackgroundMediaPlayerOptions.BackgroundMediaPath = value;
            OnPropertyChanged();
            Messenger.Send(new Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage());
        }
    }

    public bool IsLooping
    {
        get => BackgroundMediaPlayerOptions.IsLooping;
        set
        {
            if (BackgroundMediaPlayerOptions.IsLooping == value) return;
            BackgroundMediaPlayerOptions.IsLooping = value;
            OnPropertyChanged();
            Messenger.Send(new Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage());
        }
    }

    public bool IsMuted
    {
        get => BackgroundMediaPlayerOptions.IsMuted;
        set
        {
            if (BackgroundMediaPlayerOptions.IsMuted == value) return;
            BackgroundMediaPlayerOptions.IsMuted = value;
            OnPropertyChanged();
            Messenger.Send(new Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage());
        }
    }

    [Command("SetBackgroundMediaFolderCommand")]
    private async Task SetBackgroundMediaFolderAsync()
    {
        ValueResult<bool, string?> result = FileSystemPickerInteraction.PickFolder("Select background media folder");
        if (result.TryGetValue(out string? path))
        {
            await TaskContext.SwitchToMainThreadAsync();
            BackgroundMediaPlayerOptions.BackgroundMediaPath = path;
            OnPropertyChanged(nameof(BackgroundMediaPath));
            Messenger.Send(new Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage());
        }
    }

    [Command("ResetBackgroundMediaFolderCommand")]
    private void ResetBackgroundMediaFolder()
    {
        BackgroundMediaPlayerOptions.BackgroundMediaPath = string.Empty;
        OnPropertyChanged(nameof(BackgroundMediaPath));
        Messenger.Send(new Snap.Hutao.Remastered.Service.BackgroundMediaPlayer.Message.BackgroundMediaOptionsChangedMessage());
    }

    public partial IFileSystemPickerInteraction FileSystemPickerInteraction { get; }

    public partial ITaskContext TaskContext { get; }

    // TODO: Replace with IObservableProperty
    public NameCultureInfoValue? SelectedCulture
    {
        get => field ??= Selection.Initialize(CultureOptions.Cultures, CultureOptions.CurrentCulture.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                CultureOptions.CurrentCulture.Value = value.Value;
                AppInstance.Restart(string.Empty);
            }
        }
    }

    // TODO: Replace with IObservableProperty
    public NameValue<DayOfWeek>? SelectedFirstDayOfWeek
    {
        get => field ??= CultureOptions.DayOfWeeks.FirstOrDefault(d => d.Value == CultureOptions.FirstDayOfWeek.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                CultureOptions.FirstDayOfWeek.Value = value.Value;
            }
        }
    }

    // TODO: Replace with IObservableProperty
    public NameValue<BackdropType>? SelectedBackdropType
    {
        get => field ??= AppOptions.BackdropTypes.Single(t => t.Value == AppOptions.BackdropType.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                AppOptions.BackdropType.Value = value.Value;
            }
        }
    }

    // TODO: Replace with IObservableProperty
    public NameValue<ElementTheme>? SelectedElementTheme
    {
        get => field ??= AppOptions.LazyElementThemes.Value.Single(t => t.Value == AppOptions.ElementTheme.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                AppOptions.ElementTheme.Value = value.Value;
                FrameworkTheming.SetTheme(ThemeHelper.ElementToFramework(value.Value));
            }
        }
    }

    // TODO: Replace with IObservableProperty
    public NameValue<BackgroundImageType>? SelectedBackgroundImageType
    {
        get => field ??= AppOptions.BackgroundImageTypes.Single(t => t.Value == AppOptions.BackgroundImageType.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                AppOptions.BackgroundImageType.Value = value.Value;
            }
        }
    }

    // TODO: Replace with IObservableProperty
    public NameValue<LastWindowCloseBehavior>? SelectedLastWindowCloseBehavior
    {
        get => field ??= AppOptions.LastWindowCloseBehaviors.Single(t => t.Value == AppOptions.LastWindowCloseBehavior.Value);
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                AppOptions.LastWindowCloseBehavior.Value = value.Value;
            }
        }
    }

    [Command("SetBackgroundImageFolderCommand")]
    private async Task SetBackgroundImageFolderAsync()
    {
        ValueResult<bool, string?> result = FileSystemPickerInteraction.PickFolder(SH.ViewPageSettingBackgroundImagePickFolderTitle);
        if (result.TryGetValue(out string? path))
        {
            await TaskContext.SwitchToMainThreadAsync();
            AppOptions.BackgroundImagePath.Value = path;
        }
    }

    [Command("ResetBackgroundImageFolderCommand")]
    private void ResetBackgroundImageFolder()
    {
        AppOptions.BackgroundImagePath.Value = string.Empty;
    }
}
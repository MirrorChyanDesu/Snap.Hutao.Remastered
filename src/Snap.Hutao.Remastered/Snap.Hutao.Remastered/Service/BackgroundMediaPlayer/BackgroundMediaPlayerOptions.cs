// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Snap.Hutao.Remastered.Service.BackgroundMediaPlayer;

public enum BackgroundMediaType
{
    None = 0,
    LocalFolder = 1,
    HutaoWeb = 2,
}

[Service(ServiceLifetime.Singleton)]
public sealed partial class BackgroundMediaPlayerOptions : ObservableObject
{
    [ObservableProperty]
    public partial BackgroundMediaType BackgroundMediaType { get; set; } = BackgroundMediaType.None;

    [ObservableProperty]
    public partial string? BackgroundMediaPath { get; set; } = "https://launcher-webstatic.mihoyo.com/launcher-public/2026/01/08/f3c44cd72c6214ed680afe5fe90b26fc_6413191254498564796.webm";

    [ObservableProperty]
    public partial bool IsMuted { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLooping { get; set; } = true;
}

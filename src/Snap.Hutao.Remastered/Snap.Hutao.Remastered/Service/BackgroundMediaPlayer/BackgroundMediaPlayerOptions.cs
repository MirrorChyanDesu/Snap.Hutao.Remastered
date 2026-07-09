// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Snap.Hutao.Remastered.Service.BackgroundMediaPlayer;

[Service(ServiceLifetime.Singleton)]
public sealed partial class BackgroundMediaPlayerOptions : ObservableObject
{
    [ObservableProperty]
    public partial BackgroundMediaType BackgroundMediaType { get; set; } = BackgroundMediaType.None;

    [ObservableProperty]
    public partial string? BackgroundMediaPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsMuted { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLooping { get; set; } = true;
}

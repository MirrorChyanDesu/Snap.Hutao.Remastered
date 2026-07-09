// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Snap.Hutao.Remastered.Web.Hoyolab.HoyoPlay;

internal sealed class OfficialLauncherBackground
{
    [JsonPropertyName("retcode")]
    public int Retcode { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("data")]
    public OfficialLauncherBackgroundData? Data { get; init; }
}

internal sealed class OfficialLauncherBackgroundData
{
    [JsonPropertyName("game_info_list")]
    public List<OfficialLauncherGameInfo> GameInfoList { get; init; } = default!;
}

internal sealed class OfficialLauncherGameInfo
{
    [JsonPropertyName("game")]
    public OfficialLauncherGame Game { get; init; } = default!;

    [JsonPropertyName("backgrounds")]
    public List<OfficialLauncherBackgroundItem> Backgrounds { get; init; } = default!;
}

internal sealed class OfficialLauncherGame
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("biz")]
    public string Biz { get; init; } = default!;
}

internal sealed class OfficialLauncherBackgroundItem
{
    [JsonPropertyName("video")]
    public OfficialLauncherVideo? Video { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = default!;
}

internal sealed class OfficialLauncherVideo
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = default!;
}

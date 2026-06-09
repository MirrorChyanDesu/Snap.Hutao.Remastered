// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Remastered.Core;
using Snap.Hutao.Remastered.Model.Intrinsic;

namespace Snap.Hutao.Remastered.UI.Xaml.Data.Converter.Specialized;

public sealed partial class HardChallengeDifficultyIconConverter : ValueConverter<HardChallengeDifficultyLevel, Uri>
{
    public static Uri Convert(string iconName)
    {
        return InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/Icon/{iconName}.png");
    }

    public override Uri Convert(HardChallengeDifficultyLevel from)
    {
        return InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/Icon/UI_LeyLineChallenge_Medal_{from:D}.png");
    }
}
// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Snap.Hutao.Remastered.Core.LifeCycle.InterProcess.Model;

public sealed class ToastNotificationRequest
{
    public ToastNotificationRequest(string rawXml)
    {
        RawXml = rawXml;
    }

    public string RawXml { get; }
}

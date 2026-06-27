// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Remastered.Core.Setting;

namespace Snap.Hutao.Remastered.Web;

public static class ServerDomain
{
    private static volatile ServerDomainMode currentMode = ServerDomainMode.Primary;

    /// <summary>
    /// Sync mode from external setting (e.g. called after AppOptions loads persisted value).
    /// </summary>
    public static void SetMode(ServerDomainMode mode)
    {
        currentMode = mode;
    }

    public static string GetHomaRoot()
    {
        return IsBackup() ? "https://homa.hutaorp.org" : "https://homa.snaphutaorp.org";
    }

    public static string GetApiRoot()
    {
        return IsBackup() ? "https://api.hutaorp.org" : "https://api.snaphutaorp.org";
    }

    public static string GetRootDomain()
    {
        return IsBackup() ? "https://hutaorp.org" : "https://snaphutaorp.org";
    }

    /// <summary>
    /// Auto fallback: switch to backup domain and persist to LocalSetting.
    /// During early startup LocalSetting may not be available; only the in-memory state is set.
    /// </summary>
    public static void TryAutoFallback()
    {
        if (currentMode is ServerDomainMode.Primary)
        {
            currentMode = ServerDomainMode.Backup;
            UnsafeLocalSetting.Set(SettingKeys.ServerDomainMode, ServerDomainMode.Backup);
        }
    }

    public static bool IsBackupMode()
    {
        return currentMode is ServerDomainMode.Backup;
    }

    private static bool IsBackup()
    {
        return IsBackupMode();
    }
}

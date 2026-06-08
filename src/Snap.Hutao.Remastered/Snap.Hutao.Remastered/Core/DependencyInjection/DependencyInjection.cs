// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Microsoft.Windows.Globalization;
using Quartz;
using Snap.Hutao.Remastered.Core.Logging;
using Snap.Hutao.Remastered.Factory.Process;
using Snap.Hutao.Remastered.Model.Entity.Database;
using Snap.Hutao.Remastered.Service;
using Snap.Hutao.Remastered.Service.Notification;
using Snap.Hutao.Remastered.Web.Response;
using Snap.Hutao.Remastered.Win32;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Snap.Hutao.Remastered.Core.DependencyInjection;

public static class DependencyInjection
{
    public static ServiceProvider Initialize(bool isDefault = false)
    {
        IServiceCollection services = new ServiceCollection()

            // Microsoft extension
            .AddLogging(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFilter(DbLoggerCategory.Database.Command.Name, level => level >= LogLevel.Information)
                    .AddFilter(DbLoggerCategory.Query.Name, level => level >= LogLevel.Information)
                    .AddDebug()
                    .AddSentryTelemetry();
            })
            .AddMemoryCache()

            // Quartz
            .AddQuartz()

            // Hutao extensions
            .AddJsonOptions()
            .AddDatabase()
            .AddServices()
            .AddResponseValidation()
            .AddConfiguredHttpClients()

            // Discrete services
            .AddSingleton<IMessenger, WeakReferenceMessenger>();

        ServiceProvider serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
#if DEBUG
            ValidateOnBuild = true,
            ValidateScopes = true,
#endif
        });

        if (isDefault)
        {
            serviceProvider.EnsureDatabaseMigrated();
            Ioc.Default.ConfigureServices(serviceProvider);
        }

        serviceProvider.InitializeCulture();
        serviceProvider.InitializeNotification();

        return serviceProvider;
    }

    extension(IServiceProvider serviceProvider)
    {
        private void EnsureDatabaseMigrated()
        {
            string dbFile = HutaoRuntime.GetDataDirectoryFile("Userdata.db");
            string sqlConnectionString = $"Data Source={dbFile}";

            try
            {
                using (AppDbContext context = AppDbContext.Create(serviceProvider, sqlConnectionString))
                {
                    if (context.Database.GetPendingMigrations().Any())
                    {
                        serviceProvider.GetRequiredService<ILogger<AppDbContext>>().LogInformation("[Database] Performing AppDbContext Migrations");
                        context.Database.Migrate();
                    }
                }
            }
            catch (DbException ex)
            {
                string message = $"""
                    Snap Hutao 在执行数据库迁移时发生错误。
                    Snap Hutao encountered an error while performing database migration.

                    Database at '{dbFile}'

                    {ex.Message}
                    """;
                HutaoNative.Instance.ShowErrorMessage("Warning | 警告", message);
                ProcessFactory.KillCurrent();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCulture()
        {
            CultureOptions cultureOptions = serviceProvider.GetRequiredService<CultureOptions>();
            cultureOptions.SystemCulture = CultureInfo.CurrentCulture;

            CultureInfo cultureInfo = cultureOptions.CurrentCulture.Value;

            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = cultureInfo.Name;
            }
            catch (COMException)
            {
                // 0x80070032 ERROR_NOT_SUPPORTED
            }

            SH.Culture = cultureInfo;
            XamlApplicationLifetime.CultureInfoInitialized = true;

            ILogger<CultureOptions> logger = serviceProvider.GetRequiredService<ILogger<CultureOptions>>();
            logger.LogDebug("System Culture: {System}", cultureOptions.SystemCulture);
            logger.LogDebug("Current Culture: {Current}", cultureInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeNotification()
        {
            _ = serviceProvider.GetRequiredService<IAppNotificationLifeTime>();
        }
    }
}
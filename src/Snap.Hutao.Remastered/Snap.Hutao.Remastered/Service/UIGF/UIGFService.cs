// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.


namespace Snap.Hutao.Remastered.Service.UIGF;

[Service(ServiceLifetime.Singleton, typeof(IUIGFService))]
public sealed partial class UIGFService : IUIGFService
{
    private readonly IServiceProvider serviceProvider;
    private readonly JsonSerializerOptions jsonOptions;

    [GeneratedConstructor]
    public partial UIGFService(IServiceProvider serviceProvider);

    public ValueTask ExportAsync(UIGFExportOptions exportOptions, CancellationToken token = default)
    {
        IUIGFExportService exportService = serviceProvider.GetRequiredKeyedService<IUIGFExportService>(exportOptions.Version);
        return exportService.ExportAsync(exportOptions, token);
    }

    public ValueTask ImportAsync(UIGFImportOptions importOptions, CancellationToken token = default)
    {
        UIGFVersion version = importOptions.UIGF.Info.Version switch
        {
            "v4.0" => UIGFVersion.UIGF40,
            "v4.1" => UIGFVersion.UIGF41,
            "v4.2" => UIGFVersion.UIGF42,
            _ => UIGFVersion.None,
        };

        IUIGFImportService importService = serviceProvider.GetRequiredKeyedService<IUIGFImportService>(version);
        return importService.ImportAsync(importOptions, token);
    }

    public bool Parse(string json, out Model.InterChange.GachaLog.UIGF? uigf)
    {
        try
        {
            uigf = JsonSerializer.Deserialize<Model.InterChange.GachaLog.UIGF>(json, jsonOptions);

            if (uigf!.Info.Version == "v4.2")
            {
                uigf = JsonSerializer.Deserialize<Model.InterChange.GachaLog.UIGF42>(json, jsonOptions);
            }
            return true;
        }
        catch
        {
            uigf = null;
            return false;
        }
    }
}

// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Snap.Hutao.Remastered.Service.UIGF;

[Service(ServiceLifetime.Transient, typeof(IUIGFExportService), Key = UIGFVersion.UIGF21)]
public sealed partial class UIGF21ExportService : AbstractUIGF3ExportService
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UIGF21ExportService(IServiceProvider serviceProvider);

    protected override string Version { get; } = "v2.1";
}

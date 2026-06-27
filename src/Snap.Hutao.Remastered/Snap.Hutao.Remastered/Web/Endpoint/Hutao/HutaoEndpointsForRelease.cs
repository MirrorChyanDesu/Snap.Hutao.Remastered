// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Snap.Hutao.Remastered.Web.Endpoint.Hutao;

[Service(ServiceLifetime.Singleton, typeof(IHutaoEndpoints), Key = HutaoEndpointsKind.Release)]
public sealed class HutaoEndpointsForRelease : IHutaoEndpoints
{
    string IHomaRootAccess.Root { get => ServerDomain.GetHomaRoot(); }

    string IInfrastructureRootAccess.Root { get => ServerDomain.GetApiRoot(); }

    string IInfrastructureRawRootAccess.RawRoot { get => ServerDomain.GetApiRoot(); }
}
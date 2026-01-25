// <copyright file="HubAuthStubs.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using Microsoft.AspNetCore.SignalR;

/// <summary>Minimal Hub used to test RequireAuthorization on /hub/search when EnforceSecurity (PR-02).</summary>
public sealed class SearchHubAuthStub : Hub
{
}

/// <summary>Minimal Hub used to test RequireAuthorization on /hub/relay when EnforceSecurity (PR-02).</summary>
public sealed class RelayHubAuthStub : Hub
{
}

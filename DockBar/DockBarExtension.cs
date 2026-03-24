// Copyright (c) DockBar
// Licensed under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace DockBar;

[ComVisible(true)]
[Guid("8E3BB8F6-7B45-42E2-9F77-6731615C4529")]
[ComDefaultInterface(typeof(IExtension))]
public sealed partial class DockBarExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly DockBarCommandProvider _provider = new();

    public DockBarExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null!,
        };
    }

    public void Dispose()
    {
        _extensionDisposedEvent.Set();
    }
}

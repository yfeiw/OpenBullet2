﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenBullet2.Core.Entities;
using OpenBullet2.Core.Models.Proxies;
using OpenBullet2.Core.Repositories;
using RuriLib.Models.Proxies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenBullet2.Core.Services;

/// <summary>
/// A reload service that will reload proxies from an <see cref="IProxyGroupRepository"/>.
/// </summary>
public class ProxyReloadService : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly IServiceScopeFactory _scopeFactory;

    public ProxyReloadService(IServiceScopeFactory scopeFactory)
    {
        _semaphore = new SemaphoreSlim(1, 1);
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Reloads proxies from a group with a given <paramref name="groupId"/> of a user with a given
    /// <paramref name="userId"/>.
    /// </summary>
    public async Task<IEnumerable<Proxy>> ReloadAsync(int groupId, int userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var proxyGroupsRepo = scope.ServiceProvider.GetRequiredService<IProxyGroupRepository>();
        var proxyRepo = scope.ServiceProvider.GetRequiredService<IProxyRepository>();

        List<ProxyEntity> entities;

        // Only allow reloading one group at a time (multiple threads should
        // not use the same DbContext at the same time).
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            // If the groupId is -1 reload all proxies
            if (groupId == -1)
            {
                entities = userId == 0
                    ? await proxyRepo.GetAll().ToListAsync(cancellationToken).ConfigureAwait(false)
                    : await proxyRepo.GetAll().Include(p => p.Group).ThenInclude(g => g.Owner)
                        .Where(p => p.Group.Owner.Id == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var group = await proxyGroupsRepo.GetAsync(groupId, cancellationToken).ConfigureAwait(false);
                entities = await proxyRepo.GetAll()
                    .Where(p => p.Group.Id == groupId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        var proxyFactory = new ProxyFactory();
        return entities.Select(e => ProxyFactory.FromEntity(e));
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}

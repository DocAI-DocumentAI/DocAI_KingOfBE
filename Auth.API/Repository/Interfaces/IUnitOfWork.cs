using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Repository.Interfaces;

public interface IUnitOfWork : IGenericRepositoryFactory, IDisposable
{
    int Commit();

    Task<int> CommitAsync();
}

public interface IUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    TContext Context { get; }
}
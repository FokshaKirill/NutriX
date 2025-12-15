using Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DatabaseContext _db;

        public Repository(DatabaseContext db)
        {
            _db = db;
        }

        public async Task<T?> GetByIdAsync(int id)
            => await _db.Set<T>().FindAsync(id);

        public IQueryable<T> Query()
            => _db.Set<T>();

        public async Task AddAsync(T entity)
        {
            await _db.Set<T>().AddAsync(entity);
        }

        public Task UpdateAsync(T entity)
        {
            _db.Set<T>().Update(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(T entity)
        {
            _db.Set<T>().Remove(entity);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
            => await _db.SaveChangesAsync();
    }

}

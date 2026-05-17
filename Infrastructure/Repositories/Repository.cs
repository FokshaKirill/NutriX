using Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DatabaseContext _db;

        public Repository(DatabaseContext db)
        {
            _db = db;
        }

        public async Task<T?> GetByIdAsync(Guid id)
            => await _db.Set<T>().FindAsync(id);
        
        public async Task<List<T>> GetAllAsync()
            => await _db.Set<T>().ToListAsync();

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
        
        public async Task AddRangeAsync(IEnumerable<T> entities)
            => await _db.Set<T>().AddRangeAsync(entities);
        
        public void RemoveRange(IEnumerable<T> entities)
            => _db.Set<T>().RemoveRange(entities);
    }

}

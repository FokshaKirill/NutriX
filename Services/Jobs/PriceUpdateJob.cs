using Domain.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Jobs
{
    /// <summary>
    /// Hangfire-джоб: обновляет цены всех продуктов каждые 5 дней.
    /// Изменение рандомное ±5–15% от текущей цены.
    /// </summary>
    public class PriceUpdateJob
    {
        private readonly IRepository<Product> _products;
        private readonly ILogger<PriceUpdateJob> _log;

        public PriceUpdateJob(IRepository<Product> products, ILogger<PriceUpdateJob> log)
        {
            _products = products;
            _log      = log;
        }

        public async Task ExecuteAsync(decimal minPct = 5m, decimal maxPct = 15m)
        {
            _log.LogInformation("[PriceUpdateJob] Старт обновления цен...");

            var products = await _products.Query()
                .Where(p => p.PricePerUnit > 0 && p.Unit != "категория")
                .ToListAsync();

            var rng     = new Random();
            int updated = 0;

            foreach (var product in products)
            {
                var pct   = (decimal)(rng.NextDouble() * (double)(maxPct - minPct) + (double)minPct) / 100m;
                var sign  = rng.Next(2) == 0 ? 1m : -1m;
                var delta = product.PricePerUnit * pct * sign;

                product.PricePerUnit = Math.Max(1m, Math.Round(product.PricePerUnit + delta, 2));
                await _products.UpdateAsync(product);
                updated++;
            }

            await _products.SaveChangesAsync();
            _log.LogInformation("[PriceUpdateJob] Готово. Обновлено: {Count}", updated);
        }
    }
}
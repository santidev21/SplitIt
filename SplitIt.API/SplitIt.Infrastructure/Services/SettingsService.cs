using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;

namespace SplitIt.Infrastructure.Services
{
    public class SettingsService
    {
        public const string RegistrationEnabled = "RegistrationEnabled";
        public const string MaxExpenseAmount = "MaxExpenseAmount";

        private readonly AppDbContext _context;

        public SettingsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetRawAsync(string key)
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value;
        }

        public async Task<T> GetValueAsync<T>(string key, T fallback)
        {
            var raw = await GetRawAsync(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            try
            {
                return (T)Convert.ChangeType(raw, typeof(T));
            }
            catch
            {
                return fallback;
            }
        }

        public async Task SetValueAsync(string key, string value)
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                _context.AppSettings.Add(new AppSetting { Key = key, Value = value });
            }
            else
            {
                setting.Value = value;
            }
            await _context.SaveChangesAsync();
        }

        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            return await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Infrastructure.Services
{
    public class CurrenciesService
    {
        private readonly AppDbContext _context;

        public CurrenciesService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Currency>> GetAllAsync()
        {
            return await _context.Currencies.ToListAsync();
        }

    }
}

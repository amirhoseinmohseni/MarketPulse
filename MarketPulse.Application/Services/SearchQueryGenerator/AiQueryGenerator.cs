using MarketPulse.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketPulse.Application.Services.SearchQueryGenerator
{
    public class AiQueryGenerator : ISearchQueryGenerator
    {
        public Task<List<SearchQuery>> GenerateAsync(string idea, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}

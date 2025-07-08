using System;
using System.Collections.Generic;

namespace Analytics.CrossCutting.Dtos
{
    public class LinkStatsDto
    {
        public Guid LinkId { get; set; }
        public int TotalClicks { get; set; }
        public int UniqueClicks { get; set; }
        public DateTime? FirstClick { get; set; }
        public DateTime? LastClick { get; set; }
        public List<DailyClickStatsDto> DailyStats { get; set; } = new();
        public List<CountryStatsDto> CountryStats { get; set; } = new();
    }

    public class DailyClickStatsDto
    {
        public DateTime Date { get; set; }
        public int Clicks { get; set; }
    }

    public class CountryStatsDto
    {
        public string Country { get; set; } = null!;
        public int Clicks { get; set; }
    }
}
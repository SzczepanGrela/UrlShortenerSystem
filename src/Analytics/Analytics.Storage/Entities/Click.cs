using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UrlShortenerSystem.Common.Storage.Entities;

namespace Analytics.Storage.Entities
{
    [Table("Clicks", Schema = "Analytics")]
    public class Click : BaseEntity
    {
        [Required]
        public Guid LinkId { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(500)]
        public string? Referer { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }
    }
}
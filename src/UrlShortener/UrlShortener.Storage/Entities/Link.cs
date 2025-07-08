using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using UrlShortenerSystem.Common.Storage.Entities;

namespace UrlShortener.Storage.Entities
{
    [Index(nameof(ShortCode), IsUnique = true)]
    [Index(nameof(IsActive))]
    [Index(nameof(ExpirationDate))]
    [Index(nameof(CreationDate))]
    [Index(nameof(OriginalUrl), nameof(IsActive))]
    [Table("Links", Schema = "UrlShortener")]
    public class Link : BaseEntity
    {
        [Required]
        [MaxLength(2048)]
        public string OriginalUrl { get; set; } = null!;

        [Required]
        [MaxLength(10)]
        public string ShortCode { get; set; } = null!;

        [Required]
        public DateTime CreationDate { get; set; }

        public DateTime? ExpirationDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
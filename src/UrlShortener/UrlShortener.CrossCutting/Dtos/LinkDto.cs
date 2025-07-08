using System;
using System.ComponentModel.DataAnnotations;

namespace UrlShortener.CrossCutting.Dtos
{
    public class LinkDto
    {
        public Guid Id { get; set; }
        
        [Required]
        [Url]
        [MaxLength(2048)]
        public string OriginalUrl { get; set; } = null!;
        
        public string ShortUrl { get; set; } = null!;
        
        public string ShortCode { get; set; } = null!;
        
        public DateTime CreationDate { get; set; }
        
        public DateTime? ExpirationDate { get; set; }
        
        public bool IsActive { get; set; }
    }
}
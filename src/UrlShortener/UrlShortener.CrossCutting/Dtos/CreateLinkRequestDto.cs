using System;
using System.ComponentModel.DataAnnotations;

namespace UrlShortener.CrossCutting.Dtos
{
    public class CreateLinkRequestDto
    {
        [Required]
        [Url]
        [MaxLength(2048)]
        public string OriginalUrl { get; set; } = null!;
        
        public DateTime? ExpirationDate { get; set; }
    }
}
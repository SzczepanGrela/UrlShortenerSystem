using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using UrlShortenerSystem.Common.CrossCutting.Interfaces;

namespace UrlShortenerSystem.Common.Storage.Entities
{
    [Index(nameof(LanguageCode), IsUnique = false)]
    public class BaseTranslation : BaseEntity, IEntityTranslation
    {
        [MaxLength(16)]
        [Required]
        public string LanguageCode { get; set; } = null!;
    }
}
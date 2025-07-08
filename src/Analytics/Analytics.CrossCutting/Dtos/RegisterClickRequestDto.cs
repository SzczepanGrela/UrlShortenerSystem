using System;
using System.ComponentModel.DataAnnotations;

namespace Analytics.CrossCutting.Dtos
{
    public class RegisterClickRequestDto
    {
        [Required]
        public Guid LinkId { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public string? Referer { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace Analytics.CrossCutting.Dtos
{
    public class ClickDto
    {
        public Guid Id { get; set; }

        [Required]
        public Guid LinkId { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public string? Referer { get; set; }

        public string? Country { get; set; }

        public string? City { get; set; }
    }
}
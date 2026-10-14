using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace CatholicCompanion.Api.Models
{
    public class DateRequest : IValidatableObject
    {
        [Required]
        [StringLength(50)]
        public string? Date { get; set; }

        public bool TryGetDate(out DateOnly date)
        {
            if (!DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                date = default;
                return false;
            }

            // Preserve the calendar day supplied by existing ISO timestamp clients.
            if (Date?.Length >= 10 && DateOnly.TryParseExact(Date[..10], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!TryGetDate(out var date) || date.Year < 1900 || date.Year > 2100)
            {
                yield return new ValidationResult("Provide a valid date between 1900 and 2100.",
                    new[] { nameof(Date) });
            }
        }
    }
}

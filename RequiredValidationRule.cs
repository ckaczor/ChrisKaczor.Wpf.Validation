using System.Globalization;
using System.Windows.Controls;

namespace ChrisKaczor.Wpf.Validation
{
    public class RequiredValidationRule : ValidationRule
    {
        public static string GetErrorMessage(object? fieldValue)
        {
            var errorMessage = string.Empty;

            if (fieldValue == null || string.IsNullOrWhiteSpace(fieldValue.ToString()))
                errorMessage = "Required";

            return errorMessage;
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var error = GetErrorMessage(value);

            return !string.IsNullOrEmpty(error) ? new ValidationResult(false, error) : ValidationResult.ValidResult;
        }
    }
}

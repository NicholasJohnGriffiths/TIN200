namespace TINWeb.Models
{
    public enum TinStatus
    {
        Blank = 0,
        Tin200 = 1,
        Tin200Potential = 2,
        Tin1000 = 3,
        TinTest = 4
    }

    public static class TinStatusHelper
    {
        public static readonly (int Value, string Label)[] DropdownOptions =
        {
            ((int)TinStatus.Tin200, "TIN200"),
            ((int)TinStatus.Tin200Potential, "TIN200Potential"),
            ((int)TinStatus.Tin1000, "TIN1000"),
            ((int)TinStatus.TinTest, "TINTest")
        };

        public static bool IsValidSelection(int? status)
        {
            return !status.HasValue
                || status.Value == (int)TinStatus.Blank
                || status.Value == (int)TinStatus.Tin200
                || status.Value == (int)TinStatus.Tin200Potential
                || status.Value == (int)TinStatus.Tin1000
                || status.Value == (int)TinStatus.TinTest;
        }

            public static bool IsTestCompany(int? status)
            {
                return status.HasValue && status.Value == (int)TinStatus.TinTest;
            }

        public static bool TryParseLegacyTin200(string? rawValue, out int? status)
        {
            var value = rawValue?.Trim();
            if (string.IsNullOrWhiteSpace(value)
                || string.Equals(value, "blank", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            {
                status = null;
                return true;
            }

            if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "TIN200", StringComparison.OrdinalIgnoreCase))
            {
                status = (int)TinStatus.Tin200;
                return true;
            }

            if (string.Equals(value, "TIN200Potential", StringComparison.OrdinalIgnoreCase))
            {
                status = (int)TinStatus.Tin200Potential;
                return true;
            }

            if (string.Equals(value, "TIN1000", StringComparison.OrdinalIgnoreCase))
            {
                status = (int)TinStatus.Tin1000;
                return true;
            }

            status = null;
            return false;
        }
    }
}

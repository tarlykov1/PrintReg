namespace GSPLabelPrinter.Configuration;

public static class AppSettingsValidator
{
    public static (bool Ok, string Message, string Code) Validate(AppSettings settings)
    {
        if (settings.Server.Port != 5187) return (false, "Порт приложения должен быть 5187.", "INVALID_SETTINGS");
        if (settings.Data.MaximumBackups < 0 || settings.Data.MaximumBackups > 500) return (false, "Количество резервных копий должно быть от 0 до 500.", "INVALID_SETTINGS");
        if (settings.Printing.LabelWidthMm <= 0 || settings.Printing.LabelWidthMm > 200) return (false, "Ширина наклейки должна быть от 1 до 200 мм.", "INVALID_SETTINGS");
        if (settings.Printing.LabelHeightMm <= 0 || settings.Printing.LabelHeightMm > 200) return (false, "Высота наклейки должна быть от 1 до 200 мм.", "INVALID_SETTINGS");
        if (settings.Printing.Copies < 1 || settings.Printing.Copies > 10) return (false, "Количество копий должно быть от 1 до 10.", "INVALID_SETTINGS");
        if (settings.Printing.FullNameFontSize < 6 || settings.Printing.FullNameFontSize > 40) return (false, "Размер шрифта ФИО должен быть от 6 до 40.", "INVALID_SETTINGS");
        if (settings.Printing.PositionFontSize < 6 || settings.Printing.PositionFontSize > 30) return (false, "Размер шрифта должности должен быть от 6 до 30.", "INVALID_SETTINGS");
        if (settings.Printing.MarginMm < 0 || settings.Printing.MarginMm > 15) return (false, "Поля должны быть от 0 до 15 мм.", "INVALID_SETTINGS");
        if (!settings.Printing.Orientation.Equals("Portrait", StringComparison.OrdinalIgnoreCase) && !settings.Printing.Orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase)) return (false, "Ориентация должна быть Portrait или Landscape.", "INVALID_SETTINGS");
        return (true, string.Empty, string.Empty);
    }

    public static AppSettings SanitizeForLocalApplication(AppSettings settings)
    {
        settings.Server.Port = 5187;
        settings.Data.CsvPath = "data/employees.csv";
        settings.Data.BackupDirectory = "backup";
        return settings;
    }
}

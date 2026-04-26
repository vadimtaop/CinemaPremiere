using CinemaPremiereApp.Classes;
using CinemaPremiereApp.Properties;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CinemaPremiereApp
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                // Читаем сохраненные параметры из настроек
                string savedColor = Settings.Default.PrimaryColor;
                bool isDark = Settings.Default.IsDarkMode;

                // Получаем текущий объект темы
                var paletteHelper = new PaletteHelper();
                Theme theme = paletteHelper.GetTheme();

                theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

                // Если цвет сохранен, пытаемся его применить
                if (!string.IsNullOrEmpty(savedColor) &&
                    Enum.TryParse(savedColor, out MaterialDesignColor colorEnum))
                {
                    Color selectedColor = SwatchHelper.Lookup[colorEnum];
                    theme.SetPrimaryColor(selectedColor);
                }

                // Применяем настроенную тему
                paletteHelper.SetTheme(theme);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка настроек автозапуска:\n{ex.Message}",
                    "Кинотеатр \"Премьера\"",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

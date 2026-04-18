using CinemaPremiereApp.Classes;
using CinemaPremiereApp.Properties;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CinemaPremiereApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();

        public SettingsPage()
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(Settings.Default.PrimaryColor))
            {
                foreach (ComboBoxItem item in ColorComboBox.Items)
                {
                    if (item.Tag?.ToString() == Settings.Default.PrimaryColor)
                    {
                        ColorComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void ColorComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorComboBox.SelectedItem is ComboBoxItem selectedItem
                && selectedItem.Tag != null)
            {
                // Извлекаем название цвета из Tag
                string colorName = selectedItem.Tag.ToString();

                try
                {
                    // Получаем текущую тему приложения
                    Theme theme = _paletteHelper.GetTheme();

                    if (Enum.TryParse(colorName, out MaterialDesignColor colorEnum))
                    {
                        Color selectedColor = SwatchHelper.Lookup[colorEnum];

                        theme.SetPrimaryColor(selectedColor);

                        // Применяем обновленную тему
                        _paletteHelper.SetTheme(theme);

                        // Сохраняем выбор в настройках проекта
                        Settings.Default.PrimaryColor = colorName;
                        Settings.Default.Save();
                    }
                }
                catch (Exception ex)
                {
                    MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
                }
            }
        }
    }
}

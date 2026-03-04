using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CinemaPremiereApp.Windows
{
    /// <summary>
    /// Логика взаимодействия для GeneralWindow.xaml
    /// </summary>
    public partial class GeneralWindow : Window
    {
        public GeneralWindow()
        {
            InitializeComponent();
        }

        private void ThemeToggleButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                PaletteHelper paletteHelper = new PaletteHelper();

                var theme = paletteHelper.GetTheme();

                bool isDark = toggleButton.IsChecked ?? false;

                theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

                paletteHelper.SetTheme(theme);
            }
        }
    }
}

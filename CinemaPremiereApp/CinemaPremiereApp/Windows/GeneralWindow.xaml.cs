using CinemaPremiereApp.Classes;
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
        // Статическая ссылка на текущее активное окно
        public static GeneralWindow Instance { get; private set; }

        public GeneralWindow()
        {
            InitializeComponent();

            Instance = this;
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

        private void MenuButtonClick(object sender, RoutedEventArgs e)
        {
            MainDrawerHost.IsLeftDrawerOpen = true;
        }

        // Метод перехода по пунктам меню
        private async void MenuListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuListBox.SelectedItem == null)
                return;

            if (OrdersItem.IsSelected)
            {
                MainFrame.Navigate(new Pages.OrdersPage());
            }

            if (FilmsItem.IsSelected)
            {
                MainFrame.Navigate(new Pages.FilmsPage());
            }

            if (ScheduleItem.IsSelected)
            {
                MainFrame.Navigate(new Pages.OrdersPage());
            }

            if (SettingsItem.IsSelected)
            {
                MainFrame.Navigate(new Pages.OrdersPage());
            }

            if (ExitItem.IsSelected)
            {
                bool isConfirmed = await DialogClass.ShowConfirmDialog("Выход из системы",
                    "Вы уверены, что хотите выйти из учетной записи?",
                    "Да, выйти",
                    "Отмена");

                if (isConfirmed)
                {
                    MenuButton.Visibility = Visibility.Collapsed;

                    MainFrame.Navigate(new Pages.AuthPage());
                }
            }

            MainDrawerHost.IsLeftDrawerOpen = false;
            MenuListBox.SelectedItem = null;
        }
    }
}

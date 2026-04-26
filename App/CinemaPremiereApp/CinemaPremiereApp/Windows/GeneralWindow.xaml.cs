using CinemaPremiereApp.Classes;
using CinemaPremiereApp.Properties;
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

                Settings.Default.IsDarkMode = isDark;
                Settings.Default.Save();
            }
        }

        private void MenuButtonClick(object sender, RoutedEventArgs e)
        {
            MainDrawerHost.IsLeftDrawerOpen = true;
        }

        // Метод перехода по пунктам меню
        private async void MenuListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var currentListBox = sender as ListBox;
            if (currentListBox == null || currentListBox.SelectedItem == null)
                return;

            if (currentListBox == TopMenuListBox)
                BottomMenuListBox.SelectedIndex = -1;
            else
                TopMenuListBox.SelectedIndex = -1;

            var selectedItem = currentListBox.SelectedItem as ListBoxItem;
            if (selectedItem != null)
            {
                switch (selectedItem.Name)
                {
                    case "OrdersItem":
                        MainFrame.Navigate(new Pages.OrdersPage());
                        break;
                    case "FilmsItem":
                        MainFrame.Navigate(new Pages.FilmsPage());
                        break;
                    case "ScheduleItem":
                        MainFrame.Navigate(new Pages.SchedulePage());
                        break;
                    case "SettingsItem":
                        MainFrame.Navigate(new Pages.SettingsPage());
                        break;
                    case "AboutItem":
                        MainFrame.Navigate(new Pages.AboutPage());
                        break;
                    case "ExitItem":
                        bool isConfirmed = await DialogClass.ShowConfirmDialog("Выход из системы",
                            "Вы уверены, что хотите выйти из учетной записи?",
                            "Да, выйти",
                            "Отмена");

                        if (isConfirmed)
                        {
                            MenuButton.Visibility = Visibility.Collapsed;

                            MainFrame.Navigate(new Pages.AuthPage());
                        }
                        break;
                }

                MainDrawerHost.IsLeftDrawerOpen = false;
                TopMenuListBox.SelectedItem = null;
                BottomMenuListBox.SelectedItem = null;
            }
        }

        private void MainSnackbarPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MainSnackbar.IsActive = false;
        }
    }
}

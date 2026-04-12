using CinemaPremiereApp.Windows;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CinemaPremiereApp.Classes
{
    public static class MessageClass
    {
        // Успешное уведомление
        public static void SuccessMessage(string message)
        {
            var snackbar = GeneralWindow.Instance?.MainSnackbar;
            if (snackbar != null)
            {
                SetMessage(snackbar, Color.FromRgb(56, 142, 60), message, PackIconKind.Check);
            }
            
        }

        // Предупреждающее уведомление
        public static void WarningMessage(string message)
        {
            var snackbar = GeneralWindow.Instance?.MainSnackbar;
            if (snackbar != null)
            {
                SetMessage(snackbar, Color.FromRgb(255, 143, 0), message, PackIconKind.Warning);
            }
        }

        // Ошибочное уведомление
        public static void ErrorMessage(string message)
        {
            var snackbar = GeneralWindow.Instance?.MainSnackbar;
            if (snackbar != null)
            {
                SetMessage(snackbar, Color.FromRgb(211, 47, 47), message, PackIconKind.CloseCircle);
            }
        }

        // Настройки уведомления
        public static void SetMessage(Snackbar snackbar, Color color, string message, PackIconKind packIconKind)
        {
            // Используем Dispatcher, чтобы выполнение было в главном потоке
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Очистка очереди
                    snackbar.MessageQueue.Clear();

                    // Сбрасываем фиксированную высоту, чтобы растянуть уведомление
                    snackbar.Height = double.NaN;

                    // Установка цвета фона и шрифта
                    snackbar.Background = new SolidColorBrush(color);
                    snackbar.Foreground = new SolidColorBrush(Colors.White);

                    // Создания контейнера
                    Grid grid = new Grid();

                    grid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = GridLength.Auto
                    });

                    grid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });

                    // Создание иконки
                    PackIcon icon = new PackIcon
                    {
                        Kind = packIconKind,
                        Width = 24,
                        Height = 24,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    Grid.SetColumn(icon, 0);

                    // Создание текста
                    TextBlock textBlock = new TextBlock
                    {
                        Text = message,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        FontSize = 16,
                    };
                    Grid.SetColumn(textBlock, 1);

                    // Добавляем элементы в контейнер
                    grid.Children.Add(icon);
                    grid.Children.Add(textBlock);

                    // Отображение уведомления
                    snackbar.MessageQueue.Enqueue(
                        content: grid,
                        actionContent: null,
                        actionHandler: null,
                        actionArgument: null,
                        promote: true,
                        neverConsiderToBeDuplicate: false,
                        durationOverride: TimeSpan.FromSeconds(3.5));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Произошла неизвестная ошибка при выводе уведомления: {ex.Message}",
                        "Кинотеатр \"Премьера\"",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });
        }
    }
}

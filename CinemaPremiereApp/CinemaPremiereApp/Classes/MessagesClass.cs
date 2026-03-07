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
    public static class MessagesClass
    {
        // Успешное уведомление
        public static void SuccessMessage(this Snackbar snackbar, string message)
        {
            SetMessage(snackbar, Color.FromRgb(56, 142, 60), message, PackIconKind.Check);
        }

        // Предупреждающее уведомление
        public static void WarningMessage(this Snackbar snackbar, string message)
        {
            SetMessage(snackbar, Color.FromRgb(255, 143, 0), message, PackIconKind.Warning);
        }

        // Ошибочное уведомление
        public static void ErrorMessage(this Snackbar snackbar, string message)
        {
            SetMessage(snackbar, Color.FromRgb(211, 47, 47), message, PackIconKind.CloseCircle);
        }

        // Настройки уведомления
        public static void SetMessage(Snackbar snackbar, Color color, string message, PackIconKind packIconKind)
        {
            try
            {
                // Очистка очереди
                snackbar.MessageQueue.Clear();

                // Установка цвета фона
                snackbar.Background = new SolidColorBrush(color);

                // Установка цвета шрифта
                snackbar.Foreground = new SolidColorBrush(Colors.White);

                // Создания контейнера
                StackPanel stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Создание иконки
                PackIcon icon = new PackIcon
                {
                    Kind = packIconKind,
                    //Kind = PackIconKind.Warning,
                    //Kind = PackIconKind.Check,
                    Width = 24,
                    Height = 24,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                // Создание текста
                TextBlock textBlock = new TextBlock
                {
                    Text = message,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 16,
                };

                // Добавляем элементы в контейнер
                stackPanel.Children.Add(icon);
                stackPanel.Children.Add(textBlock);

                // Отображение уведомления
                snackbar.MessageQueue.Enqueue(
                    content: stackPanel,
                    actionContent: null,
                    actionHandler: null,
                    actionArgument: null,
                    promote: true,
                    neverConsiderToBeDuplicate: false,
                    durationOverride: TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла неизвестная ошибка: {ex.Message}",
                    "Кинотеатр \"Премьера\"",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

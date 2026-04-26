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
    public static class DialogClass
    {
        public static async Task<bool> ShowConfirmDialog(string title, string message, string yes, string no)
        {
            var dialogContent = new StackPanel
            {
                Margin = new Thickness(20),
                Width = 350
            };

            dialogContent.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            dialogContent.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var yesButton = new Button
            {
                Content = yes,
                IsDefault = true,
                FontWeight = FontWeights.Bold,
                Style = (Style)Application.Current.FindResource("MaterialDesignFlatButton"),
                Command = DialogHost.CloseDialogCommand,
                CommandParameter = true
            };

            var noButton = new Button
            {
                Content = no,
                IsCancel = true,
                Style = (Style)Application.Current.FindResource("MaterialDesignFlatButton"),
                Command = DialogHost.CloseDialogCommand,
                CommandParameter = false
            };

            buttonsPanel.Children.Add(noButton);
            buttonsPanel.Children.Add(yesButton);
            dialogContent.Children.Add(buttonsPanel);

            var result = await DialogHost.Show(dialogContent, "GlobalDialogHost");

            return result is bool boolResult && boolResult;
        }
    }
}

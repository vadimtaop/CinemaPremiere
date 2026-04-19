using CinemaPremiereApp.Ado;
using CinemaPremiereApp.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Data.Entity;
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
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace CinemaPremiereApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для SchedulePage.xaml
    /// </summary>
    /// 
    public class ScheduleMovie
    {
        public string Title { get; set; }
        public string ImagePath { get; set; }
        public string AgeRating { get; set; }
        public string Genre { get; set; }
    }

    public class ScheduleSession : INotifyPropertyChanged
    {
        private string _time;
        private string _title;
        private string _price;

        public string Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); }
        }
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        public string Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ScheduleDay
    {
        public string DateText { get; set; }
        public ObservableCollection<ScheduleSession> Sessions { get; set; }
            = new ObservableCollection<ScheduleSession>();
    }

    public class ScheduleSaveData
    {
        public List<ScheduleMovie> SavedMovies { get; set; }
        public List<ScheduleDay> SavedDays { get; set; }
        public string MainTitle { get; set; }
        public string Subtitle { get; set; }
        public string Phone { get; set; }
    }

    public partial class SchedulePage : Page
    {
        // Список фильмов, который будет отрисовываться на холсте
        public ObservableCollection<ScheduleMovie> Movies { get; set; } 
            = new ObservableCollection<ScheduleMovie>();

        // Коллекция дней
        public ObservableCollection<ScheduleDay> Days { get; set; }
            = new ObservableCollection<ScheduleDay>();

        public List<Films> AvailableFilms { get; set; }

        // Путь к файлу сохранения в папке с приложением
        private string savePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autosave.json");

        public SchedulePage()
        {
            InitializeComponent();

            // Привязываем данные страницы к самой себе
            this.DataContext = this;

            InitAsync();
        }

        private async void InitAsync()
        {
            await LoadDataAsync();
            await LoadScheduleAsync();
            SetupAutoSave();
        }

        private async Task SaveScheduleAsync()
        {
            string title = TitleTextBox.Text;
            string sub = SubtitleTextBox.Text;
            string phone = PhoneTextBox.Text;

            await Task.Run(() =>
            {
                try
                {
                    var data = new
                    {
                        SavedMovies = Movies.ToList(),
                        SavedDays = Days.ToList(),
                        MainTitle = title,
                        Subtitle = sub,
                        Phone = phone
                    };

                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(savePath, json);
                }
                catch
                {

                }
            });
        }

        private async Task LoadScheduleAsync()
        {
            if (!File.Exists(savePath))
                return;

            try
            {
                string json = await Task.Run(() => File.ReadAllText(savePath));
                var data = JsonConvert.DeserializeObject<ScheduleSaveData>(json);

                if (data != null)
                {
                    // Загружаем постеры
                    Movies.Clear();
                    foreach (var m in data.SavedMovies)
                        Movies.Add(m);

                    // Загружаем дни и сеансы
                    Days.Clear();
                    foreach (var d in data.SavedDays)
                        Days.Add(d);

                    // Восстанавливаем текст
                    TitleTextBox.Text = data.MainTitle;
                    SubtitleTextBox.Text = data.Subtitle;
                    PhoneTextBox.Text = data.Phone;
                }
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                AvailableFilms = await AppData.db.Films
                    .Include(f => f.Genres)
                    .Include(f => f.AgeRatings)
                    .ToListAsync();

                FilmsListBox.ItemsSource = AvailableFilms;
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
            }
        }

        private void SetupAutoSave()
        {
            // Следим за спиской фильмов
            Movies.CollectionChanged += async (s, e) => await SaveScheduleAsync();

            // Следим за списком дней
            Days.CollectionChanged += async (s, e) =>
            {
                await SaveScheduleAsync();

                if (e.NewItems != null)
                {
                    foreach (ScheduleDay day in e.NewItems)
                    {
                        day.Sessions.CollectionChanged += async (s1, e1) =>
                        {
                            await SaveScheduleAsync();

                            if (e1.NewItems != null)
                                foreach (ScheduleSession sess in e1.NewItems)
                                    SubcribeSession(sess);
                        };
                    }
                }
            };

            foreach (var day in Days)
            {
                day.Sessions.CollectionChanged += async (s, e) =>
                {
                    await SaveScheduleAsync();

                    if (e.NewItems != null)
                        foreach (ScheduleSession sess in e.NewItems)
                            SubcribeSession(sess);
                };

                foreach (var sess in day.Sessions)
                    SubcribeSession(sess);
            }
        }

        private async void OnSessionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await SaveScheduleAsync();
        }

        private void SubcribeSession(ScheduleSession session)
        {
            if (session == null)
                return;

            session.PropertyChanged -= OnSessionPropertyChanged;
            session.PropertyChanged += OnSessionPropertyChanged;
        }

        private void ExportButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Прячем кнопки удаления перед рендером
                ToggleUIElements(Visibility.Collapsed);

                // Создаем диалог сохранения файла
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                    Title = "Сохранить расписание",
                    FileName = $"Расписание_{DateTime.Now:dd_MM_yyyy}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Получаем размеры нашего холста (если 0 - берем 1920x1080)
                    double width = ExportGrid.ActualWidth > 0 ? ExportGrid.ActualWidth : 1920;
                    double height = ExportGrid.ActualHeight > 0 ? ExportGrid.ActualHeight : 1080;

                    // Создаем виртуальноую камеру
                    RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                        (int)width, (int)height, 96, 96, PixelFormats.Pbgra32);

                    // Фотографируем контейнер
                    renderTargetBitmap.Render(ExportGrid);

                    // Определяем формат
                    BitmapEncoder encoder;
                    string extension = System.IO.Path.GetExtension(saveFileDialog.FileName).ToLower();

                    if (extension == ".png")
                    {
                        encoder = new PngBitmapEncoder();
                    }
                    else
                    {
                        encoder = new JpegBitmapEncoder { QualityLevel = 100 };
                    }

                    encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                    // Сохранение файла
                    using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(stream);
                    }

                    MessageClass.SuccessMessage($"Успех\nРасписание сохранено в формате {extension.ToUpper()}");
                }
            }
            catch (Exception ex)
            {
                MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
            }
            finally
            {
                // Всегда возвращаем кнопки назад
                ToggleUIElements(Visibility.Visible);
            }
        }

        private void ToggleUIElements(Visibility visibility)
        {
            foreach (var button in FindVisualChildren<Button>(ExportGrid))
            {
                if (button.Tag?.ToString() == "HideOnExport")
                {
                    button.Visibility = visibility;
                }
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);

                    if (child != null && child is T)
                        yield return (T)child;

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }

        private async void AddSelectedMovieButtonClick(object sender, RoutedEventArgs e)
        {
            // Берем выбранный фильм из ListBox в диалоге
            var selectedFilm = FilmsListBox.SelectedItem as Films;

            if (selectedFilm != null)
            {
                // Создаем объект
                var movie = new ScheduleMovie
                {
                    Title = selectedFilm.Title,
                    ImagePath = selectedFilm.PosterPath?.ToString(),
                    AgeRating = selectedFilm.AgeRatings?.Name.ToString() + "+",
                    Genre = selectedFilm.GenresDisplay
                };

                Movies.Add(movie);

                //Закрываем диалог
                DialogHost.CloseDialogCommand.Execute(null, null);

                await SaveScheduleAsync();
            }
            else
            {
                MessageClass.ErrorMessage($"Ошибка\nВыберите фильм из списка");
            }
        }

        private async void RemoveMovieButtonClick(object sender, RoutedEventArgs e)
        {
            var movieToRemove = (sender as Button).DataContext as ScheduleMovie;

            if (movieToRemove != null)
            {
                Movies.Remove(movieToRemove);
                await SaveScheduleAsync();
            }
        }

        private async void ClearPostersButtonClick(object sender, RoutedEventArgs e)
        {
            // Проверка на наличие постеров
            if (Movies.Count == 0)
                return;

            bool isConfirmed = await DialogClass.ShowConfirmDialog(
                "Очистка постеров",
                "Вы точно хотите удалить все постеры из расписания?",
                "Удалить",
                "Отмена");

            if (isConfirmed)
            {
                try
                {
                    Movies.Clear();
                    await SaveScheduleAsync();
                    MessageClass.SuccessMessage($"Успех\nВсе постеры удалены");
                }
                catch (Exception ex)
                {
                    MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
                }
            }
        }

        private async void ClearSessionsButtonClick(object sender, RoutedEventArgs e)
        {
            // Проверка на наличие сеансов
            bool hasAnySession = Days.Any(d => d.Sessions.Count > 0);

            if (!hasAnySession)
                return;

            bool isConfirmed = await DialogClass.ShowConfirmDialog(
                "Очистка постеров",
                "Вы точно хотите удалить все сеансы из расписания?",
                "Удалить",
                "Отмена");

            if (isConfirmed)
            {
                try
                {
                    foreach (var day in Days)
                        day.Sessions.Clear();

                    await SaveScheduleAsync();
                    MessageClass.SuccessMessage($"Успех\nВсе сеансы удалены");
                }
                catch (Exception ex)
                {
                    MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
                }
            }
        }

        private async void AddDayButtonClick(object sender, RoutedEventArgs e)
        {
            if (Days.Count < 4)
            {
                Days.Add(new ScheduleDay { DateText = "Новая дата" });
                await SaveScheduleAsync();
            }
            else
            {
                MessageClass.ErrorMessage($"Ошибка\nМаксимальное ограничение в 4 дня");
            }
        }

        private async void AddSessionToDayButtonClick(object sender, RoutedEventArgs e)
        {
            var day = (sender as Button).DataContext as ScheduleDay;

            if (day != null)
            {
                var newSession = new ScheduleSession
                {
                    Time = "14:00",
                    Title = "",
                    Price = "250 р."
                };

                SubcribeSession(newSession);
                day.Sessions.Add(newSession);
                await SaveScheduleAsync();
            }
        }

        private async void RemoveSessionButtonClick(object sender, RoutedEventArgs e)
        {
            var session = (sender as Button).DataContext as ScheduleSession;

            foreach (var day in Days)
            {
                if (day.Sessions.Contains(session))
                {
                    day.Sessions.Remove(session);
                    await SaveScheduleAsync();
                    break;
                }
            }
        }

        private async void RemoveDayButtonClick(object sender, RoutedEventArgs e)
        {
            // Получаем конкретный день
            var day = (sender as Button).DataContext as ScheduleDay;

            if (day == null)
                return;

            bool isConfirmed = await DialogClass.ShowConfirmDialog(
                "Удаление дня",
                $"Вы точно хотите удалить день \"{day.DateText}\"?\nВсе сеансы этого дня будут стерты.",
                "Удалить",
                "Отмена");

            if (isConfirmed)
            {
                try
                {
                    Days.Remove(day);

                    await SaveScheduleAsync();
                    MessageClass.SuccessMessage($"Успех\nДень удален из расписания");
                }
                catch (Exception ex)
                {
                    MessageClass.ErrorMessage($"Ошибка\n{ex.Message}");
                }
            }
        }

        private async void SyncSaveEvent(object sender, RoutedEventArgs e)
        {
            await SaveScheduleAsync();
        }
    }
}

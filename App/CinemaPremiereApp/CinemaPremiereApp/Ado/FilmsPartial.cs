using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CinemaPremiereApp.Ado
{
    public partial class Films
    {
        // Свойство для объединения жанров
        public string GenresDisplay
        {
            get
            {
                if (this.Genres == null || this.Genres.Count == 0)
                    return "Жанры не указаны";

                return string.Join(", ", this.Genres.Select(g => g.Name));
            }
        }

        public object PosterPath
        {
            get
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Posters");

                BitmapImage noPhoto = new BitmapImage(new Uri("pack://application:,,,/Images/NoPhoto.png"));

                if (string.IsNullOrWhiteSpace(this.PosterFileName))
                {
                    return noPhoto;
                }

                string fullPath = Path.Combine(folderPath, this.PosterFileName);

                if (File.Exists(fullPath))
                {
                    return new Uri(fullPath);
                }

                return noPhoto;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public string PosterPath
        {
            get
            {
                if (string.IsNullOrEmpty(this.PosterFileName))
                    return "/Images/Logo.png";

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Images", "Posters", this.PosterFileName);
            }
        }
    }
}

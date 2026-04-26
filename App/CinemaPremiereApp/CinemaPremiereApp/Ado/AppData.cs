using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CinemaPremiereApp.Ado
{
    public static class AppData
    {
        public static CinemaPremiereDbEntities db = new CinemaPremiereDbEntities();

        public static Users CurrentUser { get; set; }
    }
}

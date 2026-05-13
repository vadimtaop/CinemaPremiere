using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CinemaPremiereApp.Ado
{
    public partial class Users
    {
        public bool IsLockedOut
        {
            get
            {
                return this.LockoutEnd.HasValue && this.LockoutEnd.Value > DateTime.Now;
            }
        }
    }
}

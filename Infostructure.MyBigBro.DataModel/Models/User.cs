using System.ComponentModel.DataAnnotations;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("User", Schema = "dbo")]
    public class User : IUser
    {
        [Key]
        public int Id
        {
            get;
            set;
        }

        public string UserName
        {
            get;
            set;
        }

        public string Email
        {
            get;
            set;
        }

        public string Password
        {
            get;
            set;
        }

        public bool IsAdmin
        {
            get;
            set;
        }
    }
}

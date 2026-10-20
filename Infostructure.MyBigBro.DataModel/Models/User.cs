using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("user", Schema = "public")]
    public class User : IUser
    {
        [Key]
        [Column("id")]
        public int Id
        {
            get;
            set;
        }

        [Column("user_name")]
        public string UserName
        {
            get;
            set;
        }

        [Column("email")]
        public string Email
        {
            get;
            set;
        }

        [Column("password")]
        public string Password
        {
            get;
            set;
        }

        [Column("is_admin")]
        public bool IsAdmin
        {
            get;
            set;
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("device", Schema = "public")]
    public class Device : IDevice
    {
        [Key]
        [Column("id")]
        public int Id
        {
            get;
            set;
        }

        [Column("device_name")]
        public string DeviceName
        {
            get;
            set;
        }
    }
}

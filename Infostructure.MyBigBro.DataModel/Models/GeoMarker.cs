using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("geo_marker", Schema = "public")]
    public class GeoMarker : IGeoMarker
    {
        [Key]
        [Column("id")]
        public int Id
        {
            get;
            set;
        }

        [Column("marker_date_time")]
        public DateTime MarkerDateTime
        {
            get;
            set;
        }

        [Column("x_coord")]
        public double XCoord
        {
            get;
            set;
        }

        [Column("y_coord")]
        public double YCoord
        {
            get;
            set;
        }

        [Column("device_id")]
        public int DeviceId
        {
            get;
            set;
        }

        [Column("user_id")]
        public int? UserId
        {
            get;
            set;
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("captured_image_geo_marker", Schema = "public")]
    public class CapturedImageGeoMarker : ICapturedImageGeoMarker
    {
        [Key]
        [Column("id")]
        public int Id
        {
            get;
            set;
        }

        [Column("captured_image_id")]
        public int CapturedImageId
        {
            get;
            set;
        }

        [Column("geo_marker_id")]
        public int GeoMarkerId
        {
            get;
            set;
        }
    }
}

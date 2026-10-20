using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("web_cam", Schema = "public")]
    public class WebCam : IWebCam
    {
        [Key]
        [Column("id")]
        public int Id
        {
            get;
            set;
        }

        [Column("name")]
        public string Name
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

        [Column("radius_of_visibility")]
        public double RadiusOfVisibility
        {
            get;
            set;
        }

        [Column("url")]
        public string Url
        {
            get;
            set;
        }
    }
}

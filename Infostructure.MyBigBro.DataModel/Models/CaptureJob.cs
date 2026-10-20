using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infostructure.MyBigBro.DataModel.Models
{
    [Table("capture_job", Schema = "public")]
    public class CaptureJob
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("geo_marker_id")]
        public int GeoMarkerId { get; set; }

        [Column("web_cam_id")]
        public int WebCamId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "PENDING";

        [Column("attempts")]
        public int Attempts { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("processed_at")]
        public DateTime? ProcessedAt { get; set; }

        [Column("last_error")]
        public string LastError { get; set; }
    }
}

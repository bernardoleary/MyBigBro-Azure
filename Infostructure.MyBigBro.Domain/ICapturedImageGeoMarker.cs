namespace Infostructure.MyBigBro.Domain
{
    public interface ICapturedImageGeoMarker
    {
        int Id { get; set; }
        int CapturedImageId { get; set; }
        int GeoMarkerId { get; set; }
    }
}
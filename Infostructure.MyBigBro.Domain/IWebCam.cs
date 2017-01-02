namespace Infostructure.MyBigBro.Domain
{
    public interface IWebCam
    {
        int Id { get; set; }
        string Name { get; set; }
        double XCoord { get; set; }
        double YCoord { get; set; }
        double RadiusOfVisibility { get; set; }
        string Url { get; set; }
    }
}

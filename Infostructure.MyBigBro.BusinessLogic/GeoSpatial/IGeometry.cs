namespace Infostructure.MyBigBro.BusinessLogic.GeoSpatial
{
    public interface IGeometry
    {
        double GetDistancePythagoras(double x1, double y1, double x2, double y2);
    }
}
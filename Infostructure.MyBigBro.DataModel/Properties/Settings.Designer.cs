using System;

namespace Infostructure.MyBigBro.DataModel.Properties
{
    internal sealed partial class Settings
    {
        private static readonly Settings DefaultInstance = new Settings();

        public static Settings Default => DefaultInstance;

        public string MyBigBro =>
            Environment.GetEnvironmentVariable("ConnectionStrings__MyBigBroPostgres")
            ?? "Host=localhost;Port=5433;Database=mybigbro;Username=mybigbro;Password=mybigbro_dev_pwd";
    }
}

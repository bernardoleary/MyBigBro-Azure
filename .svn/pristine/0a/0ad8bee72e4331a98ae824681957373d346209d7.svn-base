using System.IO;
using System.Net;

namespace Infostructure.MyBigBro.BusinessLogic.WebCam
{
    // This class enables mocking the WebResponse class.
    public class WebCamDataRequest : IWebCamDataRequest
    {
        public WebCamWebResponse GetWebCamResponse(string url)
        {
            return new WebCamWebResponse(WebRequest.Create(url));
        }

        public Stream GetWebCamResponseStream(WebCamWebResponse response)
        {
            return response.GetResponseStream();
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Infostructure.MyBigBro.BusinessLogic.WebCam
{
    public class WebCamWebResponse : WebResponse
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly string _url;
        private readonly WebRequest _webRequest;

        public WebCamWebResponse(string url)
        {
            _url = url;
        }

        public WebCamWebResponse(WebRequest webRequest)
        {
            _webRequest = webRequest;
        }

        public WebCamWebResponse()
        {
        }

        public override Stream GetResponseStream()
        {
            if (!string.IsNullOrWhiteSpace(_url))
            {
                var bytes = SharedHttpClient.GetByteArrayAsync(_url).GetAwaiter().GetResult();
                return new MemoryStream(bytes);
            }

            if (_webRequest != null)
            {
                var response = _webRequest.GetResponse();
                return response.GetResponseStream();
            }

            return Stream.Null;
        }
    }
}

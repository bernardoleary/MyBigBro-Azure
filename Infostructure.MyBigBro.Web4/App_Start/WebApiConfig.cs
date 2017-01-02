﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Web.Http;

namespace Infostructure.MyBigBro.Web4
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            //config.Routes.MapHttpRoute(
            //    name: "DefaultApi",
            //    routeTemplate: "api/{controller}/{id}",
            //    defaults: new { id = RouteParameter.Optional }
            //    );

            //config.Routes.MapHttpRoute(
            //    name: "WebCamsAll",
            //    routeTemplate: "api/webcams",
            //    defaults: new { Action = "Get", Controller = "WebCams" }
            //    );

            config.Routes.MapHttpRoute(
                name: "WebCamsNearest",
                routeTemplate: "api/webcams/nearest",
                defaults: new { Action = "GetNearest", Controller = "WebCams" }
                );

            config.Routes.MapHttpRoute(
                name: "GeoMarkerLatest",
                routeTemplate: "api/geomarkers/latest",
                defaults: new { Action = "GetLatest", Controller = "GeoMarkers" }
                );

            config.Routes.MapHttpRoute(
                name: "WebCamsNearestMany",
                routeTemplate: "api/webcams/nearestmany",
                defaults: new { Action = "GetNearestMany", Controller = "WebCams" }
                );

            /*
             * The following route replaces two action specific GET/POST ones.
             */

            config.Routes.MapHttpRoute(
                name: "PostMarker",
                routeTemplate: "api/geomarkers",
                defaults: new { Controller = "GeoMarkers" }
                );

            config.Routes.MapHttpRoute(
                name: "CapturedImagesGeoMarkerById",
                routeTemplate: "api/geomarkers/{id}/capturedimages",
                defaults: new { Action = "Get", Controller = "CapturedImagesGeoMarker" }
                );

            config.Routes.MapHttpRoute(
                name: "CapturedImagesGeoMarkerAll",
                routeTemplate: "api/geomarkers/capturedimages",
                defaults: new { Action = "Get", Controller = "CapturedImagesGeoMarker" }
                );

            config.Routes.MapHttpRoute(
                name: "CoutOfCapturedImagesForDevice",
                routeTemplate: "api/geomarkers/capturedimages/count",
                defaults: new { Action = "Get", Controller = "CapturedImagesGeoMarker" }
                );

            config.Formatters.JsonFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/html"));
        }
    }
}

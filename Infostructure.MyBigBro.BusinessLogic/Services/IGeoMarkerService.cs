﻿using System;
using System.Collections.Generic;
using Infostructure.MyBigBro.Domain;

namespace Infostructure.MyBigBro.BusinessLogic.Services
{
    public interface IGeoMarkerService
    {
        int ProcessGeoMarker(IGeoMarker geoMarker);
        int ProcessGeoMarker(IGeoMarkerDto geoMarkerDto);
        IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImage(int top);
        IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImage(int top, string deveiceName);
        IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImageForTimeframe(DateTime startDateTime, DateTime endDateTime);
        IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImageForTimeframe(DateTime startDateTime, DateTime endDateTime, string deveiceName);
        IGeoMarker GetLatestGeoMarkerForDevice(string deviceName);
        int GetCountOfMarkersWithImage(string deviceName);
    }
}

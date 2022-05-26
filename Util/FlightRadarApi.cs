using Newtonsoft.Json.Linq;
using System;
using Serilog;

namespace Simvars.Util
{
    public static class FlightRadarApi
    {
        private const double LongitudeModifier = 0.8;// Info for JAAP: Changed from 0.3 for a bigger area
        private const double LatitudeModifier =  0.8;// Info for JAAP: Changed from 0.5 for a bigger area

        public static JObject GetAircraftData(string flightRadarId)
        {
            return ApiRequest.MakeGetRequest("https://data-live.flightradar24.com/clickhandler/?version=1.5&flight=" + flightRadarId);
        }

        public static JObject GetAircraftNearby(double longitude, double latitude)
        {
            string longitudeLow = (longitude - LongitudeModifier).ToString("#.000");
            string longitudeHigh = (longitude + LongitudeModifier).ToString("#.000");

            string latitudeLow = (latitude - LatitudeModifier).ToString("#.000");
            string latitudeHigh = (latitude + LatitudeModifier).ToString("#.000");

            string coordString = latitudeHigh + "%2C" + latitudeLow + "%2C" + longitudeLow + "%2C" + longitudeHigh;
            coordString = coordString.Replace(",", ".");
            string url = "https://data-live.flightradar24.com/zones/fcgi/feed.js?faa=1&bounds=" + coordString +
                         "&satellite=1&mlat=1&flarm=1&adsb=1&gnd=1&air=1&vehicles=0&estimated=1&maxage=14400&gliders=1&stats=1";

            Log.Information(url);

            return ApiRequest.MakeGetRequest(url);
        }
    }
}

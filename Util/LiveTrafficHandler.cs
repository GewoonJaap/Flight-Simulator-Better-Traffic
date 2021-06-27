using System;
using Newtonsoft.Json.Linq;
using Simvars.Model;

namespace Simvars.Util
{
    public class LiveTrafficHandler
    {
        public void FetchNewData(PlayerAircraft plane)
        {
            JObject data = FlightRadarApi.GetAircraftNearby(plane.Longitude, plane.Latitude);
            Console.WriteLine(data);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FlightSimulator.SimConnect;
using Newtonsoft.Json.Linq;
using Simvars.Emum;
using Simvars.Model;

namespace Simvars.Util
{
    public class LiveTrafficHandler
    {
        private List<Aircraft> _liveTrafficAircraft;
        private readonly SimConnect _simConnect;
        private int _requestCount = 0;

        public LiveTrafficHandler(SimConnect simConnect)
        {
            _liveTrafficAircraft = new List<Aircraft>();
            _simConnect = simConnect;
        }

        public void FetchNewData(PlayerAircraft plane)
        {
            JObject planeData = FlightRadarApi.GetAircraftNearby(plane.Longitude, plane.Latitude);
            if ((bool)planeData["success"] != true) return;
            ParsePlaneData((JObject)planeData["data"]);
        }

        private void ParsePlaneData(JObject planeData)
        {
            foreach (JProperty property in planeData.Properties())
            {
                //Determine if object is a plane, we only want planes from the api, not the other stat keys ;)
                if (!char.IsDigit(property.Name.ToCharArray()[0])) continue;

                Aircraft aircraft = _liveTrafficAircraft.FirstOrDefault(item => item.FlightRadarId == property.Name);
                if (aircraft == null)
                {
                    JObject extraData = FlightRadarApi.GetAircraftData(property.Name);
                    if ((bool)extraData["success"])
                    {
                        extraData = (JObject)extraData["data"];
                    }

                    aircraft = new Aircraft()
                    {
                        Longitude = (double)property.Value[2],
                        Latitude = (double)property.Value[1],
                        Heading = (int)property.Value[3],
                        Altimeter = (int)property.Value[4],
                        Speed = (int)property.Value[5],
                        Callsign = (string)property.Value[16],
                        FlightRadarId = property.Name,
                        IsGrounded = (bool)property.Value[14],
                        TailNumber = (string)extraData["identification"]?["number"]?["default"] ?? (string)property.Value[16],
                        Model = (string)extraData["aircraft"]?["model"]?["text"] ?? "Airbus A320 Neo Asobo",
                    };
                    _liveTrafficAircraft.Add(aircraft);
                    SpawnPlane(aircraft);
                }
            }
        }

        private void SpawnPlane(Aircraft aircraft)
        {
            var requestId = DataRequests.AI_SPAWN + _requestCount;
            _requestCount = (_requestCount + 1) % 10000;
            Console.WriteLine(@"Spawning a plane " + aircraft.TailNumber + " lat: " + aircraft.Latitude + " long: " + aircraft.Longitude);
            var position = new SIMCONNECT_DATA_INITPOSITION
            {
                Latitude = aircraft.Latitude,
                Longitude = aircraft.Longitude,
                Altitude = aircraft.Altimeter,
                Pitch = 0,
                Bank = 0,
                Heading = aircraft.Heading,
                OnGround = (uint)(aircraft.IsGrounded ? 1 : 0),
                Airspeed = (uint)aircraft.Speed,
            };
            _simConnect.AICreateNonATCAircraft("Airbus A320 Neo KLM", aircraft.TailNumber, position, requestId);
            aircraft.RequestId = requestId;
        }
    }
}

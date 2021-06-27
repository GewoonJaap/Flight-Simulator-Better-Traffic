using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FlightSimulator.SimConnect;
using Newtonsoft.Json.Linq;
using Simvars.Emum;
using Simvars.Model;
using Simvars.Struct;

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

        public void SetObjectId(uint requestId, uint objectId)
        {
            Aircraft aircraft = _liveTrafficAircraft.FirstOrDefault(item => item.RequestId == requestId);
            if (aircraft != null)
            {
                Console.WriteLine("Setting object ID for: " + aircraft.Callsign);
                aircraft.ObjectId = objectId;
            }
        }

        private void ParsePlaneData(JObject planeData)
        {
            foreach (JProperty property in planeData.Properties())
            {
                //Determine if object is a plane, we only want planes from the api, not the other stat keys ;)
                if (!char.IsDigit(property.Name.ToCharArray()[0])) continue;

                Aircraft aircraft = _liveTrafficAircraft.FirstOrDefault(item => item.FlightRadarId == property.Name);

                double Longitude = (double)property.Value[2];
                double Latitude = (double)property.Value[1];
                int Heading = (int)property.Value[3];
                int Altimeter = (int)property.Value[4];
                int Speed = (int)property.Value[5];
                string Callsign = (string)property.Value[16];
                bool isGrounded = (bool)property.Value[14];

                if (aircraft == null)
                {
                    JObject extraData = FlightRadarApi.GetAircraftData(property.Name);
                    if ((bool)extraData["success"])
                    {
                        extraData = (JObject)extraData["data"];
                    }

                    aircraft = new Aircraft()
                    {
                        Longitude = Longitude,
                        Latitude = Latitude,
                        Heading = Heading,
                        Altimeter = Altimeter,
                        Speed = Speed,
                        Callsign = Callsign,
                        FlightRadarId = property.Name,
                        IsGrounded = isGrounded,
                        TailNumber = (string)extraData["identification"]?["number"]?["default"] ?? Callsign,
                        Model = (string)extraData["aircraft"]?["model"]?["text"] ?? "Airbus A320 Neo Asobo",
                    };
                    _liveTrafficAircraft.Add(aircraft);
                    SpawnPlane(aircraft);
                }
                else
                {
                    PositionData position;
                    position.Latitude = Latitude;
                    position.Longitude = Longitude;
                    position.Altitude = Altimeter;
                    position.Heading = Heading;
                    position.Pitch = 0;
                    position.Bank = 0;
                    position.Airspeed = (uint)aircraft.Speed;
                    position.OnGround = (uint)(aircraft.IsGrounded ? 1 : 0);
                    Console.WriteLine("Updating a plane " + aircraft.TailNumber + " lat: " + aircraft.Latitude + " long: " + aircraft.Longitude + " request ID: " + aircraft.RequestId);
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.planeLocation, aircraft.ObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                }
            }
        }

        private void SpawnPlane(Aircraft aircraft)
        {
            var requestId = DataRequests.AI_SPAWN + _requestCount;
            aircraft.RequestId = (10000 + _requestCount);
            Console.WriteLine(@"Spawning a plane " + aircraft.TailNumber + " lat: " + aircraft.Latitude + " long: " + aircraft.Longitude + " request ID: " + aircraft.RequestId);
            _requestCount = (_requestCount + 1) % 10000;
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
        }
    }
}

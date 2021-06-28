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
            List<String> flightRadarIds = new List<string>();
            foreach (JProperty property in planeData.Properties())
            {
                //Determine if object is a plane, we only want planes from the api, not the other stat keys ;)
                if (!char.IsDigit(property.Name.ToCharArray()[0])) continue;
                flightRadarIds.Add(property.Name);

                Aircraft aircraft = _liveTrafficAircraft.FirstOrDefault(item => item.FlightRadarId == property.Name);

                double Longitude = (double)property.Value[2];
                double Latitude = (double)property.Value[1];
                int Heading = (int)property.Value[3];
                int Altimeter = (int)property.Value[4];
                int Speed = (int)property.Value[5];
                string Callsign = (string)property.Value[16];
                bool isGrounded = (bool)property.Value[14];
                string AirportOrigin = null;
                string AirportDestination = null;
                string TailNumber = Callsign;
                string Model = "Airbus A320 Neo";
                string Airline = "Asobo";

                if (aircraft == null)
                {
                    JObject extraData = FlightRadarApi.GetAircraftData(property.Name);
                    if ((bool)extraData["success"])
                    {
                        extraData = (JObject)extraData["data"];
                    }
                    try
                    {
                        TailNumber = (string)extraData["identification"]?["number"]?["default"] ?? Callsign;
                        Model = (string)extraData["aircraft"]?["model"]?["text"] ?? "Airbus A320 Neo";
                        Airline = (string)extraData["airline"]?["name"] ?? "Asobo";
                        AirportOrigin = (string)extraData["airport"]?["origin"]?["code"]?["icao"] ?? null;

                        AirportDestination = (string)extraData["airport"]?["destination"]?["code"]?["icao"] ?? null;
                    }
                    catch (Exception e)
                    {
                        // Console.WriteLine(e);
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
                        TailNumber = TailNumber,
                        Model = Model,
                        Airline = Airline,
                        AirportOrigin = AirportOrigin,
                        AirportDestination = AirportDestination,
                    };
                    aircraft.MatchedModel = ModelMatching.MatchModel(aircraft.Model, aircraft.Airline);

                    _liveTrafficAircraft.Add(aircraft);
                    SpawnPlane(aircraft);
                    continue;
                }

                aircraft.Latitude = Latitude;
                aircraft.Longitude = Longitude;
                aircraft.Altimeter = Altimeter;
                aircraft.Heading = Heading;
                aircraft.Speed = Speed;
                aircraft.IsGrounded = isGrounded;
                if (!aircraft.IsGrounded)
                {
                    Console.WriteLine("Updating a flying plane " + aircraft.TailNumber + " lat: " + aircraft.Latitude + " long: " + aircraft.Longitude + " request ID: " + aircraft.RequestId + " speed: " + aircraft.Speed + " heading: " + aircraft.Heading);

                    aircraft.Waypoints.Add(new Waypoint()
                    {
                        Altitude = Altimeter,
                        IsGrounded = isGrounded,
                        Latitude = Latitude,
                        Longitude = Longitude,
                        Speed = Speed
                    });
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, aircraft.ObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());
                }
                else
                {
                    PositionData position = new PositionData
                    {
                        Latitude = aircraft.Latitude,
                        Longitude = aircraft.Longitude,
                        Altitude = aircraft.Altimeter,
                        Heading = aircraft.Heading,
                        Pitch = 0,
                        Bank = 0,
                        Airspeed = (uint)aircraft.Speed,
                        OnGround = (uint)(aircraft.IsGrounded ? 1 : 0)
                    };
                    Console.WriteLine("Updating a grounded plane " + aircraft.TailNumber + " lat: " + aircraft.Latitude + " long: " + aircraft.Longitude + " request ID: " + aircraft.RequestId + " speed: " + aircraft.Speed + " heading: " + aircraft.Heading);
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.ObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                }
            }

            DespawnOldPlanes(flightRadarIds);
        }

        private void DespawnOldPlanes(List<string> flightradarIds)
        {
            List<Aircraft> removedPlanes = new List<Aircraft>();
            _liveTrafficAircraft.ForEach(plane =>
            {
                if (flightradarIds.Contains(plane.FlightRadarId)) return;

                var requestId = DataRequests.AI_SPAWN + _requestCount;
                Console.WriteLine(@"Deleting a plane " + plane.TailNumber + " request ID: " + _requestCount);
                _requestCount = (_requestCount + 1) % 10000;
                _simConnect.AIRemoveObject(plane.ObjectId, requestId);
                removedPlanes.Add(plane);
            });
            removedPlanes.ForEach(plane =>
            {
                _liveTrafficAircraft.Remove(plane);
            });
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
            _simConnect.AICreateNonATCAircraft(aircraft.MatchedModel, aircraft.TailNumber, position, requestId);
        }
    }
}

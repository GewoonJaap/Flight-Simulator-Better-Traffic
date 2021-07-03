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
        public List<Aircraft> LiveTrafficAircraft;
        private readonly SimConnect _simConnect;
        private int _requestCount = 0;
        private const int MaxPlanes = 40;

        public LiveTrafficHandler(SimConnect simConnect)
        {
            LiveTrafficAircraft = new List<Aircraft>();
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
            Aircraft aircraft = LiveTrafficAircraft.FirstOrDefault(item => item.RequestId == requestId);
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

                Aircraft aircraft = LiveTrafficAircraft.FirstOrDefault(item => item.FlightRadarId == property.Name);

                double longitude = (double)property.Value[2];
                double latitude = (double)property.Value[1];
                int heading = (int)property.Value[3];
                double altimeter = (int)property.Value[4] * 0.3048;
                int speed = (int)property.Value[5];
                string callsign = (string)property.Value[16];
                bool isGrounded = (bool)property.Value[14];
                string airportOrigin = null;
                string airportDestination = null;
                string tailNumber = callsign;
                string model = "Airbus A320 Neo";
                string airline = "Asobo";

                if (aircraft == null)
                {
                    if (LiveTrafficAircraft.Count >= MaxPlanes) continue;
                    JObject extraData = FlightRadarApi.GetAircraftData(property.Name);
                    if ((bool)extraData["success"])
                    {
                        extraData = (JObject)extraData["data"];
                    }
                    try
                    {
                        tailNumber = (string)extraData["identification"]?["number"]?["default"] ?? callsign;
                        model = (string)extraData["aircraft"]?["model"]?["text"] ?? "Airbus A320 Neo";
                        airline = (string)extraData["airline"]?["name"] ?? "Asobo";
                        airportOrigin = (string)extraData["airport"]?["origin"]?["code"]?["icao"] ?? null;

                        airportDestination = (string)extraData["airport"]?["destination"]?["code"]?["icao"] ?? null;
                    }
                    catch (Exception e)
                    {
                        // Console.WriteLine(e);
                    }

                    aircraft = new Aircraft()
                    {
                        Longitude = longitude,
                        Latitude = latitude,
                        Heading = heading,
                        Altimeter = altimeter,
                        Speed = speed,
                        Callsign = callsign,
                        FlightRadarId = property.Name,
                        IsGrounded = isGrounded,
                        TailNumber = tailNumber,
                        Model = model,
                        Airline = airline,
                        AirportOrigin = airportOrigin,
                        AirportDestination = airportDestination,
                    };
                    aircraft.MatchedModel = ModelMatching.MatchModel(aircraft.Model, aircraft.Airline);

                    LiveTrafficAircraft.Add(aircraft);
                    SpawnPlane(aircraft);
                    continue;
                }

                aircraft.Latitude = latitude;
                aircraft.Longitude = longitude;
                aircraft.Altimeter = altimeter;
                aircraft.Heading = heading;
                aircraft.Speed = speed;
                aircraft.IsGrounded = isGrounded;
                if (!aircraft.IsGrounded)
                {
                    Console.WriteLine("Updating a flying plane " + aircraft.TailNumber + " lat: " + aircraft.Latitude + " long: " + aircraft.Longitude + " request ID: " + aircraft.RequestId + " speed: " + aircraft.Speed + " heading: " + aircraft.Heading + " objectId " + aircraft.ObjectId);

                    aircraft.Waypoints.Add(new Waypoint()
                    {
                        Altitude = altimeter,
                        IsGrounded = isGrounded,
                        Latitude = latitude,
                        Longitude = longitude,
                        Speed = speed
                    });
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints,
                    aircraft.ObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());
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
                        OnGround = 0
                    };
                    Console.WriteLine("Updating a grounded plane " + aircraft.TailNumber + " lat: " + aircraft.Latitude + " long: " + aircraft.Longitude + " request ID: " + aircraft.RequestId + " speed: " + aircraft.Speed + " heading: " + aircraft.Heading + " objectId " + aircraft.ObjectId);
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints,
                        aircraft.ObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());

                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.ObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                }
            }

            DespawnOldPlanes(flightRadarIds);
        }

        private void DespawnOldPlanes(List<string> flightradarIds)
        {
            List<Aircraft> removedPlanes = new List<Aircraft>();
            LiveTrafficAircraft.ForEach(plane =>
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
                LiveTrafficAircraft.Remove(plane);
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
                OnGround = (uint)(aircraft.IsGrounded ? 0 : 1),
                Airspeed = (uint)aircraft.Speed,
            };
            _simConnect.AICreateNonATCAircraft(aircraft.MatchedModel, aircraft.TailNumber, position, requestId);
        }
    }
}

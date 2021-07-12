using Microsoft.FlightSimulator.SimConnect;
using Newtonsoft.Json.Linq;
using Simvars.Emum;
using Simvars.Model;
using Simvars.Struct;
using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace Simvars.Util
{
    public class LiveTrafficHandler
    {
        public List<Aircraft> LiveTrafficAircraft;
        private readonly SimConnect _simConnect;
        private int _requestCount = 0;
        private int MaxPlanes = 40;
        private List<Addon> _addons;

        public LiveTrafficHandler(SimConnect simConnect)
        {
            LiveTrafficAircraft = new List<Aircraft>();
            _simConnect = simConnect;

            Settings settings = SettingsReader.FetchSettings();
            if (settings.MaximumAmountOfPlanes >= 0) MaxPlanes = settings.MaximumAmountOfPlanes;
            _addons = AddonScanner.ScanAddons();
        }

        public void FetchNewData(PlayerAircraft plane)
        {
            JObject planeData = FlightRadarApi.GetAircraftNearby(plane.Longitude, plane.Latitude);
            if ((bool)planeData["success"] != true) return;
            ParsePlaneData((JObject)planeData["data"]);
        }

        public void SetObjectId(uint requestId, uint objectId)
        {
            Aircraft aircraft = LiveTrafficAircraft.FirstOrDefault(item => item.requestId == requestId);
            if (aircraft != null)
            {
                Log.Information($"Setting object ID: {objectId} for: {aircraft.callsign}");
                aircraft.objectId = objectId;

                PositionData position = new PositionData
                {
                    Latitude = aircraft.latitude,
                    Longitude = aircraft.longitude,
                    Altitude = aircraft.altimeter,
                    Heading = aircraft.heading,
                    Pitch = 0,
                    Bank = 0,
                    Airspeed = (uint)aircraft.speed,
                    OnGround = (uint)(aircraft.isGrounded ? 1 : 0)
                };
                _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
            }
        }

        private void ParsePlaneData(JObject planeData)
        {
            List<string> flightRadarIds = new List<string>();
            foreach (JProperty property in planeData.Properties())
            {
                //Determine if object is a plane, we only want planes from the api, not the other stat keys ;)
                if (!char.IsDigit(property.Name.ToCharArray()[0])) continue;
                flightRadarIds.Add(property.Name);

                Aircraft aircraft = LiveTrafficAircraft.FirstOrDefault(item => item.flightRadarId == property.Name);

                double longitude = (double)property.Value[2];
                double latitude = (double)property.Value[1];
                int heading = (int)property.Value[3];
                double altimeter = (int)property.Value[4];// * 0.3048;
                int speed = (int)property.Value[5];
                string callsign = (string)property.Value[16];
                bool isGrounded = (bool)property.Value[14];
                string icaoAirline = (string)property.Value[18];
                string airportOrigin = null;
                string airportDestination = null;
                string tailNumber = callsign;
                string model = "Airbus A320 Neo";
                string modelCode = "A320";
                string airline = "Asobo";

                if (aircraft == null)
                {
                    if (LiveTrafficAircraft.Count >= MaxPlanes) continue;
                    JObject extraData = FlightRadarApi.GetAircraftData(property.Name);
                    if ((bool)extraData["success"])
                    {
                        extraData = (JObject)extraData["data"];
                    }
                    else
                    {
                        Log.Error($"Failed to fetch extra data for {callsign}");
                    }
                    try
                    {
                        tailNumber = (string)extraData["identification"]?["number"]?["default"] ?? callsign;
                        model = (string)extraData["aircraft"]?["model"]?["text"] ?? "Airbus A320 Neo";
                        modelCode = (string)extraData["aircraft"]?["model"]?["code"] ?? "A32N";
                        airline = (string)extraData["airline"]?["name"] ?? "Asobo";
                        airportOrigin = (string)extraData["airport"]?["origin"]?["code"]?["icao"] ?? null;

                        airportDestination = (string)extraData["airport"]?["destination"]?["code"]?["icao"] ?? null;
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to parse extra data for {callsign}");
                    }

                    aircraft = new Aircraft()
                    {
                        longitude = longitude,
                        latitude = latitude,
                        heading = heading,
                        altimeter = altimeter,
                        speed = speed,
                        callsign = callsign,
                        flightRadarId = property.Name,
                        isGrounded = isGrounded,
                        tailNumber = tailNumber,
                        model = model,
                        airline = airline,
                        airportOrigin = airportOrigin,
                        airportDestination = airportDestination,
                        modelCode = modelCode,
                        icaoAirline = icaoAirline
                    };
                    aircraft.matchedModel = ModelMatching.MatchModel(aircraft.modelCode, aircraft.model, aircraft.airline, aircraft.icaoAirline, _addons);

                    LiveTrafficAircraft.Add(aircraft);
                    SpawnPlane(aircraft);
                    continue;
                }

                if (aircraft.objectId == 0) continue;

                if (!aircraft.isGrounded)
                {
                    Log.Information("Updating a flying plane " + aircraft.tailNumber + " lat: " + latitude + " long: " + longitude + " request ID: " + aircraft.requestId + " speed: " + speed + " heading: " + heading + " objectId " + aircraft.objectId);

                    aircraft.waypoints.Add(new Waypoint()
                    {
                        Altitude = altimeter,
                        IsGrounded = isGrounded,
                        Latitude = latitude,
                        Longitude = longitude,
                        Speed = speed
                    });
                }
                else
                {
                    PositionData position = new PositionData
                    {
                        Latitude = aircraft.latitude,
                        Longitude = aircraft.longitude,
                        Altitude = aircraft.altimeter,
                        Heading = aircraft.heading,
                        Pitch = 0,
                        Bank = 0,
                        Airspeed = (uint)aircraft.speed,
                        OnGround = 1
                    };
                    Log.Information("Updating a grounded plane " + aircraft.tailNumber + " lat: " + aircraft.latitude + " long: " + aircraft.longitude + " request ID: " + aircraft.requestId + " speed: " + aircraft.speed + " heading: " + aircraft.heading + " objectId " + aircraft.objectId);

                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                }
                aircraft.latitude = latitude;
                aircraft.longitude = longitude;
                aircraft.altimeter = altimeter;
                aircraft.heading = heading;
                aircraft.speed = speed;
                aircraft.isGrounded = isGrounded;

                _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints,
                    aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());
            }

            DespawnOldPlanes(flightRadarIds);
        }

        private void DespawnOldPlanes(List<string> flightradarIds)
        {
            List<Aircraft> removedPlanes = new List<Aircraft>();
            LiveTrafficAircraft.ForEach(plane =>
            {
                if (flightradarIds.Contains(plane.flightRadarId)) return;

                var requestId = DataRequests.AI_SPAWN + _requestCount;
                Log.Information(@"Deleting a plane " + plane.tailNumber + " request ID: " + _requestCount);
                if (plane.objectId != 0)
                {
                    _requestCount = (_requestCount + 1) % 10000;
                    _simConnect.AIRemoveObject(plane.objectId, requestId);
                }

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
            aircraft.requestId = (10000 + _requestCount);
            Log.Information(@"Spawning a plane " + aircraft.tailNumber + " lat: " + aircraft.latitude + " long: " + aircraft.longitude + " request ID: " + aircraft.requestId);
            _requestCount = (_requestCount + 1) % 10000;
            var position = new SIMCONNECT_DATA_INITPOSITION
            {
                Latitude = aircraft.latitude,
                Longitude = aircraft.longitude,
                Altitude = aircraft.altimeter,
                Pitch = 0,
                Bank = 0,
                Heading = aircraft.heading,
                OnGround = (uint)(aircraft.isGrounded ? 0 : 1),
                Airspeed = (uint)aircraft.speed,
            };
            _simConnect.AICreateNonATCAircraft(aircraft.matchedModel, aircraft.tailNumber, position, requestId);
        }
    }
}

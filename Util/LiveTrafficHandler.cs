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
        private int MaxPlanes = 120;
        private List<Addon> _addons;
        private readonly int _teleportFixDelay = 30;

        public bool HighAltitudeTraffic { get; set; }

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
                 // _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation,
                 // aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                var request = DataRequests.AI_RELEASE + _requestCount;
                _requestCount = (_requestCount + 1) % 10000;
                _simConnect.AIReleaseControl(objectId, request);
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
                int altimeter = (int)Math.Round((int)property.Value[4] * 0.3048); // Info for JAAP: I changed it from (int)property.Value[4]; for the right altitude Math.Round((int)property.Value[4] * 0.3048)
                int altimeterFeet = (int)property.Value[4];
                int speed = (int)property.Value[5];
                string callsign = (string)property.Value[16];
                bool isGrounded = (bool)property.Value[14];
                string icaoAirline = (string)property.Value[18];
                string airportOrigin = null;
                string airportDestination = null;
                string tailNumber = callsign;
                string model = "Airbus A320 Neo";
                string modelCode = "A320";
                string airline = "";

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
                        continue;
                    }
                    foreach (char c in callsign)
                    {
                        if (char.IsDigit(c)) break;
                        airline += c;
                    }

                    try
                    {
                        tailNumber = (string)extraData["identification"]?["number"]?["default"] ?? callsign;
                        model = (string)extraData["aircraft"]?["model"]?["text"] ?? "Airbus A320 Neo";
                        modelCode = (string)extraData["aircraft"]?["model"]?["code"] ?? "A32N";
                        airline = (string)extraData["airline"]?["name"] ?? airline;
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
                        altimeterFeet = altimeterFeet,
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
                        icaoAirline = icaoAirline,
                        isTeleportFixed = isGrounded,
                        spawnTime = DateTime.Now,
                        corrTime = DateTime.Now,
                        corrTimeTaxi = DateTime.Now
                    };
                    aircraft.matchedModel = ModelMatching.MatchModel(aircraft, _addons);

                    // Mauflo: This will fix the Problem with airports under the sea level - but only lower 100 feet under the level ;-)
                    /*if (!aircraft.onceSetGround && aircraft.altimeter <= 0 || aircraft.speed < 30)
                    {
                        aircraft.isGrounded = true; aircraft.altimeter = -100; //We should be shure, that the planes get grounded
                    }*/

                    LiveTrafficAircraft.Add(aircraft);
                    SpawnPlane(aircraft);
                    continue;
                }

                if (!aircraft.isGrounded)
                {
                    if ((DateTime.Now - aircraft.corrTime).Seconds > _teleportFixDelay && aircraft.speed > 15 && aircraft.onceFixAltitudeCallsign != aircraft.callsign) // speed>30 = controll if should start or if landing happend onceFixAltitude Airplains should not touched    && aircraft.onceFixAltitudeCallsign != aircraft.callsign
                    {
                        aircraft.onceFixAltitudeCallsign = aircraft.callsign;
                        aircraft.corrTime = DateTime.Now;// Info for JAAP - This is new because if you don't actualize the time then the fixing will only done once and not every _teleportFixDelay - that means that the airplane fliy straight ahen and does not follow the course
                        if ((aircraft.heading - heading > 5) || (aircraft.heading - heading < 5)) // Correct the route only when necessary
                        {
                            aircraft.waypoints.Add(new Waypoint()
                            {
                                Latitude = latitude,
                                Longitude = longitude,
                                Altitude = altimeter,
                                Speed = speed,
                                IsGrounded = isGrounded
                            });
                            Log.Information("Updating a flying plane " + aircraft.tailNumber + " lat: " + latitude + " long: " + longitude + " request ID: " + aircraft.requestId + " speed: " + speed + " heading: " + heading + " objectId " + aircraft.objectId);
                            _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());
                        }
                    }
                }
                else
                {
                    if (aircraft.objectId != 0)
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
                        Log.Information("Setteling a grounded plane " + aircraft.tailNumber + " lat: " + aircraft.latitude + " long: " + aircraft.longitude + " request ID: " + aircraft.requestId + " speed: " + aircraft.speed + " heading: " + aircraft.heading + " objectId " + aircraft.objectId);
                        _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                    }
                }

                //if (aircraft.altimeter == 0){ aircraft.isGrounded = true; } //Sometimes the aircrafts are not recognized correctly grounded
                // Info for JAAP: That has to happen once, because otherwise there would be no wheels at the grounded Spawn one, and the landing one have to stop the waypoint following
                // Every 10 seconds new data input (ec. nessecary for positioning grounded planes)
                if (aircraft.isGrounded && !aircraft.onceSetGround)
                {
                    if (aircraft.objectId != 0)
                    {
                        aircraft.onceSetGround = true;
                        aircraft.isGrounded = true;
                        _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());


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
                        Log.Information("Setteling a grounded plane " + aircraft.tailNumber + " lat: " + aircraft.latitude + " long: " + aircraft.longitude + " request ID: " + aircraft.requestId + " speed: " + aircraft.speed + " heading: " + aircraft.heading + " objectId " + aircraft.objectId);
                        _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                    }

                }
                // That is the function to turn on or off the teleporting of the high altitude traffic
                if (HighAltitudeTraffic && altimeter > 9144)
                {
                    aircraft.onceFixAltitudeCallsign = aircraft.callsign;
                }
                else
                {
                    aircraft.onceFixAltitudeCallsign = "";
                }
                
                // Correct the Altitude over 30.000ft for that airplane
                //<____________________________________________________>
                // Here all airliners over 30.000 feet will be teleportet and fly a little then it will teleportet again.
                // The problem is, that the AI airplane will correct the altitude to ground level after a while. so the altitude will not stay if we not teleport it.
                if (altimeter > 9144 && aircraft.onceFixAltitudeCallsign == aircraft.callsign) //
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
                        OnGround = 0
                    };
                    Log.Information("Changing altitute for a highflying plane " + aircraft.tailNumber + " lat: " + aircraft.latitude + " long: " + aircraft.longitude + " request ID: " + aircraft.requestId + " speed: " + aircraft.speed + " heading: " + aircraft.heading + " objectId " + aircraft.objectId);
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());
                }
                if (altimeter > 9144 && aircraft.onceFixAltitudeCallsign != aircraft.callsign) //
                {
                    aircraft.waypoints.Add(new Waypoint()
                    {
                        Latitude = latitude,
                        Longitude = longitude,
                        Altitude = altimeter,
                        Speed = speed,
                        IsGrounded = isGrounded
                    });
                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());
                }
                
                //<____________________________________________________>

                aircraft.latitude = latitude;
                aircraft.longitude = longitude;
                aircraft.altimeter = altimeter;
                aircraft.heading = heading;
                aircraft.speed = speed;
                aircraft.isGrounded = isGrounded;
            }
            try
            {
                DespawnOldPlanes(flightRadarIds);
            }
            catch (Exception ex)
            {
                Log.Error($"Error when trying to despawn aircraft, {ex.Message}");
            }
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
                Airspeed = (uint)aircraft.speed
            };
            _simConnect.AICreateNonATCAircraft(aircraft.matchedModel, aircraft.tailNumber, position, requestId);
        }
    }
}

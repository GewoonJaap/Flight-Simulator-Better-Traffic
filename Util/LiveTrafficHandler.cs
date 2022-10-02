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
        private int MaxPlanes = 60;
        private List<Addon> _addons;
        private int _teleportFixDelay = 30;
        private int toOnGround;

        public string excludeAirportOrigin;
        public string excludeAirportDestination;
        public string excludeStatus;

        public bool excludeGround { get; set; }
        public bool ExclGaTraffic { get; set; }
        public bool ExclGlidTraffic { get; set; }
        public bool ExclAirlTraffic { get; set; }
        public bool ExclGroundTraffic { get; set; }
        public bool ExclLowAltTraffic { get; set; }
        public bool ExclMidAltTraffic { get; set; }
        public bool ExclHigAltTraffic { get; set; }
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
                    Altitude = aircraft.altimeterMeter,
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
                int altimeter = (int)property.Value[4];  //In feet (int)Math.Round((int)property.Value[4] * 0.3048); // Info for JAAP: I changed it from (int)property.Value[4]; for the right altitude Math.Round((int)property.Value[4] * 0.3048)
                int altimeterMeter = (int)Math.Round((int)property.Value[4] * 0.3048);
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
                string infoExclude = "";
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
                        altimeterMeter = altimeterMeter,
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
                        infoExclude = infoExclude,
                        isTeleportFixed = false,
                        spawnTime = DateTime.Now,
                        corrTime = DateTime.Now,
                        corrTime1 = DateTime.Now
                    };
                    aircraft.matchedModel = ModelMatching.MatchModel(aircraft, _addons);


                    if (!isGrounded)
                    {
                        aircraft.countApproaching = 3;
                    }
                    else
                    {
                        aircraft.countApproaching = 0;
                    }

                    // Mauflo: This will fix the Problem with airports under the sea level - but only lower 100 feet under the level ;-)
                    if (!aircraft.onceSetGround && aircraft.altimeter <= 0 || aircraft.speed < 16)
                    {
                        aircraft.isGrounded = true; aircraft.altimeter = -100; //We should be shure, that the planes get grounded
                    }
                    if (altimeter <= 0) aircraft.isTeleportFixed = true; // Correct Altitude over 10.000 ft only for aircrafts they are not started from an airport

                    LiveTrafficAircraft.Add(aircraft);

                    if (!aircraft.infoExclude.Contains("EXCLUDED"))
                    {
                        SpawnPlane(aircraft);
                    }


                    continue;
                }

                if (aircraft.objectId == 0) continue;

                if (!aircraft.infoExclude.Contains("EXCLUDED"))
                {
                    if (aircraft.speed < 9) aircraft.isGrounded = true; //Slow GA Traffic should be grounded in this way
                    //Here starts the handling for the movement
                    // *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-
                    if (!aircraft.isGrounded) // Update a waypoint of an aircraft in flight with every third data retrieval...
                    {
                        if (aircraft.icaoAirline == "") _teleportFixDelay = 10; _teleportFixDelay = 30; // Info for JAAP: Faster waypoints for GA traffic that the reactions are faster and the course is more reliable
                        if ((DateTime.Now - aircraft.corrTime).Seconds > _teleportFixDelay && aircraft.speed > 20 && aircraft.onceFixAltitudeCallsign != aircraft.callsign) // speed>30 = controll if should start or if landing happend onceFixAltitude Airplains should not touched    && aircraft.onceFixAltitudeCallsign != aircraft.callsign
                        {
                            aircraft.onceFixAltitudeCallsign = aircraft.callsign;
                            aircraft.corrTime = DateTime.Now;// Info for JAAP - This is new because if you don't actualize the time then the fixing will only done once and not every _teleportFixDelay - that means that the airplane fliy straight ahen and does not follow the course
                            // Info for JAAP - Before start set the heading of the runway! That's necessary because turning on the night textures only works with the waypoint function and that lets the plane rotate on the spot snd therefor the heading is noct the same as the runway after 30 seconds
                            if (!aircraft.alignHeading && (aircraft.longitudeBefore > 0 || aircraft.latitudeBefore > 0)) // Info for JAAP: Only for airplanes they are movements on the ground before!
                            {
                                if (aircraft.DepartingHeadingCheck) // Check if the plane is starting and give them the correct heading
                                {
                                    PositionData position = new PositionData
                                    {
                                        Latitude = aircraft.latitudeBefore,
                                        Longitude = aircraft.longitudeBefore,
                                        Altitude = aircraft.altimeterMeterBefore, // In flight use correct heading 
                                        Heading = aircraft.heading, 
                                        Pitch = 0,
                                        Bank = 0,
                                        Airspeed = (uint)speed,
                                        OnGround = (uint)(isGrounded ? 0 : 1)
                                    };
                                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                                    aircraft.alignHeading = true; //important: set only once before the start
                                }
                                else
                                {
                                    PositionData position = new PositionData
                                    {
                                        Latitude = aircraft.latitudeBefore,
                                        Longitude = aircraft.longitudeBefore,
                                        Altitude = aircraft.altimeterMeterBefore,
                                        Heading = aircraft.StartHeading, //For Start use the Runway Heading = Last Heading on Ground
                                        Pitch = 0,
                                        Bank = 0,
                                        Airspeed = (uint)speed,
                                        OnGround = (uint)(isGrounded ? 0 : 1)
                                    };
                                    _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                                    aircraft.alignHeading = true; //important: set only once before the start
                                }
                                aircraft.DepartingHeadingCheck = true; //Now calculate with the correct Heading
                            }

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
                    else
                    {
                        aircraft.isGrounded = false;
                        if (aircraft.countApproaching == 0) // Grounding only when 30 seconds (delay from start) is over
                        {
                            if (aircraft.latitude != aircraft.latitudeBefore || aircraft.longitude != aircraft.longitudeBefore)
                            {

                            }
                            PositionData position = new PositionData
                            {
                                Latitude = aircraft.latitude,
                                Longitude = aircraft.longitude,
                                Altitude = aircraft.altimeterMeter,
                                Heading = aircraft.heading,
                                Pitch = 0,
                                Bank = 0,
                                Airspeed = (uint)aircraft.speed,
                                OnGround = 0// (uint)(isGrounded ? 0 : 1)                            
                            };
                            Log.Information("Setteling a grounded plane " + aircraft.tailNumber + " lat: " + aircraft.latitude + " long: " + aircraft.longitude + " request ID: " + aircraft.requestId + " speed: " + aircraft.speed + " heading: " + aircraft.heading + " objectId " + aircraft.objectId);
                            _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);

                            // This function is neccasery turn on the Night Textures on ground - but that lets the plane rotate on the spot
                            aircraft.latitude = aircraft.latitude;
                            aircraft.longitude = aircraft.longitude;
                            aircraft.altimeter = altimeter;
                            aircraft.speed = 10; //speed;
                            aircraft.isGrounded = true;// isGrounded;
                            _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());


                            aircraft.isGrounded = true;
                            aircraft.latitudeBefore = aircraft.latitude;
                            aircraft.longitudeBefore = aircraft.longitude;
                            aircraft.headingBefore = aircraft.heading;
                            aircraft.altimeterMeterBefore = aircraft.altimeterMeter;
                        }
                        if (aircraft.countApproaching > 0) aircraft.countApproaching --;
                        aircraft.StartHeading = aircraft.heading;
                    }
                    // *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-

                    //Here a plane after landing will be grounded (It is important to get the waypoint and positioning to get the wheels on the ground)
                    //*******************************************
                    if (aircraft.isGrounded && !aircraft.onceSetGround && aircraft.countApproaching == 0)
                    {
                        _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aircraft.GetWayPointObjectArray());
                        aircraft.onceSetGround = true;
                        aircraft.isGrounded = true;

                        PositionData position = new PositionData
                        {
                            Latitude = aircraft.latitude,
                            Longitude = aircraft.longitude,
                            Altitude = aircraft.altimeterMeter,
                            Heading = aircraft.heading,
                            Pitch = 0,
                            Bank = 0,
                            Airspeed = (uint)aircraft.speed,
                            OnGround = (uint)(isGrounded ? 0 : 1)
                        };
                        Log.Information("Setteling a grounded plane " + aircraft.tailNumber + " lat: " + aircraft.latitude + " long: " + aircraft.longitude + " request ID: " + aircraft.requestId + " speed: " + aircraft.speed + " heading: " + aircraft.heading + " objectId " + aircraft.objectId);
                        _simConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneLocation, aircraft.objectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, position);
                    }
                    //********************************************/

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
                    if (altimeter > 29999 && aircraft.onceFixAltitudeCallsign == aircraft.callsign && aircraft.infoExclude != "HIGH ALT EXCLUDED")
                    {
                        PositionData position = new PositionData
                        {
                            Latitude = aircraft.latitude,
                            Longitude = aircraft.longitude,
                            Altitude = aircraft.altimeterMeter,
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
                    if (altimeter > 29999 && aircraft.onceFixAltitudeCallsign != aircraft.callsign && aircraft.infoExclude != "HIGH ALT EXCLUDED") // && aircraft.objectId != 0
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
                        aircraft.onceFixAltitudeCallsign = aircraft.callsign;
                    }
                    //<____________________________________________________>
                }

                //Exclude checkbox handling
                //.-.-.-.-.-.-.-.-.-.-.-.-.
                //Exclude GA traffic 
                if (ExclGaTraffic && aircraft.icaoAirline == "" && aircraft.infoExclude != "GA EXCLUDED")
                {
                    excludeStatus = "Hide";
                    aircraft.infoExclude = "GA EXCLUDED";
                }
               if (!ExclGaTraffic && aircraft.icaoAirline == "" && aircraft.infoExclude == "GA EXCLUDED")
                {
                    excludeStatus = "Show";
                    aircraft.infoExclude = "";
                }

                // Exclude gliders
                if (ExclGlidTraffic && aircraft.modelCode == "GLID" && aircraft.infoExclude != "GLIDER EXCLUDED")
                {
                    excludeStatus = "Hide";
                    aircraft.infoExclude = "GLIDER EXCLUDED";
                }
               if (!ExclGlidTraffic && aircraft.modelCode == "GLID" && aircraft.infoExclude == "GLIDER EXCLUDED")
                {
                    excludeStatus = "Show";
                    aircraft.infoExclude = "";
                }

                // Exclude airlines
                if (ExclAirlTraffic && aircraft.icaoAirline != "" && aircraft.infoExclude != "AIRLINES EXCLUDED")
                {
                    excludeStatus = "Hide";
                    aircraft.infoExclude = "AIRLINES EXCLUDED";
                }
               if (!ExclAirlTraffic && aircraft.icaoAirline != "" && aircraft.infoExclude == "AIRLINES EXCLUDED")
                {
                    excludeStatus = "Show";
                    aircraft.infoExclude = "";
                }

                // Exclude ground traffic
                if (ExclGroundTraffic && altimeter <= 0 && aircraft.infoExclude != "GROUND EXCLUDED")
                {
                    excludeStatus = "Hide";
                    aircraft.infoExclude = "GROUND EXCLUDED";
                }
                if (!ExclGroundTraffic && altimeter <= 0 && aircraft.infoExclude == "GROUND EXCLUDED")
                {
                    excludeStatus = "Show";
                    aircraft.infoExclude = "";
                }

                // Exclude low altitude traffic
                if (ExclLowAltTraffic && aircraft.altimeter > 0 && aircraft.altimeter <= 9999 && aircraft.infoExclude != "LOW ALT EXCLUDED")
                {
                    excludeStatus = "Hide";
                    aircraft.infoExclude = "LOW ALT EXCLUDED";
                }
                else if (!ExclLowAltTraffic && aircraft.altimeter > 0 && aircraft.altimeter <= 9999 && aircraft.infoExclude == "LOW ALT EXCLUDED")
                {
                    excludeStatus = "Show";
                    aircraft.infoExclude = "";
                }

                // Exclude mid altitude traffic
                if (ExclMidAltTraffic && aircraft.altimeter > 9999 && aircraft.altimeter <= 19999 && aircraft.infoExclude != "MID ALT EXCLUDED")
                {
                    excludeStatus = "Hide";
                    aircraft.infoExclude = "MID ALT EXCLUDED";
                }
                if (!ExclMidAltTraffic && aircraft.altimeter > 9999 && aircraft.altimeter <= 19999 && aircraft.infoExclude == "MID ALT EXCLUDED")
                {
                    excludeStatus = "Show";
                    aircraft.infoExclude = "";
                }

                // Exclude high altitude traffic
                if (ExclHigAltTraffic && aircraft.altimeter > 19999 && aircraft.infoExclude != "HIGH ALT EXCLUDED")
                {
                    excludeStatus = "Hide";
                    aircraft.infoExclude = "HIGH ALT EXCLUDED";
                }
                if (!ExclHigAltTraffic && aircraft.altimeter > 19000 && aircraft.infoExclude == "HIGH ALT EXCLUDED")
                {
                    excludeStatus = "Show";
                    aircraft.infoExclude = "";
                }


                if (excludeStatus == "Hide") //Despawn the aircraft
                {
                    var request = DataRequests.AI_RELEASE + _requestCount;
                    _requestCount = (_requestCount + 1) % 10000;
                    _simConnect.AIRemoveObject(aircraft.objectId, request);
                    excludeStatus = "";
                }
                
                if (excludeStatus == "Show") // Spawn the aircraft
                {
                    LiveTrafficAircraft.Remove(aircraft);
                    var request = DataRequests.AI_RELEASE + _requestCount;
                    _requestCount = (_requestCount + 1) % 10000;
                    SetObjectId(aircraft.objectId, (uint)request);
                    LiveTrafficAircraft.Add(aircraft);
                    SpawnPlane(aircraft);
                    aircraft.isTeleportFixed = false; //for the new altituide correction 
                    aircraft.onceSetGround = false; // for the new set when it was grounded
                    excludeStatus = "";
                }
                //.-.-.-.-.-.-.-.-.-.-.-.-.

                aircraft.latitude = latitude;
                aircraft.longitude = longitude;
                aircraft.altimeter = altimeter;
                aircraft.altimeterMeter = altimeterMeter;
                aircraft.heading = heading;
                aircraft.speed = speed;
                aircraft.isGrounded = isGrounded;
                infoExclude = aircraft.infoExclude;
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
                Altitude = aircraft.altimeterMeter,
                Pitch = 0,
                Bank = 0,
                Heading = aircraft.heading,
                OnGround = (uint)(aircraft.isGrounded ? 0 : 1),
                Airspeed = (uint)(aircraft.speed-((aircraft.speed/100)*50))
            };
            if(aircraft.infoExclude != aircraft.callsign)
            _simConnect.AICreateNonATCAircraft(aircraft.matchedModel, aircraft.tailNumber, position, requestId);

        }
    }
}

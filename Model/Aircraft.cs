using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;
using Serilog;

namespace Simvars.Model
{
    public class Aircraft
    {
        #region SimData

        public int requestId { get; set; }
        public uint objectId { get; set; } = 0;
        public string matchedModel { get; set; }

        public bool isTeleportFixed { get; set; } = false;
        public DateTime spawnTime { get; set; }
        public DateTime corrTime { get; set; }
        public DateTime corrTime1 { get; set; }
        public string onceFixAltitudeCallsign { get; set; }
        public bool onceSetGround { get; set; } = false;

        #endregion SimData

        #region Aircraft

        public string flightRadarId { get; set; }
        public string callsign { get; set; }
        public string tailNumber { get; set; }
        public string model { get; set; }
        public string airline { get; set; }
        public string icaoAirline { get; set; }
        public string modelCode { get; set; }

        public string shorterModelCode { get => modelCode.Remove(modelCode.Length - 1, 1); }

        public string shortModel { get => model.Substring(0, model.IndexOf('-') > -1 ? model.IndexOf('-') : model.Length); }

        #endregion Aircraft

        #region FlightPath

        public double latitude { get; set; }
        public double longitude { get; set; }
        public double altimeter { get; set; }
        public int speed { get; set; }
        public int heading { get; set; }
        public bool isGrounded { get; set; }
        public string airportOrigin { get; set; }
        public string airportDestination { get; set; }

        public List<Waypoint> waypoints { get; set; } = new();
        public double altimeterFeet { get;  set; }

        #endregion FlightPath

        public SIMCONNECT_DATA_WAYPOINT[] GetSimConnectDataWaypoints()
        {
            SIMCONNECT_DATA_WAYPOINT[] result = new SIMCONNECT_DATA_WAYPOINT[waypoints.Count];
            if (waypoints.Count == 0) Log.Information("Trying to generate a waypoint but I have no waypoint data! " + callsign);
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i].IsGrounded)
                {
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL | SIMCONNECT_WAYPOINT_FLAGS.COMPUTE_VERTICAL_SPEED);
                }
                else
                {
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL);
                }
                result[i].Altitude = waypoints[i].Altitude;
                result[i].Latitude = waypoints[i].Latitude;
                result[i].Longitude = waypoints[i].Longitude;
                result[i].ktsSpeed = waypoints[i].Speed;
                Log.Information("Setting waypoint " + i + " for " + tailNumber + " lat " + result[i].Latitude + " long " + result[i].Longitude + " speed " + result[i].ktsSpeed + "  altitude " + result[i].Altitude + " objectId " + objectId);
            }

            waypoints.RemoveAt(0);

            return result;
        }

        public object[] GetWayPointObjectArray()
        {
            //var dataWaypoints = GetSimConnectDataWaypoints();

            SIMCONNECT_DATA_WAYPOINT[] wp = new SIMCONNECT_DATA_WAYPOINT[1];

            if (isGrounded)
            {
                wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL | SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED);
            }
            else
            {
                wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL | SIMCONNECT_WAYPOINT_FLAGS.COMPUTE_VERTICAL_SPEED);
            }

            wp[0].Altitude = altimeter * 2; // for a better altitude performance calculation for the AI plane;
            wp[0].Latitude = latitude;
            wp[0].Longitude = longitude;
            wp[0].ktsSpeed = speed;

            var obj = new Object[wp.Length];
            wp.CopyTo(obj, 0);
            return obj;
        }
    }
}

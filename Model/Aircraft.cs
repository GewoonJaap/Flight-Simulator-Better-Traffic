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

        public bool tiktak { get; set; }
        public bool alignHeading { get; set; } = false;
        public string onceFixAltitudeCallsign { get; set; }
        public bool onceSetGround { get; set; } = false;
        public bool DepartingHeadingCheck { get; set; } = false;

        #endregion SimData

        #region Aircraft

        public string flightRadarId { get; set; }
        public string callsign { get; set; }
        public string tailNumber { get; set; }
        public string model { get; set; }
        public string airline { get; set; }
        public string icaoAirline { get; set; }
        public string modelCode { get; set; }
        public string infoExclude { get; set; }

        public string shorterModelCode { get => modelCode.Remove(modelCode.Length - 1, 1); }

        public string shortModel { get => model.Substring(0, model.IndexOf('-') > -1 ? model.IndexOf('-') : model.Length); }

        #endregion Aircraft

        #region FlightPath
        //Variables for later use to collect waypoints
        /* public int wpCounter = 0;
        public int altitudeCorrection = 2;
        public double[] wpLongitude = new double[10];
        public double[] wpLatitude = new double[10];
        public double[] wpAltitude = new double[10];
        public double[] wpAltitudeMeter = new double[10];
        public int[] wpHeading = new int[10];
        public int[] wpSpeed = new int[10];
        public bool[] wpIsGrounded = new bool[10];
        public double latitudeTaxi { get; set; }
        public double longitudeTaxi { get; set; }*/

        public int countTaxi { get; set; }

        public bool checkDeparting { get; set; }
        public bool checkApproaching { get; set; }
        public int countApproaching { get; set; }
        public int checkAltitude { get; set; }
        public int checkHeading { get; set; }
        public bool finnishCollectingWp { get; set; } = false;
        public double latitude { get; set; }
        public double latitudeBefore { get; set; }
        public double longitude { get; set; }
        public double longitudeBefore { get; set; }
        public double altimeter { get; set; }
        public double altimeterBefore { get; set; }
        public double altimeterMeter{ get; set; }
        public double altimeterMeterBefore { get; set; }
        public int speed { get; set; }
        public int heading { get; set; }
        public int StartHeading { get; set; }
        public double headingBefore { get; set; }
        public bool isGrounded { get; set; }
        public string airportOrigin { get; set; }
        public string airportDestination { get; set; }

        public List<Waypoint> waypoints { get; set; } = new();
        

        #endregion FlightPath

        public SIMCONNECT_DATA_WAYPOINT[] GetSimConnectDataWaypoints()
        {
            SIMCONNECT_DATA_WAYPOINT[] result = new SIMCONNECT_DATA_WAYPOINT[waypoints.Count];
            if (waypoints.Count == 0) Log.Information("Trying to generate a waypoint but I have no waypoint data! " + callsign);
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i].IsGrounded)
                {
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED); //(SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND | 
        }
                else
                {
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED);
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
                wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL | SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND);
            }
            else
            {
                wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL | SIMCONNECT_WAYPOINT_FLAGS.COMPUTE_VERTICAL_SPEED);
            }

            wp[0].Altitude = altimeter;
            wp[0].Latitude = latitude;
            wp[0].Longitude = longitude;
            wp[0].ktsSpeed = speed;

            var obj = new Object[wp.Length];
            wp.CopyTo(obj, 0);
            return obj;
        }
    }
}

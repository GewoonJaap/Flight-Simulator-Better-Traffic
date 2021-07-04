using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Generic;

namespace Simvars.Model
{
    public class Aircraft
    {
        #region SimData

        public int RequestId;
        public uint ObjectId = 0;
        public string MatchedModel;

        #endregion SimData

        #region Aircraft

        public string FlightRadarId;
        public string Callsign;
        public string TailNumber;
        public string Model;
        public string Airline;

        #endregion Aircraft

        #region FlightPath

        public double Latitude;
        public double Longitude;
        public double Altimeter;
        public int Speed;
        public int Heading;
        public bool IsGrounded;
        public string AirportOrigin;
        public string AirportDestination;

        public List<Waypoint> Waypoints = new List<Waypoint>();

        #endregion FlightPath

        public SIMCONNECT_DATA_WAYPOINT[] GetSimConnectDataWaypoints()
        {
            SIMCONNECT_DATA_WAYPOINT[] result = new SIMCONNECT_DATA_WAYPOINT[Waypoints.Count];
            if (Waypoints.Count == 0) Console.WriteLine("Trying to generate a waypoint but I have no waypoint data! " + Callsign);
            for (int i = 0; i < Waypoints.Count; i++)
            {
                if (Waypoints[i].IsGrounded)
                {
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL);
                }
                else
                {
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL);
                }
                result[i].Altitude = Waypoints[i].Altitude;
                result[i].Latitude = Waypoints[i].Latitude;
                result[i].Longitude = Waypoints[i].Longitude;
                result[i].ktsSpeed = Waypoints[i].Speed;
                Console.WriteLine("Setting waypoint " + i + " for " + TailNumber + " lat " + result[i].Latitude + " long " + result[i].Longitude + " speed " + result[i].ktsSpeed + "  altitude " + result[i].Altitude + " objectId " + ObjectId);
            }

            Waypoints.RemoveAt(0);

            return result;
        }

        public object[] GetWayPointObjectArray()
        {
            //var dataWaypoints = GetSimConnectDataWaypoints();

            SIMCONNECT_DATA_WAYPOINT[] wp = new SIMCONNECT_DATA_WAYPOINT[1];

            if (IsGrounded)
            {
                wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL);
            }
            else
            {
                wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL | SIMCONNECT_WAYPOINT_FLAGS.COMPUTE_VERTICAL_SPEED);
            }
            wp[0].Altitude = Altimeter;
            wp[0].Latitude = Latitude;
            wp[0].Longitude = Longitude;
            wp[0].ktsSpeed = Speed;

            var obj = new Object[wp.Length];
            wp.CopyTo(obj, 0);
            return obj;
        }
    }
}

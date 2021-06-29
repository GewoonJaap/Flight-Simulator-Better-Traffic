using System;
using System.Collections.Generic;
using System.Windows.Documents;
using Microsoft.FlightSimulator.SimConnect;
using Simvars.Emum;

namespace Simvars.Model
{
    internal class Aircraft
    {
        #region SimData

        public int RequestId;
        public uint ObjectId;
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
        public int Altimeter;
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
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND);
                }
                else
                {
                    result[i].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED);
                }
                result[i].Altitude = Waypoints[i].Altitude;
                result[i].Latitude = Waypoints[i].Latitude;
                result[i].Longitude = Waypoints[i].Longitude;
                result[i].ktsSpeed = Waypoints[i].Speed;
                Console.WriteLine("Setting waypoint " + i + " for " + TailNumber + " lat " + result[i].Latitude + " long " + result[i].Longitude + " speed " + result[i].ktsSpeed + "  altitude " + result[i].Altitude);
            }

            Waypoints.RemoveAt(0);

            return result;
        }

        public object[] GetWayPointObjectArray()
        {
            var dataWaypoints = GetSimConnectDataWaypoints();

            SIMCONNECT_DATA_WAYPOINT[] wp = new SIMCONNECT_DATA_WAYPOINT[1];

            wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED | SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND);
            wp[0].Altitude = 0;
            wp[0].Latitude = 51.95473;
            wp[0].Longitude = 4.435475;
            wp[0].ktsSpeed = 0;

            var obj = new Object[dataWaypoints.Length];
            dataWaypoints.CopyTo(obj, 0);
            return obj;
        }
    }
}

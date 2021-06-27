using Microsoft.FlightSimulator.SimConnect;
using Simvars.Emum;

namespace Simvars.Model
{
    internal class Aircraft
    {
        #region SimData

        public int RequestId;
        private SimConnect m_simConnect;
        public uint ObjectId;

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

        #endregion FlightPath
    }
}

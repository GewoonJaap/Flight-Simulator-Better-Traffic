using System;
using System.Collections.Generic;
using Simvars.Struct;

namespace Simvars.Model
{
    public class PlayerAircraft
    {
        private const int N = 100;
        public double Longitude { get; private set; }
        public double Latitude { get; private set; }
        public double Altitude { get; private set; }
        public double Airspeed { get; private set; }
        public double Pitch { get; private set; }
        public double Bank { get; private set; }
        public double Delta { get; private set; }
        public double Turn { get; private set; }
        public double Heading { get; private set; }
        public double VerticalSpeed { get; private set; }
        public double GroundSpeed { get; private set; }
        public double[] AirspeedData = new double[N];
        public List<double> y = new List<double>();
        public List<double> x = new List<double>();

        private int cnt = 0;

        public void Update(PlayerAircraftStruct planeStructure)
        {
            Console.WriteLine("Got data, long: " + Longitude + " lat: " + Latitude);
            Longitude = planeStructure.longitude;
            Latitude = planeStructure.latitude;
            Altitude = planeStructure.altitude;
            Airspeed = planeStructure.airspeed;
            Pitch = planeStructure.pitch;
            Bank = planeStructure.bank;
            Delta = planeStructure.delta;
            Turn = planeStructure.turn;
            Heading = planeStructure.heading;
            VerticalSpeed = planeStructure.verticalSpeed;
            GroundSpeed = planeStructure.groundSpeed;
            //AirspeedData[cnt] = Airspeed;
            //cnt = (cnt + 1) % N;
            if (y.Count == N)
            {
                y.RemoveAt(0);
                x.RemoveAt(0);
            }
            y.Add(Airspeed);
            x.Add(cnt++);
        }
    }
}

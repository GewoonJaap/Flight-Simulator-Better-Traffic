﻿using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Threading;

using Microsoft.FlightSimulator.SimConnect;
using Simvars.Emum;
using Simvars.Util;

namespace Simvars
{
    public enum DEFINITION
    {
        Dummy = 0
    };

    public enum REQUEST
    {
        Dummy = 0,
        Struct1
    };

    // String properties must be packed inside of a struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct Struct1
    {
        // this is how you declare a fixed size string
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public String sValue;

        // other definitions can be added to this struct ...
    };

    public class SimvarRequest : ObservableObject
    {
        public DEFINITION eDef = DEFINITION.Dummy;
        public REQUEST eRequest = REQUEST.Dummy;

        public string sName { get; set; }
        public bool bIsString { get; set; }

        public double dValue
        {
            get { return m_dValue; }
            set { this.SetProperty(ref m_dValue, value); }
        }

        private double m_dValue = 0.0;

        public string sValue
        {
            get { return m_sValue; }
            set { this.SetProperty(ref m_sValue, value); }
        }

        private string m_sValue = null;

        public string sUnits { get; set; }

        public bool bPending = true;

        public bool bStillPending
        {
            get { return m_bStillPending; }
            set { this.SetProperty(ref m_bStillPending, value); }
        }

        private bool m_bStillPending = false;
    };

    public class SimvarsViewModel : BaseViewModel, IBaseSimConnectWrapper
    {
        #region IBaseSimConnectWrapper implementation

        /// User-defined win32 event
        public const int WM_USER_SIMCONNECT = 0x0402;

        /// Window handle
        private IntPtr m_hWnd = new IntPtr(0);

        /// SimConnect object
        private SimConnect m_oSimConnect = null;

        public bool bConnected
        {
            get { return m_bConnected; }
            private set { this.SetProperty(ref m_bConnected, value); }
        }

        private bool m_bConnected = false;

        private uint m_iCurrentDefinition = 0;
        private uint m_iCurrentRequest = 0;

        public int GetUserSimConnectWinEvent()
        {
            return WM_USER_SIMCONNECT;
        }

        public void ReceiveSimConnectMessage()
        {
            m_oSimConnect?.ReceiveMessage();
        }

        public void SetWindowHandle(IntPtr _hWnd)
        {
            m_hWnd = _hWnd;
        }

        public void Disconnect()
        {
            Console.WriteLine("Disconnect");

            m_oTimer.Stop();
            bOddTick = false;

            if (m_oSimConnect != null)
            {
                /// Dispose serves the same purpose as SimConnect_Close()
                m_oSimConnect.Dispose();
                m_oSimConnect = null;
            }

            sConnectButtonLabel = "Connect";
            bConnected = false;

            // Set all requests as pending
            foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
            {
                oSimvarRequest.bPending = true;
                oSimvarRequest.bStillPending = true;
            }
        }

        #endregion IBaseSimConnectWrapper implementation

        #region UI bindings

        public string sConnectButtonLabel
        {
            get { return m_sConnectButtonLabel; }
            private set { this.SetProperty(ref m_sConnectButtonLabel, value); }
        }

        private string m_sConnectButtonLabel = "Connect";

        public bool bObjectIDSelectionEnabled
        {
            get { return m_bObjectIDSelectionEnabled; }
            set { this.SetProperty(ref m_bObjectIDSelectionEnabled, value); }
        }

        private bool m_bObjectIDSelectionEnabled = false;

        public SIMCONNECT_SIMOBJECT_TYPE eSimObjectType
        {
            get { return m_eSimObjectType; }
            set
            {
                this.SetProperty(ref m_eSimObjectType, value);
                bObjectIDSelectionEnabled = (m_eSimObjectType != SIMCONNECT_SIMOBJECT_TYPE.USER);
                ClearResquestsPendingState();
            }
        }

        private SIMCONNECT_SIMOBJECT_TYPE m_eSimObjectType = SIMCONNECT_SIMOBJECT_TYPE.USER;
        public ObservableCollection<uint> lObjectIDs { get; private set; }

        public uint iObjectIdRequest
        {
            get { return m_iObjectIdRequest; }
            set
            {
                this.SetProperty(ref m_iObjectIdRequest, value);
                ClearResquestsPendingState();
            }
        }

        private uint m_iObjectIdRequest = 0;

        public string[] aSimvarNames
        {
            get { return SimUtils.SimVars.Names; }
            private set { }
        }

        public string sSimvarRequest
        {
            get { return m_sSimvarRequest; }
            set { this.SetProperty(ref m_sSimvarRequest, value); }
        }

        private string m_sSimvarRequest = null;

        public string[] aUnitNames
        {
            get { return SimUtils.Units.Names; }
            private set { }
        }

        public string sUnitRequest
        {
            get { return m_sUnitRequest; }
            set { this.SetProperty(ref m_sUnitRequest, value); }
        }

        private string m_sUnitRequest = null;

        public string sSetValue
        {
            get { return m_sSetValue; }
            set { this.SetProperty(ref m_sSetValue, value); }
        }

        private string m_sSetValue = null;

        public ObservableCollection<SimvarRequest> lSimvarRequests { get; private set; }

        public SimvarRequest oSelectedSimvarRequest
        {
            get { return m_oSelectedSimvarRequest; }
            set { this.SetProperty(ref m_oSelectedSimvarRequest, value); }
        }

        private SimvarRequest m_oSelectedSimvarRequest = null;

        public uint[] aIndices
        {
            get { return m_aIndices; }
            private set { }
        }

        private readonly uint[] m_aIndices = new uint[100] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
                                                            10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
                                                            20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
                                                            30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
                                                            40, 41, 42, 43, 44, 45, 46, 47, 48, 49,
                                                            50, 51, 52, 53, 54, 55, 56, 57, 58, 59,
                                                            60, 61, 62, 63, 64, 65, 66, 67, 68, 69,
                                                            70, 71, 72, 73, 74, 75, 76, 77, 78, 79,
                                                            80, 81, 82, 83, 84, 85, 86, 87, 88, 89,
                                                            90, 91, 92, 93, 94, 95, 96, 97, 98, 99 };

        public uint iIndexRequest
        {
            get { return m_iIndexRequest; }
            set { this.SetProperty(ref m_iIndexRequest, value); }
        }

        private uint m_iIndexRequest = 0;

        public bool bSaveValues
        {
            get { return m_bSaveValues; }
            set { this.SetProperty(ref m_bSaveValues, value); }
        }

        private bool m_bSaveValues = true;

        public bool bFSXcompatible
        {
            get { return m_bFSXcompatible; }
            set { this.SetProperty(ref m_bFSXcompatible, value); }
        }

        private bool m_bFSXcompatible = false;

        public bool bIsString
        {
            get { return m_bIsString; }
            set { this.SetProperty(ref m_bIsString, value); }
        }

        private bool m_bIsString = false;

        public bool bOddTick
        {
            get { return m_bOddTick; }
            set { this.SetProperty(ref m_bOddTick, value); }
        }

        private bool m_bOddTick = false;

        public ObservableCollection<string> lErrorMessages { get; private set; }

        public BaseCommand cmdToggleConnect { get; private set; }
        public BaseCommand cmdAddRequest { get; private set; }
        public BaseCommand cmdRemoveSelectedRequest { get; private set; }
        public BaseCommand cmdTrySetValue { get; private set; }
        public BaseCommand cmdLoadFiles { get; private set; }
        public BaseCommand cmdSaveFile { get; private set; }

        #endregion UI bindings

        #region Real time

        private DispatcherTimer m_oTimer = new DispatcherTimer();

        #endregion Real time

        private int requestCount = 0;

        public SimvarsViewModel()
        {
            lObjectIDs = new ObservableCollection<uint>();
            lObjectIDs.Add(1);

            lSimvarRequests = new ObservableCollection<SimvarRequest>();
            lErrorMessages = new ObservableCollection<string>();

            cmdToggleConnect = new BaseCommand((p) => { ToggleConnect(); });
            cmdAddRequest = new BaseCommand((p) => { AddRequest((m_iIndexRequest == 0) ? m_sSimvarRequest : (m_sSimvarRequest + ":" + m_iIndexRequest), sUnitRequest, bIsString); });
            cmdRemoveSelectedRequest = new BaseCommand((p) => { RemoveSelectedRequest(); });
            cmdTrySetValue = new BaseCommand((p) => { TrySetValue(); });
            cmdLoadFiles = new BaseCommand((p) => { LoadFiles(); });
            cmdSaveFile = new BaseCommand((p) => { SaveFile(false); });

            m_oTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            m_oTimer.Tick += new EventHandler(OnTick);
        }

        private void Connect()
        {
            Console.WriteLine("Connect");

            try
            {
                /// The constructor is similar to SimConnect_Open in the native API
                m_oSimConnect = new SimConnect("Simconnect - Simvar test", m_hWnd, WM_USER_SIMCONNECT, null, bFSXcompatible ? (uint)1 : 0);

                /// Listen to connect and quit msgs
                m_oSimConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(SimConnect_OnRecvOpen);
                m_oSimConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(SimConnect_OnRecvQuit);

                /// Listen to exceptions
                m_oSimConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(SimConnect_OnRecvException);

                /// Catch a simobject data request
                m_oSimConnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(SimConnect_OnRecvSimobjectDataBytype);
            }
            catch (COMException ex)
            {
                Console.WriteLine("Connection to KH failed: " + ex.Message);
            }
        }

        private void SpawnPlane()
        {
            FlightRadarApi.GetAircraftData("2830dfbd");
            var requestID = DataRequests.AI_SPAWN + requestCount;
            requestCount = (requestCount + 1) % 10000;
            Console.WriteLine(@"Spawning a plane!");
            var position = new SIMCONNECT_DATA_INITPOSITION
            {
                Latitude = 52.328807,
                Longitude = 4.738229,
                Altitude = 1000,
                Pitch = 10,
                Bank = 0,
                Heading = 40,
                OnGround = 0,
                Airspeed = 100,
            };
            m_oSimConnect.AICreateNonATCAircraft("Airbus A320 Neo KLM", "TEST", position, requestID);
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("SimConnect_OnRecvOpen");
            Console.WriteLine("Connected to KH");

            sConnectButtonLabel = "Disconnect";
            bConnected = true;

            // Register pending requests
            foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
            {
                if (oSimvarRequest.bPending)
                {
                    oSimvarRequest.bPending = !RegisterToSimConnect(oSimvarRequest);
                    oSimvarRequest.bStillPending = oSimvarRequest.bPending;
                }
            }

            m_oTimer.Start();
            bOddTick = false;
            SpawnPlane();
        }

        /// The case where the user closes game
        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("SimConnect_OnRecvQuit");
            Console.WriteLine("KH has exited");

            Disconnect();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            SIMCONNECT_EXCEPTION eException = (SIMCONNECT_EXCEPTION)data.dwException;
            Console.WriteLine("SimConnect_OnRecvException: " + eException.ToString());

            lErrorMessages.Add("SimConnect : " + eException.ToString());
        }

        private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            Console.WriteLine("SimConnect_OnRecvSimobjectDataBytype");

            uint iRequest = data.dwRequestID;
            uint iObject = data.dwObjectID;
            if (!lObjectIDs.Contains(iObject))
            {
                lObjectIDs.Add(iObject);
            }
            foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
            {
                if (iRequest == (uint)oSimvarRequest.eRequest && (!bObjectIDSelectionEnabled || iObject == m_iObjectIdRequest))
                {
                    if (oSimvarRequest.bIsString)
                    {
                        Struct1 result = (Struct1)data.dwData[0];
                        oSimvarRequest.dValue = 0;
                        oSimvarRequest.sValue = result.sValue;
                    }
                    else
                    {
                        double dValue = (double)data.dwData[0];
                        oSimvarRequest.dValue = dValue;
                        oSimvarRequest.sValue = dValue.ToString("F9");
                    }

                    oSimvarRequest.bPending = false;
                    oSimvarRequest.bStillPending = false;
                }
            }
        }

        // May not be the best way to achive regular requests. See SimConnect.RequestDataOnSimObject
        private void OnTick(object sender, EventArgs e)
        {
            Console.WriteLine("OnTick");

            bOddTick = !bOddTick;

            foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
            {
                if (!oSimvarRequest.bPending)
                {
                    m_oSimConnect?.RequestDataOnSimObjectType(oSimvarRequest.eRequest, oSimvarRequest.eDef, 0, m_eSimObjectType);
                    oSimvarRequest.bPending = true;
                }
                else
                {
                    oSimvarRequest.bStillPending = true;
                }
            }
        }

        private void ToggleConnect()
        {
            if (m_oSimConnect == null)
            {
                try
                {
                    Connect();
                }
                catch (COMException ex)
                {
                    Console.WriteLine("Unable to connect to KH: " + ex.Message);
                }
            }
            else
            {
                Disconnect();
            }
        }

        private void ClearResquestsPendingState()
        {
            foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
            {
                oSimvarRequest.bPending = false;
                oSimvarRequest.bStillPending = false;
            }
        }

        private bool RegisterToSimConnect(SimvarRequest _oSimvarRequest)
        {
            if (m_oSimConnect != null)
            {
                if (_oSimvarRequest.bIsString)
                {
                    /// Define a data structure containing string value
                    m_oSimConnect.AddToDataDefinition(_oSimvarRequest.eDef, _oSimvarRequest.sName, "", SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    /// IMPORTANT: Register it with the simconnect managed wrapper marshaller If you
                    /// skip this step, you will only receive a uint in the .dwData field.
                    m_oSimConnect.RegisterDataDefineStruct<Struct1>(_oSimvarRequest.eDef);
                }
                else
                {
                    /// Define a data structure containing numerical value
                    m_oSimConnect.AddToDataDefinition(_oSimvarRequest.eDef, _oSimvarRequest.sName, _oSimvarRequest.sUnits, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    /// IMPORTANT: Register it with the simconnect managed wrapper marshaller If you
                    /// skip this step, you will only receive a uint in the .dwData field.
                    m_oSimConnect.RegisterDataDefineStruct<double>(_oSimvarRequest.eDef);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private void AddRequest(string _sNewSimvarRequest, string _sNewUnitRequest, bool _bIsString)
        {
            Console.WriteLine("AddRequest");

            //string sNewSimvarRequest = _sOverrideSimvarRequest != null ? _sOverrideSimvarRequest : ((m_iIndexRequest == 0) ? m_sSimvarRequest : (m_sSimvarRequest + ":" + m_iIndexRequest));
            //string sNewUnitRequest = _sOverrideUnitRequest != null ? _sOverrideUnitRequest : m_sUnitRequest;
            SimvarRequest oSimvarRequest = new SimvarRequest
            {
                eDef = (DEFINITION)m_iCurrentDefinition,
                eRequest = (REQUEST)m_iCurrentRequest,
                sName = _sNewSimvarRequest,
                bIsString = _bIsString,
                sUnits = _bIsString ? null : _sNewUnitRequest
            };

            oSimvarRequest.bPending = !RegisterToSimConnect(oSimvarRequest);
            oSimvarRequest.bStillPending = oSimvarRequest.bPending;

            lSimvarRequests.Add(oSimvarRequest);

            ++m_iCurrentDefinition;
            ++m_iCurrentRequest;
        }

        private void RemoveSelectedRequest()
        {
            lSimvarRequests.Remove(oSelectedSimvarRequest);
        }

        private void TrySetValue()
        {
            Console.WriteLine("TrySetValue");

            if (m_oSelectedSimvarRequest != null && m_sSetValue != null)
            {
                if (!m_oSelectedSimvarRequest.bIsString)
                {
                    double dValue = 0.0;
                    if (double.TryParse(m_sSetValue, NumberStyles.Any, null, out dValue))
                    {
                        m_oSimConnect.SetDataOnSimObject(m_oSelectedSimvarRequest.eDef, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_DATA_SET_FLAG.DEFAULT, dValue);
                    }
                }
                else
                {
                    Struct1 sValueStruct = new Struct1()
                    {
                        sValue = m_sSetValue
                    };
                    m_oSimConnect.SetDataOnSimObject(m_oSelectedSimvarRequest.eDef, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_DATA_SET_FLAG.DEFAULT, sValueStruct);
                }
            }
        }

        private void LoadFiles()
        {
            Microsoft.Win32.OpenFileDialog oOpenFileDialog = new Microsoft.Win32.OpenFileDialog();
            oOpenFileDialog.Multiselect = true;
            oOpenFileDialog.Filter = "Simvars files (*.simvars)|*.simvars";
            if (oOpenFileDialog.ShowDialog() == true)
            {
                foreach (string sFilename in oOpenFileDialog.FileNames)
                {
                    LoadFile(sFilename);
                }
            }
        }

        private void LoadFile(string _sFileName)
        {
            string[] aLines = System.IO.File.ReadAllLines(_sFileName);
            for (uint i = 0; i < aLines.Length; ++i)
            {
                // Format : Simvar,Unit
                string[] aSubStrings = aLines[i].Split(',');
                if (aSubStrings.Length >= 2) // format check
                {
                    // values check
                    string[] aSimvarSubStrings = aSubStrings[0].Split(':'); // extract Simvar name from format Simvar:Index
                    string sSimvarName = Array.Find(SimUtils.SimVars.Names, s => s == aSimvarSubStrings[0]);
                    string sUnitName = Array.Find(SimUtils.Units.Names, s => s == aSubStrings[1]);
                    bool bIsString = aSubStrings.Length > 2 && bool.Parse(aSubStrings[2]);
                    if (sSimvarName != null && (sUnitName != null || bIsString))
                    {
                        AddRequest(aSubStrings[0], sUnitName, bIsString);
                    }
                    else
                    {
                        if (sSimvarName == null)
                        {
                            lErrorMessages.Add("l." + i.ToString() + " Wrong Simvar name : " + aSubStrings[0]);
                        }
                        if (sUnitName == null)
                        {
                            lErrorMessages.Add("l." + i.ToString() + " Wrong Unit name : " + aSubStrings[1]);
                        }
                    }
                }
                else
                {
                    lErrorMessages.Add("l." + i.ToString() + " Bad input format : " + aLines[i]);
                    lErrorMessages.Add("l." + i.ToString() + " Must be : SIMVAR,UNIT");
                }
            }
        }

        private void SaveFile(bool _bWriteValues)
        {
            Microsoft.Win32.SaveFileDialog oSaveFileDialog = new Microsoft.Win32.SaveFileDialog();
            oSaveFileDialog.Filter = "Simvars files (*.simvars)|*.simvars";
            if (oSaveFileDialog.ShowDialog() == true)
            {
                using (StreamWriter oStreamWriter = new StreamWriter(oSaveFileDialog.FileName, false))
                {
                    foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
                    {
                        // Format : Simvar,Unit
                        string sFormatedLine = oSimvarRequest.sName + "," + oSimvarRequest.sUnits + "," + oSimvarRequest.bIsString;
                        if (bSaveValues)
                        {
                            sFormatedLine += ",  " + oSimvarRequest.dValue.ToString();
                        }
                        oStreamWriter.WriteLine(sFormatedLine);
                    }
                }
            }
        }

        public void SetTickSliderValue(int _iValue)
        {
            m_oTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)(_iValue));
        }
    }
}

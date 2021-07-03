using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using Microsoft.FlightSimulator.SimConnect;
using Sentry;
using Simvars.Emum;
using Simvars.Model;
using Simvars.Struct;
using Simvars.Util;

namespace Simvars
{
    public enum Definition
    {
        Dummy = 0
    }

    public enum Request
    {
        Dummy = 0
    }

    // String properties must be packed inside of a struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct Struct1
    {
        // this is how you declare a fixed size string
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string sValue;

        // other definitions can be added to this struct ...
    }

    public class SimvarRequest : ObservableObject
    {
        private bool _mBStillPending;

        private double _mDValue;

        private string _mSValue;

        public bool BPending = true;
        public Definition EDef = Definition.Dummy;
        public Request ERequest = Request.Dummy;

        public string sName { get; set; }
        public bool bIsString { get; set; }

        public double dValue
        {
            get => _mDValue;
            set => SetProperty(ref _mDValue, value);
        }

        public string sValue
        {
            get => _mSValue;
            set => SetProperty(ref _mSValue, value);
        }

        public string sUnits { get; set; }

        public bool bStillPending
        {
            get => _mBStillPending;
            set => SetProperty(ref _mBStillPending, value);
        }
    }

    public class SimvarsViewModel : BaseViewModel, IBaseSimConnectWrapper
    {
        #region Real time

        private readonly DispatcherTimer _mOTimer = new();

        #endregion Real time

        private bool _clicked;

        private Timer _dataTimer;
        private LiveTrafficHandler _liveTrafficHandler;

        private PlayerAircraft _plane;

        public SimvarsViewModel()
        {
            using (SentrySdk.Init(o =>
            {
                o.Dsn = "https://0992ad58083f40cda51d04f3ccfc190f@o252778.ingest.sentry.io/5846102";
                // When configuring for the first time, to see what the SDK is doing:
                o.Debug = true;
                // Set traces_sample_rate to 1.0 to capture 100% of transactions for performance
                // monitoring. We recommend adjusting this value in production.
                o.TracesSampleRate = 1.0;
            }))
            {
                lObjectIDs = new ObservableCollection<uint> { 1 };

                lSimvarRequests = new ObservableCollection<SimvarRequest>();
                lErrorMessages = new ObservableCollection<string>();

                cmdToggleConnect = new BaseCommand(p => { ToggleConnect(); });
                cmdAddRequest = new BaseCommand(p =>
                {
                    AddRequest(_mIIndexRequest == 0 ? _mSSimvarRequest : _mSSimvarRequest + ":" + _mIIndexRequest,
                        sUnitRequest, bIsString);
                });
                cmdLoadFiles = new BaseCommand(p => { ChangeWayPoint(); });

                _mOTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
                _mOTimer.Tick += OnTick;

                AddonScanner.ScanAddons();
            }
        }

        private void ChangeWayPoint()
        {
            _liveTrafficHandler?.LiveTrafficAircraft?.ForEach(item =>
            {
                Console.WriteLine("Setting waypoint manual objectId " + item.ObjectId);
                if (!_clicked)
                {
                    Console.WriteLine(@"Going to eindhoven");
                    var wp = new SIMCONNECT_DATA_WAYPOINT[1];

                    wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED |
                                          SIMCONNECT_WAYPOINT_FLAGS.ON_GROUND |
                                          SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL);
                    wp[0].Altitude = 0;
                    wp[0].Latitude = 51.452165;
                    wp[0].Longitude = 5.376859;
                    wp[0].ktsSpeed = 0;

                    var obj = new object[wp.Length];
                    wp.CopyTo(obj, 0);
                    _mOSimConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, item.ObjectId,
                        SIMCONNECT_DATA_SET_FLAG.DEFAULT, obj);
                }
                else
                {
                    Console.WriteLine(@"Going to Live Location");
                    var wp = new SIMCONNECT_DATA_WAYPOINT[1];

                    wp[0].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED |
                                          SIMCONNECT_WAYPOINT_FLAGS.ALTITUDE_IS_AGL);
                    wp[0].Altitude = item.Altimeter;
                    wp[0].Latitude = item.Latitude;
                    wp[0].Longitude = item.Longitude;
                    wp[0].ktsSpeed = item.Speed;

                    var obj = new object[wp.Length];
                    wp.CopyTo(obj, 0);
                    _mOSimConnect.SetDataOnSimObject(SimConnectDataDefinition.PlaneWaypoints, item.ObjectId,
                        SIMCONNECT_DATA_SET_FLAG.DEFAULT, obj);
                }
            });
            _clicked = !_clicked;
        }

        private void Connect()
        {
            Console.WriteLine(@"Connect");

            _plane = new PlayerAircraft();

            try
            {
                /// The constructor is similar to SimConnect_Open in the native API
                _mOSimConnect = new SimConnect("Simconnect - Enhanced Live Traffic", _mHWnd, WM_USER_SIMCONNECT, null,
                    bFSXcompatible ? (uint)1 : 0);

                /// Listen to connect and quit msgs
                _mOSimConnect.OnRecvOpen += SimConnect_OnRecvOpen;
                _mOSimConnect.OnRecvQuit += SimConnect_OnRecvQuit;
                _mOSimConnect.OnRecvAssignedObjectId +=
                    SimConnect_OnRecvAssignedObjectId;

                /// Listen to exceptions
                _mOSimConnect.OnRecvException += SimConnect_OnRecvException;

                /// Catch a simobject data request
                _mOSimConnect.OnRecvSimobjectDataBytype += SimConnect_OnRecvSimobjectDataBytype;
                _mOSimConnect.OnRecvSimobjectData += simconnect_OnRecvSimobjectData;

                _liveTrafficHandler = new LiveTrafficHandler(_mOSimConnect);
            }
            catch (COMException ex)
            {
                Console.WriteLine(@"Connection to KH failed: " + ex.Message);
            }
        }

        private void DataTimerCallback(object? state)
        {
            _liveTrafficHandler.FetchNewData(_plane);
        }

        private void SimConnect_OnRecvAssignedObjectId(SimConnect sender, SIMCONNECT_RECV_ASSIGNED_OBJECT_ID data)
        {
            Console.WriteLine(@"Recieved object ID " + data.dwObjectID + @" from request: " + data.dwRequestID);
            _liveTrafficHandler.SetObjectId(data.dwRequestID, data.dwObjectID);
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine(@"SimConnect_OnRecvOpen");
            Console.WriteLine(@"Connected to KH");

            _dataTimer = new Timer(DataTimerCallback, null, 0, 1000 * 10);

            sConnectButtonLabel = "Disconnect";
            bConnected = true;

            // Register pending requests
            foreach (var oSimvarRequest in lSimvarRequests)
                if (oSimvarRequest.BPending)
                {
                    oSimvarRequest.BPending = !RegisterToSimConnect(oSimvarRequest);
                    oSimvarRequest.bStillPending = oSimvarRequest.BPending;
                }

            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneLocation, "Plane Longitude", "degrees",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneLocation, "Plane Latitude", "degrees",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneLocation, "Plane Altitude", "meters",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneLocation, "Plane Pitch Degrees", "degrees",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneLocation, "Plane Bank Degrees", "degrees",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneLocation, "Plane Heading Degrees True",
                "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneLocation, "AIRSPEED TRUE", "knots",
                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.AddToDataDefinition(SimConnectDataDefinition.PlaneWaypoints, "AI WAYPOINT LIST", "number",
                SIMCONNECT_DATATYPE.WAYPOINT, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            _mOSimConnect.RegisterDataDefineStruct<PlayerAircraftStruct>(SimConnectDataDefinition.PlaneLocation);
            _mOSimConnect.RequestDataOnSimObject(DataRequests.REQUEST_1, SimConnectDataDefinition.PlaneLocation,
                SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND, 0, 0, 0, 0);

            _mOTimer.Start();
            bOddTick = false;
        }

        /// The case where the user closes game
        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine(@"SimConnect_OnRecvQuit");
            Console.WriteLine(@"KH has exited");

            Disconnect();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            var eException = (SIMCONNECT_EXCEPTION)data.dwException;
            Console.WriteLine("SimConnect_OnRecvException: " + eException);

            lErrorMessages.Add("SimConnect : " + eException);
        }

        private void simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            var planeStructure = (PlayerAircraftStruct)data.dwData[0];
            _plane.Update(planeStructure);
        }

        private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            Console.WriteLine(@"SimConnect_OnRecvSimobjectDataBytype");

            var iRequest = data.dwRequestID;
            var iObject = data.dwObjectID;
            if (!lObjectIDs.Contains(iObject)) lObjectIDs.Add(iObject);
            foreach (var oSimvarRequest in lSimvarRequests)
                if (iRequest == (uint)oSimvarRequest.ERequest &&
                    (!bObjectIdSelectionEnabled || iObject == _mIObjectIdRequest))
                {
                    if (oSimvarRequest.bIsString)
                    {
                        var result = (Struct1)data.dwData[0];
                        oSimvarRequest.dValue = 0;
                        oSimvarRequest.sValue = result.sValue;
                    }
                    else
                    {
                        var dValue = (double)data.dwData[0];
                        oSimvarRequest.dValue = dValue;
                        oSimvarRequest.sValue = dValue.ToString("F9");
                    }

                    oSimvarRequest.BPending = false;
                    oSimvarRequest.bStillPending = false;
                }
        }

        // May not be the best way to achive regular requests. See SimConnect.RequestDataOnSimObject
        private void OnTick(object sender, EventArgs e)
        {
            Console.WriteLine(@"OnTick");

            bOddTick = !bOddTick;

            foreach (var oSimvarRequest in lSimvarRequests)
                if (!oSimvarRequest.BPending)
                {
                    _mOSimConnect?.RequestDataOnSimObjectType(oSimvarRequest.ERequest, oSimvarRequest.EDef, 0,
                        _mESimObjectType);
                    oSimvarRequest.BPending = true;
                }
                else
                {
                    oSimvarRequest.bStillPending = true;
                }
        }

        private void ToggleConnect()
        {
            if (_mOSimConnect == null)
                try
                {
                    Connect();
                }
                catch (COMException ex)
                {
                    Console.WriteLine("Unable to connect to KH: " + ex.Message);
                }
            else
                Disconnect();
        }

        private bool RegisterToSimConnect(SimvarRequest oSimvarRequest)
        {
            if (_mOSimConnect != null)
            {
                if (oSimvarRequest.bIsString)
                {
                    /// Define a data structure containing string value
                    _mOSimConnect.AddToDataDefinition(oSimvarRequest.EDef, oSimvarRequest.sName, "",
                        SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    /// IMPORTANT: Register it with the simconnect managed wrapper marshaller If you
                    /// skip this step, you will only receive a uint in the .dwData field.
                    _mOSimConnect.RegisterDataDefineStruct<Struct1>(oSimvarRequest.EDef);
                }
                else
                {
                    /// Define a data structure containing numerical value
                    _mOSimConnect.AddToDataDefinition(oSimvarRequest.EDef, oSimvarRequest.sName, oSimvarRequest.sUnits,
                        SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    /// IMPORTANT: Register it with the simconnect managed wrapper marshaller If you
                    /// skip this step, you will only receive a uint in the .dwData field.
                    _mOSimConnect.RegisterDataDefineStruct<double>(oSimvarRequest.EDef);
                }

                return true;
            }

            return false;
        }

        private void AddRequest(string sNewSimvarRequest, string sNewUnitRequest, bool bIsString)
        {
            Console.WriteLine(@"AddRequest");

            //string sNewSimvarRequest = _sOverrideSimvarRequest != null ? _sOverrideSimvarRequest : ((m_iIndexRequest == 0) ? m_sSimvarRequest : (m_sSimvarRequest + ":" + m_iIndexRequest));
            //string sNewUnitRequest = _sOverrideUnitRequest != null ? _sOverrideUnitRequest : m_sUnitRequest;
            var oSimvarRequest = new SimvarRequest
            {
                EDef = (Definition)_mICurrentDefinition,
                ERequest = (Request)_mICurrentRequest,
                sName = sNewSimvarRequest,
                bIsString = bIsString,
                sUnits = bIsString ? null : sNewUnitRequest
            };

            oSimvarRequest.BPending = !RegisterToSimConnect(oSimvarRequest);
            oSimvarRequest.bStillPending = oSimvarRequest.BPending;

            lSimvarRequests.Add(oSimvarRequest);

            ++_mICurrentDefinition;
            ++_mICurrentRequest;
        }

        #region IBaseSimConnectWrapper implementation

        /// User-defined win32 event
        public const int WM_USER_SIMCONNECT = 0x0402;

        /// Window handle
        private IntPtr _mHWnd = new(0);

        /// SimConnect object
        private SimConnect _mOSimConnect;

        public bool bConnected
        {
            get => _mBConnected;
            private set => SetProperty(ref _mBConnected, value);
        }

        private bool _mBConnected;

        private uint _mICurrentDefinition;
        private uint _mICurrentRequest;

        public int GetUserSimConnectWinEvent()
        {
            return WM_USER_SIMCONNECT;
        }

        public void ReceiveSimConnectMessage()
        {
            _mOSimConnect?.ReceiveMessage();
        }

        public void SetWindowHandle(IntPtr hWnd)
        {
            _mHWnd = hWnd;
        }

        public void Disconnect()
        {
            _dataTimer.Dispose();
            Console.WriteLine(@"Disconnect");

            _mOTimer.Stop();
            bOddTick = false;

            if (_mOSimConnect != null)
            {
                /// Dispose serves the same purpose as SimConnect_Close()
                _mOSimConnect.Dispose();
                _mOSimConnect = null;
            }

            sConnectButtonLabel = "Connect";
            bConnected = false;

            // Set all requests as pending
            foreach (var oSimvarRequest in lSimvarRequests)
            {
                oSimvarRequest.BPending = true;
                oSimvarRequest.bStillPending = true;
            }
        }

        #endregion IBaseSimConnectWrapper implementation

        #region UI bindings

        public string sConnectButtonLabel
        {
            get => _mSConnectButtonLabel;
            private set => SetProperty(ref _mSConnectButtonLabel, value);
        }

        private string _mSConnectButtonLabel = "Connect";

        public bool bObjectIdSelectionEnabled
        {
            get => _mBObjectIdSelectionEnabled;
            set => SetProperty(ref _mBObjectIdSelectionEnabled, value);
        }

        private bool _mBObjectIdSelectionEnabled;

        private readonly SIMCONNECT_SIMOBJECT_TYPE _mESimObjectType = SIMCONNECT_SIMOBJECT_TYPE.USER;
        public ObservableCollection<uint> lObjectIDs { get; }

        private uint _mIObjectIdRequest;

        private string _mSSimvarRequest;

        public string sUnitRequest
        {
            get => _mSUnitRequest;
            set => SetProperty(ref _mSUnitRequest, value);
        }

        private string _mSUnitRequest;

        private string _mSSetValue;

        public ObservableCollection<SimvarRequest> lSimvarRequests { get; }

        private SimvarRequest _mOSelectedSimvarRequest;

        private uint _mIIndexRequest;

        private bool _mBSaveValues = true;

        public bool bFSXcompatible
        {
            get => _mBFsXcompatible;
            set => SetProperty(ref _mBFsXcompatible, value);
        }

        private bool _mBFsXcompatible;

        public bool bIsString
        {
            get => _mBIsString;
            set => SetProperty(ref _mBIsString, value);
        }

        private bool _mBIsString;

        public bool bOddTick
        {
            get => _mBOddTick;
            set => SetProperty(ref _mBOddTick, value);
        }

        private bool _mBOddTick;

        public ObservableCollection<string> lErrorMessages { get; }

        public BaseCommand cmdToggleConnect { get; }
        public BaseCommand cmdAddRequest { get; }
        public BaseCommand cmdRemoveSelectedRequest { get; private set; }
        public BaseCommand cmdTrySetValue { get; private set; }
        public BaseCommand cmdLoadFiles { get; }

        #endregion UI bindings
    }
}

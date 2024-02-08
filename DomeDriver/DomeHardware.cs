//
// ASCOM Dome driver for APS
//
// Description:	 ASCOM Driver for Asto Photo Station Dome
//
// Implements:	ASCOM Dome interface version: 0.1
// Author:		(2023) APS Software
//

using ASCOM.Astrometry.AstroUtils;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ASCOM.APS.Dome
{
    /// <summary>
    /// ASCOM Dome hardware class for APS.
    /// </summary>
    [HardwareClass()] // Class attribute flag this as a device hardware class that needs to be disposed by the local server when it exits.
    internal static class DomeHardware
    {
        // Constants used for Profile persistence
        internal const string comPortProfileName = "COM Port";
        internal const string comPortDefault = "COM1";
        internal const string ipAddressProfileName = "IP Address";
        internal const string ipAddressDefault = "192.168.1.100";
        internal const string apiKeyProfileName = "API Key";
        internal const string apiKeyDefault = "";
        internal const string connectionModeProfileName = "Connection Mode";
        internal const int connectionModeDefault = 0;
        internal const string traceStateProfileName = "Trace Level";
        internal const string traceStateDefault = "true";

        private static string DriverProgId = ""; // ASCOM DeviceID (COM ProgID) for this driver, the value is set by the driver's class initialiser.
        private static string DriverDescription = "ASCOM APS Dome Driver"; // The value is set by the driver's class initialiser.
        internal static string comPort; // COM port name (if required)
        internal static string ipAddress; // IP Address of the device
        internal static string apiKey;
        internal static int connectionMode; // Connection mode [Serial = 0 / Network = 1]
        private static bool connectedState; // Local server's connected state
        private static bool runOnce = false; // Flag to enable "one-off" activities only to run once.
        internal static Util utilities; // ASCOM Utilities object for use as required
        internal static AstroUtils astroUtilities; // ASCOM AstroUtilities object for use as required
        internal static TraceLogger tl; // Local server's trace logger object for diagnostic log with information that you specify

        //private static SerialPort serial;
        private static ASCOM.Utilities.Serial serial;
        private static Thread thread;
        private static Mutex mutex;
        private static HttpClient client;
        private const int OP_STATUS__OK = 0;
        private const string OP_CMD__START = "|";
        private const string OP_CMD__END = "#";
        private const string OP_CMD__OPENSHUTTER = "O";         // Apertura del tetto
        private const string OP_CMD__CLOSESHUTTER = "C";        // Chiusura del tetto
        private const string OP_CMD__ABORTSLEW = "H";           // Stop movimento del tetto
        private const string OP_CMD__GETSHUTTERSTATUS = "S";    // Richiesta stato del tetto

        /// <summary>
        /// Initializes a new instance of the device Hardware class.
        /// </summary>
        static DomeHardware()
        {
            try
            {
                // Create the hardware trace logger in the static initialiser.
                // All other initialisation should go in the InitialiseHardware method.
                tl = new TraceLogger("", "APS.Hardware");

                // DriverProgId has to be set here because it used by ReadProfile to get the TraceState flag.
                DriverProgId = Dome.DriverProgId; // Get this device's ProgID so that it can be used to read the Profile configuration values

                // ReadProfile has to go here before anything is written to the log because it loads the TraceLogger enable / disable state.
                ReadProfile(); // Read device configuration from the ASCOM Profile store, including the trace state

                LogMessage("DomeHardware", $"Static initialiser completed.");
            }
            catch (Exception ex)
            {
                try { LogMessage("DomeHardware", $"Initialisation exception: {ex}"); } catch { }
                MessageBox.Show($"{ex.Message}", "Exception creating ASCOM.APS.Dome", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// Place device initialisation code here that delivers the selected ASCOM <see cref="Devices."/>
        /// </summary>
        /// <remarks>Called every time a new instance of the driver is created.</remarks>
        internal static void InitialiseHardware()
        {
            // This method will be called every time a new ASCOM client loads your driver
            LogMessage("InitialiseHardware", $"Start.");

            // Make sure that "one off" activities are only undertaken once
            if (runOnce == false)
            {
                LogMessage("InitialiseHardware", $"Starting one-off initialisation.");

                DriverDescription = Dome.DriverDescription; // Get this device's Chooser description

                LogMessage("InitialiseHardware", $"ProgID: {DriverProgId}, Description: {DriverDescription}");

                connectedState = false; // Initialise connected to false
                utilities = new Util(); //Initialise ASCOM Utilities object
                astroUtilities = new AstroUtils(); // Initialise ASCOM Astronomy Utilities object

                LogMessage("InitialiseHardware", "Completed basic initialisation");

                // Add your own "one off" device initialisation here e.g. validating existence of hardware and setting up communications

                LogMessage("InitialiseHardware", $"One-off initialisation complete.");
                runOnce = true; // Set the flag to ensure that this code is not run again
            }
        }

        // PUBLIC COM INTERFACE IDomeV2 IMPLEMENTATION

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialogue form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public static void SetupDialog()
        {
            // Don't permit the setup dialogue if already connected
            if (IsConnected)
                MessageBox.Show("Already connected, just press OK");

            using (SetupDialogForm F = new SetupDialogForm(tl))
            {
                var result = F.ShowDialog();
                if (result == DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        /// <summary>Returns the list of custom action names supported by this driver.</summary>
        /// <value>An ArrayList of strings (SafeArray collection) containing the names of supported actions.</value>
        public static ArrayList SupportedActions
        {
            get
            {
                LogMessage("SupportedActions Get", "Returning empty ArrayList");
                return new ArrayList();
            }
        }

        /// <summary>Invokes the specified device-specific custom action.</summary>
        /// <param name="ActionName">A well known name agreed by interested parties that represents the action to be carried out.</param>
        /// <param name="ActionParameters">List of required parameters or an <see cref="String.Empty">Empty String</see> if none are required.</param>
        /// <returns>A string response. The meaning of returned strings is set by the driver author.
        /// <para>Suppose filter wheels start to appear with automatic wheel changers; new actions could be <c>QueryWheels</c> and <c>SelectWheel</c>. The former returning a formatted list
        /// of wheel names and the second taking a wheel name and making the change, returning appropriate values to indicate success or failure.</para>
        /// </returns>
        public static string Action(string actionName, string actionParameters)
        {
            LogMessage("Action", $"Action {actionName}, parameters {actionParameters} is not implemented");
            throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and does not wait for a response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        public static void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");
            // TODO The optional CommandBlind method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBlind must send the supplied command to the mount and return immediately without waiting for a response

            throw new MethodNotImplementedException($"CommandBlind - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a boolean response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the interpreted boolean response received from the device.
        /// </returns>
        public static bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");
            // TODO The optional CommandBool method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBool must send the supplied command to the mount, wait for a response and parse this to return a True or False value

            throw new MethodNotImplementedException($"CommandBool - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a string response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the string response received from the device.
        /// </returns>
        public static string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");

            string response = "";

            if (connectionMode == 0)
            {
                _ = mutex.WaitOne();

                serial.ClearBuffers();
                serial.Transmit(OP_CMD__START + command + OP_CMD__END);
                //serial.DiscardOutBuffer();
                //serial.WriteLine(command);

                response = serial.ReceiveTerminated(OP_CMD__END);   //string response = serial.ReadLine();
                mutex.ReleaseMutex();
            }
            else
            {
                string uri_s = ipAddress + "/xhr?cmd=" + command + "&apikey=" + apiKey;

                if (!uri_s.StartsWith("http://")) { uri_s = "http://" + uri_s; }
                StringContent content = new StringContent("{}", Encoding.UTF8, "application/json");

                using (HttpRequestMessage http_request = new HttpRequestMessage { Method = HttpMethod.Post, RequestUri = new Uri(uri_s), Content = content })
                {
                    using (HttpResponseMessage http_response = client.SendAsync(http_request).Result)
                    {
                        if (command == OP_CMD__GETSHUTTERSTATUS && (http_response.StatusCode == System.Net.HttpStatusCode.OK || http_response.StatusCode == System.Net.HttpStatusCode.Forbidden))
                        {
                            HttpContent http_c = http_response.Content;
                            string js = http_c.ReadAsStringAsync().Result;
                            var p = js.IndexOf("shs\":");
                            // imposta la risposta nel formato "CMD000STATO" perchè possa essere 
                            response = command + OP_STATUS__OK.ToString("D3") + js.Substring(p + 5, 1);
                        }
                    }
                }
            }

            return response;
        }

        /// <summary>
        /// Deterministically release both managed and unmanaged resources that are used by this class.
        /// </summary>
        /// <remarks>
        /// TODO: Release any managed or unmanaged resources that are used in this class.
        /// 
        /// Do not call this method from the Dispose method in your driver class.
        ///
        /// This is because this hardware class is decorated with the <see cref="HardwareClassAttribute"/> attribute and this Dispose() method will be called 
        /// automatically by the  local server executable when it is irretrievably shutting down. This gives you the opportunity to release managed and unmanaged 
        /// resources in a timely fashion and avoid any time delay between local server close down and garbage collection by the .NET runtime.
        ///
        /// For the same reason, do not call the SharedResources.Dispose() method from this method. Any resources used in the static shared resources class
        /// itself should be released in the SharedResources.Dispose() method as usual. The SharedResources.Dispose() method will be called automatically 
        /// by the local server just before it shuts down.
        /// 
        /// </remarks>
        public static void Dispose()
        {
            try { LogMessage("Dispose", $"Disposing of assets and closing down."); } catch { }

            try
            {
                // Clean up the trace logger and utility objects
                tl.Enabled = false;
                tl.Dispose();
                tl = null;
            }
            catch { }

            try
            {
                utilities.Dispose();
                utilities = null;
            }
            catch { }

            try
            {
                astroUtilities.Dispose();
                astroUtilities = null;
            }
            catch { }
        }

        /// <summary>
        /// Set True to connect to the device hardware. Set False to disconnect from the device hardware.
        /// You can also read the property to check whether it is connected. This reports the current hardware state.
        /// </summary>
        /// <value><c>true</c> if connected to the hardware; otherwise, <c>false</c>.</value>
        public static bool Connected
        {
            get
            {
                LogMessage("Connected", $"Get {IsConnected}");
                return IsConnected;
            }
            set
            {
                LogMessage("Connected", $"Set {value}");
                if (value == IsConnected)
                    return;

                if (connectionMode == 0)
                    Connected_Serial(value);
                else
                    Connected_Network(value);
            }
        }

        /// <summary>
        /// Returns a description of the device, such as manufacturer and model number. Any ASCII characters may be used.
        /// </summary>
        /// <value>The description.</value>
        public static string Description
        {
            get
            {
                LogMessage("Description Get", DriverDescription);
                return DriverDescription;
            }
        }

        /// <summary>
        /// Descriptive and version information about this ASCOM driver.
        /// </summary>
        public static string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverInfo = $"ASCOM APS Dome Driver. Version: {version.Major}.{version.Minor}";
                LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        /// <summary>
        /// A string containing only the major and minor version of the driver formatted as 'm.n'.
        /// </summary>
        public static string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = $"{version.Major}.{version.Minor}";
                LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        /// <summary>
        /// The interface version number that this device supports.
        /// </summary>
        public static short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                LogMessage("InterfaceVersion Get", "2");
                return Convert.ToInt16("2");
            }
        }

        /// <summary>
        /// The short name of the driver, for display purposes
        /// </summary>
        public static string Name
        {
            get
            {
                string name = "APS Dome";
                LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region IDome Implementation

        //private static bool domeShutterState = false; // Variable to hold the open/closed status of the shutter, true = Open

        private static ShutterState shutterState = ShutterState.shutterClosed;

        /// <summary>
        /// Immediately stops any and all movement of the dome.
        /// </summary>
        internal static void AbortSlew()
        {
            try
            {
                string res = CommandString(OP_CMD__ABORTSLEW, false);
                shutterState = ShutterState.shutterError;
                LogMessage("AbortSlew", "aborting slew");
            }
            catch (Exception ex)
            {
                LogMessage("AbortSlew", "Error abort slew: " + ex.Message);
            }
        }

        /// <summary>
        /// The altitude (degrees, horizon zero and increasing positive to 90 zenith) of the part of the sky that the observer wishes to observe.
        /// </summary>
        internal static double Altitude
        {
            get
            {
                LogMessage("Altitude Get", "Not implemented");
                throw new PropertyNotImplementedException("Altitude", false);
            }
        }

        /// <summary>
        /// <para><see langword="true" /> when the dome is in the home position. Raises an error if not supported.</para>
        /// <para>
        /// This is normally used following a <see cref="FindHome" /> operation. The value is reset
        /// with any azimuth slew operation that moves the dome away from the home position.
        /// </para>
        /// <para>
        /// <see cref="AtHome" /> may optionally also become true during normal slew operations, if the
        /// dome passes through the home position and the dome controller hardware is capable of
        /// detecting that; or at the end of a slew operation if the dome comes to rest at the home
        /// position.
        /// </para>
        /// </summary>
        internal static bool AtHome
        {
            get
            {
                LogMessage("AtHome Get", "Not implemented");
                throw new PropertyNotImplementedException("AtHome", false);
            }
        }

        /// <summary>
        /// <see langword="true" /> if the dome is in the programmed park position.
        /// </summary>
        internal static bool AtPark
        {
            get
            {
                LogMessage("AtPark Get", "Not implemented");
                throw new PropertyNotImplementedException("AtPark", false);
            }
        }

        /// <summary>
        /// The dome azimuth (degrees, North zero and increasing clockwise, i.e., 90 East, 180 South, 270 West). North is true north and not magnetic north.
        /// </summary>
        internal static double Azimuth
        {
            get
            {
                LogMessage("Azimuth Get", "Not implemented");
                throw new PropertyNotImplementedException("Azimuth", false);
            }
        }

        /// <summary>
        /// <see langword="true" /> if driver can perform a search for home position.
        /// </summary>
        internal static bool CanFindHome
        {
            get
            {
                LogMessage("CanFindHome Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// <see langword="true" /> if the driver is capable of parking the dome.
        /// </summary>
        internal static bool CanPark
        {
            get
            {
                LogMessage("CanPark Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// <see langword="true" /> if driver is capable of setting dome altitude.
        /// </summary>
        internal static bool CanSetAltitude
        {
            get
            {
                LogMessage("CanSetAltitude Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// <see langword="true" /> if driver is capable of rotating the dome. Muste be <see "langword="false" /> for a 
        /// roll-off roof or clamshell.
        /// </summary>
        internal static bool CanSetAzimuth
        {
            get
            {
                LogMessage("CanSetAzimuth Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// <see langword="true" /> if the driver can set the dome park position.
        /// </summary>
        internal static bool CanSetPark
        {
            get
            {
                LogMessage("CanSetPark Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// <see langword="true" /> if the driver is capable of opening and closing the shutter or roof
        /// mechanism.
        /// </summary>
        internal static bool CanSetShutter
        {
            get
            {
                LogMessage("CanSetShutter Get", true.ToString());
                return true;
            }
        }

        /// <summary>
        /// <see langword="true" /> if the dome hardware supports slaving to a telescope.
        /// </summary>
        internal static bool CanSlave
        {
            get
            {
                LogMessage("CanSlave Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// <see langword="true" /> if the driver is capable of synchronizing the dome azimuth position
        /// using the <see cref="SyncToAzimuth" /> method.
        /// </summary>
        internal static bool CanSyncAzimuth
        {
            get
            {
                LogMessage("CanSyncAzimuth Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// Close the shutter or otherwise shield the telescope from the sky.
        /// </summary>
        internal static void CloseShutter()
        {
            try
            {
                string res = CommandString(OP_CMD__CLOSESHUTTER, false);
                shutterState = ShutterState.shutterClosing;
                LogMessage("CloseShutter", "Shutter closing");
            }
            catch (Exception ex)
            {
                LogMessage("CloseShutter", "Error closing shutter: " + ex.Message);
            }
        }

        /// <summary>
        /// Start operation to search for the dome home position.
        /// </summary>
        internal static void FindHome()
        {
            LogMessage("FindHome", "Not implemented");
            throw new MethodNotImplementedException("FindHome");
        }

        /// <summary>
        /// Open shutter or otherwise expose telescope to the sky.
        /// </summary>
        internal static void OpenShutter()
        {
            try
            {
                string res = CommandString(OP_CMD__OPENSHUTTER, false);
                shutterState = ShutterState.shutterOpening;
                LogMessage("OpenShutter", "Shutter opening");
            }
            catch (Exception ex)
            {
                LogMessage("OpenShutter", "Error opening shutter: " + ex.Message);
            }
        }

        /// <summary>
        /// Rotate dome in azimuth to park position.
        /// </summary>
        internal static void Park()
        {
            LogMessage("Park", "Not implemented");
            throw new MethodNotImplementedException("Park");
        }

        /// <summary>
        /// Set the current azimuth position of dome to the park position.
        /// </summary>
        internal static void SetPark()
        {
            LogMessage("SetPark", "Not implemented");
            throw new MethodNotImplementedException("SetPark");
        }

        /// <summary>
        /// Gets the status of the dome shutter or roof structure.
        /// </summary>
        internal static ShutterState ShutterStatus
        {
            get
            {
                LogMessage("ShutterStatus Get", shutterState.ToString());

                return shutterState;

                //LogMessage("ShutterStatus Get", false.ToString());
                //if (domeShutterState)
                //{
                //    LogMessage("ShutterStatus", ShutterState.shutterOpen.ToString());
                //    return ShutterState.shutterOpen;
                //}
                //else
                //{
                //    LogMessage("ShutterStatus", ShutterState.shutterClosed.ToString());
                //    return ShutterState.shutterClosed;
                //}
            }
        }

        /// <summary>
        /// <see langword="true"/> if the dome is slaved to the telescope in its hardware, else <see langword="false"/>.
        /// </summary>
        internal static bool Slaved
        {
            get
            {
                LogMessage("Slaved Get", false.ToString());
                return false;
            }
            set
            {
                LogMessage("Slaved Set", "not implemented");
                throw new PropertyNotImplementedException("Slaved", true);
            }
        }

        /// <summary>
        /// Ensure that the requested viewing altitude is available for observing.
        /// </summary>
        /// <param name="Altitude">
        /// The desired viewing altitude (degrees, horizon zero and increasing positive to 90 degrees at the zenith)
        /// </param>
        internal static void SlewToAltitude(double Altitude)
        {
            LogMessage("SlewToAltitude", "Not implemented");
            throw new MethodNotImplementedException("SlewToAltitude");
        }

        /// <summary>
        /// Ensure that the requested viewing azimuth is available for observing.
        /// The method should not block and the slew operation should complete asynchronously.
        /// </summary>
        /// <param name="Azimuth">
        /// Desired viewing azimuth (degrees, North zero and increasing clockwise. i.e., 90 East,
        /// 180 South, 270 West)
        /// </param>
        internal static void SlewToAzimuth(double Azimuth)
        {
            LogMessage("SlewToAzimuth", "Not implemented");
            throw new MethodNotImplementedException("SlewToAzimuth");
        }

        /// <summary>
        /// <see langword="true" /> if any part of the dome is currently moving or a move command has been issued, 
        /// but the dome has not yet started to move. <see langword="false" /> if all dome components are stationary
        /// and no move command has been issued. /> 
        /// </summary>
        internal static bool Slewing
        {
            get
            {
                LogMessage("Slewing Get", false.ToString());
                return false;
            }
        }

        /// <summary>
        /// Synchronize the current position of the dome to the given azimuth.
        /// </summary>
        /// <param name="Azimuth">
        /// Target azimuth (degrees, North zero and increasing clockwise. i.e., 90 East,
        /// 180 South, 270 West)
        /// </param>
        internal static void SyncToAzimuth(double Azimuth)
        {
            LogMessage("SyncToAzimuth", "Not implemented");
            throw new MethodNotImplementedException("SyncToAzimuth");
        }

        #endregion

        #region Private properties and methods
        // Useful methods that can be used as required to help with driver development

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private static bool IsConnected
        {
            get
            {
                // TODO check that the driver hardware connection exists and is connected to the hardware
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private static void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal static void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Dome";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(DriverProgId, traceStateProfileName, string.Empty, traceStateDefault));
                comPort = driverProfile.GetValue(DriverProgId, comPortProfileName, string.Empty, comPortDefault);
                ipAddress = driverProfile.GetValue(DriverProgId, ipAddressProfileName, string.Empty, ipAddressDefault);
                apiKey = driverProfile.GetValue(DriverProgId, apiKeyProfileName, string.Empty, apiKeyDefault);
                connectionMode = Convert.ToInt32(driverProfile.GetValue(DriverProgId, connectionModeProfileName, string.Empty, connectionModeDefault.ToString()));
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal static void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Dome";
                driverProfile.WriteValue(DriverProgId, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(DriverProgId, comPortProfileName, comPort);
                driverProfile.WriteValue(DriverProgId, ipAddressProfileName, ipAddress);
                driverProfile.WriteValue(DriverProgId, apiKeyProfileName, apiKey);
                driverProfile.WriteValue(DriverProgId, connectionModeProfileName, connectionMode.ToString());
            }
        }

        /// <summary>
        /// Log helper function that takes identifier and message strings
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        internal static void LogMessage(string identifier, string message)
        {
            tl.LogMessageCrLf(identifier, message);
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal static void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            LogMessage(identifier, msg);
        }
        #endregion

        #region Driver Methods
        internal static void Connected_Serial(bool value)
        {
            if (value)
            {
                LogMessage("Connected Set", $"Connecting to port {comPort}");

                try
                {
                    serial = new Serial
                    {
                        PortName = comPort,
                        Speed = SerialSpeed.ps9600, // GetBaudRate(),
                        DTREnable = false,
                        Connected = true,
                        ReceiveTimeout = 5
                    };
                    serial.ClearBuffers();

                    //serial = new SerialPort
                    //{
                    //    PortName = comPort,
                    //    BaudRate = 9600,
                    //    DtrEnable = false
                    //};
                    //serial.Open();
                    //serial.DataReceived += Serial_DataReceived;

                    connectedState = true;

                    mutex = new Mutex();

                    StartThread();
                }
                catch (ArgumentException ex)
                {
                    connectedState = false;
                    LogMessage("Connected Unset", "PortIsNotSelected");
                    throw new ASCOM.NotConnectedException("PortIsNotSelected", ex);
                }
            }
            else
            {
                LogMessage("Connected Unset", "DisconnectingFromPort" + " {0}", comPort);

                AbortThread();

                mutex.Close();
                mutex.Dispose();
                mutex = null;
                if (serial != null)
                {
                    // disconnect from the device
                    //try { serial.Close(); }
                    try { serial.Connected = false; }   // If device is disconnected, it gives a Port Closed Exception
                    catch (Exception ex) { throw new ASCOM.NotConnectedException("CouldNotDisconnectFromPort" + " " + comPort, ex); }
                    serial.Dispose();
                    serial = null;
                }

                connectedState = false;
            }
        }

        internal static void Connected_Network(bool value)
        {
            if (value)
            {
                connectedState = true;
                LogMessage("Connected Set", "Connessione a {0}", ipAddress);

                try
                {
                    client = new HttpClient();
                    string uri_s = ipAddress + "/xhr?cmd=" + OP_CMD__GETSHUTTERSTATUS + "&apikey=" + apiKey;

                    if (!uri_s.StartsWith("http://")) { uri_s = "http://" + uri_s; }
                    if (!Uri.TryCreate(uri_s, UriKind.Absolute, out Uri uri)) { throw new Exception("Inserire un indirizzo IP valido"); }

                    StringContent content = new StringContent("{}", Encoding.UTF8, "application/json");

                    using (HttpRequestMessage request = new HttpRequestMessage { Method = HttpMethod.Post, RequestUri = uri, Content = content })
                    {
                        using (HttpResponseMessage response = client.SendAsync(request).Result)
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                throw new Exception("Errore di connessione all'indirizzo " + ipAddress + " (" + response.StatusCode + ")");
                            }
                        }
                    }

                    connectedState = true;

                    StartThread();
                }
                catch (Exception ex)
                {
                    client = null;
                    connectedState = false;
                    string exMsg = ex.Message;
                    if (ex.HResult == -2146233088) { try { exMsg = ex.InnerException.InnerException.InnerException.Message; } catch { } }
                    LogMessage("Connected Unset", exMsg);
                    throw new ASCOM.NotConnectedException(exMsg, ex);
                }
            }
            else
            {
                client = null;
                connectedState = false;
                LogMessage("Connected Set", "Disconnessione da {0}", ipAddress);

                AbortThread();
            }
        }

        internal static void StartThread()
        {
            GetShutterState();

            thread = new Thread(MainThread) { IsBackground = true };
            thread.Start();
        }

        internal static void AbortThread()
        {
            if (thread != null)
            {
                thread.Abort();
                thread.Join();
                thread = null;
                while (thread != null) { Thread.Sleep(100); }
            }
        }

        internal static void GetShutterState()
        {
            try
            {
                string res = CommandString(OP_CMD__GETSHUTTERSTATUS, false);
                res = SerialCommandResponseParse(res);
                if (res.Substring(0, 1) == OP_CMD__GETSHUTTERSTATUS.ToString())
                {
                    int i = Convert.ToInt32(res.Substring(4));
                    shutterState = (ShutterState)i;
                }
            }
            catch (Exception ex)
            {
                LogMessage("GetShutterState", "Error: " + ex.Message);
                shutterState = ShutterState.shutterError;
            }
        }

        private static string SerialCommandResponseParse(string response)
        {
            return response.Replace(OP_CMD__START, "").Replace(OP_CMD__END, "");
        }

        private static void MainThread()
        {
            //bool isRunning = true;
            //Stopwatch stopwatch = Stopwatch.StartNew();
            //int timeoutMilliseconds = 60000;

            while (IsConnected) //(isRunning && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (connectionMode == 0)
                {
                    try
                    {
                        string res = serial.ReceiveTerminated(OP_CMD__END); // CommandString("S", false);
                                                                            //string s = serial.ReadLine();
                        res = SerialCommandResponseParse(res);
                        if (res.Substring(0, 1) == OP_CMD__GETSHUTTERSTATUS.ToString())
                        {
                            int i = Convert.ToInt32(res.Substring(4));
                            shutterState = (ShutterState)i;
                        }
                    }
                    catch { }

                    Thread.Sleep(500);
                }
                else
                {
                    GetShutterState();

                    Thread.Sleep(2000);
                }
            }
        }
        #endregion
    }
}


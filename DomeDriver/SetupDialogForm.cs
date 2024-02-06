using ASCOM.Utilities;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ASCOM.APS.Dome
{
    [ComVisible(false)] // Form not registered for COM!
    public partial class SetupDialogForm : Form
    {
        const string NO_PORTS_MESSAGE = "Nessuna porta COM disponibile";
        TraceLogger tl; // Holder for a reference to the driver's trace logger

        bool closeForm = true;
        public SetupDialogForm(TraceLogger tlDriver)
        {
            InitializeComponent();

            // Save the provided trace logger for use within the setup dialogue
            tl = tlDriver;

            // Initialise current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void CmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            tl.Enabled = chkTrace.Checked;

            DomeHardware.connectionMode = tabCtrl1.SelectedIndex;

            if (DomeHardware.connectionMode == 0)
            {
                if (comboBoxComPort.Items.Count == 0)
                {
                    comboBoxComPort.SelectedItem = null;
                    _ = MessageBox.Show("Nessuna porta disponibile", "Errore porta COM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    tl.LogMessage("Setup OK", $"New configuration values - NO COM ports detected on this machine");
                    closeForm = false;
                }
                else if (string.IsNullOrEmpty((string)comboBoxComPort.SelectedItem))
                {
                    _ = MessageBox.Show("Nessuna porta selezionata", "Errore porta COM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    tl.LogMessage("Setup OK", $"New configuration values - COM Port: Not selected");
                    closeForm = false;
                }
                else
                {
                    DomeHardware.comPort = (string)comboBoxComPort.SelectedItem;
                    tl.LogMessage("Setup OK", $"New configuration values - COM Port: {DomeHardware.comPort}");
                    closeForm = true;
                }
            }
            else
            {
                string uri_s = txtIP.Text;

                if (!uri_s.StartsWith("http://"))
                {
                    uri_s = "http://" + uri_s;
                }

                if (!Uri.TryCreate(uri_s, UriKind.Absolute, out _))
                {
                    _ = MessageBox.Show("Inserire un indirizzo IP valido", "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    tl.LogMessage("Setup OK", $"New configuration values - IP address: not valid");
                    closeForm = false;
                }
                else
                {
                    DomeHardware.ipAddress = txtIP.Text;
                    DomeHardware.apiKey = txtApiKey.Text;
                    tl.LogMessage("Setup OK", $"New configuration values - IP address: {DomeHardware.ipAddress}");
                    closeForm = true;
                }
            }
        }

        private void CmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            closeForm = true;
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("https://ascom-standards.org/");
            }
            catch (Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI()
        {

            // Set the trace checkbox
            chkTrace.Checked = tl.Enabled;

            SetComPorts();

            // select the current port if possible
            if (comboBoxComPort.Items.Contains(DomeHardware.comPort))
            {
                comboBoxComPort.SelectedItem = DomeHardware.comPort;
            }

            txtIP.Text = DomeHardware.ipAddress;
            txtApiKey.Text = DomeHardware.apiKey;
            tabCtrl1.SelectedIndex = DomeHardware.connectionMode;

            tl.LogMessage("InitUI", $"Set UI controls to Trace: {chkTrace.Checked}, COM Port: {comboBoxComPort.SelectedItem}, IP Address: {txtIP.Text}");
        }

        private void SetupDialogForm_Load(object sender, EventArgs e)
        {
            // Bring the setup dialogue to the front of the screen
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            else
            {
                TopMost = true;
                Focus();
                BringToFront();
                TopMost = false;
            }
        }

        private void SetupDialogForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !closeForm;
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            SetComPorts();
        }

        private void SetComPorts()
        {
            // set the list of COM ports to those that are currently available
            comboBoxComPort.Items.Clear(); // Clear any existing entries
            using (Serial serial = new Serial()) // User the Se5rial component to get an extended list of COM ports
            {
                comboBoxComPort.Items.AddRange(serial.AvailableCOMPorts);
            }
            comboBoxComPort.SelectedIndex = 0;
        }
    }
}
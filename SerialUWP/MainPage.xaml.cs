using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SerialUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SerialDevice serialPort = null;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;
        private ObservableCollection<DeviceInformation> listOfDevices;
        //List<string> listPorts = new List<string>();
        private CancellationTokenSource ReadCancellationTokenSource;

        public MainPage()
        {
            this.InitializeComponent();
            btnSend.IsEnabled = false;
            tbSend.IsEnabled = false;
            listOfDevices = new ObservableCollection<DeviceInformation>();
            list_Ports();
        }

        private async void list_Ports()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }
                //Debug.WriteLine(listOfDevices);
                //cbPort.ItemsSource = listPorts;
                cbPort.ItemsSource = listOfDevices;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }



        private void tbLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(tbLog, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null)
            {
                dataWriteObject = new DataWriter(serialPort.OutputStream);
                await writeSerial(tbSend.Text);
            }
            if (dataWriteObject != null)
            {
                dataWriteObject.DetachStream();
                dataWriteObject = null;
            }
        }

        private async Task writeSerial(string data)
        {
            var bufferWrite = data;
            if (data.Contains("\r\n"))
            {
                bufferWrite = data;
            }
            else
            {
                bufferWrite = data + Environment.NewLine;
            }
            Task<UInt32> storeAsyncTask;
            if (bufferWrite.Length != 0)
            {
                dataWriteObject.WriteString(bufferWrite);
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();
                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    updateLog(data);
                }
            }
        }

        private async void serialConfig()
        {
            var selection = cbPort.SelectedItem;

            if (cbPort.SelectedIndex == -1)
            {
                displayDialog("Device Connection", "Please, Select Port");
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection;
            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(50);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(50);
                serialPort.BaudRate = Convert.ToUInt32(cbBaud.SelectedItem);
                serialPort.Parity = (SerialParity)Convert.ToUInt16(cbParity.SelectedIndex);  //SerialParity.None;
                serialPort.StopBits = (SerialStopBitCount)Convert.ToUInt16(cbStopBit.SelectedIndex); //SerialStopBitCount.One;
                serialPort.DataBits = Convert.ToUInt16(cbDataSize.SelectedItem);
                serialPort.Handshake = (SerialHandshake)Convert.ToUInt16(cbHandshake.SelectedIndex); // SerialHandshake.None;
                ReadCancellationTokenSource = new CancellationTokenSource();

                if (serialPort != null)
                {
                    //displayDialog("Device Connection", "Port Connected");
                    updateLog(string.Format("Serial Port {0} Opened", entry.Name));
                    btnSend.IsEnabled = true;
                    tbSend.IsEnabled = true;
                    btnConnect.Content = "Disconnect";

                    var port = serialPort.PortName;
                    Debug.WriteLine(port.ToString());
                }

                listen();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (btnConnect.Content.Equals("Connect"))
            {
                serialConfig();
            }
            else if (btnConnect.Content.Equals("Disconnect"))
            {
                closeDevice();
            }
        }

        private async void displayDialog(string header, string text)
        {
            ContentDialog dispDialog = new ContentDialog()
            {
                Title = header,
                Content = text,
                CloseButtonText = "OK"
            };

            await dispDialog.ShowAsync();
        }

        private async void listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);
                    while (true)
                    {
                        await readData(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    closeDevice();
                }
            }
            finally
            {
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        private async Task readData(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;
            uint ReadBufferLength = 4096;
            cancellationToken.ThrowIfCancellationRequested();
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);
            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                string tempRead = dataReaderObject.ReadString(bytesRead);
                updateLog(tempRead);
            }
        }

        private void cancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        private void updateLog(string info)
        {
            if (info.Contains("\r\n"))
            {
                tbLog.Text += info;
                Debug.Write(info);
            }
            else
            {
                tbLog.Text += info + Environment.NewLine; // + "\r\n";
                Debug.WriteLine(info);
            }
        }

        private void closeDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
                //displayDialog("Device Connection", "Port Disconnected");
                var selection = (DeviceInformation)cbPort.SelectedItem;
                updateLog(string.Format("Serial Port {0} Closed", selection.Name));
                btnSend.IsEnabled = false;
                tbSend.IsEnabled = false;
                btnConnect.Content = "Connect";
            }
            serialPort = null;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            cbPort.SelectedIndex = -1;
            listOfDevices.Clear();
            cbPort.ItemsSource = listOfDevices;
            list_Ports();
        }
    }

}

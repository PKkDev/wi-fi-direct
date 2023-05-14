using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Devices.WiFiDirect;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace WiFiDirect.Hub
{
    public sealed partial class MainWindow : Window
    {
        WiFiDirectAdvertisementPublisher _publisher;
        WiFiDirectConnectionListener _listener;

        List<WiFiDirectInformationElement> _informationElements = new();

        public ObservableCollection<ConnectedDevice> ConnectedDevices = new ObservableCollection<ConnectedDevice>();

        public MainWindow()
        {
            InitializeComponent();

            _informationElements = new();
            AddWiFiDirectInformationElement("tetElement");
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            _listener = new WiFiDirectConnectionListener();

            try
            {
                // This can raise an exception if the machine does not support WiFi. Sorry.
                _listener.ConnectionRequested += OnConnectionRequested; ;
            }
            catch (Exception ex)
            {
            }

            _publisher = new WiFiDirectAdvertisementPublisher();
            _publisher.StatusChanged += PublisherStatusChanged;

            #region Legacy settings HostPot

            _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;

            // Legacy settings are meaningful only if IsAutonomousGroupOwnerEnabled is true.
            if (_publisher.Advertisement.IsAutonomousGroupOwnerEnabled)
            {
                _publisher.Advertisement.LegacySettings.IsEnabled = true;

                _publisher.Advertisement.LegacySettings.Ssid = "mytestssid";

                PasswordCredential lCred = new();
                lCred.Password = "12345678";
                _publisher.Advertisement.LegacySettings.Passphrase = lCred;
            }

            #endregion Legacy settings HostPot

            // Add the information elements.
            foreach (var informationElement in _informationElements)
            {
                _publisher.Advertisement.InformationElements.Add(informationElement);
            }

            _publisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;

            _publisher.Start();

            if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                // Advertisement started.
            }
            else
            {
                // Advertisement failed to start. Status is {_publisher.Status}
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _publisher.Stop();
            _publisher.StatusChanged -= PublisherStatusChanged;

            _listener.ConnectionRequested -= OnConnectionRequested;
        }

        private void OnConnectionRequested(
            WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs args)
        {
            WiFiDirectConnectionRequest connectionRequest = args.GetConnectionRequest();

            DispatcherQueue.TryEnqueue(async () =>
            {
                var success = await HandleConnectionRequestAsync(connectionRequest);

                if (!success)
                {
                    // Decline the connection request
                    connectionRequest.Dispose();
                }
            });
        }

        private async Task<bool> HandleConnectionRequestAsync(WiFiDirectConnectionRequest connectionRequest)
        {
            string deviceName = connectionRequest.DeviceInformation.Name;

            return true;
        }


        private void PublisherStatusChanged(
            WiFiDirectAdvertisementPublisher sender, WiFiDirectAdvertisementPublisherStatusChangedEventArgs args)
        {
            switch (args.Status)
            {
                case WiFiDirectAdvertisementPublisherStatus.Started:
                    {
                        break;
                    }
                case WiFiDirectAdvertisementPublisherStatus.Created:
                    {
                        break;
                    }
                case WiFiDirectAdvertisementPublisherStatus.Stopped:
                    {
                        break;
                    }
                case WiFiDirectAdvertisementPublisherStatus.Aborted:
                    {
                        break;
                    }
            }
        }

        // WARNING! This custom OUI is for demonstration purposes only.
        // OUI values are assigned by the IEEE Registration Authority.
        // Replace this custom OUI with the value assigned to your organization.
        private readonly byte[] CustomOui = { 0xAA, 0xBB, 0xCC };
        private readonly byte CustomOuiType = 0xDD;
        private void AddWiFiDirectInformationElement(string txtInformationElement)
        {
            WiFiDirectInformationElement informationElement = new WiFiDirectInformationElement();

            // Information element blob
            DataWriter dataWriter = new DataWriter();
            dataWriter.UnicodeEncoding = UnicodeEncoding.Utf8;
            dataWriter.ByteOrder = ByteOrder.LittleEndian;
            dataWriter.WriteUInt32(dataWriter.MeasureString(txtInformationElement));
            dataWriter.WriteString(txtInformationElement);
            informationElement.Value = dataWriter.DetachBuffer();

            // Organizational unit identifier (OUI)
            informationElement.Oui = CryptographicBuffer.CreateFromByteArray(CustomOui);

            // OUI Type
            informationElement.OuiType = CustomOuiType;

            // Save this information element so we can add it when we advertise.
            _informationElements.Add(informationElement);
        }

        private async void SendMessageBtn_Click(object sender, RoutedEventArgs e)
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice == null) return;
            //await connectedDevice.SocketRW.WriteMessageAsync(txtSendMessage.Text);
        }

        private void CloseDeviceBtn_Click(object sender, RoutedEventArgs e)
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice == null) return;

            ConnectedDevices.Remove(connectedDevice);

            // Close socket and WiFiDirect object
            connectedDevice.Dispose();
        }
    }
}

using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using Windows.Devices.WiFiDirect;
using Windows.Security.Credentials;

namespace WiFiDirect.Hub
{
    public sealed partial class MainWindow : Window
    {
        WiFiDirectAdvertisementPublisher _publisher;
        WiFiDirectConnectionListener _listener;

        public MainWindow()
        {
            InitializeComponent();
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
            _publisher.StatusChanged += publisherStatusChanged;

            _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;
            _publisher.Advertisement.LegacySettings.IsEnabled = true;
            _publisher.Advertisement.LegacySettings.Ssid = "mytestssid";

            PasswordCredential lCred = new PasswordCredential();
            lCred.Password = "12345678";
            _publisher.Advertisement.LegacySettings.Passphrase = lCred;

            _publisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;

            _publisher.Start();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_publisher != null && _publisher.Status != WiFiDirectAdvertisementPublisherStatus.Stopped)
            {
                _publisher.Stop();
            }
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


        private void publisherStatusChanged(
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
    }
}

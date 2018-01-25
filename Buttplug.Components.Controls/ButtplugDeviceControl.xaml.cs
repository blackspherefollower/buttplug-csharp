using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Buttplug.Core;
using Buttplug.Core.Messages;
using Buttplug.Server;

namespace Buttplug.Components.Controls
{
    /// <summary>
    /// Interaction logic for ButtplugDeviceControl.xaml
    /// </summary>
    public partial class ButtplugDeviceControl
    {
        private class DeviceListItem
        {
            public readonly ButtplugDeviceInfo Info;

            public bool Connected;

            public DeviceListItem(ButtplugDeviceInfo aInfo)
            {
                Info = aInfo;
                Connected = true;
            }

            public override string ToString()
            {
                return $"{Info}" + (Connected ? string.Empty : " (disconnected)");
            }
        }

        private class DeviceList : ObservableCollection<DeviceListItem>
        {
            public void UpdateList()
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public event EventHandler<List<ButtplugDeviceInfo>> DeviceSelectionChanged;

        public event EventHandler StartScanning;

        public event EventHandler StopScanning;

        private readonly DeviceList _devices;

        private ButtplugServer _bpServer;

        public ButtplugDeviceControl()
        {
            InitializeComponent();
            _devices = new DeviceList();
            InitializeComponent();
            DeviceListBox.ItemsSource = _devices;
            DeviceListBox.SelectionMode = SelectionMode.Multiple;
            DeviceListBox.SelectionChanged += SelectionChangedHandler;
        }

        public void SetButtplugServer(ButtplugServer aServer)
        {
            _bpServer = aServer;
            _bpServer.MessageReceived += OnMessageReceived;
        }

        public void Reset()
        {
            _devices.Clear();
            StoppedScanning();
        }

        public void DeviceAdded(ButtplugDeviceInfo aDev)
        {
            var devAdd = _devices.Where(dl => dl.Info.Index == aDev.Index).ToList();

            if (devAdd.Any())
            {
                foreach (var dr in devAdd)
                {
                    dr.Connected = true;
                    _devices.UpdateList();
                }
            }
            else
            {
                _devices.Add(new DeviceListItem(aDev));
            }
        }

        public void DeviceRemoved(uint aIndex)
        {
            var devRem = _devices.Where(dl => dl.Info.Index == aIndex).ToList();
            foreach (var dr in devRem)
            {
                dr.Connected = false;
                _devices.UpdateList();
            }
        }

        private void OnMessageReceived(object aObj, MessageReceivedEventArgs aEvent)
        {
            Dispatcher.InvokeAsync(() =>
            {
                switch (aEvent.Message)
                {
                    case DeviceAdded m:
                        DeviceAdded(new ButtplugDeviceInfo(m.DeviceIndex, m.DeviceName, m.DeviceMessages));
                        break;

                    case DeviceRemoved d:
                        DeviceRemoved(d.DeviceIndex);
                        break;
                }
            });
        }

        private void SelectionChangedHandler(object aObj, EventArgs aEvent)
        {
            DeviceSelectionChanged?.Invoke(this,
                DeviceListBox.SelectedItems.Cast<DeviceListItem>()
                    .Where(aLi => aLi.Connected)
                    .Select(aLi => aLi.Info).ToList());
        }

        private void StoppedScanning()
        {
            ScanButton.Content = "Start Scanning";
        }

        private async void ScanButton_Click(object aSender, RoutedEventArgs aEvent)
        {
            // Disable button until we're done here
            ScanButton.Click -= ScanButton_Click;
            if ((string)ScanButton.Content == "Start Scanning")
            {
                StartScanning?.Invoke(this, new EventArgs());
                if (_bpServer != null)
                {
                    await _bpServer.SendMessage(new StartScanning());
                }

                ScanButton.Content = "Stop Scanning";
            }
            else
            {
                StopScanning?.Invoke(this, new EventArgs());
                if (_bpServer != null)
                {
                    await _bpServer?.SendMessage(new StopScanning());
                }

                ScanButton.Content = "Start Scanning";
            }

            ScanButton.Click += ScanButton_Click;
        }
    }
}

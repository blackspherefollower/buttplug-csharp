﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows;
using Buttplug.Client;
using Buttplug.Components.Controls;
using Buttplug.Core.Messages;
using static Buttplug.Client.DeviceEventArgs;

namespace Buttplug.Apps.ExampleClientGUI
{
    public partial class ExampleClientPanel
    {
        public ConcurrentDictionary<uint, ButtplugClientDevice> Devices = new ConcurrentDictionary<uint, ButtplugClientDevice>();

        private ButtplugWSClient _client;

        private ButtplugDeviceControl devControl;

        public ExampleClientPanel(ButtplugDeviceControl aDevControl)
        {
            InitializeComponent();
            devControl = aDevControl;

            devControl.StartScanning += OnStartScanning;
            devControl.StopScanning += OnStopScanning;
        }

        private async void ConnToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ConnToggleButton.IsEnabled = false;
            if (ConnToggleButton.Content as string == "Disconnect")
            {
                if (_client != null)
                {
                    await _client.Disconnect();
                    _client = null;
                    devControl.Reset();
                }

                ConnToggleButton.Content = "Connect";
                AdressTextBox.IsEnabled = true;
            }
            else
            {
                ConnToggleButton.Content = "Disconnect";
                AdressTextBox.IsEnabled = false;
                if (_client == null)
                {
                    devControl.Reset();
                    _client = new ButtplugWSClient("Example Client");

                    _client.DeviceAdded += OnDeviceChanged;
                    _client.DeviceRemoved += OnDeviceChanged;
                    _client.ErrorReceived += OnError;

                    Connect();
                }
            }

            ConnToggleButton.IsEnabled = true;
        }

        private async void Connect()
        {
            try
            {
                if (_client != null)
                {
                    await _client.Connect(new Uri(AdressTextBox.Text));
                    await _client.RequestDeviceList();

                    foreach (var dev in _client.getDevices())
                    {
                        devControl.DeviceAdded(new ButtplugDeviceInfo(dev.Index, dev.Name, dev.AllowedMessages));
                    }
                }
            }
            catch (Exception ex)
            {
                OnError(this, new ErrorEventArgs(new Error(ex.Message, Error.ErrorClass.ERROR_UNKNOWN, 0)));
            }
        }

        private void OnDeviceChanged(object sender, DeviceEventArgs e)
        {
            switch (e.Action)
            {
                case DeviceAction.ADDED:
                    devControl.DeviceAdded(new ButtplugDeviceInfo(e.Device.Index, e.Device.Name, e.Device.AllowedMessages));
                    break;

                case DeviceAction.REMOVED:
                    devControl.DeviceRemoved(e.Device.Index);
                    break;
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Dispatcher.Invoke(async () =>
            {
                MessageBox.Show(e.Message.ErrorMessage, "Error");
                if (_client.IsConnected)
                {
                    await _client.Disconnect();
                }
                _client = null;
                ConnToggleButton.Content = "Connect";
                AdressTextBox.IsEnabled = true;
            });
        }

        private void OnStartScanning(object sender, EventArgs args)
        {
            if (_client != null)
            {
                _client.StartScanning();
            }
        }

        private void OnStopScanning(object sender, EventArgs args)
        {
            if (_client != null)
            {
                _client.StopScanning();
            }
        }

        private void SendLinear_Click(object sender, RoutedEventArgs e)
        {
            if (!_client.IsConnected)
            {
                return;
            }

            foreach (var dev in Devices.Values)
            {
                if (dev.AllowedMessages.ContainsKey("FleshlightLaunchFW12Cmd"))
                {
                    _client.SendDeviceMessage(dev,
                        new FleshlightLaunchFW12Cmd(dev.Index,
                            Convert.ToUInt32(LinearSpeed.Value),
                            Convert.ToUInt32(LinearPosition.Value),
                            _client.nextMsgId));
                }
            }
        }

        private void SendVibrate_Click(object sender, RoutedEventArgs e)
        {
            if (!_client.IsConnected)
            {
                return;
            }

            foreach (var dev in Devices.Values)
            {
                if (dev.AllowedMessages.TryGetValue("VibrateCmd", out var attrs))
                {
                    attrs.TryGetValue("VibratorCount", out var vcStr);
                    try
                    {
                        uint vibratorCount = Convert.ToUInt32(vcStr);

                        for (uint i = 0; i < vibratorCount; i++)
                        {
                            _client.SendDeviceMessage(dev,
                                new VibrateCmd(dev.Index,
                                    new List<VibrateCmd.VibrateIndex>
                                    { new VibrateCmd.VibrateIndex(i, VibrateSpeed.Value) },
                                    _client.nextMsgId));
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void SendRotate_Click(object sender, RoutedEventArgs e)
        {
            if (!_client.IsConnected)
            {
                return;
            }

            foreach (var dev in Devices.Values)
            {
                if (dev.AllowedMessages.ContainsKey("VorzeA10CycloneCmd"))
                {
                    bool clockwise = RotateSpeed.Value > 0;
                    _client.SendDeviceMessage(dev,
                        new VorzeA10CycloneCmd(dev.Index,
                            Convert.ToUInt32(RotateSpeed.Value * (clockwise ? 1 : -1)),
                            clockwise,
                            _client.nextMsgId));
                }
            }
        }
    }
}
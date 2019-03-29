// <copyright file="UWPBluetoothManager.cs" company="Nonpolynomial Labs LLC">
//     Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
//     Copyright (c) Nonpolynomial Labs LLC. All rights reserved. Licensed under the BSD 3-Clause
//     license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bluegiga;
using Bluegiga.BLE.Events.GAP;
using Bluegiga.BLE.Events.System;
using Buttplug.Core.Logging;
using Buttplug.Devices.Configuration;
using JetBrains.Annotations;

namespace Buttplug.Server.Managers.BlueGigaBluetoothManager
{
    public class BlueGigaBluetoothManager : DeviceSubtypeManager
    {
        internal class AdvertisementReceivedEventArgs
        {
            public BlueGigaAdvertisement Advertisement;
            public BlueGigaContainer BgContainer;

            public AdvertisementReceivedEventArgs(BlueGigaAdvertisement aAdvertisement, BlueGigaContainer aContainer)
            {
                Advertisement = aAdvertisement;
                BgContainer = aContainer;
            }
        }
        
        public class BlueGigaServiceInfo
        {
            public Guid uuid;
            public bool discovered = false;
            public bool scanned = false;
            public ushort start;
            public ushort end;
            public Dictionary<Guid, ushort> charactistics = new Dictionary<Guid, ushort>();
        }

        internal enum BlueGigaState
        {
            ADVERTISEMENT,
            CONNECTING,
            CONNECTED,
            SCANNINGSERVICES,
            SERVICESSCANNED,
            DISCONNECTED
        }

        internal class BlueGigaAdvertisement
        {
            public string Address;
            public List<string> Names = new List<string>();
            public Dictionary<Guid, BlueGigaServiceInfo> Services = new Dictionary<Guid, BlueGigaServiceInfo>();
            public int Flags = 0;

            public byte[] RawAddress { get; internal set; }
            public byte AddressType { get; internal set; }
            public byte ConnectionHandle { get; internal set; }
            public BlueGigaState State = BlueGigaState.ADVERTISEMENT;
            public Guid? ServiceScanning = null;

            public event EventHandler<EventArgs> ConnectionComplete;

            public void FireConnectionComplete()
            {
                ConnectionComplete?.Invoke(this, new EventArgs());
            }
        }

        internal class BlueGigaContainer : IDisposable
        {
            public bool Scanning;
            public ConcurrentDictionary<string, BlueGigaAdvertisement> Advertisements = new ConcurrentDictionary<string, BlueGigaAdvertisement>();
            public ConcurrentDictionary<byte, string> Connections = new ConcurrentDictionary<byte, string>();

            private BGLib _bglib;
            private SerialPort _port;
            
            private IButtplugLog _bpLogger;

            public BlueGigaContainer(IButtplugLogManager aLogManager, SerialPort aPort)
            {
                _bpLogger = aLogManager.GetLogger(this.GetType());
                _port = aPort;
                _bglib = new BGLib();
                _bglib.BLEEventSystemBoot += handleBGBootEvent;
                _bglib.BLEEventGAPScanResponse += handleScanResponseEvent;
                _bglib.BLEEventConnectionStatus += ConnectionStatusEvent;
                _bglib.BLEEventATTClientGroupFound += ATTClientGroupFoundEvent;
                _bglib.BLEEventATTClientFindInformationFound += ATTClientFindInformationFoundEvent;
                _bglib.BLEEventATTClientProcedureCompleted += ATTClientProcedureCompletedEvent;
                _bglib.BLEEventATTClientAttributeValue += ATTClientAttributeValueEvent;

                _port.Handshake = Handshake.RequestToSend;
                _port.DataReceived += handleSerialDataReceived;
            }

            public event EventHandler<AdvertisementReceivedEventArgs> AdvertisementReceived;

            public void Start()
            {
                _port.Open();
                _bglib.SendCommand(_port, _bglib.BLECommandSystemGetInfo());
                // I wish there was a way to cleanup old connections...
                for( var i = 0; i < 10; i++)
                {
                    _bglib.SendCommand(_port, _bglib.BLECommandConnectionDisconnect((byte)i));
                }
            }

            public void Dispose()
            {
                _port.Close();
            }

            private void handleSerialDataReceived(object aSender, SerialDataReceivedEventArgs aEvent)
            {
                var port = (SerialPort)aSender;
                var inData = new byte[port.BytesToRead];
                port.Read(inData, 0, inData.Length);
                foreach (var data in inData)
                {
                    _bglib.Parse(data);
                }
            }

            private void handleBGBootEvent(object aSender, BootEventArgs aE)
            {
                // Log the bluetooth radio info, in case we need the information from the user later.
            
                _bpLogger.Debug("Bluetooth Radio Information:");

                var info =
                    $"Device='BlueGiga',major='{aE.major}',minor='{aE.minor}',patch='{aE.patch}',build='{aE.build}',ll_version='{aE.ll_version}',protocol_version='{aE.protocol_version}',hw='{aE.hw}' ";
                _bpLogger.Debug(info);
            }

            private void handleScanResponseEvent(object aSender, ScanResponseEventArgs aEvent)
            {
                //Console.WriteLine($"{ByteToHex(aEvent.sender)} sent: {ByteToHex(aEvent.data)}");
                // pull all advertised service info from ad packet

                var ad = Advertisements.GetOrAdd(ByteToHex(aEvent.sender), (aAddress) => new BlueGigaAdvertisement() {Address = aAddress, RawAddress = aEvent.sender, AddressType = aEvent.address_type});

                byte[] this_field = { };
                var bytes_left = 0;
                var field_offset = 0;
                bool changed = false;
                for (var i = 0; i < aEvent.data.Length; i++)
                {
                    if (bytes_left == 0)
                    {
                        bytes_left = aEvent.data[i];
                        this_field = new byte[aEvent.data[i]];
                        field_offset = i + 1;
                    }
                    else
                    {
                        this_field[i - field_offset] = aEvent.data[i];
                        bytes_left--;
                        if (bytes_left != 0) continue;
                        if (this_field[0] == 0x01)
                        {
                            if (ad.Flags == (ad.Flags | this_field[1]))
                            {
                                continue;
                            }

                            ad.Flags |= this_field[1];
                            changed = true;
                        }
                        else if (this_field[0] == 0x02 || this_field[0] == 0x03)
                        {
                            // partial or complete list of 16-bit UUIDs
                            var svc = ArrayToGuid(this_field.Skip(1).Take(2).ToArray());
                            if (!ad.Services.TryGetValue(svc, out var svcInfo))
                            {
                                ad.Services.Add(svc, new BlueGigaServiceInfo() {uuid = svc});
                                changed = true;
                            }
                        }
                        else if (this_field[0] == 0x04 || this_field[0] == 0x05)
                        {
                            // partial or complete list of 32-bit UUIDs
                            var svc = ArrayToGuid(this_field.Skip(1).Take(4).ToArray());
                            if (!ad.Services.TryGetValue(svc, out var svcInfo))
                            {
                                ad.Services.Add(svc, new BlueGigaServiceInfo() {uuid = svc});
                                changed = true;
                            }
                        }
                        else if (this_field[0] == 0x06 || this_field[0] == 0x07)
                        {
                            // partial or complete list of 128-bit UUIDs
                            var svc = ArrayToGuid(this_field.Skip(1).Take(16).ToArray());
                            if (!ad.Services.TryGetValue(svc, out var svcInfo))
                            {
                                ad.Services.Add(svc, new BlueGigaServiceInfo() {uuid = svc});
                                changed = true;
                            }
                        }
                        else if (this_field[0] == 0x08 || this_field[0] == 0x09)
                        {
                            // partial or complete list of 128-bit UUIDs
                            var name = System.Text.Encoding.UTF8.GetString(this_field.Skip(1).ToArray());
                            if (!ad.Names.Contains(name))
                            {
                                ad.Names.Add(name);
                                changed = true;
                            }
                        }
                    }
                }

                if (changed)
                {
                    AdvertisementReceived?.Invoke(this, new AdvertisementReceivedEventArgs(ad, this));
                }
            }

            private Guid ArrayToGuid(byte[] aBytes)
            {
                var data = "";
                if (aBytes.Length == 2)
                    data = "0000";
                data += ByteToHex(aBytes.Reverse().ToArray());
                if (data.Length == 8)
                    data += "00001000800000805F9B34FB";
                return new Guid(data);
            }

            private string ByteToHex(byte[] b)
            {
                StringBuilder hex = new StringBuilder(b.Length *2);
                foreach (var by in b)
                {
                    hex.AppendFormat("{0:x2}", by);
                }

                return hex.ToString();
            }

            internal void StartScanning()
            {
                Scanning = true;
                _bglib.SendCommand(_port, _bglib.BLECommandGAPSetScanParameters(200, 200, 1));
                _bglib.SendCommand(_port, _bglib.BLECommandGAPDiscover(1));
            }

            public void StopScanning()
            {
                _bglib.SendCommand(_port, _bglib.BLECommandGAPEndProcedure());
                Scanning = false;
            }

            public BlueGigaDevice GetConnection(BlueGigaAdvertisement target)
            {
                byte[] cmd = _bglib.BLECommandGAPConnectDirect(target.RawAddress, target.AddressType, 0x20, 0x30, 0x100, 0); // 125ms interval, 125ms window, active scanning
                _bglib.SendCommand(_port, cmd);
                target.State = BlueGigaState.CONNECTING;
                
                return new BlueGigaDevice() {Advertisment = target, Container = this};
            }

            public void ConnectionStatusEvent(object sender, Bluegiga.BLE.Events.Connection.StatusEventArgs aEvent)
            {
                String log = String.Format("ble_evt_connection_status: connection={0}, flags={1}, address=[ {2}], address_type={3}, conn_interval={4}, timeout={5}, latency={6}, bonding={7}" + Environment.NewLine,
                    aEvent.connection,
                    aEvent.flags,
                    ByteToHex(aEvent.address),
                    aEvent.address_type,
                    aEvent.conn_interval,
                    aEvent.timeout,
                    aEvent.latency,
                    aEvent.bonding
                );
                Console.Write(log);

                if ((aEvent.flags & 0x05) == 0x05)
                {
                    var ad = Advertisements.GetOrAdd(ByteToHex(aEvent.address), (aAddress) => new BlueGigaAdvertisement() {Address = aAddress, RawAddress = aEvent.address, AddressType = aEvent.address_type});
                    ad.ConnectionHandle = aEvent.connection;
                    // update state
                    ad.State = BlueGigaState.CONNECTED;
                    Connections.AddOrUpdate(ad.ConnectionHandle, ad.Address, (aB, aS) => (aS = ad.Address));

                    // connected, now perform service discovery
                    var cmd = _bglib.BLECommandATTClientReadByGroupType(aEvent.connection, 0x0001, 0xFFFF, new byte[] { 0x00, 0x28 }); // "service" UUID is 0x2800 (little-endian for UUID uint8array)
                    _bglib.SendCommand(_port, cmd);

                    // update state
                    ad.State = BlueGigaState.SCANNINGSERVICES;
                }
            }
            
            public void ATTClientGroupFoundEvent(object sender, Bluegiga.BLE.Events.ATTClient.GroupFoundEventArgs e)
            {
                String log = String.Format("ble_evt_attclient_group_found: connection={0}, start={1}, end={2}, uuid=[ {3}]" + Environment.NewLine,
                    e.connection,
                    e.start,
                    e.end,
                    ArrayToGuid(e.uuid)
                    );
                Console.Write(log);

                if (Connections.TryGetValue(e.connection, out var address) && Advertisements.TryGetValue(address, out var ad))
                {
                    if (ad.Services.TryGetValue(ArrayToGuid(e.uuid), out var data))
                    {
                        data.start = e.start;
                        data.end = e.end;
                        data.discovered = true;
                    }
                    else
                    {
                        ad.Services.Add(ArrayToGuid(e.uuid), new BlueGigaServiceInfo()
                        {
                            uuid = ArrayToGuid(e.uuid),
                            start = e.start,
                            end = e.end,
                            discovered = true
                        });
                    }
                }
                else
                {
                    Console.WriteLine("Failed to find connection data!");
                }
            }

            public void ATTClientFindInformationFoundEvent(object sender, Bluegiga.BLE.Events.ATTClient.FindInformationFoundEventArgs e)
            {
                String log = String.Format("ble_evt_attclient_find_information_found: connection={0}, chrhandle={1}, uuid=[ {2}]" + Environment.NewLine,
                    e.connection,
                    e.chrhandle,
                    ArrayToGuid(e.uuid)
                    );
                Console.Write(log);
                
                if (Connections.TryGetValue(e.connection, out var address) && Advertisements.TryGetValue(address, out var ad))
                {
                    //ad.working.SetResult(true);
                }
                else
                {
                    Console.WriteLine("Failed to find connection data!");
                }
            }

            public void ATTClientProcedureCompletedEvent(object sender, Bluegiga.BLE.Events.ATTClient.ProcedureCompletedEventArgs aEvent)
            {
                String log = String.Format("ble_evt_attclient_procedure_completed: connection={0}, result={1}, chrhandle={2}" + Environment.NewLine,
                    aEvent.connection,
                    aEvent.result,
                    aEvent.chrhandle
                    );
                Console.Write(log);
                   
                if (Connections.TryGetValue(aEvent.connection, out var address) && Advertisements.TryGetValue(address, out var ad))
                {
                    if (ad.State == BlueGigaState.SCANNINGSERVICES)
                    {
                        if (ad.ServiceScanning != null && ad.Services.TryGetValue((Guid)ad.ServiceScanning, out var osvc) )
                        {
                            osvc.scanned = true;
                            ad.ServiceScanning = null;
                        }

                        foreach (var svc in ad.Services.Values.Where(s => s.scanned == false))
                        {
                            ad.ServiceScanning = svc.uuid;
                            // found the Heart Rate service, so now search for the attributes inside
                            var cmd = _bglib.BLECommandATTClientFindInformation(ad.ConnectionHandle, svc.start,
                                svc.end);
                            _bglib.SendCommand(_port, cmd);
                            return;
                        }

                        ad.State = BlueGigaState.SERVICESSCANNED;
                        ad.FireConnectionComplete();
                    }
                }
                else
                {
                    Console.WriteLine("Failed to find connection data!");
                }
            }

            public void ATTClientAttributeValueEvent(object sender, Bluegiga.BLE.Events.ATTClient.AttributeValueEventArgs e)
            {
                String log = String.Format("ble_evt_attclient_attribute_value: connection={0}, atthandle={1}, type={2}, value=[ {3}]" + Environment.NewLine,
                    e.connection,
                    e.atthandle,
                    e.type,
                    ByteToHex(e.value)
                    );
                Console.Write(log);
            }

            internal void Send(byte[] aValue, byte connectionHandle, ushort endpoint)
            {
                _bglib.SendCommand(_port, _bglib.BLECommandATTClientWriteCommand(connectionHandle, endpoint, aValue));
            }
        }
        internal class BlueGigaDevice
        {
            public BlueGigaAdvertisement Advertisment;
            public BlueGigaContainer Container;
        }

        [NotNull]
        private readonly List<string> _seenAddresses = new List<string>();

        [NotNull]
        private readonly Dictionary<string, BlueGigaContainer> _adapters = new Dictionary<string, BlueGigaContainer>();

        public BlueGigaBluetoothManager(IButtplugLogManager aLogManager)
            : base(aLogManager)
        {
            BpLogger.Info("Loading BlueGiga Bluetooth Manager");

            //ToDo: we might have multiple BlueGiga devices, but I've only got one right now
            
            var port = new SerialPort("COM5", 256000);

            // Find an appropriate serial port
            if (port == null)
            {
                BpLogger.Warn("No bluetooth adapter available for BlugGiga Bluetooth Manager Connection");
                return;
            }
            var bgContainer = new BlueGigaContainer(aLogManager, port);
            _adapters.Add(port.PortName, bgContainer);
            bgContainer.AdvertisementReceived += handleAdvertisement;
            bgContainer.Start();

            BpLogger.Debug("BlugGiga Manager found working Bluetooth LE Adapter");
        }

        private async void handleAdvertisement(object aSender, AdvertisementReceivedEventArgs aEvent) {

            if (aEvent?.Advertisement == null)
            {
                BpLogger.Debug("Null BLE advertisement received: skipping");
                return;
            }

            var advertNames = string.Join(", ", aEvent.Advertisement.Names);
            BpLogger.Trace("BLE device found: " + advertNames);

            // We always need a name to match against.
            if (advertNames == string.Empty)
            {
                return;
            }
            
            foreach (var advertName in aEvent.Advertisement.Names)
            {
                if (advertName == string.Empty)
                {
                    continue;
                }

                if (_seenAddresses.Contains(aEvent.Advertisement.Address + advertName))
                {
                    // Skip stuff we've already processed
                    continue;
                }

                var deviceCriteria = new BluetoothLEProtocolConfiguration(advertName);

                var deviceFactory = DeviceConfigurationManager.Manager.Find(deviceCriteria);

                // If we don't have a protocol to match the device, we can't do anything with it.
                if (deviceFactory == null || !(deviceFactory.Config is BluetoothLEProtocolConfiguration bleConfig))
                {
                    BpLogger.Debug($"No usable device factory available for {advertName}.");
                    // If we've got an actual name this time around, and we don't have any factories
                    // available that match the info we have, add to our seen list so we won't keep
                    // rechecking. If a device does have a factory, but doesn't connect, we still want to
                    // try again.
                    _seenAddresses.Add(aEvent.Advertisement.Address + advertName);
                    return;
                }


                try
                {
                    var dev = aEvent.BgContainer.GetConnection(aEvent.Advertisement);
                    TaskCompletionSource<bool> connected = new TaskCompletionSource<bool>();
                    dev.Advertisment.ConnectionComplete += (s, e) => { connected?.SetResult(true); };
                    await connected.Task;
                    connected = null;
                    var bleDevice = await BlueGigaBluetoothDeviceInterface.Create(LogManager, bleConfig, advertName, dev)
                        .ConfigureAwait(false);
                    var btDevice = await deviceFactory.CreateDevice(LogManager, bleDevice).ConfigureAwait(false);
                    InvokeDeviceAdded(new DeviceAddedEventArgs(btDevice));
                }
                catch (Exception ex)
                {
                    BpLogger.Error(
                        $"Cannot connect to device {advertName} {aEvent.Advertisement.Address}: {ex.Message}");
                }
            }
        }

        public override void StartScanning()
        {
            BpLogger.Info("Starting BLE Scanning");
            _seenAddresses.Clear();

            foreach (var bgContainer in _adapters.Values)
            {
                bgContainer.StartScanning();
            }
        }

        public override void StopScanning()
        {
            BpLogger.Info("Stopping BLE Scanning");
            foreach (var bgContainer in _adapters.Values)
            {
                bgContainer.StopScanning();
            }

            InvokeScanningFinished();
        }

        public override bool IsScanning()
        {
            foreach (var bgContainer in _adapters.Values)
            {
                if (bgContainer.Scanning)
                {
                    return true;
                }
            }

            return false;
        }
    }

}

﻿using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.DeviceSimulator.PipeMessages;

namespace Buttplug.Server.Managers.SimulatorManager
{
    public class SimulatorManager : DeviceSubtypeManager
    {
        private NamedPipeServerStream _pipeServer;

        private Task _readThread;
        private Task _writeThread;

        private CancellationTokenSource _tokenSource;

        private bool _scanning;

        private PipeMessageParser _parser;

        private ButtplugLogManager _logManager;

        private ConcurrentQueue<IDeviceSimulatorPipeMessage> _msgQueue = new ConcurrentQueue<IDeviceSimulatorPipeMessage>();

        public SimulatorManager(IButtplugLogManager aLogManager)
            : base(aLogManager)
        {
            BpLogger.Info("Loading Simulator Manager");
            _scanning = false;

            _parser = new PipeMessageParser();

            _logManager = new ButtplugLogManager();

            _pipeServer = new NamedPipeServerStream("ButtplugDeviceSimulator", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);

            _tokenSource = new CancellationTokenSource();
            _readThread = new Task(() => { connAccepter(_tokenSource.Token); }, _tokenSource.Token, TaskCreationOptions.LongRunning);
            _writeThread = new Task(() => { pipeWriter(_tokenSource.Token); }, _tokenSource.Token, TaskCreationOptions.LongRunning);
            _readThread.Start();
            _writeThread.Start();
        }

        internal void Vibrate(SimulatedButtplugDevice aDev, double aSpeed)
        {
            _msgQueue.Enqueue(new Vibrate(aDev.Identifier, aSpeed));
        }

        internal void Rotate(SimulatedButtplugDevice aDev, uint aSpeed, bool aClockwise)
        {
            _msgQueue.Enqueue(new Rotate(aDev.Identifier, aSpeed, aClockwise));
        }

        internal void StopDevice(SimulatedButtplugDevice aDev)
        {
            _msgQueue.Enqueue(new StopDevice(aDev.Identifier));
        }

        internal void Linear(SimulatedButtplugDevice aDev, uint aSpeed, uint aPosition)
        {
            _msgQueue.Enqueue(new Linear(aDev.Identifier, aSpeed, aPosition));
        }

        private void connAccepter(CancellationToken aCancellationToken)
        {
            while (!aCancellationToken.IsCancellationRequested)
            {
                if (!_pipeServer.IsConnected)
                {
                    _scanning = false;
                    _pipeServer.WaitForConnection();
                    pipeReader(aCancellationToken);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
        }

        private void pipeReader(CancellationToken aCancellationToken)
        {
            while (!aCancellationToken.IsCancellationRequested && _pipeServer.IsConnected)
            {
                var buffer = new byte[4096];
                string msg = string.Empty;
                var len = -1;
                while (len < 0 || (len == buffer.Length && buffer[4095] != '\0'))
                {
                    var waiter = _pipeServer.ReadAsync(buffer, 0, buffer.Length);
                    while (!waiter.GetAwaiter().IsCompleted)
                    {
                        if (!_pipeServer.IsConnected)
                        {
                            return;
                        }

                        Thread.Sleep(10);
                    }

                    len = waiter.GetAwaiter().GetResult();

                    if (len > 0)
                    {
                        msg += Encoding.ASCII.GetString(buffer, 0, len);
                    }
                }

                switch (_parser.Deserialize(msg))
                {
                    case FinishedScanning fs:
                        InvokeScanningFinished();
                        _scanning = false;
                        break;

                    case DeviceAdded da:
                        InvokeDeviceAdded(new DeviceAddedEventArgs(new SimulatedButtplugDevice(this, _logManager, da)));
                        break;

                    case DeviceRemoved dr:
                        //InvokeDevice (new DeviceAddedEventArgs(new SimulatedButtplugDevice(_logManager, "Test", "1234")));
                        break;

                    default:
                        break;
                }
            }
        }

        private void pipeWriter(CancellationToken aCancellationToken)
        {
            while (!aCancellationToken.IsCancellationRequested)
            {
                if (_pipeServer.IsConnected && _msgQueue.TryDequeue(out IDeviceSimulatorPipeMessage msg))
                {
                    var str = _parser.Serialize(msg);
                    if (str != null)
                    {
                        _pipeServer.Write(Encoding.ASCII.GetBytes(str), 0, str.Length);
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }

        public override void StartScanning()
        {
            BpLogger.Info("SimulatorManager start scanning");
            _scanning = true;
            _msgQueue.Enqueue(new StartScanning());
        }

        public override void StopScanning()
        {
            BpLogger.Info("SimulatorManager stop scanning");
            _scanning = false;
            _msgQueue.Enqueue(new StopScanning());
        }

        public override bool IsScanning()
        {
            return _scanning;
        }
    }
}
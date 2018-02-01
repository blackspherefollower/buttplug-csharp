using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using JetBrains.Annotations;

namespace Buttplug.Server.Bluetooth.Devices
{
    internal class KiirooBluetoothInfo : IBluetoothDeviceInfo
    {
        public enum Chrs : uint
        {
            Rx = 0,
            Tx,
            Cmd,
            Cmd2,
        }

        public string[] Names { get; } = { "ONYX", "PEARL" };

        public Guid[] Services { get; } = { new Guid("49535343-fe7d-4ae5-8fa9-9fafd205e455") };

        public Guid[] Characteristics { get; } =
        {
            // rx
            new Guid("49535343-1e4d-4bd9-ba61-23c647249616"),

            // tx
            new Guid("49535343-8841-43f4-a8d4-ecbe34729bb3"),

            // cmd
            new Guid("49535343-aca3-481c-91ec-d85e28a60318"),

            // cmd2
            new Guid("49535343-6daa-4d02-abf6-19569aca69fe"),
        };

        public IButtplugDevice CreateDevice(IButtplugLogManager aLogManager,
            IBluetoothDeviceInterface aInterface)
        {
            return new Kiiroo(aLogManager, aInterface, this);
        }
    }

    internal class Kiiroo : ButtplugBluetoothDevice
    {
        private readonly object _onyxLock = new object();
        private double _deviceSpeed;
        private double _targetPosition;
        private double _currentPosition;
        private DateTime _targetTime = DateTime.Now;
        private DateTime _currentTime = DateTime.Now;
        private Timer _onyxTimer;

        public Kiiroo([NotNull] IButtplugLogManager aLogManager,
                      [NotNull] IBluetoothDeviceInterface aInterface,
                      [NotNull] IBluetoothDeviceInfo aInfo)
            : base(aLogManager,
                   $"Kiiroo {aInterface.Name}",
                   aInterface,
                   aInfo)
        {
            MsgFuncs.Add(typeof(KiirooCmd), new ButtplugDeviceWrapper(HandleKiirooRawCmd));
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (aInterface.Name == "PEARL")
            {
                MsgFuncs.Add(typeof(VibrateCmd), new ButtplugDeviceWrapper(HandleVibrateCmd,
                    new MessageAttributes() { FeatureCount = 1 }));
                MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd));
            }
            else if (aInterface.Name == "ONYX")
            {
                MsgFuncs.Add(typeof(LinearCmd), new ButtplugDeviceWrapper(HandleLinearCmd,
                    new MessageAttributes() { FeatureCount = 1 }));
            }
        }

        private void OnBluetoothMessageReceived(object sender, BluetoothMessageReceivedEventArgs aArgs)
        {
            BpLogger.Trace($"Kirroo sent data: {BitConverter.ToString(aArgs.Data)}");
        }

        public override async Task<ButtplugMessage> Initialize()
        {
            // Start listening for incoming
            Interface.BluetoothMessageReceived += OnBluetoothMessageReceived;
            await Interface.SubscribeValue(ButtplugConsts.SystemMsgId, Info.Characteristics[(int)KiirooBluetoothInfo.Chrs.Rx]);

            // Tell the Onyx to behave?
            // Not really sure what we're doing here, but this is what we see in the wild

            // Mode select?
            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Cmd],
                new byte[] { 0x01, 0x00 }, true);

            // Twice for luck?
            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Cmd],
                new byte[] { 0x01, 0x00 }, true);

            // Set to start position
            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Tx],
                new byte[] { 0x30, 0x2c }, true);

            // Version request?
            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Tx],
                new byte[] { 0x76 }, true);

            // Handshake? Works for Onyx...
            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Cmd2],
                new byte[] { 0xff, 0x10, 0x00, 0x20, 0x00, 0x00, 0x00, 0x64, 0x00 }, true);

            // Handshake? Twice for luck?
            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Cmd2],
                new byte[] { 0xff, 0x10, 0x00, 0x20, 0x00, 0x00, 0x00, 0x64, 0x00 }, true);

            // Mode request?
            var res = await Interface.ReadValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(int)KiirooBluetoothInfo.Chrs.Cmd2]);
            BpLogger.Trace($"Kirroo read data: {BitConverter.ToString(res.Value)}");

            // Set to start position again?
            await Interface.WriteValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Tx],
                new byte[] { 0x30, 0x2c }, true);

            // Mode request?
            res = await Interface.ReadValue(ButtplugConsts.SystemMsgId,
                Info.Characteristics[(int)KiirooBluetoothInfo.Chrs.Cmd2]);
            BpLogger.Trace($"Kirroo read data: {BitConverter.ToString(res.Value)}");

            if (Interface.Name != "ONYX")
            {
                return new Ok(ButtplugConsts.SystemMsgId);
            }

            // Onyx specific

            Interface.DeviceRemoved += (sender, args) =>
            {
                _onyxTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _onyxTimer = null;
            };

            _currentTime = DateTime.Now;
            _onyxTimer = new Timer(OnOnyxTimer, null, 500, 500);

            return new Ok(ButtplugConsts.SystemMsgId);
        }

        public override void Disconnect()
        {
            if (Interface.Name == "ONYX")
            {
                _onyxTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _onyxTimer = null;
            }

            Interface.Disconnect();
        }

        private async void OnOnyxTimer(object state)
        {
            lock (_onyxLock)
            {
                // Given the time since the last iteration and the difference in distance, work out have far we should move
                var distance = Math.Abs(_targetPosition - _currentPosition);
                var oldTime = new DateTime(_currentTime.Ticks);
                var nowTime = DateTime.Now;
                var delta = nowTime.Subtract(oldTime);

                if (Convert.ToUInt32(distance * 4) == 0 && delta.TotalMilliseconds < 500)
                {
                    // Skip. We do want to occationally ping the Onyx, but only every half a second
                    return;
                }

                _currentTime = DateTime.Now;

                if (_currentTime.CompareTo(_targetTime) >= 0 || distance < 0.000001)
                {
                    // We've overdue: jump to target
                    _currentPosition = _targetPosition;
                    _onyxTimer?.Change(500, 500);
                }
                else
                {
                    // The hard part: find the persentage time gone, then add that percentate of the movement delta
                    var delta2 = _targetTime.Subtract(oldTime);

                    var move = Convert.ToDouble(delta.TotalMilliseconds) / (Convert.ToDouble(delta2.TotalMilliseconds) + 1) * distance;
                    _currentPosition += move * (_targetPosition > _currentPosition ? 1 : -1);
                    _currentPosition = Math.Max(0, Math.Min(1, _currentPosition));
                }
            }

            var res = await HandleKiirooRawCmd(new KiirooCmd(0, Convert.ToUInt32(_currentPosition * 4), ButtplugConsts.SystemMsgId));
            if (res is Error err)
            {
                BpLogger.Error(err.ErrorMessage);
            }
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            // Right now, this is a nop. The Onyx doesn't have any sort of permanent movement state,
            // and its longest movement is like 150ms or so. The Pearl is supposed to vibrate but I've
            // never gotten that to work. So for now, we just return ok.
            BpLogger.Debug("Stopping Device " + Name);

            if (Interface.Name == "PEARL" && _deviceSpeed > 0)
            {
                return await HandleKiirooRawCmd(new KiirooCmd(aMsg.DeviceIndex, 0, aMsg.Id));
            }

            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleKiirooRawCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is KiirooCmd cmdMsg))
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            return await Interface.WriteValue(cmdMsg.Id,
                Info.Characteristics[(uint)KiirooBluetoothInfo.Chrs.Tx],
                Encoding.ASCII.GetBytes($"{cmdMsg.Position},"), true);
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is SingleMotorVibrateCmd cmdMsg) || Interface.Name != "PEARL")
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            if (Math.Abs(_deviceSpeed - cmdMsg.Speed) < 0.001)
            {
                return new Ok(cmdMsg.Id);
            }

            _deviceSpeed = cmdMsg.Speed;

            return await HandleVibrateCmd(new VibrateCmd(cmdMsg.DeviceIndex,
                new List<VibrateCmd.VibrateSubcommand>() { new VibrateCmd.VibrateSubcommand(0, cmdMsg.Speed) },
                cmdMsg.Id));
        }

        private async Task<ButtplugMessage> HandleVibrateCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is VibrateCmd cmdMsg) || Interface.Name != "PEARL")
            {
                return BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler");
            }

            if (cmdMsg.Speeds.Count != 1)
            {
                return new Error(
                    "VibrateCmd requires 1 vector for this device.",
                    Error.ErrorClass.ERROR_DEVICE,
                    cmdMsg.Id);
            }

            foreach (var v in cmdMsg.Speeds)
            {
                if (v.Index != 0)
                {
                    return new Error(
                        $"Index {v.Index} is out of bounds for VibrateCmd for this device.",
                        Error.ErrorClass.ERROR_DEVICE,
                        cmdMsg.Id);
                }

                _deviceSpeed = v.Speed;
            }

            return await HandleKiirooRawCmd(new KiirooCmd(aMsg.DeviceIndex, Convert.ToUInt16(_deviceSpeed * 4), aMsg.Id));
        }

        private Task<ButtplugMessage> HandleLinearCmd([NotNull] ButtplugDeviceMessage aMsg)
        {
            if (!(aMsg is LinearCmd cmdMsg) || Interface.Name != "ONYX")
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(
                    aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            if (cmdMsg.Vectors.Count != 1)
            {
                return Task.FromResult<ButtplugMessage>(new Error(
                    "LinearCmd requires 1 vector for this device.",
                    Error.ErrorClass.ERROR_DEVICE,
                    cmdMsg.Id));
            }

            foreach (var v in cmdMsg.Vectors)
            {
                if (v.Index != 0)
                {
                    return Task.FromResult<ButtplugMessage>(new Error(
                        $"Index {v.Index} is out of bounds for LinearCmd for this device.",
                        Error.ErrorClass.ERROR_DEVICE,
                        cmdMsg.Id));
                }

                // Invert the position
                lock (_onyxLock)
                {
                    _targetPosition = 1 - v.Position;
                    _currentTime = DateTime.Now;
                    _targetTime = DateTime.Now.AddMilliseconds(v.Duration);
                    _onyxTimer?.Change(0, 50);
                }
            }

            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }
    }
}

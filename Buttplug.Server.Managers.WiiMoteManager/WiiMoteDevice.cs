using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using WiimoteLib;

namespace Buttplug.Server.Managers.XInputGamepadManager
{
    internal class WiiMoteDevice : ButtplugDevice
    {
        private Wiimote _device;

        private bool _reportAccel = false;

        public WiiMoteDevice(IButtplugLogManager aLogManager, Wiimote aDevice)
            : base(aLogManager, "WiiMote", aDevice.ID.ToString(), 1)
        {
            _device = aDevice;
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd));
            MsgFuncs.Add(typeof(VibrateCmd), new ButtplugDeviceWrapper(HandleVibrateCmd, new Dictionary<string, string>() { { "VibratorCount", "1" } }));
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));

            MsgFuncs.Add(typeof(StartAccelerometerCmd), new ButtplugDeviceWrapper(HandleStartAccelerometerCmd));
            MsgFuncs.Add(typeof(StopAccelerometerCmd), new ButtplugDeviceWrapper(HandleStopAccelerometerCmd));
            _device.WiimoteChanged += HandleWiimoteChanged;
        }

        private void HandleWiimoteChanged(object sender, WiimoteChangedEventArgs e)
        {
            if (_reportAccel)
            {
                var axis = e.WiimoteState.AccelState.RawValues;
                EmitMessage(new AccelerometerData(axis.X, axis.Y, axis.Z, Index));
            }
        }

        private Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
        {
            BpLogger.Debug("Stopping Device " + Name);
            return HandleSingleMotorVibrateCmd(new SingleMotorVibrateCmd(aMsg.DeviceIndex, 0, aMsg.Id));
        }

        private Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as SingleMotorVibrateCmd;

            if (cmdMsg is null)
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            var speeds = new List<VibrateCmd.VibrateIndex>();
            for (uint i = 0; i < VibratorCount; i++)
            {
                speeds.Add(new VibrateCmd.VibrateIndex(i, cmdMsg.Speed));
            }

            return HandleVibrateCmd(new VibrateCmd(cmdMsg.DeviceIndex, speeds, cmdMsg.Id));
        }

        private Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as VibrateCmd;
            if (cmdMsg is null)
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            foreach (var vi in cmdMsg.Speeds)
            {
                if (vi.Index != 0)
                {
                    continue;
                }

                _vibratorSpeeds[vi.Index] = _vibratorSpeeds[vi.Index] < 0 ? 0
                                          : _vibratorSpeeds[vi.Index] > 1 ? 1
                                                                          : vi.Speed;
            }

            _device?.SetRumble(Convert.ToUInt16(_vibratorSpeeds[0]) == 1);

            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        private Task<ButtplugMessage> HandleStartAccelerometerCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as StartAccelerometerCmd;
            if (cmdMsg is null)
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            _reportAccel = true;
            _device.SetReportType(InputReport.ButtonsAccel, true);
            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        private Task<ButtplugMessage> HandleStopAccelerometerCmd(ButtplugDeviceMessage aMsg)
        {
            var cmdMsg = aMsg as StopAccelerometerCmd;
            if (cmdMsg is null)
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            _reportAccel = false;
            _device.SetReportType(InputReport.Buttons, false);
            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        public override void Disconnect()
        {
            _device.SetRumble(false);
            _vibratorSpeeds[0] = 0;
            _device.Disconnect();
            _device = null;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using JetBrains.Annotations;
using System.Linq;

namespace Buttplug.Server.Managers.SimulatorManager
{
    internal class SimulatedButtplugDevice : ButtplugDevice
    {
        private SimulatorManager _manager;

        public SimulatedButtplugDevice(
            SimulatorManager aManager,
            [NotNull] IButtplugLogManager aLogManager,
            [NotNull] DeviceSimulator.PipeMessages.DeviceAdded da)
            : base(aLogManager, da.Name, da.Id)
        {
            _manager = aManager;
            if (da.HasLinear)
            {
                MsgFuncs.Add(typeof(FleshlightLaunchFW12Cmd), HandleFleshlightLaunchFW12Cmd);
            }

            if (da.HasVibrator)
            {
                MsgFuncs.Add(typeof(SingleMotorVibrateCmd), HandleSingleMotorVibrateCmd);
                MsgFuncs.Add(typeof(VibrateCmd), HandleVibrateCmd);
            }

            if (da.HasRotator)
            {
                MsgFuncs.Add(typeof(VorzeA10CycloneCmd), HandleVorzeA10CycloneCmd);
            }

            MsgFuncs.Add(typeof(StopDeviceCmd), HandleStopDeviceCmd);
        }

        public override void Disconnect()
        {
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg)
        {
            _manager.StopDevice(this);
            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            _manager.Vibrate(this, (aMsg as SingleMotorVibrateCmd).Speed);
            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg)
        {
            var speed = from x in (aMsg as VibrateCmd).Speeds where x.Index == 0 select x.Speed;
            if (speed.Any())
            {
                _manager.Vibrate(this, speed.First());
                return new Ok(aMsg.Id);
            }

            return new Error("Invalid vibrator index!", Error.ErrorClass.ERROR_DEVICE, aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleVorzeA10CycloneCmd(ButtplugDeviceMessage aMsg)
        {
            _manager.Rotate(this, (aMsg as VorzeA10CycloneCmd).Speed, (aMsg as VorzeA10CycloneCmd).Clockwise);
            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleFleshlightLaunchFW12Cmd(ButtplugDeviceMessage aMsg)
        {
            _manager.Linear(this, (aMsg as FleshlightLaunchFW12Cmd).Speed, (aMsg as FleshlightLaunchFW12Cmd).Position);
            return new Ok(aMsg.Id);
        }
    }
}
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buttplug.Core
{
    class HidManager : DeviceSubtypeManager
    {
        private IButtplugLogManager aLogger;

        public HidManager(IButtplugLogManager aLogger) : base(aLogger)
        {
            var hids = new HidEnumerator();
            foreach( var hid in hids.Enumerate() )
            {
                hid.ReadProduct(out byte[] product);
                aLogger.GetLogger(this.GetType()).Debug(Encoding.Unicode.GetString(product));
            }
        }

        public override void StartScanning()
        {

        }

        public override void StopScanning()
        {
            throw new NotImplementedException();
        }
    }
}

using System;

namespace Buttplug.Server.Managers.QuadMouse
{
    public class QuadMouseException : Exception
    {
        public QuadMouseException()
        {
        }

        public QuadMouseException(string message)
            : base(message)
        {
        }

        public QuadMouseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}

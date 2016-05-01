using System;
using System.Diagnostics;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using SwitchServerApplication;

namespace SwitchRemoteServices
{
    public class SwitchServiceClass : MarshalByRefObject, ISwitchService
    {
        private DateTime startTime;
        private ISwitchRemote mainForm = null;

        public SwitchServiceClass()
        {
            startTime = System.DateTime.Now;
        }

        public ISwitchRemote theMainForm
        {
            set
            {
                mainForm = value;
            }
        }
            
        /// <summary>
        /// Implementation of ISwitchService interface
        /// </summary>

        public DateTime getSwitchStartTime()
        {
            return startTime;
        }

        public void remoteExecute(int task, bool brut)
        {
            mainForm.doExecute(task, brut);
        }

        public int getSwitchId()
        {
            return mainForm.getNumber();
        }
    }
}


using System;

namespace SwitchRemoteServices
{
    /// <summary>
    /// Interface for Switch
    /// </summary>
    public interface ISwitchService
    {
        DateTime    getSwitchStartTime();
        int         getSwitchId();
        void        remoteExecute(int task, bool brut);
    }
}

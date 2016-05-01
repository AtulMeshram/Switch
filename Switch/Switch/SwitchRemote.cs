using System;

namespace SwitchServerApplication
{
    public interface ISwitchRemote
    {
        void doExecute(int task, bool brut);
        int getNumber();
    }
}

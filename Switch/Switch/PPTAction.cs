using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsInput;

namespace SwitchPlus
{
    public enum ActionType
    {
        UP,
        DOWN,
        LEFT,
        RIGHT,
        ESC,
        BACKSPACE,
        HOME,
        TAB,
        DELETE,
        END,
        ENTER,
        PGUP,
        PGDN,
    }
    public class PPTAction
    {
        public static void ControlPPT(ActionType at)
        {
            switch(at)
            {
                case ActionType.UP:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.UP);
                    break;
                case ActionType.DOWN:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.DOWN);
                    break;
                case ActionType.LEFT:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.LEFT);
                    break;
                case ActionType.RIGHT:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.RIGHT);
                    break;
                case ActionType.ESC:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.ESCAPE);
                    break;
                case ActionType.BACKSPACE:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.BACK);
                    break;
                case ActionType.HOME:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.HOME);
                    break;
                case ActionType.TAB:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.TAB);
                    break;
                case ActionType.DELETE:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.DELETE);
                    break;
                case ActionType.END:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.END);
                    break;
                case ActionType.ENTER:
                    InputSimulator.SimulateKeyPress(VirtualKeyCode.RETURN);
                    break;
            }
        }

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName,
            string lpWindowName);


        [DllImport("USER32.DLL")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }

    

}

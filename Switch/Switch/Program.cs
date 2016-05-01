using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Services;
using SwitchRemoteServices;
using System.Net;
    
namespace SwitchPlus
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 

        //Public Variables ------
        public static bool broadcast = false;
        public static bool brutallyM = false;
        public static bool pnlDaily = false;
        public static bool pnlSchedule = false;


        //Variables for Countdown
        public static bool selected_CD = true;
        public static int action_CD = 9;
        public static bool isActive_CD = false;
        public static bool brutally_CD = false;
        public static DateTime time_CD = System.DateTime.Now;

        public static void resetCD()
        {
            action_CD = 9;
            isActive_CD = false;
            brutally_CD = false;
            time_CD = System.DateTime.Now;        
        }
        //-------------------------------------

        // Variables for Schedule For ---------
        public static bool selected_SF = false;
        public static bool isActive_SF = false;
        public static int action_SF = 9;
        public static bool brutally_SF = false;
        public static DateTime DT_SF;
        public static RegistryKey SF = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Switch\\SF");
        //-------------------------------------

        // Variables for Daily ---------
        public static bool selected_DL = false;
        public static bool isActive_DL = false;
        public static int action_DL = 9;
        public static bool brutally_DL = false;
        public static DateTime DT_DL;
        public static RegistryKey DL = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Switch\\DL");
        public static bool DL_Reset = false;
        //-------------------------------------

        // Public variable for Message Box Response
        public static bool msgRes = false;

        [STAThread]
        static void Main(string [] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0)
            {
                return;
            }

            int count = GetNumberOfRunningInstances();

            if (count <= 1)
            {
                Application.Run(new Switch());
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Another instance of 'Switch' is already running!", "Switch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
            }
        }

        private static int GetNumberOfRunningInstances()
        {
            //yank off the appname.exe from the assemblies location
            string[] parts = System.Reflection.Assembly.GetExecutingAssembly().Location.Split("\\".ToCharArray());
            string appName = parts[parts.Length - 1];

            //build the wmi query
            string query = "select name from CIM_Process where name = '" + appName + "'";

            //load up the managementobjectsearcher with the query
            System.Management.ManagementObjectSearcher searcher = new System.Management.ManagementObjectSearcher(query);

            int runcount = 0;
            //iterate the collection and (which should only have 1 or 2 instances, and if 3 then its already running
            //1 instaces would be itself, 2 would be self and the other
            foreach (System.Management.ManagementObject item in searcher.Get())
            {
                runcount++;
                if (runcount > 1) break; //only need to know if there is more then self running
            }

            return runcount;
        }
        public static string getTaskName(int t)
        {
            switch(t)
            {
                case 1:
                    return "Shutdown";
                case 2:
                    return "Restart";
                case 3:
                    return "Sleep";
                case 4:
                    return "Hibernate";
                case 5:
                    return "Logoff";
                case 6:
                    return "Lock";
                default:
                    return "???";
            }
        }

        public static void resetSF()
        {
            isActive_SF = false;
            action_SF = 9;
            brutally_SF = false;
            DT_SF = System.DateTime.Now;
            SwitchPlus.Program.SF.SetValue("Action", SwitchPlus.Program.action_SF.ToString(), RegistryValueKind.String);
            SwitchPlus.Program.SF.SetValue("DT", SwitchPlus.Program.DT_SF.ToString("o"), RegistryValueKind.String);
            SwitchPlus.Program.SF.SetValue("isActive", "0", RegistryValueKind.String);
            SwitchPlus.Program.SF.SetValue("isbrutally", "0", RegistryValueKind.String);
        }

        public static void retSF()
        {
            try
            {
                if (Convert.ToString(SF.GetValue("isActive")).Equals("1"))
                    isActive_SF = true;
                else
                    isActive_SF = false;
                
                action_SF = Convert.ToInt32(SF.GetValue("Action"));
                if (!(action_SF >= 1 && action_SF <= 6))
                    action_SF = 9;
                if (Convert.ToString(SF.GetValue("isbrutally")).Equals("1")) 
                    brutally_SF = true;
                else
                    brutally_SF = false;
                DT_SF = DateTime.Parse(Convert.ToString(SwitchPlus.Program.SF.GetValue("DT")), null, System.Globalization.DateTimeStyles.RoundtripKind);
            }
            catch
            {
                isActive_SF = false;
                action_SF = 9;
                brutally_SF = false;
                DT_SF = System.DateTime.Now;
            }
        }

        public static void retDL()
        {
            try
            {
                if (Convert.ToString(DL.GetValue("isActive")).Equals("1"))
                    isActive_DL = true;
                else
                    isActive_DL = false;

                action_DL = Convert.ToInt32(DL.GetValue("Action"));
                if (!(action_DL >= 1 && action_DL <= 6))
                    action_DL = 9;
                if (Convert.ToString(DL.GetValue("isBrutally")).Equals("1"))
                    brutally_DL = true;
                else
                    brutally_DL = false;

                TimeSpan TS = TimeSpan.Parse(Convert.ToString(SwitchPlus.Program.DL.GetValue("DT")));
                DT_DL = DateTime.SpecifyKind(new DateTime(System.DateTime.Now.Year, System.DateTime.Now.Month, System.DateTime.Now.Day, TS.Hours, TS.Minutes, TS.Seconds), DateTimeKind.Local);
                DL_Reset = false;
            }
            catch
            {
                isActive_DL = false;
                action_DL = 9;
                brutally_DL = false;
                DT_DL = System.DateTime.Now;
                DL_Reset = true;            
            }
        }       
        
        public static bool isRemDT_SF()
        {
            if (SwitchPlus.Program.DT_SF.CompareTo(System.DateTime.Now) <= 0)
                return false;
            else
                return true;
        }

        public static void resetDL()
        {
            isActive_DL = false;
            action_DL = 9;
            brutally_DL = false;
            DT_DL = System.DateTime.Now;
            SwitchPlus.Program.DL.SetValue("Action", SwitchPlus.Program.action_SF.ToString(), RegistryValueKind.String);
            SwitchPlus.Program.DL.SetValue("DT", SwitchPlus.Program.DT_SF.ToString("o"), RegistryValueKind.String);
            SwitchPlus.Program.DL.SetValue("isActive", "0", RegistryValueKind.String);
            SwitchPlus.Program.DL.SetValue("isBrutally", "0", RegistryValueKind.String);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetToPLCSimLite.Helpers
{
    public static class Lib
    {
        #region Dll Imports
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string ipClassName, string IpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        const int GW_HWNDFIRST = 0;
        const int GW_HWNDLAST = 1;
        const int GW_HWNDNEXT = 2;
        const int GW_HWNDPREV = 3;
        const int GW_OWNER = 4;
        const int GW_CHILD = 5;
        #endregion

        public static async void ProcessKill(this Process proc, int sleep = 5000)
        {
            if (proc == null) return;
            proc.Kill();
            await Task.Delay(sleep);
            var pID = proc.Id;
            var Title = new StringBuilder(256);
            var tempHwnd = FindWindow(null, null);
            while (tempHwnd.ToInt32() != 0)
            {
                tempHwnd = GetWindow(tempHwnd, GW_HWNDNEXT);
                GetWindowText(tempHwnd, Title, Title.Capacity + 1);
                if (Title.Length >= 0)
                {
                    GetWindowThreadProcessId(tempHwnd, out uint processID);
                    if (processID == pID) SendMessage(tempHwnd, 0x0010, -1, -1);
                }
            }
        }
    }
}

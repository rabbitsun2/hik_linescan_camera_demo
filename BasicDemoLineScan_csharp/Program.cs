/*
 * 이 예에서는 SDK를 통해 라인 스캔 카메라를 연결하고 구성하는 방법을 보여줍니다.
 * This program shows how to connect and configure line scan cameras.
 */

using System;
using System.Windows.Forms;

namespace BasicDemoLineScan
{
    static class Program
    {
        /// <summary>
        /// 애플리케이션의 주요 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    class Script
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext scriptcontext)
        {
            if (scriptcontext.ExternalPlanSetup == null)
            {
                MessageBox.Show("No plan is open.", "Error");
                return;
            }

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            scriptcontext.Patient.BeginModifications();

            EQD2Converter.MainWindow mainWindow = new EQD2Converter.MainWindow(scriptcontext);
            mainWindow.ShowDialog();
        }
    }
}

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


namespace VMS.TPS
{
    class Script
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext scriptcontext)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            EQD2Converter.MainWindow mainWindow = new EQD2Converter.MainWindow(scriptcontext);
            mainWindow.ShowDialog();
        }
    }
}

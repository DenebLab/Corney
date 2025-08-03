#r "System.IO"
#r "System.Drawing"
#r "System.Windows.Forms"
using System.Windows.Forms;

using System.Diagnostics;
using Microsoft.Win32;

Console.WriteLine("Execute file: befor-make-current.csx");

string appName = "Corney";


try
{
    foreach (var process in Process.GetProcessesByName(appName))
    {
        process.Kill();
    }

}
catch (System.Exception)
{

    throw;
}








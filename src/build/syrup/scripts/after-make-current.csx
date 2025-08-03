#r "System.IO"
#r "System.Linq"

using System.Diagnostics;
using Microsoft.Win32;

Console.WriteLine("Execute file: after-make-current.csx");


string appName = "Corney";
string appDirPath = System.IO.Directory.GetParent(Syrup.CurrentAppPath).FullName;
string mainAppDir = System.IO.Path.Combine(appDirPath, appName);

string mainDirPath = System.IO.Path.Combine(mainAppDir, "main");

string corenyDir = System.IO.Path.Combine(mainDirPath, appName);
string corenyExe = System.IO.Path.Combine(corenyDir, "Corney.exe");

string app = $"\"{corenyExe}\"";



try
{
    var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
    rk.SetValue(appName, app);
}
catch (Exception)
{
    return false;
}
Process process = new Process();
// Configure the process using the StartInfo properties.
process.StartInfo.FileName = corenyExe;

process.Start();



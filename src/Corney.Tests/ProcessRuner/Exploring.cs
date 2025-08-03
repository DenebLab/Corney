using System.IO;
using Corney.Features.Processes;
using Microsoft.Win32;
using Xunit;

namespace Corney.Tests.ProcessRuner;

public class Exploring
{
    [Fact]
    public void FactMethodName()
    {
        var path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        var key = Registry.CurrentUser.OpenSubKey(path, true);
        var ss = @"C:\work\temp\runner\Run1.exe -clients=client1,client2 -waitTime=10 -SeriesMode=2";
        var t1 = Pharse.Tokenize2(ss);

        var aa = Path.GetFullPath(t1.Program);
        var processWrapper = new ProcessWrapper();
        processWrapper.Start(t1);
        //var t = new ProcessWrapper("'SiteScrapera.exe\a\a\' -clients=client1,client2 -waitTime=10 -SeriesMode=2", "", false, false);
    }
}
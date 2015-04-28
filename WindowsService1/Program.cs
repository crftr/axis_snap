using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace WindowsService1
{
    static class Program
    {
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] { new Service1() };
            ServiceBase.Run(ServicesToRun);
        }
    }
}

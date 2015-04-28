using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

using snapService;

namespace WindowsService1
{
    public partial class Service1 : ServiceBase
    {
        /* EventID convention */
        internal enum lg_CamEventID : int
        {
            // Service IDs (1xxx)
            Start    = 1000,
            Stop     = 1001,
            Shutdown = 1002,

            // Application IDs (2xxx)
            ThreadStarted = 2000,

            // Error IDs (3xxx)
            ErrorJpegDownload = 3000,
            ZipNotFound = 3001,

            // Debug IDs (9xxx)
            DebugGeneric = 9000,
        }

        Thread worker = null;

        public Service1()
        {
            InitializeComponent();

            eventLog1.Source = "JpegHarvest";
            eventLog1.Log = "axis_snap";
            
            if (!System.Diagnostics.EventLog.SourceExists(eventLog1.Source))
            {
                System.Diagnostics.EventLog.CreateEventSource(eventLog1.Source, eventLog1.Log);
            }
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("Service STARTED", 
                EventLogEntryType.SuccessAudit,
                (int)lg_CamEventID.Start);
            
            worker = new Thread(new ThreadStart(snapService.CWorker.harvestThreadAgent));
            worker.Start();
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("Service STOPPED", 
                EventLogEntryType.SuccessAudit, 
                (int)lg_CamEventID.Stop);
            worker.Abort();
        }

        protected override void OnShutdown()
        {
            eventLog1.WriteEntry("Service STOPPED (Shutdown)", 
                EventLogEntryType.SuccessAudit,
                (int)lg_CamEventID.Shutdown);
            worker.Abort();
        }
    } // class Service1
} // namespace WindowsService1

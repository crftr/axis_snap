using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Timers;

namespace snapService
{
    class CWorker
    {
        #region Main working thread

        internal static void harvestThreadAgent()
        {
            log = new System.Diagnostics.EventLog();
            log.Source = "JpegHarvest";
            log.Log = "axis_snap";

            /* SERVICE Config */
            int _sleepInterval = int.Parse(ConfigurationManager.AppSettings["mainThreadSleep_ms"]);

            /* CAMERAS Config */
            _numCameras = int.Parse(ConfigurationManager.AppSettings["numCameras"]);

            /* Capture Parameters */
            _captureIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["captureIntervalInSeconds"]);

            /* LOCAL ARCHIVE Config */
            _localArchiveRoot = ConfigurationManager.AppSettings["localArchiveRoot"];

            /* ZIP Config */
            _zipArchiveDuration_hr = int.Parse(ConfigurationManager.AppSettings["zip_DurationPerArchive_hr"]);

            _cameraJpegPrefix = "AxisCamera";
            _cameraJpegSuffix = "_jpg";
            _cameraLogicalIDPrefix = "AxisCamera";
            _cameraLogicalIDSuffix = "_logicalID";
            _cameraCommonNameSuffix = "_commonName";

            _jpegNamingPrefix = "cam_";

            Timer jpegHarvestTimer = new Timer();
            jpegHarvestTimer.AutoReset = false;
            jpegHarvestTimer.Elapsed += new ElapsedEventHandler(jpegHarvestTimer_Elapsed);
            
            while (true)
            {
                if (!jpegHarvestTimer.Enabled)
                {
                    jpegHarvestTimer.Interval = ((60 - DateTime.Now.Second) % _captureIntervalInSeconds) * 1000;
                    jpegHarvestTimer.Start();
                }

                System.Threading.Thread.Sleep(_sleepInterval);
            }
        }

        #endregion

        #region Timer event handlers

        static void jpegHarvestTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            WebClient oClient = new WebClient();

            string dateString = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss");
            string jpegSourceURL;
            string cameraLogicalID;
            string cameraCommonName;
            string jpegDestFilePath;

            for (int idx = 0; idx < _numCameras; idx++)
            {
                // JPEG Capture

                jpegSourceURL = ConfigurationManager.AppSettings[_cameraJpegPrefix + idx.ToString() + _cameraJpegSuffix];
                cameraLogicalID = ConfigurationManager.AppSettings[_cameraLogicalIDPrefix + idx.ToString() + _cameraLogicalIDSuffix];
                cameraCommonName = ConfigurationManager.AppSettings[_cameraLogicalIDPrefix + idx.ToString() + _cameraCommonNameSuffix];
                jpegDestFilePath = _localArchiveRoot + _jpegNamingPrefix + cameraCommonName + "_" + dateString + ".jpeg";

                try
                {
                    oClient.DownloadFile(jpegSourceURL, jpegDestFilePath);  // 1. Fetch Jpeg from encoder

                    ZipWriter oZipAgent = getZipWriter(idx);                // 2. Store Jpeg in zip (creates zip, if needed)
                    oZipAgent.BeginUpdate();
                    oZipAgent.AddFileUncompressed(jpegDestFilePath);
                    oZipAgent.CommitUpdate();
                    oZipAgent.Close();

                    System.IO.File.Delete(jpegDestFilePath);                // 3. Delete temporary Jpeg
                }
                catch (Exception ex)
                {
                    log.WriteEntry("ERROR (jpegHarvestTimer_Elapsed)\n\n" +
                        "jpegSourceURL = " + jpegSourceURL + "\n\n" +
                        "jpegDestFilePath = " + jpegDestFilePath + "\n\n" +
                        ex.ToString(),
                        EventLogEntryType.Error, 
                        (int)WindowsService1.Service1.lg_CamEventID.ErrorJpegDownload);
                }
            }
        }

        #endregion

        #region Zip Methods

        static ZipWriter getZipWriter(int cameraIndex)
        {
            int zipArchiveStart = DateTime.Now.Hour - (DateTime.Now.Hour % _zipArchiveDuration_hr);
            int zipArchiveStop = zipArchiveStart + _zipArchiveDuration_hr;

            // Zip Naming: E.g. cam_LobbyDoor_2007.12.31_0300-0400.zip

            string zipArchiveName = "cam_" +
                ConfigurationManager.AppSettings["AxisCamera" + cameraIndex + "_commonName"] + "_" +
                DateTime.Now.ToString("yyyy.MM.dd_") +
                zipArchiveStart.ToString().PadLeft(2, '0') + "00-" +
                zipArchiveStop.ToString().PadLeft(2, '0')  + "00.zip";

            return new ZipWriter(_localArchiveRoot + zipArchiveName);
        }

        #endregion

        private static EventLog log;
        private static string _cameraJpegPrefix;
        private static string _cameraJpegSuffix;
        private static string _cameraLogicalIDPrefix;
        private static string _cameraLogicalIDSuffix;
        private static string _cameraCommonNameSuffix;
        private static int _captureIntervalInSeconds;
        private static int _zipArchiveDuration_hr;
        private static string _jpegNamingPrefix;
        private static string _localArchiveRoot;
        private static int _numCameras;
    }

    #region ZIP CLASSES

    public class ZipDataSource : IStaticDataSource
    {
        string filename;

        public ZipDataSource(string filename)
        {
            this.filename = filename;
        }

        public System.IO.Stream GetSource()
        {
            return System.IO.File.OpenRead(filename);
        }
    }

    public class ZipReader
    {
        private ZipFile oZipFile;
        private string pathToZip;

        public ZipReader(string ZipPath)
        {
            this.pathToZip = ZipPath;
            if (!System.IO.File.Exists(this.pathToZip))
            {
                EventLog log = new System.Diagnostics.EventLog();
                log.Source = "JpegHarvest";
                log.Log = "axis_snap";

                log.WriteEntry("ZipReader: " + pathToZip + " is not available.",
                    EventLogEntryType.Warning, 
                    (int)WindowsService1.Service1.lg_CamEventID.ZipNotFound);
            }
            else
            {
                oZipFile = new ZipFile(this.pathToZip);
            }
        }

        public void ExtractZip(string targetDir)
        {
            FastZip oFastZip = new FastZip();
            oFastZip.ExtractZip(this.pathToZip, targetDir, FastZip.Overwrite.Always, null, "", "", true);
        }

        public ZipFile getZipFile() { return oZipFile; }
    }

    public class ZipWriter
    {
        private ZipFile oZipFile;
        private string pathToZip;

        public ZipWriter(string ZipPath)
        {
            this.pathToZip = ZipPath;
            if (!System.IO.File.Exists(this.pathToZip))
            {
                oZipFile = ZipFile.Create(this.pathToZip);
            }
            else
            {
                oZipFile = new ZipFile(this.pathToZip);
            }
        }

        public void AddFileUncompressed(string filePath)
        {
            oZipFile.Add(new ZipDataSource(filePath),
                         Path.GetFileName(filePath),
                         CompressionMethod.Stored,
                         true);
        }

        public void BeginUpdate()  { oZipFile.BeginUpdate();  }
        public void Close()        { oZipFile.Close();        }
        public void CommitUpdate() { oZipFile.CommitUpdate(); }
    }

    #endregion
}

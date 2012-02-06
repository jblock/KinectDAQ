using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Research.Kinect.Audio;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace KinectDAQ
{
    class KinectBackgroundController : INotifyPropertyChanged
    {
        // KinectMicArray class courtesy of Microsoft
        // http://kinectaudioposition.codeplex.com/
        #region Properties
        /// <summary>
        /// PropertyChanged Event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
        // Audio Source Angle and Current Beam Angle
        public double SourceAngleProperty { get { return _sourceAngle; } }
        public double BeamAngleProperty { get { return _beamAngle; } }
        #endregion Properties

        #region Constructors
        // Constructor
        public KinectBackgroundController()
        {
            _sourceAngle = 0.0;
            _beamAngle = 0.0;
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.WorkerSupportsCancellation = true;
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.WorkerReportsProgress = true;
            worker.RunWorkerAsync();

        }
        // Deconstructor
        ~KinectBackgroundController()
        {
            if (worker != null) this.Dispose();
        }

        // Dispose method
        public void Dispose()
        {
            worker.CancelAsync();
            worker.Dispose();
            worker = null;
        }
        #endregion Constructors

        private BackgroundWorker worker = new BackgroundWorker();
        private double _sourceAngle;
        private double _beamAngle;
        private readonly int sleep = 100;

        // Convert radian (Kinect) to degree (WPF)
        private double RadToDeg(double rad)
        {
            return 180.0 * rad / Math.PI;
        }
        // Event handler from Background worker's Progress changed Event
        // NotifyPropertyChnaged cannot be called from BackgrounWorker Thread
        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string s = e.UserState as string;
            if (s == "Beam")
            {
                NotifyPropertyChanged("BeamAngleProperty");
            }
            else
            {
                NotifyPropertyChanged("SourceAngleProperty");
            }
        }
        #region BackgroundWorker
        /// <summary>
        /// Get audio source position angle from Kinect Microphone Array
        /// </summary>
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            using (var source = new KinectAudioSource())
            {
                source.SystemMode = SystemMode.OptibeamArrayOnly;
                source.BeamChanged += audio_BeamChanged;
                //Start capturing audio                               
                using (var audioStream = source.Start())
                {
                    while (worker != null)
                    {
                        if (source.SoundSourcePositionConfidence > 0.75)
                        {
                            _sourceAngle = RadToDeg(source.SoundSourcePosition); // Why does the original source flip this angle?
                            worker.ReportProgress(0, "Source");
                        }
                        Thread.Sleep(sleep);
                    }
                }
            }
        }

        /// <summary>
        /// Get Current Beam Angle 
        /// </summary>
        private void audio_BeamChanged(object sender, BeamChangedEventArgs e)
        {
            _beamAngle = RadToDeg(e.Angle); // We're not using the beam angle information, but this shouldn't be negated
            worker.ReportProgress(0, "Beam");
        }
        #endregion BackgroundWorker
    }
}

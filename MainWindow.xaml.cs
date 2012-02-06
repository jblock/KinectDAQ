using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AForge;
using AForge.Imaging;
using Coding4Fun.Kinect.Wpf;
using Microsoft.Research.Kinect.Nui;

namespace KinectDAQ
{
    /// <summary>
    /// Interaction logic for KinectLocalizerWindow.xaml
    /// </summary>
    public partial class KinectLocalizerWindow : Window
    {
        static Boolean DEBUG = false;
        static int EXPERIMENT_DURATION = 25; //seconds
        #region member variables

        static int MIN_RED_DEFAULT_VALUE = 137;
        static int MAX_RED_DEFAULT_VALUE = 255;

        static int MIN_GREEN_DEFAULT_VALUE = 0;
        static int MAX_GREEN_DEFAULT_VALUE = 105;

        static int MIN_BLUE_DEFAULT_VALUE = 0;
        static int MAX_BLUE_DEFAULT_VALUE = 62;

        IntRange mRedRange;
        IntRange mGreenRange;
        IntRange mBlueRange;
        Blob[] mBlobs;
        Runtime nui;
        AForge.Imaging.Filters.ColorFiltering mRGBFilter;
        AForge.Imaging.Filters.BlobsFiltering blobsFilter;
        AForge.Imaging.BlobCounter bCounter;
        Bitmap image;

        KinectBackgroundController mKinectBackgroundController;
        //ArduinoIO mMotorController; Unnecessary
        ArduinoWorker mWorker;

        String redBlobX = "0";
        String redBlobY = "0";

        Boolean mIsExperimentRunning;
        System.Diagnostics.Stopwatch mWatch;
        StringBuilder mDataSet;
        String[] mDataRow;

        String mFilePath;

        Boolean mIsChangingSliderValues;

        #endregion

        public KinectLocalizerWindow()
        {
            InitializeComponent();
        }

        #region initialization methods
        private void MemberInitialization()
        {
            mIsExperimentRunning = false;
            mIsChangingSliderValues = false;
            mWatch = new System.Diagnostics.Stopwatch();
            mDataRow = new String[6];
            mDataSet = new StringBuilder();
        }

        private void KinectInitialization()
        {
            nui = Runtime.Kinects[0]; // Assumes that there are kinects present
            try
            {
                nui.Initialize(RuntimeOptions.UseColor);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("ERROR");
                return;
            }
            nui.NuiCamera.ElevationAngle = (Camera.ElevationMaximum + Camera.ElevationMinimum) / 2;
            nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_VideoFrameReady);
            nui.VideoStream.Open(ImageStreamType.Video, 2,
                ImageResolution.Resolution640x480, ImageType.Color);

            mKinectBackgroundController = new KinectBackgroundController();
        }

        private void ArduinoControlInitialization()
        {
            mWorker = new ArduinoWorker(4, 19200, DEBUG);
        }

        private void AForgeCVInitialization()
        {
            redSliderMin.Value = MIN_RED_DEFAULT_VALUE;
            redSliderMax.Value = MAX_RED_DEFAULT_VALUE;

            greenSliderMin.Value = MIN_GREEN_DEFAULT_VALUE;
            greenSliderMax.Value = MAX_GREEN_DEFAULT_VALUE;

            blueSliderMin.Value = MIN_BLUE_DEFAULT_VALUE;
            blueSliderMax.Value = MAX_BLUE_DEFAULT_VALUE;

            mRedRange = new IntRange((int)redSliderMin.Value, (int)redSliderMax.Value);
            mGreenRange = new IntRange((int)greenSliderMin.Value, (int)greenSliderMax.Value);
            mBlueRange = new IntRange((int)blueSliderMin.Value, (int)blueSliderMax.Value);

            mRGBFilter = new AForge.Imaging.Filters.ColorFiltering();
            mRGBFilter.Red = mRedRange;
            mRGBFilter.Green = mGreenRange;
            mRGBFilter.Blue = mBlueRange;

            blobsFilter = new AForge.Imaging.Filters.BlobsFiltering();
            blobsFilter.CoupledSizeFiltering = true;
            blobsFilter.MinHeight = 40;
            blobsFilter.MinWidth = 40;
            bCounter = new BlobCounter();
        }

        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MemberInitialization();
            KinectInitialization();
            ArduinoControlInitialization();
            AForgeCVInitialization();
            //mMotorController = new ArduinoIO(4, 19200, DEBUG);
        }

        #region video event
        void nui_VideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            image = AForge.Imaging.Image.Clone(BitmapFromSource(e.ImageFrame.ToBitmapSource()), System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            mRGBFilter.ApplyInPlace(image);
            blobsFilter.ApplyInPlace(image);

            bCounter.ProcessImage(image);
            mBlobs = bCounter.GetObjectsInformation();

            redBlobX = mBlobs.Length > 0 ? mBlobs[0].CenterOfGravity.X.ToString() : "0";
            redBlobY = mBlobs.Length > 0 ? mBlobs[0].CenterOfGravity.Y.ToString() : "0";

            if (!mIsExperimentRunning) // If I was better at multithreading this wouldn't be necessary
            {
                if ((isDisplayingVideo != null && isDisplayingVideo.IsChecked == true) || mIsChangingSliderValues == false)
                {
                    if (image != null) image1.Source = ToBitmapSource(image);
                    unTouchedImage.Source = e.ImageFrame.ToBitmapSource();
                }
                textBox1.Text = (mBlobs.Length != 0) ?
                    (mBlobs.Length.ToString() + " " + mBlobs[0].CenterOfGravity.X.ToString()
                    + "," + mBlobs[0].CenterOfGravity.Y.ToString()) :
                    "none";
            }
            else
            {
                if (mWatch.ElapsedMilliseconds <= EXPERIMENT_DURATION * 1000)
                {
                    //mDataRow[1] = mMotorController.queryArduino().ToString();
                    mDataRow[1] = mWorker.SensorStatusProperty.ToString();
                    mDataRow[2] = mKinectBackgroundController.BeamAngleProperty.ToString();
                    mDataRow[3] = mKinectBackgroundController.SourceAngleProperty.ToString();
                    mDataRow[4] = redBlobX;
                    mDataRow[5] = redBlobY;
                    long time = mWatch.ElapsedMilliseconds;
                    mDataRow[0] = time.ToString();
                    String formattedDataRow = String.Join(",", mDataRow);
                    mStatusDisplay.Text = formattedDataRow;
                    mDataSet.AppendLine(formattedDataRow);
                }
                else
                {
                    mWatch.Reset();
                    mIsExperimentRunning = false;
                    File.WriteAllText(mFilePath, mDataSet.ToString());
                }
                textBox1.Text = "Running";
            }
            image.Dispose();

        }
        #endregion

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            nui.Uninitialize();
            endProgram();
        }

        #region image helper methods
        private Bitmap BitmapFromSource(BitmapSource bitmapSource)
        {
            Bitmap bitmap;
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapSource));
                enc.Save(outStream);
                bitmap = new Bitmap(outStream);
            }
            return bitmap;
        }

        private static BitmapSource ToBitmapSource(System.Drawing.Bitmap src)
        {
            BitmapSource BitSrc = null;
            var hbmp = src.GetHbitmap();
            try
            {
                BitSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                     hbmp,
                     IntPtr.Zero,
                     Int32Rect.Empty,
                     BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Win32Exception)
            {
                BitSrc = null;
            }
            finally
            {
                NativeMethods.DeleteObject(hbmp);
            }
            return BitSrc;
        }

        /// <summary>
        /// FxCop requires all Marshalled functions to be in a class called NativeMethods.
        /// 
        /// Added to improve efficiency in bitmap conversions
        /// </summary>
        internal static class NativeMethods
        {
            [DllImport("gdi32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DeleteObject(IntPtr hObject);
        }
        #endregion

        private void endProgram()
        {
            //mMotorController.closeSerialPort();
            mWorker.ArduinoIOSource.closeSerialPort();
            Environment.Exit(0);
        }

        private void beginExperimentalTrial()
        {
            mFilePath = @"C:\Users\blockja\Dropbox\ME Lab\Repo\KinectAudioDAQ\Data\" + DateTime.Now.ToString("MM-dd-yyyy-HH-mm-ss") + ".csv";
            if (!File.Exists(mFilePath))
            {
                if (mWorker.ArduinoIOSource.checkMotor() != ArduinoIO.MADE_CONNECTION)
                {
                    throw new Exception();
                }
                mDataSet.Clear();
                String[] arr = new String[6];
                mWatch.Start();
            }
        }

        #region wpf events
        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (slidersInitialized() == true)
            {
                checkAndSwapSliderValues(redSliderMin, redSliderMax);
                mRedRange = new IntRange((int)redSliderMin.Value, (int)redSliderMax.Value);

                checkAndSwapSliderValues(greenSliderMin, greenSliderMax);
                mGreenRange = new IntRange((int)greenSliderMin.Value, (int)greenSliderMax.Value);

                checkAndSwapSliderValues(blueSliderMin, blueSliderMax);
                mBlueRange = new IntRange((int)blueSliderMin.Value, (int)blueSliderMax.Value);

                if (mRGBFilter != null)
                {
                    mRGBFilter.Red = mRedRange;
                    mRGBFilter.Green = mGreenRange;
                    mRGBFilter.Blue = mBlueRange;
                }
            }
        }

        private bool slidersInitialized()
        {
            return (redSliderMin != null) && (redSliderMax != null)
                && (blueSliderMin != null) && (blueSliderMax != null)
                && (greenSliderMin != null) && (greenSliderMax != null);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            mIsExperimentRunning = true;
            beginExperimentalTrial();
        }

        private void slider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mIsChangingSliderValues = true;
        }

        private void slider_MouseLeave(object sender, MouseEventArgs e)
        {
            mIsChangingSliderValues = false;
        }

        /// <summary>
        /// Assumes that s1 is the left slider and s2 is the right slider for each color
        /// </summary>
        /// <param name="s1"></param>
        /// <param name="s2"></param>
        private void checkAndSwapSliderValues(Slider s1, Slider s2)
        {
            if (s1.Value > s2.Value)
            {
                double tempValue = s1.Value;
                s1.Value = s2.Value;
                s2.Value = tempValue;
            }
        }

        #endregion
    }
}

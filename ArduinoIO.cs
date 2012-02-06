using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.ComponentModel;

namespace KinectDAQ
{
    class ArduinoIO
    {
        public static int NO_CONNECTION = 128;
        public static int SENT_SUCCESFULLY = 127;
        public static int MADE_CONNECTION = 126;
        private Boolean debugMode;
        public int currentStatus;
        public int comPort;
        public int baudRate;
        public SerialPort serialPort;

        public ArduinoIO(int comPort, int baudRate, Boolean debug)
        {
            this.comPort = comPort;
            this.baudRate = baudRate;
            currentStatus = ArduinoIO.NO_CONNECTION;
            this.debugMode = debug;
        }

        public ArduinoIO(int comPort, int baudRate) : this(comPort, baudRate, false) { }

        public int initializeMotor()
        {
            if (!this.debugMode)
            {
                this.serialPort = new SerialPort("COM" + comPort, baudRate);
                this.serialPort.Open();
                if (!this.serialPort.IsOpen)
                {
                    return ArduinoIO.NO_CONNECTION;
                }
                else
                {
                    this.currentStatus = ArduinoIO.MADE_CONNECTION;
                }
            }
            Console.WriteLine("Motor Controller Connected");
            return ArduinoIO.MADE_CONNECTION;
        }

        public int checkMotor()
        {
            if (!this.debugMode)
            {
                if (this.currentStatus != ArduinoIO.NO_CONNECTION)
                {
                    return this.serialPort.IsOpen ? ArduinoIO.MADE_CONNECTION : ArduinoIO.NO_CONNECTION;
                }
                else
                {
                    return ArduinoIO.NO_CONNECTION;
                }
            }
            else return ArduinoIO.MADE_CONNECTION;
        }

        public void closeSerialPort()
        {
            if (!this.debugMode) serialPort.Close();
        }

        public int queryArduino()
        {
            //currentStatus = serialPort.ReadLine();
            //byte[] arr = { (byte)('4') };
            //serialPort.Write(arr,0,1);
            if (this.debugMode) return 1111111;
            if (int.TryParse(serialPort.ReadLine(), out currentStatus))
            {
                return currentStatus;
            }
            else return ArduinoIO.NO_CONNECTION; // No Status to be Found
        }

        public int sendCommand(String command)
        {
            if (this.debugMode) return ArduinoIO.SENT_SUCCESFULLY;
            if (this.serialPort.IsOpen)
            {
                serialPort.Write(command + '\n');
                return ArduinoIO.SENT_SUCCESFULLY;
            }
            return ArduinoIO.NO_CONNECTION;
        }
    }
    class ArduinoWorker : INotifyPropertyChanged
    {
        private int _sensors;
        private BackgroundWorker worker = new BackgroundWorker();
        public ArduinoIO ArduinoIOSource;
        private int comPort;
        private int baudRate;
        private Boolean debugmode;

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
        public int SensorStatusProperty { get { return _sensors; } }

        public ArduinoWorker(int comPort, int baudRate, Boolean debug)
        {
            this.comPort = comPort;
            this.baudRate = baudRate;
            this.debugmode = debug;

            ArduinoIOSource = new ArduinoIO(this.comPort, this.baudRate, this.debugmode);
            ArduinoIOSource.initializeMotor();
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.WorkerSupportsCancellation = true;
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.WorkerReportsProgress = true;
            worker.RunWorkerAsync();
        }

        public ArduinoWorker(int comPort, int baudRate) : this(comPort,baudRate,false) {}

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (ArduinoIOSource.checkMotor() != ArduinoIO.NO_CONNECTION) {
                while (worker != null)
                {
                    _sensors = ArduinoIOSource.queryArduino();
                    worker.ReportProgress(0, "sensor");
                }
            }
        }
        ~ArduinoWorker()
        {
            if (worker != null) this.Dispose();
        }
        public void Dispose()
        {
            worker.CancelAsync();
            worker.Dispose();
            worker = null;
        }
        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string s = e.UserState as string;
            if (s == "sensor")
            {
                NotifyPropertyChanged("SensorStatusProperty");
            }
        }
    }
}

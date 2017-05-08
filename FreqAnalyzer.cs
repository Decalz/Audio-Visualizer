using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace AVBXR
{
    class FreqAnalyzer
    {
        private bool status;               //check whether the status of the program is enabled or not
        private DispatcherTimer disptimer;         //timer that refreshes the display
        private float[] FFT;               //buffer for fft data
        private ProgBar pb_left, pb_right;         //progressbars for left and right channel intensity
        private WASAPIPROC procdata;        //callback function to obtain data
        private int lastout;             //last output level
        private int lastoutcount;                //last output level counter
        public List<byte> specdata;   //spectrum data buffer
        private Spectrum spectrum;         //spectrum dispay control
        private ComboBox devices;       //device list
        private bool initflag;          //initialized flag
        private int devindex;               //used device index
        private Chart chartspectrum;

        private int linenum = 16;            // number of spectrum lines

        //constructor for the frequency analyzer
        public FreqAnalyzer(ProgBar pleft, ProgBar pright, Spectrum spec, ComboBox devicelist, Chart chart)
        {
            FFT = new float[1024];   
            lastout = 0;
            lastoutcount = 0;
            disptimer = new DispatcherTimer();
            disptimer.Tick += disptimer_Tick;
            disptimer.Interval = TimeSpan.FromMilliseconds(25); //40hz refresh rate = 25, this is the best value
            disptimer.IsEnabled = false;
            pb_left = pleft;                                    //code for left and right channel progressbars
            pb_right = pright;
            pb_left.Minimum(0);
            pb_right.Minimum(0);
            pb_right.Maximum(ushort.MaxValue);
            pb_left.Maximum(ushort.MaxValue);
            procdata = new WASAPIPROC(Process);
            specdata = new List<byte>();
            spectrum = spec;
            chartspectrum = chart;
            devices = devicelist;
            initflag = false;

            chart.Series.Add("wave");                                       //code for chart view
            chart.Series["wave"].ChartType = SeriesChartType.FastLine;
            chart.Series["wave"].ChartArea = "ChartArea1";

            chart.ChartAreas["ChartArea1"].AxisX.MajorGrid.Enabled = false;
            chart.ChartAreas["ChartArea1"].AxisY.MajorGrid.Enabled = false;
            chart.ChartAreas["ChartArea1"].AxisY.Maximum = 255;
            chart.ChartAreas["ChartArea1"].AxisY.Minimum = 0;
            chart.ChartAreas["ChartArea1"].AxisX.Maximum = 64;
            chart.ChartAreas["ChartArea1"].AxisX.Minimum = 0;
            for (int i = 0; i < chart.ChartAreas["ChartArea1"].AxisX.Maximum; i++)
            {
                chart.Series["wave"].Points.Add(0);
            }

            Initialize();
                       
        }


        //flag that enables the display of the program
        public bool Enable_Display { get; set; }

        //flag that enables the functionality of the program
        public bool Func_Enable
        {
            get { return status; }
            set
            {
                status = value;
                if (value)
                {
                    if (!initflag)
                    {
                        var array = (devices.Items[devices.SelectedIndex] as string).Split(' ');
                        devindex = Convert.ToInt32(array[0]);
                        bool result = BassWasapi.BASS_WASAPI_Init(devindex, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, procdata, IntPtr.Zero);
                        if (!result)
                        {
                            var error = Bass.BASS_ErrorGetCode();
                            MessageBox.Show(error.ToString());
                        }
                        else
                        {
                            initflag = true;
                            devices.Enabled = false;
                        }
                    }
                    BassWasapi.BASS_WASAPI_Start();
                }
                else BassWasapi.BASS_WASAPI_Stop(true);
                System.Threading.Thread.Sleep(500);
                disptimer.IsEnabled = value;
            }
        }

        //initialize the tools
        private void Initialize()
        {
            bool result = false;
            for (int i = 0; i < BassWasapi.BASS_WASAPI_GetDeviceCount(); i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device.IsEnabled && device.IsLoopback)
                {
                    devices.Items.Add(string.Format("{0} - {1}", i, device.name));
                }
            }
            devices.SelectedIndex = 0;
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            result = Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            if (!result) throw new Exception("Initialize Error");
        }

        //timer to refresh the dta
        private void disptimer_Tick(object sender, EventArgs e)
        {
            int ret = BassWasapi.BASS_WASAPI_GetData(FFT, (int)BASSData.BASS_DATA_FFT2048);  //get channel fft data//default BASS_DATA_FFT2048
            if (ret < -1) return;
            int x, y;
            int b0 = 0;

            //computes the spectrum data, the code is taken from a bass_wasapi sample.
            for (x = 0; x < linenum; x++)
            {
                float peak = 0;
                int b1 = (int)Math.Pow(2, x * 10.0 / (linenum - 1));
                if (b1 > 1023) b1 = 1023;
                if (b1 <= b0) b1 = b0 + 1;
                for (; b0 < b1; b0++)                       //FFT transformation. Takes all values from the audio and assigns them to different frequencies
                {
                    if (peak < FFT[1 + b0]) peak = FFT[1 + b0];        
                }
                y = (int)(Math.Sqrt(peak) * 3 * 255 - 4);
                if (y > 255) y = 255;
                if (y < 0) y = 0;
                specdata.Add((byte)y);
            }

            if (Enable_Display) spectrum.Set(specdata);
            for (int i = 0; i < specdata.ToArray().Length; i++)
            {
                try
                {
                    chartspectrum.Series["wave"].Points.Add(specdata[i]);
                }
                catch (Exception)
                {
                }
                try
                {
                    chartspectrum.Series["wave"].Points.RemoveAt(0);
                }
                catch (Exception)
                {
                }

            }
            specdata.Clear();


            int level = BassWasapi.BASS_WASAPI_GetLevel();  
            pb_left.Value(Utils.LowWord32(level));
            pb_right.Value(Utils.HighWord32(level));
            if (level == lastout && level != 0) lastoutcount++;
            lastout = level;

            //Required, because some programs hang the output. If the output hangs for a 75ms
            //this piece of code re initializes the output so it doesn't make a gliched sound for long.
            if (lastoutcount >= 3)
            {
                lastoutcount = 0;
                pb_left.Value(0);
                pb_right.Value(0);
                Clean_Up();
                Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                initflag = false;
                Func_Enable = true;
            }

        }

        //required for continuous recording
        private int Process(IntPtr buffer, int length, IntPtr user)
        {
            return length;
        }

        //cleanup
        public void Clean_Up()
        {
            BassWasapi.BASS_WASAPI_Free();
            Bass.BASS_Free();
        }
    }
}

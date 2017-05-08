using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AVBXR
{
    public partial class Form1 : Form
    {
        private string comm;        //command value, used for music controls
        private bool isOpen;        //boolean value to check if 
        FreqAnalyzer analyzer;      //frequency analyzer instance

        [DllImport("winmm.dll")]    //visual studio library for audio streaming
        private static extern long mciSendString(string strCommand, StringBuilder strReturn, int iReturnLength, IntPtr hwndCallBack);  //preset function to stream audio

        public Form1()
        {
            InitializeComponent();

            analyzer = new FreqAnalyzer(progBar1, progBar2, spectrum1, comboBox1, chart1);      //start a FreqAnalyzer class and 
            analyzer.Func_Enable = true;
            analyzer.Enable_Display = true;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog audio = new OpenFileDialog();
            audio.Filter = "MP3 files|*.mp3|All files (*.*)|*.*";
            audio.FilterIndex = 1;

            if (audio.ShowDialog() == DialogResult.OK)
            {
                this.textBox1.Text = audio.FileName.ToString();
            }
        }

        public void Play(bool loop)     //function to start streaming selected audio
        {
            if (isOpen)
            {
                comm = "play MediaFile";
                if (loop)
                {
                    comm += " REPEAT";
                    mciSendString(comm, null, 0, IntPtr.Zero);
                }
            }
        }

        public void OpenPlayer (string fileName) {
            comm = "open \"" + fileName + "\" type mpegvideo alias MediaFile";
            mciSendString(comm, null, 0, IntPtr.Zero);
            isOpen = true;
        }

        public void ClosePlayer()
        {
            comm = "close MediaFile";
            mciSendString(comm, null, 0, IntPtr.Zero);
            isOpen = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                this.OpenPlayer(this.textBox1.Text);
                this.Play(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                this.ClosePlayer();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void Form1_Load(object sender, EventArgs e)         //empty required methods
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
        }

        private void chart2_Click(object sender, EventArgs e)
        {
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)        //activates the Spectrum Display
        {
            elementHost1.Visible = true;
            chart1.Visible = false;
            timer1.Enabled = false;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)        //activates the chart display
        {
            elementHost1.Visible = false;
            chart1.Visible = true;
            timer1.Enabled = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OCR
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            textBox4.Enabled = false;
        }

        static public byte[] GetBytesFromUrl(string url)
        {
            byte[] b;
            HttpWebRequest myReq = (HttpWebRequest)WebRequest.Create(url);
            WebResponse myResp = myReq.GetResponse();

            Stream stream = myResp.GetResponseStream();
            //int i;
            using (BinaryReader br = new BinaryReader(stream))
            {

                b = br.ReadBytes(500000);
                br.Close();
            }
            myResp.Close();
            return b;
        }



        static public void WriteBytesToFile(string fileName, byte[] content)
        {

            FileStream fs = new FileStream(fileName, FileMode.Create);
            BinaryWriter w = new BinaryWriter(fs);
            try
            {
                w.Write(content);
            }
            finally
            {
                fs.Close();
                w.Close();
            }
        }

        /// <summary>
        /// 图像灰度化
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        public static Bitmap ToGray(Bitmap bmp)
        {
            for (int i = 0; i < bmp.Width; i++)
            {
                for (int j = 0; j < bmp.Height; j++)
                {
                    //获取该点的像素的RGB的颜色
                    Color color = bmp.GetPixel(i, j);
                    //利用公式计算灰度值
                    int gray = (int)(color.R * 0.3 + color.G * 0.59 + color.B * 0.11);
                    Color newColor = Color.FromArgb(gray, gray, gray);
                    bmp.SetPixel(i, j, newColor);
                }
            }
            return bmp;
        }

        public bool[] getRoundPixel(Bitmap bitmap, int x, int y)//返回(x,y)周围像素的情况，为黑色，则设置为true
        {
            bool[] pixels = new bool[8];
            Color c;
            int num = 0;
            for (int i = -1; i < 2; i++)
            {
                for (int j = -1; j < 2; j++)
                {
                    c = bitmap.GetPixel(x + i, y + j);
                    if (i != 0 || j != 0)
                    {
                        if (255 == c.G)//因为经过了二值化，所以只要检查RGB中一个属性的值
                        {
                            pixels[num] = false;//为白色，设置为false
                            num++;
                        }
                        else if (0 == c.G)
                        {
                            pixels[num] = true;//为黑色，设置为true
                            num++;
                        }
                    }
                }
            }
            return pixels;
        }

        /// <summary>
        /// 对矩阵M进行中值滤波
        /// </summary>
        /// <param name="m">矩阵M</param>
        /// <param name="windowRadius">过滤半径</param>
        /// <returns>结果矩阵</returns>
        private byte[,] MedianFilterFunction(byte[,] m, int windowRadius)
        {
            int width = m.GetLength(0);
            int height = m.GetLength(1);

            byte[,] lightArray = new byte[width, height];

            //开始滤波
            for (int i = 0; i <= width - 1; i++)
            {
                for (int j = 0; j <= height - 1; j++)
                {
                    //得到过滤窗口矩形
                    Rectangle rectWindow = new Rectangle(i - windowRadius, j - windowRadius, 2 * windowRadius + 1, 2 * windowRadius + 1);
                    if (rectWindow.Left < 0) rectWindow.X = 0;
                    if (rectWindow.Top < 0) rectWindow.Y = 0;
                    if (rectWindow.Right > width - 1) rectWindow.Width = width - 1 - rectWindow.Left;
                    if (rectWindow.Bottom > height - 1) rectWindow.Height = height - 1 - rectWindow.Top;
                    //将窗口中的颜色取到列表中
                    List<byte> windowPixelColorList = new List<byte>();
                    for (int oi = rectWindow.Left; oi <= rectWindow.Right - 1; oi++)
                    {
                        for (int oj = rectWindow.Top; oj <= rectWindow.Bottom - 1; oj++)
                        {
                            windowPixelColorList.Add(m[oi, oj]);
                        }
                    }
                    //排序
                    windowPixelColorList.Sort();
                    //取中值
                    byte middleValue = 0;
                    if ((windowRadius * windowRadius) % 2 == 0)
                    {
                        //如果是偶数
                        middleValue = Convert.ToByte((windowPixelColorList[windowPixelColorList.Count / 2] + windowPixelColorList[windowPixelColorList.Count / 2 - 1]) / 2);
                    }
                    else
                    {
                        //如果是奇数
                        middleValue = windowPixelColorList[(windowPixelColorList.Count - 1) / 2];
                    }
                    //设置为中值
                    lightArray[i, j] = middleValue;
                }
            }
            return lightArray;
        }

        /// <summary>
        /// 中值滤波算法处理
        /// </summary>
        /// <param name="bmp">原始图片</param>
        /// <param name="bmp">是否是彩色位图</param>
        /// <param name="windowRadius">过滤半径</param>
        public Bitmap ColorfulBitmapMedianFilterFunction(Bitmap srcBmp, int windowRadius, bool IsColorfulBitmap)
        {
            if (windowRadius < 1)
            {
                throw new Exception("过滤半径小于1没有意义");
            }
            //创建一个新的位图对象
            Bitmap bmp = new Bitmap(srcBmp.Width, srcBmp.Height);

            //存储该图片所有点的RGB值
            byte[,] mR, mG, mB;
            mR = new byte[srcBmp.Width, srcBmp.Height];
            if (IsColorfulBitmap)
            {
                mG = new byte[srcBmp.Width, srcBmp.Height];
                mB = new byte[srcBmp.Width, srcBmp.Height];
            }
            else
            {
                mG = mR;
                mB = mR;
            }

            for (int i = 0; i <= srcBmp.Width - 1; i++)
            {
                for (int j = 0; j <= srcBmp.Height - 1; j++)
                {
                    mR[i, j] = srcBmp.GetPixel(i, j).R;
                    if (IsColorfulBitmap)
                    {
                        mG[i, j] = srcBmp.GetPixel(i, j).G;
                        mB[i, j] = srcBmp.GetPixel(i, j).B;
                    }
                }
            }

            mR = MedianFilterFunction(mR, windowRadius);
            if (IsColorfulBitmap)
            {
                mG = MedianFilterFunction(mG, windowRadius);
                mB = MedianFilterFunction(mB, windowRadius);
            }
            else
            {
                mG = mR;
                mB = mR;
            }
            for (int i = 0; i <= bmp.Width - 1; i++)
            {
                for (int j = 0; j <= bmp.Height - 1; j++)
                {
                    bmp.SetPixel(i, j, Color.FromArgb(mR[i, j], mG[i, j], mB[i, j]));
                }
            }
            return bmp;
        }

        /// <summary>
        /// 图像二值化1：取图片的平均灰度作为阈值，低于该值的全都为0，高于该值的全都为255
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        public static Bitmap ConvertTo1Bpp1(Bitmap bmp)
        {
            int average = 0;
            for (int i = 0; i < bmp.Width; i++)
            {
                for (int j = 0; j < bmp.Height; j++)
                {
                    Color color = bmp.GetPixel(i, j);
                    average += color.B;
                }
            }
            average = (int)average / (bmp.Width * bmp.Height);

            for (int i = 0; i < bmp.Width; i++)
            {
                for (int j = 0; j < bmp.Height; j++)
                {
                    //获取该点的像素的RGB的颜色
                    Color color = bmp.GetPixel(i, j);
                    int value = 255 - color.B;
                    Color newColor = value > average ? Color.FromArgb(0, 0, 0) : Color.FromArgb(255,

255, 255);
                    bmp.SetPixel(i, j, newColor);
                }
            }
            return bmp;
        }

        public Boolean tesseract()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            System.Diagnostics.Process p = new Process();
            p.StartInfo.FileName = @"C:\WINDOWS\system32\cmd.exe ";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.StandardInput.WriteLine("\"C:\\Program Files(x86)\\Tesseract - OCR\tesseract.exe \"");
            p.StandardInput.WriteLine("tesseract \"" + path + @"\bmp.jpg" + "\" \"" + path + "\\output\"" + " -l chi_sim");
            p.StandardInput.WriteLine("exit");
            p.WaitForExit();
            p.Close();
            p.Dispose();
            return true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            pictureBox1.Image = null;
            pictureBox2.Image = null;
            pictureBox3.Image = null;
            pictureBox4.Image = null;
            Bitmap bt = null;
            Bitmap bmp = null;
            Image img = null;
            Boolean flag = true;
            string file = null;
            OpenFileDialog dialog = null;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if(!textBox2.Text.Equals("")) {
                try
                {
                    img = System.Drawing.Image.FromFile(textBox2.Text);
                }
                catch {
                    flag = false;
                }
                file = textBox2.Text;
            }
            
            if(!flag) { dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    file = dialog.FileName;
                }
                else
                {   
                    textBox1.Clear();
                }
            }
            try
            {
                img = System.Drawing.Image.FromFile(file);
                bt = new System.Drawing.Bitmap(img);
                bmp = new Bitmap(bt);
                pictureBox1.Image = bmp;   
            }
            catch
            {
            }
            if (checkBox1.Checked == true)
            {
                try {
                    img = System.Drawing.Image.FromFile(file);
                    bt = new System.Drawing.Bitmap(img);
                    Bitmap bt1 = new Bitmap(ToGray(bt));
                    pictureBox2.Image = bt1;
                    img.Dispose();
                }
                catch{}
            }
            if (checkBox2.Checked == true)
            {
                int a = 0;
                try { a = int.Parse(textBox4.Text); }
                catch { MessageBox.Show("滤波半径必须是大于1的整数"); }
                Bitmap bt2;
                bt = ColorfulBitmapMedianFilterFunction(bt, a, false);
                bt2 = new Bitmap(bt);
                pictureBox3.Image = bt2;
            }
            if (checkBox3.Checked == true)
            {
                Bitmap bt3;
                bt = ConvertTo1Bpp1(bt);
                bt3 = new Bitmap(bt);
                pictureBox4.Image = bt3;
            }
            try { bt.Save(path + @"\bmp.jpg", ImageFormat.Jpeg); } catch { }
           
            textBox1.Clear();
            if (!tesseract())
                MessageBox.Show("OCR Failed");
            else
            {
                StreamReader sr = new StreamReader(path + @"\output.txt", Encoding.UTF8);
                string line = "";
                Boolean a = false;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line != "" && line != " ")
                    {
                        textBox1.AppendText(line);
                        textBox1.AppendText("\n");
                        a = true;
                    }
                }
                if (!a) textBox1.AppendText("呃，照片太调皮，什么也没读出来~");
                sr.Close();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Clipboard.SetData(DataFormats.Text, textBox1.Text);
            MessageBox.Show("复制成功！");
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked == true) 
                textBox4.Enabled = true; 
            if (checkBox2.Checked == false) 
                textBox4.Enabled = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string line;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string url = string.Format(textBox3.Text);

            try
            {
                WriteBytesToFile(path + @"\img.bmp", GetBytesFromUrl(url));
                System.Drawing.Image img = System.Drawing.Image.FromFile(path + @"\img.bmp");
                System.Drawing.Image bmp = new System.Drawing.Bitmap(img);
                pictureBox1.Image = bmp;
                img.Dispose();
            }
            catch { MessageBox.Show("无法识别的URL"); }
            {
                pictureBox1.Visible = true;
                System.Diagnostics.Process p = new Process();
                p.StartInfo.FileName = @"C:\WINDOWS\system32\cmd.exe ";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.StandardInput.WriteLine("\"C:\\Program Files(x86)\\Tesseract - OCR\tesseract.exe \"");
                p.StandardInput.WriteLine("tesseract \"" + path + @"\img.bmp" + "\" \"" + path + "\\output\"" + " -l chi_sim");
                p.StandardInput.WriteLine("exit");
                p.WaitForExit();
                p.Close();
                p.Dispose();
                StreamReader sr = new StreamReader(path + @"\output.txt", Encoding.UTF8);
                line = "";
                while ((line = sr.ReadLine()) != null)
                {
                    if (line != "" && line != " ")
                        textBox1.AppendText(line);
                    textBox1.AppendText("\n");
                }
                sr.Close();
            }
        }
    }
}

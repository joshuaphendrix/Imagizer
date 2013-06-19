using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace Imagizer
{
    public partial class Form1 : Form
    {
        ImageBeast beast;
        Timer statusChecker;

        public Form1()
        {
            InitializeComponent();

            statusChecker = new Timer();
            statusChecker.Interval = 1000;
            statusChecker.Enabled = false;
            statusChecker.Tick += new EventHandler(statusChecker_Tick);
        }

        private void statusChecker_Tick(object sender, EventArgs e)
        {
            pbStatus.Value = beast.count;

            pbStatus.Visible = !beast.isDone;
            statusChecker.Enabled = !beast.isDone;

            if(beast.isDone)
            {
                this.lstFiles.Items.Clear();

                if(beast.unProcessed.Count>0)
                {
                    foreach(string s in beast.unProcessed)
                        this.lstFiles.Items.Add(s);

                    MessageBox.Show("Some files could not be processed at this time.  Feel free to try again");
                }
            }
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            Object width;
            Object height;

            pbStatus.Minimum = 0;
            pbStatus.Maximum = this.lstFiles.Items.Count;

            string[] files = new string[this.lstFiles.Items.Count]; 
            this.lstFiles.Items.CopyTo(files,0);

            if(rdoPercent.Checked)
            {
                width = Double.Parse(this.txtWidth.Text)/100;
                height = Double.Parse(this.txtHeight.Text)/100;

                beast = new ImageBeast(files,this.txtExportTo.Text,(Double)width,(Double)height);
                beast.start();
            }else
            {
                width = Int32.Parse(this.txtWidth.Text);
                height = Int32.Parse(this.txtHeight.Text);

                beast = new ImageBeast(files,this.txtExportTo.Text,(Int32)width,(Int32)height);
                beast.start();
            }

            statusChecker.Enabled = true;
        }

        #region "Helper Function"

        private void panel1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop,false);

            System.Collections.Specialized.StringCollection sc = new System.Collections.Specialized.StringCollection();

            foreach(string s in files)
                foreach(string t in getFiles(s,this.chkSubFolders.Checked))
                    sc.Add(t);

            foreach(string s in sc)
            {
                foreach(string filter in this.txtFilter.Text.Split(",".ToCharArray(),StringSplitOptions.RemoveEmptyEntries))
                {
                    if(s.Trim().ToLower().Contains(filter))
                    {
                        this.lstFiles.Items.Add(s);
                        break;
                    }
                }
                
            }
        }
        private void panel1_DragEnter(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
		        e.Effect = DragDropEffects.All;
	        else
		        e.Effect = DragDropEffects.None;
        }

        private System.Collections.Generic.List<string> getFiles(string location, bool recursive)
        {
            System.Collections.Generic.List<string>  sc = new List<string>();

            System.IO.FileInfo fi = new System.IO.FileInfo(location);

            if(fi.Exists)
            {
                sc.Add(fi.FullName);
                return sc;
            }

            System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(location);

            if(di.Exists)
            {
                foreach(System.IO.FileInfo f in di.GetFiles())
                {
                    foreach(string s in getFiles(f.FullName,recursive))
                        sc.Add(s);
                }
                
                if(recursive)
                {
                    foreach(System.IO.DirectoryInfo d in di.GetDirectories())
                    {
                        foreach(string s in getFiles(d.FullName,recursive))
                            sc.Add(s);
                    }
                }
            }

            return sc;

        }

        public class ImageBeast
        {
            private string[] filesToProcess;
            private System.Collections.Specialized.StringCollection failedFiles; public System.Collections.Specialized.StringCollection unProcessed{get{return failedFiles;}}

            private int _count; public int count{get{return _count;}}
            private string _destDir;
            private object width;
            private object height;
            private bool scale;
            private bool keepGoing; public bool isDone{get{return !keepGoing;}}

            public ImageBeast(string[] files, string destDir, int width, int height)
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(destDir);
                if(!di.Exists) createDir(di);

                _destDir = di.FullName + "\\";

                filesToProcess = files;
                _count = 0;;
                scale = false;
                this.width = width;
                this.height = height;

                keepGoing = true;

                failedFiles = new System.Collections.Specialized.StringCollection();
            }
            public ImageBeast(string[] files, string destDir, double width, double height)
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(destDir);
                if(!di.Exists) createDir(di);

                _destDir = di.FullName + "\\";

                filesToProcess = files;
                _count = 0;
                scale = true;
                this.width = width;
                this.height = height;

                keepGoing = true;

                failedFiles = new System.Collections.Specialized.StringCollection();
            }

            public void start()
            {
                System.Threading.ThreadStart ts = new System.Threading.ThreadStart(run);
                System.Threading.Thread t = new System.Threading.Thread(ts);

                t.Start();
            }
            public void stop()
            {
                keepGoing = false;
            }
            private void run()
            {
                foreach(string s in filesToProcess)
                {
                    if(!keepGoing)
                        break;

                    System.IO.FileInfo fi = new System.IO.FileInfo(s);
                    Image thumb;
                    if(scale)
                    {
                        thumb = GenerateThumbNail(s,(Double)width,(Double)height);
                    }else
                    {
                        thumb = GenerateThumbNail(s,(Int32)width,(Int32)height);
                    }

                    if(thumb != null)
                    {
                        System.IO.FileInfo f = new System.IO.FileInfo(_destDir+fi.LastWriteTime.Year.ToString()+"\\"+fi.Name);
                        if(!f.Directory.Exists) createDir(f.Directory);
                        thumb.Save(f.FullName,ImageFormat.Jpeg);
                        f.CreationTime = fi.LastWriteTime;
                    }else
                    {
                        failedFiles.Add(fi.FullName);
                    }

                    _count ++;
                    if(count%100 == 0)
                    {
                        System.Threading.Thread.Sleep(10);
                        GC.Collect();
                    }
                }

                keepGoing = false;
            }

            private Image GenerateThumbNail(string fileName,double width, double height)
            {
                try
                {
                    Image i = Image.FromFile(fileName);
                    width = (double)i.Width * width;
                    height = (double)i.Height * height;
                    i.Dispose();
                    return GenerateThumbNail(fileName,(Int32)width, (Int32)height);
                }catch(Exception ex){return null;}
            }
            private Image GenerateThumbNail(string fileName,int width, int height)
            {
                try
                {
                    System.Drawing.Image img = System.Drawing.Image.FromFile(fileName); 
                    System.Drawing.Image thumbNail = new Bitmap(width,height,img.PixelFormat);

                    Graphics g = Graphics.FromImage(thumbNail);
                    g.CompositingMode    = CompositingMode.SourceCopy;
                    g.CompositingQuality = CompositingQuality.HighSpeed;
                    g.SmoothingMode      = SmoothingMode.HighSpeed;
                    g.InterpolationMode  = InterpolationMode.HighQualityBicubic;

                    Rectangle r = new Rectangle(0, 0, width, height);
                    g.DrawImage(img, r);

                    g.Dispose();
                    img.Dispose();
                    return thumbNail;
                }
                catch (Exception ex) {return null;}
            }

            private static void createDir(System.IO.DirectoryInfo di)
            {
                if(!di.Parent.Exists)
                    createDir(di.Parent);

                if(!di.Exists)
                {
                    di.Create();
                }
            }
        }
        #endregion

        private void btnClear_Click(object sender, EventArgs e)
        {
            this.lstFiles.Items.Clear();
        }
    }
}
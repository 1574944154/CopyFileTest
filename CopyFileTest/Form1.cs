using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace CopyFileTest
{
    public delegate void UpdateProgressValueEvent();

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }

        private void updateProgress1Value()
        {
            progressBar1.Value++;
            label5.Text = progressBar1.Value.ToString() + "/" + progressBar1.Maximum.ToString();
        }

        private void updateProgress2Value()
        {
            lock (progressBar2)
            {
                progressBar2.Value++;
            }
            label6.Text = progressBar2.Value.ToString() + "/" + progressBar2.Maximum.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择文件路径";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                btn_fromdir.Text = dialog.SelectedPath;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择文件路径";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                btn_todir.Text = dialog.SelectedPath;
            }
        }

        private void btn_start_Click(object sender, EventArgs e)
        {
            int fileNum = scanFileNum(btn_fromdir.Text);
            progressBar1.Maximum = fileNum;
            progressBar2.Maximum = fileNum;
            progressBar1.Value = 0;
            progressBar2.Value = 0;
            sw.Restart();   //重置计时器

            UpdateProgressValueEvent updateProValue1 = new UpdateProgressValueEvent(updateProgress1Value);
            UpdateProgressValueEvent updateProValue2 = new UpdateProgressValueEvent(updateProgress2Value);

            cst = new CopySingleThread(btn_fromdir.Text, (btn_todir.Text+"\\copySingle"));
            cmt = new CopyMultiThread(btn_fromdir.Text, (btn_todir.Text + "\\copyMulti"));
            cmt.OnUpdateProgressValue += new UpdateProgressValueEvent(updateProgress2Value);
            cst.OnUpdateProgressValue += new UpdateProgressValueEvent(updateProgress1Value);

            Thread thread0 = new Thread(new ThreadStart(cst.startCopy)); 
            Thread thread1 = new Thread(new ThreadStart(cmt.startCopy));
            thread0.IsBackground = true;
            thread1.IsBackground = true;
            thread0.Start();
            thread1.Start();
        }

        public static int scanFileNum(string fileDir)
        {
            int num = 0;
            string[] files = Directory.GetFiles(fileDir);
            string[] fromDirs = Directory.GetDirectories(fileDir);
            foreach (string fromDir in fromDirs)
            {
                num = num + scanFileNum(fromDir);
            }
            return num + files.Length;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            sw = new Stopwatch();  //计时
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if(progressBar1.Value!=progressBar1.Maximum)
            {
                label7.Text = sw.Elapsed.ToString().Split(':')[2];
            }
            if(progressBar2.Value!=progressBar2.Maximum)
            {
                label8.Text = sw.Elapsed.ToString().Split(':')[2];
            }
        }

        private Stopwatch sw;  //计时
        private CopyMultiThread cmt;
        private CopySingleThread cst;
    }

    public class CopyMultiThread
    {
        public event UpdateProgressValueEvent OnUpdateProgressValue;

        public CopyMultiThread(string fromDir, string toDir)
        {
            IsScanComplete = false;
            this.fromDir = fromDir;
            this.toDir = toDir;
            Tasks = new ConcurrentQueue<string[]>();

        }

        public void startCopy()
        {
            Thread thread0 = new Thread(new ThreadStart(createDirThenEnqueue));
            thread0.IsBackground = true;
            thread0.Start();

            for(int i=0;i<20;i++)
            {
                ThreadPool.QueueUserWorkItem(copyFileTask);
            }
        }

        private void createDirThenEnqueue()
        {
            createDirThenEnqueue(fromDir, toDir);
            IsScanComplete = true;
        }

        private void createDirThenEnqueue(string fromDir, string toDir)
        {
            if(!Directory.Exists(toDir))
            {
                Directory.CreateDirectory(toDir);
            }
            string[] files = Directory.GetFiles(fromDir);

            foreach(string file in files)
            {
                string filename = Path.GetFileName(file);
                string toFilename = Path.Combine(toDir, filename);

                string[] task = { file, toFilename };
                Tasks.Enqueue(task);
            }

            string[] fromDirs = Directory.GetDirectories(fromDir);
            foreach(string fromDirName in fromDirs)
            {
                string dirName = Path.GetFileName(fromDirName);
                string toDirName = Path.Combine(toDir, dirName);
                createDirThenEnqueue(fromDirName, toDirName);
            }
        }

        private void copyFileTask(object state)
        {
            while(true)
            {
                if(Tasks.Count==0 && IsScanComplete==true)
                {
                    
                    return;
                }
                string[] task;
                bool isFalse = Tasks.TryDequeue(out task);
                if(!isFalse)
                {
                    continue;
                }
                else
                {
                    File.Copy(task[0], task[1]);
                    if(OnUpdateProgressValue!=null)
                    {
                        OnUpdateProgressValue();
                    }
                }
            }
        }

        private string fromDir;
        private string toDir;
        private bool IsScanComplete;
        private ConcurrentQueue<string[]> Tasks;
    }

    public class CopySingleThread
    {
        public event UpdateProgressValueEvent OnUpdateProgressValue;

        public CopySingleThread(string fromDir, string toDir)
        {
            this.fromDir = fromDir;
            this.toDir = toDir;
        }

        public void startCopy()
        {
            copyRecursion(fromDir, toDir);
        }

        private void copyRecursion(string fromDir, string toDir)  //递归复制
        {
            if (!Directory.Exists(toDir))
            {
                Directory.CreateDirectory(toDir);
            }

            string[] files = Directory.GetFiles(fromDir);
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                string toFilename = Path.Combine(toDir, filename);
                File.Copy(file, toFilename);
                if(OnUpdateProgressValue!=null)
                {
                    OnUpdateProgressValue();
                }
                
            }
            string[] fromDirs = Directory.GetDirectories(fromDir);
            foreach (string fromDirName in fromDirs)
            {
                string dirName = Path.GetFileName(fromDirName);
                string toDirName = Path.Combine(toDir, dirName);
                copyRecursion(fromDirName, toDirName);
            }
        }

        private string fromDir;
        private string toDir;
    }
}

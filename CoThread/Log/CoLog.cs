﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dcjet.Framework.X.Log
{
    public class CoLog : IDLog
    {
        private static bool? isOn;
        /// <summary>
        /// 是否停掉记录日志
        /// </summary>
        public static bool IsOn
        {
            get
            {
                if (isOn.HasValue == false)
                {
                    isOn = config.isOn;
                }
                return isOn.Value;
            }
            set
            {
                isOn = value;
            }
        }

        private static bool? isArchiveOn;
        /// <summary>
        /// 是否启用日志归档
        /// </summary>
        public static bool IsArchiveOn
        {
            get
            {
                if (isArchiveOn.HasValue == false)
                {
                    isArchiveOn = config.isAchiveOn;
                }
                return isArchiveOn.Value;
            }

            set
            {
                isArchiveOn = value;
            }
        }

        private static StringBuilder sb = new StringBuilder();

        /// <summary>
        /// 查看缓存中的日志内容
        /// </summary>
        public static string LogContent { get { return sb.ToString(); } }

        private static object lockObj = new object();

        private static Thread thread;

        private static DateTime? nextRunTime = null;

        private static TinyLogConfig config = new TinyLogConfig();


        //错误次数
        private static int errorCount = 0;

        private static int archiveErrorCount = 0;

        static CoLog()
        {
            //变更区域 初始化10000
            sb = new StringBuilder(10000);
        }

        private string logID;

        //随机因子
        private static int factor = 100;

        public string LogID
        {
            get
            {
                if (logID == null)
                    logID = DateTime.Now.ToString("yyyyMMddhhmmssfff" + new Random(DateTime.Now.Millisecond + factor++).Next(100, 999));
                return logID;
            }
        }



        public void Init(string logName, string logFolderName)
        {
            config.logFileName = logName;
            if (string.IsNullOrEmpty(logFolderName) == false)
                config.logPath = logFolderName;
        }

        public void Error(string strInfo)
        {
            InnerWrite("ERROR", strInfo ?? "", LogID);
        }

        public void Debug(string strInfo)
        {
            InnerWrite("DEBUG", strInfo ?? "", LogID);
        }

        public void Fatal(string strInfo)
        {
            InnerWrite("FATAL", strInfo ?? "", LogID);
        }

        public void Info(string strInfo)
        {
            InnerWrite("INFO", strInfo ?? "", LogID);
        }

        public void Warn(string strInfo)
        {
            InnerWrite("WARN", strInfo ?? "", LogID);
        }

        private static void InnerWrite(string head, string info, string logId)
        {
            //日志关闭时，不写入日志
            if (!IsOn)
                return;
            lock (lockObj)
            {
                if (sb.Length < config.cacheLogMaxLength)
                {
                    sb.AppendLine("线程号[" + logId + "]"+string.Format(config.formatString, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), head, info));
                    if (sb.Length > config.cacheLogLength)
                    {
                        nextRunTime = DateTime.Now;
                    }
                }
                else
                {
                    nextRunTime = DateTime.Now;
                }

                //首次启动
                if (thread == null)
                {
                    thread = new Thread(new ThreadStart(writeSb2File));
                    //设置为后台线程
                    thread.IsBackground = true;
                    thread.Start();
                }
            }
        }

        private static void writeSb2File()
        {
            while (true)
            {
                if (IsOn && sb.Length > 0)
                {
                    try
                    {
                        if (nextRunTime.HasValue == false)
                        {
                            nextRunTime = DateTime.Now.AddMilliseconds(config.refreshInternal);
                        }
                        if (DateTime.Now > nextRunTime)
                        {
                            FileSave();
                            nextRunTime = DateTime.Now.AddMilliseconds(config.refreshInternal);
                        }
                    }
                    catch (Exception ex)
                    {
                        InnerWrite("innerFatal", "日志数据写入到文件错误:" + ex.ToString(), "");
                        innerError();
                    }
                }
                Thread.Sleep(config.unitInternal);
            }
        }

        private static void FileSave()
        {
            string filePath = null;
            var strSb = "";
            //获取文件路径
            if (Path.IsPathRooted(config.logPath) == false)
            {
                filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.logPath, config.logFileName);
            }
            else
            {
                filePath = Path.Combine(config.logPath, config.logFileName);
            }
            if (Directory.Exists(Path.GetDirectoryName(filePath)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            //文件是否存在
            if (File.Exists(filePath))
            {
                //判断旧文件大小，如果超过就存放到.old中
                FileInfo fi = new FileInfo(filePath);
                if (fi.Length > config.fileMaxLength)
                {
                    if (File.Exists(filePath + ".old"))
                    {
                        File.Delete(filePath + ".old");
                    }
                    fi.MoveTo(filePath + ".old");
                    if (IsArchiveOn)
                    {
                        var athread = new Thread(new ThreadStart(achive));
                        //设置为后台线程
                        athread.IsBackground = true;
                        athread.Start();
                    }
                    //重新写入到文件
                    lock (lockObj)
                    {
                        strSb = sb.ToString();
                        sb.Clear();
                    }
                    File.WriteAllText(filePath, strSb, Encoding.UTF8);

                }
                else
                {
                    lock (lockObj)
                    {
                        strSb = sb.ToString();
                        sb.Clear();
                    }
                    File.AppendAllText(filePath, strSb, Encoding.UTF8);
                }
            }
            else
            {
                lock (lockObj)
                {
                    strSb = sb.ToString();
                    sb.Clear();
                }
                File.WriteAllText(filePath, strSb, Encoding.UTF8);
            }

        }

        /// <summary>
        /// 归档方法
        /// </summary>
        private static void achive()
        {
            if (IsArchiveOn)
            {
                try
                {
                    string filePath = null;
                    //获取文件路径
                    if (Path.IsPathRooted(config.logPath) == false)
                    {
                        filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.logPath, config.logFileName);
                    }
                    else
                    {
                        filePath = Path.Combine(config.logPath, config.logFileName);
                    }
                    var dir = Path.Combine(Path.GetDirectoryName(filePath), "archive");
                    //创建归档目录
                    if (Directory.Exists(dir) == false)
                    {
                        Directory.CreateDirectory(dir);
                    }
                    //将当前old文件归档保存到归档库内
                    var srcFile = filePath + ".old";
                    var tgtFile = Path.Combine(dir, Path.GetFileName(filePath) + ".old." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip");
                    Helpers.GZipHelper.Compress(srcFile, tgtFile);
                    File.Delete(srcFile);

                    //归档最大目录大小
                    if (GetDirSize(new DirectoryInfo(dir)) > config.archiveDirMaxLength)
                    {
                        ClearDir(dir);
                    }
                }
                catch (Exception ex)
                {
                    InnerWrite("innerFatal", "归档现场出错:" + ex.ToString(), "");
                    innerAchiveError();
                }

            }
        }

        /// <summary>
        /// 内部错误
        /// </summary>
        private static void innerError()
        {
            errorCount++;
            if (errorCount > config.errorRetry)
            {
                InnerWrite("innerFatal", "内部出错次数为" + errorCount + " 并已经重置","");
                IsOn = false;
                errorCount = 0;
            }
        }

        /// <summary>
        /// 归档的内部错误处理
        /// </summary>
        private static void innerAchiveError()
        {
            archiveErrorCount++;
            if (archiveErrorCount > config.errorRetry)
            {
                InnerWrite("innerFatal", "归档内部出错次数为" + archiveErrorCount + " 并已经重置","");
                IsArchiveOn = false;
                archiveErrorCount = 0;
            }
        }

        /// <summary>
        /// 获取目录文件总大小
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static long GetDirSize(DirectoryInfo d)
        {
            long Size = 0;
            // 所有文件大小.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                Size += fi.Length;
            }
            // 遍历出当前目录的所有文件夹.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                Size += GetDirSize(di);   //这就用到递归了，调用父方法,注意，这里并不是直接返回值，而是调用父返回来的
            }
            return (Size);
        }

        /// <summary>
        /// 清空目录
        /// </summary>
        /// <param name="srcPath"></param>
        public static void ClearDir(string srcPath)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(srcPath);
                FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  //返回目录中所有文件和子目录
                foreach (FileSystemInfo i in fileinfo)
                {
                    if (i is DirectoryInfo)            //判断是否文件夹
                    {
                        DirectoryInfo subdir = new DirectoryInfo(i.FullName);
                        subdir.Delete(true);          //删除子目录和文件
                    }
                    else
                    {
                        File.Delete(i.FullName);      //删除指定文件
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }

    /// <summary>
    ///  日志配置类，外部修改配置，请在这里扩展
    /// </summary>
    internal class TinyLogConfig
    {
        //是否开启日志
        public bool isOn = true;

        //是否开启归档
        public bool isAchiveOn = true;

        //刷新间隔 10sec刷一次
        public int refreshInternal = 10000;

        //最快落文件间隔
        public int unitInternal = 5000;

        //日志缓存建议长度 64KB×2
        public int cacheLogLength = 65536;

        //最大缓存大小，大于这个不在记录
        public int cacheLogMaxLength = 524288;

        //日志名
        public string logFileName = "default.log";

        //日志路径
        public string logPath = "TinyLogs";

        //文件最大大小 最大10M
        public long fileMaxLength = 10485760;

        //内部错误次数，默认为3，超过该次数，日志自动降级
        public int errorRetry = 3;

        //日志格式
        public string formatString = "[时间：{0}级别：{1} 日志内容]：{2}";

        #region 归档部分


        //归档文件夹最大大小 默认100M
        public long archiveDirMaxLength = 104857600;

        #endregion

    }
}

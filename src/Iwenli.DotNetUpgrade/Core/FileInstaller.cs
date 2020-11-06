using Iwenli.DotNetUpgrade.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Iwenli.DotNetUpgrade.Core
{
    /// <summary>
    /// 更新文件安装工作类
    /// </summary>
    public class FileInstaller
    {
        /// <summary>
        /// 文件操作事件类
        /// </summary>

        /// <summary>
        /// 创建一个 <see cref="FileInstaller"/> 的新对象
        /// </summary>
        public FileInstaller()
        {
            PreservedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 安装文件
        /// </summary>
        public bool Install(BackgroundWorker sender, DoWorkEventArgs e)
        {
            if (!DeletePreviousFile(sender, e))
            {
                RollbackFiles(sender, e);
                return false;
            }
            DeleteEmptyDirectories();

            if (!InstallFiles(sender, e))
            {
                DeleteInstalledFiles();
                RollbackFiles(sender, e);

                return false;
            }

            return true;
        }

        #region 属性

        /// <summary>
        /// 获得或设置更新的信息
        /// </summary>
        public UpdateMeta UpdateMeta { get; set; }

        /// <summary>
        /// 获得或设置当前更新程序所工作的目录
        /// </summary>
        public string WorkingRoot { get; set; }

        string _applicationRoot;
        /// <summary>
        /// 获得或设置应用程序目录
        /// </summary>
        public string ApplicationRoot
        {
            get
            {
                return _applicationRoot;
            }
            set
            {
                _applicationRoot = value;
                if (!_applicationRoot.EndsWith(@"\")) _applicationRoot += @"\";
            }
        }

        string _sourceFolder;
        /// <summary>
        /// 安装的源文件夹
        /// </summary>
        public string SourceFolder
        {
            get
            {
                return _sourceFolder;
            }
            set
            {
                _sourceFolder = value;
                if (!_sourceFolder.EndsWith(@"\")) _sourceFolder += @"\";
            }
        }

        /// <summary>
        /// 获得还原路径
        /// </summary>
        string RollbackPath
        {
            get
            {
                return Path.Combine(WorkingRoot, "rollback");
            }
        }


        /// <summary>
        /// 获得在安装过程中要保留的文件
        /// </summary>
        public Dictionary<string, string> PreservedFiles { get; private set; }


        #endregion

        #region 私有变量

        /// <summary>
        /// 备份文件
        /// </summary>
        readonly List<string> _bakList = new List<string>();
        readonly List<string> _installedFile = new List<string>();


        #endregion

        #region 事件

        /// <summary> 获得安装过程中发生的错误 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public Exception Exception { get; private set; }

        /// <summary>
        /// 开始删除文件
        /// </summary>
        public event EventHandler DeleteFileStart;

        /// <summary>
        /// 引发 <see cref="DeleteFileStart" /> 事件
        /// </summary>
        protected virtual void OnDeleteFileStart()
        {
            var handler = DeleteFileStart;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// 删除文件完成事件
        /// </summary>
        public event EventHandler DeleteFileFinished;

        /// <summary>
        /// 引发 <see cref="DeleteFileFinished" /> 事件
        /// </summary>
        protected virtual void OnDeleteFileFinished()
        {
            var handler = DeleteFileFinished;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// 删除文件事件
        /// </summary>
        public event EventHandler<InstallFileEventArgs> DeleteFile;

        /// <summary>
        /// 引发 <see cref="DeleteFile" /> 事件
        /// </summary>
        protected virtual void OnDeleteFile(InstallFileEventArgs e)
        {
            DeleteFile?.Invoke(this, e);
        }



        /// <summary>
        /// 开始安装文件事件
        /// </summary>
        public event EventHandler InstallFileStart;

        /// <summary>
        /// 引发 <see cref="InstallFileStart" /> 事件
        /// </summary>
        protected virtual void OnInstallFileStart()
        {
            InstallFileStart?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 完成安装文件事件
        /// </summary>
        public event EventHandler InstallFileFinished;

        /// <summary>
        /// 引发 <see cref="InstallFileFinished" /> 事件
        /// </summary>
        protected virtual void OnInstallFileFinished()
        {
            InstallFileFinished?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 安装文件事件
        /// </summary>
        public event EventHandler<InstallFileEventArgs> InstallFile;

        /// <summary>
        /// 引发 <see cref="InstallFile" /> 事件
        /// </summary>
        protected virtual void OnInstallFile(InstallFileEventArgs e)
        {
            InstallFile.Invoke(this, e);
        }

        /// <summary>
        /// 回滚文件开始事件
        /// </summary>
        public event EventHandler RollbackStart;

        /// <summary>
        /// 引发 <see cref="RollbackStart" /> 事件
        /// </summary>
        protected virtual void OnRollbackStart()
        {
            RollbackStart?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 回滚文件结束事件
        /// </summary>
        public event EventHandler RollbackFinished;

        /// <summary>
        /// 引发 <see cref="RollbackFinished" /> 事件
        /// </summary>
        protected virtual void OnRollbackFinished()
        {
            RollbackFinished?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 回滚文件事件
        /// </summary>
        public event EventHandler<InstallFileEventArgs> RollbackFile;

        /// <summary>
        /// 引发 <see cref="RollbackFile" /> 事件
        /// </summary>
        protected virtual void OnRollbackFile(InstallFileEventArgs e)
        {
            RollbackFile?.Invoke(this, e);
        }
        #endregion


        #region 工作函数

        /// <summary>
        /// 删除空目录
        /// </summary>
        void DeleteEmptyDirectories()
        {
            DeleteEmptyDirectories(ApplicationRoot, false);
        }

        /// <summary>
        /// 删除空目录
        /// </summary>
        void DeleteEmptyDirectories(string path, bool deleteSelf)
        {
            try
            {
                var list = Directory.GetDirectories(path);
                foreach (var item in list)
                {
                    DeleteEmptyDirectories(item, true);
                }

                if (deleteSelf && Directory.GetFileSystemEntries(path).Length == 0)
                {
                    Trace.TraceInformation("正在删除空目录 {0}", path);
                    Directory.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("删除空目录时发生错误：{0}", ex.Message);
            }
        }



        /// <summary>
        /// 删除原始安装文件
        /// </summary>
        bool DeletePreviousFile(BackgroundWorker sender, DoWorkEventArgs e)
        {
            if (UpdateMeta.DeleteMethod == DeletePreviousProgramMethod.None) return true;

            OnDeleteFileStart();

            var bakPath = RollbackPath;
            var rules = UpdateMeta.GetDeleteFileLimitRuleSet();

            //找到所有文件
            var allOldFiles = Directory.GetFiles(ApplicationRoot, "*.*", SearchOption.AllDirectories);

            //备份
            var index = 0;
            foreach (var file in allOldFiles)
            {
                sender.ReportProgress(Utility.GetPercentProgress(++index, allOldFiles.Length), file);

                var rPath = file.Remove(0, ApplicationRoot.Length).TrimEnd('\\');
                //保留的文件
                if (PreservedFiles.ContainsKey(rPath))
                {
                    Trace.TraceInformation("文件 {0} 在保持文件列表中，跳过删除", file);
                    continue;
                }

                var dPath = Path.Combine(bakPath, rPath);

                if ((UpdateMeta.DeleteMethod == DeletePreviousProgramMethod.AllExceptSpecified && rules.FindIndex(s => s.IsMatch(rPath)) == -1)
                        ||
                    (UpdateMeta.DeleteMethod == DeletePreviousProgramMethod.NoneButSpecified && rules.FindIndex(s => s.IsMatch(rPath)) != -1)
                    )
                {
                    OnDeleteFile(new InstallFileEventArgs(file, dPath, allOldFiles.Length, index));
                    Directory.CreateDirectory(Path.GetDirectoryName(dPath));
                    Trace.TraceInformation("备份并删除文件: {0}  ->  {1}", file, dPath);
                    File.Copy(file, dPath);

                    var tryCount = 0;
                    while (true)
                    {
                        ++tryCount;

                        try
                        {
                            File.Delete(file);
                            break;
                        }
                        catch (Exception ex)
                        {
                            this.Exception = ex;
                            Trace.TraceWarning("第[" + tryCount + "]次删除失败：" + ex.Message);
                        }
                        //如果删除失败，则等待1秒后重试
                        if (tryCount < 10)
                            Thread.Sleep(1000);
                        else return false;

                    }
                    _bakList.Add(rPath);
                }
            }
            OnDeleteFileFinished();

            return true;
        }



        /// <summary>
        /// 安装文件
        /// </summary>
        bool InstallFiles(BackgroundWorker sender, DoWorkEventArgs e)
        {
            OnInstallFileStart();

            var filelist = CreateNewFileList();
            string originalPath, newVersionFile, backupPath;
            originalPath = newVersionFile = "";
            try
            {
                var index = 0;
                foreach (var file in filelist)
                {
                    sender.ReportProgress(Utility.GetPercentProgress(++index, filelist.Length), file);

                    originalPath = Path.Combine(ApplicationRoot, file);
                    newVersionFile = Path.Combine(SourceFolder, file);
                    backupPath = Path.Combine(RollbackPath, file);

                    OnInstallFile(new InstallFileEventArgs(newVersionFile, originalPath, filelist.Length, index));


                    int tryCount;
                    if (File.Exists(originalPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                        tryCount = 0;

                        while (true)
                        {
                            ++tryCount;
                            try
                            {
                                if (File.Exists(originalPath))
                                {
                                    Trace.TraceInformation("第[" + tryCount + "]次尝试备份文件: " + originalPath + "  ->  " + backupPath);
                                    File.Copy(originalPath, backupPath, true);
                                    Trace.TraceInformation("第[" + tryCount + "]次尝试删除文件: " + originalPath);
                                    File.Delete(originalPath);
                                    Trace.TraceInformation("备份成功。");
                                }

                                break;
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceWarning("第[" + tryCount + "]次尝试失败： " + ex.Message);

                                if (tryCount < 20)
                                    Thread.Sleep(1000);
                                else throw ex;
                            }
                        }
                        _bakList.Add(file);
                    }
                    tryCount = 0;
                    while (true)
                    {
                        ++tryCount;
                        try
                        {
                            Trace.TraceInformation("正在复制新版本文件: " + newVersionFile + "  ->  " + originalPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(originalPath));
                            File.Copy(newVersionFile, originalPath);
                            Trace.TraceInformation("安装成功");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceWarning("第[" + tryCount + "]次尝试失败： " + ex.Message);

                            if (tryCount < 10)
                                Thread.Sleep(1000);
                            else throw ex;
                        }
                    }
                    //尝试删除已安装文件
                    tryCount = 0;
                    while (true)
                    {
                        ++tryCount;
                        try
                        {
                            Trace.TraceInformation("正在尝试删除已安装文件: " + newVersionFile);
                            File.Delete(newVersionFile);
                            Trace.TraceInformation("删除成功");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceWarning("第[" + tryCount + "]次尝试失败： " + ex.Message);

                            if (tryCount < 10)
                                Thread.Sleep(1000);
                            else break;
                        }

                    }
                    _installedFile.Add(file);
                    Trace.TraceInformation("安装文件: " + newVersionFile + "  ->  " + originalPath);
                }
            }
            catch (Exception ex)
            {
                Exception = new Exception($"安装文件{originalPath}到{newVersionFile}发生错误：{ex.Message}", ex);
                Trace.TraceWarning("安装文件时发生错误：" + ex.Message, ex.ToString());
                return false;
            }

            OnInstallFileFinished();

            return true;
        }

        /// <summary>
        /// 删除已安装的文件, 并还原原始文件
        /// </summary>
        void DeleteInstalledFiles()
        {
            foreach (var filepath in _installedFile)
            {
                var originalFile = Path.Combine(ApplicationRoot, filepath);

                if (File.Exists(originalFile))
                    File.Delete(originalFile);

                Trace.TraceInformation("删除已安装文件: " + originalFile);
            }
        }

        /// <summary>
        /// 回滚备份的文件
        /// </summary>
        void RollbackFiles(BackgroundWorker sender, DoWorkEventArgs e)
        {
            OnRollbackStart();
            var rootPath = RollbackPath;

            var index = 0;
            foreach (string file in _bakList)
            {
                sender.ReportProgress(Utility.GetPercentProgress(++index, _bakList.Count), file);

                var newPath = Path.Combine(ApplicationRoot, file);
                var oldPath = Path.Combine(rootPath, file);

                OnRollbackFile(new InstallFileEventArgs(oldPath, newPath, _bakList.Count, index));

                Trace.TraceInformation("还原原始文件: " + oldPath + "  ->  " + newPath);
                File.Move(oldPath, newPath);
            }

            OnRollbackFinished();
        }

        /// <summary>
        /// 创建要安装的新文件列表
        /// </summary>
        /// <returns></returns>
        string[] CreateNewFileList()
        {
            var source = SourceFolder;

            var files = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                files[i] = files[i].Remove(0, source.Length).Trim(new[] { '\\', '/' });
            }

            return files;
        }


        #endregion
    }
}

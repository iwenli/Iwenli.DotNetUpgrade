using Iwenli.DotNetUpgrade.Core;
using Iwenli.DotNetUpgrade.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Iwenli.DotNetUpgrade
{
    public partial class Updater : IDisposable
    {

        /// <summary> 获得当前更新的上下文 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public UpdateContext Context { get; private set; }
        /// <summary>
		/// 手动初始化实例
		/// </summary>
		/// <param name="appVersion">指定的应用程序版本</param>
		/// <param name="appDirectory">指定的应用程序路径</param>
		/// <param name="servers">更新包所在服务器信息</param>
        protected Updater(Version appVersion = null, string appDirectory = null, params Server[] servers) : this()
        {
            if (appVersion != null)
                Context.CurrentVersion = appVersion;
            if (!string.IsNullOrEmpty(appDirectory))
                Context.ApplicationDirectory = appDirectory;

            Context = new UpdateContext(servers);
        }

        protected Updater()
        {
            Trace.AutoFlush = true;
            Packages = new List<Package>();

            UpdatesFound += Instance_UpdatesFound;
            if (_instance == null) _instance = this;

            //初始化工作参数
            InitializeParameters();
        }

        #region Dispose 模式

        bool _disposed;

        /// <summary>
        /// 释放
        /// </summary>
        /// <param name="disposing">是否是主动调用</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            _disposed = true;

            if (disposing)
            {
                Context.LogFile = null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        ~Updater()
        {
            Dispose(false);
        }

        #endregion

        /// <summary>
        /// 初始化工作参数
        /// </summary>
        void InitializeParameters()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length < 2 || (args[1] != "/startupdate" && args[1] != "/selfupdate")) return;

            var index = 2;
            while (index < args.Length)
            {
                var name = args[index++];
                switch (name)
                {
                    //case "/ui":
                    //    Context.UpdateMainFormType = args[index++];
                    //    break;
                    case "/assembly":
                        LoadExtraAssemblies(args[index++]);
                        break;
                    case "/cv": Context.CurrentVersion = new Version(args[index++]); break;
                    case "/ad": Context.ApplicationDirectory = args[index++]; break;
                    case "/url": Context.UpdateMetaFileUrl = args[index++]; break;
                    case "/proxy": Context.ProxyAddress = args[index++]; break;
                    case "/cred":
                        var value = args[index++];
                        if (!string.IsNullOrEmpty(value))
                        {
                            var info = value.Split(':');
                            if (info.Length == 2 && !string.IsNullOrEmpty(info[0]))
                            {
                                Context.NetworkCredential = new NetworkCredential(info[0], info[1]);
                            }
                        }
                        break;
                    case "/p":
                        var pi = args[index++];
                        if (pi.StartsWith("*")) Context.ExternalProcessID.Add(int.Parse(pi.Remove(0, 1)));
                        else Context.ExternalProcessName.Add(pi);
                        break;
                    case "/log": Context.LogFile = args[index++]; break;
                    case "/forceupdate": Context.ForceUpdate = true; break;
                    case "/autokill": Context.AutoKillProcesses = true; break;
                        //case "/noui": Context.HiddenUI = true; break;
                }
            }
        }
        #region 检测更新

        int _serverIndex = 0;
        /// <summary>
		/// 选择下一个服务器
		/// </summary>
		bool PeekNextServer()
        {
            _serverIndex++;
            Context.UpdateMetaFileUrl = "";
            if (Context.Services.Count == 0)
                return false;

            if (_serverIndex >= Context.Services.Count)
            {
                _serverIndex = 0;
                return false;
            }
            return true;
        }
        /// <summary>
        /// 开始检测更新
        /// </summary>
        /// <returns></returns>
        bool BeginUpdateChecking()
        {
            var server = Context.Services[_serverIndex];
            var url = Context.UpdateMetaFileUrl = new Uri(new Uri(server.Address), server.Manifest).ToString();

            if (string.IsNullOrEmpty(url)) throw new InvalidOperationException("无法确定更新地址");
            if (Context.IsInUpdating) return false;

            var bgw = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            bgw.DoWork += UpdateMetaDownloadInternal;
            bgw.RunWorkerCompleted += (s, e) =>
            {
                if (Context.UpdateMeta == null) return;

                Context.IsInUpdating = false;

                if (Context.CurrentVersionTooLow)
                {
                    OnMinmumVersionRequired();
                    Context.Exception = new VersionTooLowException(Context.CurrentVersion, new Version(Context.UpdateMeta.RequiredMinVersion));
                    OnError();
                }
                else if (!Context.HasUpdate)
                {
                    OnNoUpdatesFound();
                }
                else OnUpdatesFound();
                OnCheckUpdateComplete();
            };
            bgw.ProgressChanged += (s, e) => OnOperationProgressChanged(e);
            Context.IsInUpdating = true;
            bgw.RunWorkerAsync();

            return true;
        }

        /// <summary>
        /// 下载更新信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateMetaDownloadInternal(object sender, DoWorkEventArgs e)
        {
            try
            {
                // 下载更新信息
                OnUpdateMetaDownloading();
                var localFile = Context.UpdateMetaFilePath;

                if (File.Exists(localFile))
                {
                    Trace.TraceInformation("正在读取本地升级信息文件 [" + localFile + "]");
                    Context.UpdateMetaTextContent = File.ReadAllText(localFile, Encoding.UTF8);
                }
                else
                {
                    //下载信息时不直接下载到文件中.这样不会导致始终创建文件夹
                    Exception ex = null;
                    byte[] data = null;
                    var url = Context.UpdateMetaFileUrl;

                    var client = Context.CreateWebClient();
                    client.DownloadProgressChanged += (x, y) => (sender as BackgroundWorker).ReportProgress(Utility.GetPercentProgress(y.BytesReceived, y.TotalBytesToReceive));

                    //远程下载。为了支持进度显示，这里必须使用异步下载
                    using (var wHandler = new AutoResetEvent(false))
                    {
                        client.DownloadDataCompleted += (x, y) =>
                        {
                            ex = y.Error;
                            if (ex == null)
                            {
                                data = y.Result;
                            }
                            wHandler.Set();
                        };
                        Trace.TraceInformation("正在从 " + url + " 下载升级信息");
                        client.DownloadDataAsync(new Uri(url));
                        //等待下载完成
                        wHandler.WaitOne();
                    }

                    if (ex != null) throw ex;

                    Trace.TraceInformation("服务器返回数据----->" + (data == null ? "<null>" : data.Length.ToString() + "字节"));
                    if (data != null && data.Length > 0x10)
                    {
                        //不是<xml标记，则执行解压缩
                        if (BitConverter.ToInt32(data, 0) != 0x6D783F3C && BitConverter.ToInt32(data, 0) != 0x3CBFBBEF)
                        {
                            Trace.TraceInformation("正在执行解压缩");
                            data = data.Decompress();
                        }
                        Context.UpdateMetaTextContent = Encoding.UTF8.GetString(data);
                    }

                    //是否返回了正确的结果?
                    if (string.IsNullOrEmpty(Context.UpdateMetaTextContent))
                    {
                        throw new ApplicationException("服务器返回了不正确的更新结果");
                    }

                }
                // 下载升级信息完成事件
                OnUpdateMetaDownloadFinished();

                if ((Context.UpdateMeta = SerializeHelper.XmlDeserialize<UpdateMeta>(Context.UpdateMetaTextContent)) == null)
                {
                    throw new ApplicationException("未能成功加载升级信息");
                }
                Trace.TraceInformation("服务器版本：{0}", Context.UpdateMeta.AppVersion);
                Trace.TraceInformation("当前版本：{0}", Context.CurrentVersion);

                //设置必须的属性
                if (Context.UpdateMeta.MustUpdate)
                {
                    Context.AutoKillProcesses = true;
                    Trace.TraceInformation("已设置自动关闭进程。");
                    Context.AutoEndProcessesWithinAppDir = true;
                    Trace.TraceInformation("已设置自动关闭同目录进程。");
                    Context.ForceUpdate = true;
                    Trace.TraceInformation("已设置强制升级。");
                }

                //判断升级
                if (!string.IsNullOrEmpty(Context.UpdateMeta.RequiredMinVersion) && Context.CurrentVersion < new Version(Context.UpdateMeta.RequiredMinVersion))
                {
                    Context.CurrentVersionTooLow = true;
                    Trace.TraceWarning("当前应用程序版本过低，无法升级。要求最低版本：{0}，当前版本：{1}。", Context.UpdateMeta.RequiredMinVersion, Context.CurrentVersion);
                }
                else
                {
                    Context.HasUpdate = new Version(Context.UpdateMeta.AppVersion) > Context.CurrentVersion;
                    Trace.TraceInformation("已找到升级：" + Context.HasUpdate);
                }

                if (Context.HasUpdate)
                {
                    //判断要升级的包
                    if (Packages == null || Packages.Count == 0)
                    {
                        var pkgList = Context.UpdatePackageListPath;
                        Trace.TraceInformation("外部升级包列表：{0}", pkgList);

                        if (File.Exists(pkgList))
                        {
                            Trace.TraceInformation("外部升级包列表：已加载成功");
                            Packages = SerializeHelper.XmlDeserialize<List<Package>>(File.ReadAllText(pkgList, Encoding.UTF8));
                            Packages.ForEach(s => s.Context = Context);
                        }
                        else
                        {
                            Trace.TraceInformation("外部升级包列表：当前不存在，正在生成升级清单");
                            GatheringDownloadPackages(sender, e);
                        }

                        var preserveFileList = Context.PreserveFileListPath;
                        Trace.TraceInformation("外部文件保留列表：{0}", preserveFileList);
                        if (File.Exists(preserveFileList))
                        {
                            Trace.TraceInformation("外部文件保留列表：已加载成功");
                            var list = SerializeHelper.XmlDeserialize<List<string>>(File.ReadAllText(preserveFileList, Encoding.UTF8));
                            list.ForEach(s => FileInstaller.PreservedFiles.Add(s, null));
                        }
                        else
                        {
                            Trace.TraceInformation("外部升级包列表：当前不存在，等待重新生成");
                        }
                    }

                    //如果没有要升级的包？虽然很奇怪，但依然当作不需要升级
                    if (Packages.Count == 0)
                    {
                        Context.HasUpdate = false;
                        Trace.TraceWarning("警告：虽然版本出现差别，但是并没有可升级的文件。将会当作无升级对待。");
                    }
                }
            }
            catch (Exception ex)
            {
                Context.IsInUpdating = false;
                Context.Exception = ex;
                Trace.TraceWarning("检测更新信息失败：" + ex.Message, ex.ToString());
                OnError();
                OnCheckUpdateComplete();
            }
        }
        #region 确定要下载的包
        /// <summary>
        /// 生成下载列表
        /// </summary>
        /// <param name="e"></param>
        void GatheringDownloadPackages(object sender, DoWorkEventArgs e)
        {
            if (Packages.Count > 0) return;
            Trace.TraceInformation("正在确定需要下载的升级包");
            OnGatheringPackages();

            if (!string.IsNullOrEmpty(Context.UpdateMeta.Package) && (Context.UpdateMeta.Packages == null || !Context.UpdateMeta.Packages.Any()))
            {
                //必须更新的包
                Trace.TraceInformation("正在添加必须升级的主要安装包");
                Packages.Add(new Package()
                {
                    FilePath = "",
                    FileSize = 0,
                    Hash = Context.UpdateMeta.MD5,
                    Method = UpdateMethod.Always,
                    Name = Context.UpdateMeta.Package,
                    Size = Context.UpdateMeta.PackageSize,
                    VerificationLevel = FileVerificationLevel.Hash,
                    Version = "0.0.0.0",
                    Context = Context
                });
            }
            if (Context.UpdateMeta.Packages != null)
            {
                //判断增量升级包
                var index = 0;
                foreach (var pkg in Context.UpdateMeta.Packages)
                {
                    (sender as BackgroundWorker).ReportProgress(Utility.GetPercentProgress(++index, Context.UpdateMeta.Packages.Count));
                    var localPath = Path.Combine(Context.ApplicationDirectory, pkg.FilePath); //对比本地路径
                    pkg.Context = Context;
                    if (pkg.Method == UpdateMethod.Always)
                    {
                        Trace.TraceInformation($"标记为始终更新，添加升级包 【{ pkg.Name }】");
                        Packages.Add(pkg);
                        continue;
                    }
                    //判断组件标记
                    if (!string.IsNullOrEmpty(pkg.ComponentId) && !CheckComponentFlag(pkg.ComponentId))
                    {
                        Trace.TraceInformation($"组件标记为 {pkg.ComponentId}，状态为false，跳过升级包 【{pkg.Name}】");
                        continue;
                    }


                    //存在即跳过，或版本比较
                    if (!System.IO.File.Exists(localPath))
                    {
                        if (Utility.HasMethod(pkg.Method, UpdateMethod.SkipIfNotExist))
                        {
                            Trace.TraceInformation($"本地路径【{pkg.FilePath}】不存在，并且指定了不存在则跳过，因此跳过更新");
                        }
                        else
                        {

                            Packages.Add(pkg);
                            Trace.TraceInformation($"本地路径【{pkg.FilePath}】不存在，添加升级包 【{pkg.Name }】");
                        }
                        continue;
                    }
                    //如果存在即跳过……那么你好去跳过了。
                    if (Utility.HasMethod(pkg.Method, UpdateMethod.SkipIfExists))
                    {
                        AddPackageToPreserveList(pkg);
                        Trace.TraceInformation($"本地路径【{pkg.FilePath}】已经存在，跳过升级包 【{pkg.Name }】");
                        continue;
                    }

                    var isNewer = (pkg.HasVerifyFlag(FileVerificationLevel.Size) && new FileInfo(localPath).Length != pkg.FileSize)
                        ||
                        (pkg.HasVerifyFlag(FileVerificationLevel.Version) && (string.IsNullOrEmpty(pkg.Version) || localPath.CompareVersion(pkg.Version)))
                        ||
                        (pkg.HasVerifyFlag(FileVerificationLevel.Hash) && localPath.GetFileHash() != pkg.FileHash)
                        ;

                    if (isNewer)
                    {
                        Trace.TraceInformation($"服务器版本更新，添加升级包 【{pkg.Name}】");
                        pkg.Context = Context;
                        Packages.Add(pkg);
                    }
                    else
                    {
                        AddPackageToPreserveList(pkg);
                        Trace.TraceInformation($"服务器版本更旧或相同，跳过升级包 【{pkg.Name}】");
                    }
                }
            }

            OnGatheredPackages();
            Trace.TraceInformation("完成确定需要下载的升级包");
        }

        /// <summary>
        /// 将指定包的文件添加到忽略列表
        /// </summary>
        /// <param name="pkg"></param>
        void AddPackageToPreserveList(Package pkg)
        {
            if (pkg == null || pkg.Files == null) return;

            var reserveDic = FileInstaller.PreservedFiles;
            foreach (var file in pkg.Files)
            {
                if (!reserveDic.ContainsKey(file))
                {
                    Trace.TraceInformation($"添加 {file} 到保持文件列表，因为下载过程中会跳过，所以不可以删除");
                    reserveDic.Add(file, null);
                }
            }
        }


        #endregion
        #endregion

        #region 更新

        FileInstaller _installer;

        /// <summary> 获得当前用于安装文件的对象 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public FileInstaller FileInstaller => _installer ??= new FileInstaller();

        /// <summary>
		/// 开始进行更新
		/// </summary>
		internal void BeginUpdate()
        {
            var bgw = new BackgroundWorker() { WorkerSupportsCancellation = true };
            bgw.DoWork += UpdateInternal;
            bgw.ProgressChanged += (s, e) => OnOperationProgressChanged(e);
            bgw.RunWorkerCompleted += (s, e) =>
            {
                Context.IsInUpdating = false;
            };
            Context.IsInUpdating = true;
            bgw.RunWorkerAsync();
            CleanTemp();
        }

        /// <summary>
        /// 运行更新进程(主更新进程)
        /// </summary>
        void UpdateInternal(object sender, DoWorkEventArgs e)
        {
            try
            {
                UpdateMetaDownloadInternal(sender, e);

                //下载升级包。下载完成的时候校验也就完成了
                if (!DownloadPackages(e)) return;

                //解压缩升级包
                ExtractPackage((BackgroundWorker)sender, e);
            }
            catch (Exception ex)
            {
                Context.IsInUpdating = false;
                Context.Exception = ex;
                Trace.TraceWarning("更新中断，发生错误：" + ex.Message, ex.ToString());
                OnError();
            }
            throw new NotImplementedException();
        }

        #region 更新包下载

        #region 公共属性

        /// <summary>
        /// 当前需要下载的升级包
        /// </summary>
        public List<Package> Packages { get; private set; }

        /// <summary> 获得当前需要下载的升级包数目 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public int PackageCount => Packages.Count;

        /// <summary> 获得已完成下载的任务个数 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public int DownloadedPackageCount => Packages.Count(m => m.IsDownloaded);

        /// <summary> 获得正在下载的任务个数 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public int DownloadingPackageCount => Packages.Count(m => m.IsDownloading);
        #endregion

        /// <summary> 执行下载 </summary>
        bool DownloadPackages(DoWorkEventArgs e)
        {
            Directory.CreateDirectory(Context.UpdatePackagePath);

            Trace.TraceInformation("开始下载网络更新包");

            var workerCount = Math.Max(1, Context.MultipleDownloadCount);
            var workers = new List<WebClient>(workerCount);
            var evt = new AutoResetEvent(false);
            var hasError = false;

            //Ping
            if (!string.IsNullOrEmpty(Context.UpdateMeta.UpdatePingUrl))
            {
                try
                {
                    Context.CreateWebClient().UploadData(new Uri(Context.UpdateMeta.UpdatePingUrl), new byte[0]);
                }
                catch (Exception)
                {
                }
            }

            //生成下载队列
            Trace.TraceInformation("正在初始化 {0} 个WebClient", workerCount);
            for (var i = 0; i < workerCount; i++)
            {
                var client = Context.CreateWebClient();
                client.DownloadFileCompleted += (s, e) =>
                {
                    var pkg = e.UserState as Package;
                    var cnt = s as WebClient;

                    pkg.LastError = e.Error;
                    if (e.Error != null)
                    {
                        Trace.TraceWarning($"包【{pkg.Name}】下载失败：{e.Error.Message}");
                        OnPackageDownloadFailed(new PackageEventArgs(pkg));
                    }
                    else if (pkg.IsLocalFileValid != true)
                    {
                        Trace.TraceWarning($"包【{pkg.Name}】MD5校验失败", "错误");
                        pkg.LastError = new Exception("不文件哈希值不正确或文件不存在");
                        OnPackageHashMismatch(new PackageEventArgs(pkg));
                    }

                    if (pkg.LastError != null)
                    {
                        //如果出错，且重试次数在限制范围内，则重试
                        pkg.IncreaseFailureCounter();
                        if (pkg.RetryCount <= Context.MaxiumRetryDownloadCount)
                        {
                            Trace.TraceWarning($"包【{pkg.Name}】未能成功下载，正在进行第 {pkg.RetryCount} 次重试，最大重试次数为 {Context.MaxiumRetryDownloadCount}", "错误");
                            cnt.DownloadFileAsync(new Uri(pkg.SourceUri), pkg.LocalSavePath, pkg);
                            OnPackageDownloadRetried(new PackageEventArgs(pkg));
                            return;
                        }
                        //标记出错
                        hasError = true;
                    }

                    //包下载完成事件
                    pkg.IsDownloading = false;
                    pkg.IsDownloaded = pkg.LastError == null;
                    OnPackageDownloadFinished(new PackageEventArgs(e.UserState as Package));

                    lock (Packages)
                    {
                        Trace.TraceInformation($"包【{pkg.Name}】下载操作完成：{(pkg.IsDownloaded ? "下载成功" : "下载失败")}");
                        evt.Set();
                    }
                };
                client.DownloadProgressChanged += (s, e) =>
                {
                    var pkg = e.UserState as Package;
                    pkg.DownloadedSize = e.BytesReceived;
                    pkg.Size = e.TotalBytesToReceive > 0 ? e.TotalBytesToReceive : pkg.Size;
                    OnDownloadProgressChanged(new PackageDownloadProgressChangedEventArgs(pkg, pkg.Size,
                                                                             pkg.DownloadedSize, e.ProgressPercentage));
                };
                workers.Add(client);
            }

            //开始处理事务
            while (!hasError)
            {
                var breakFlag = false;
                lock (Packages)
                {
                    //没有错误，则分配下个任务
                    WebClient client;
                    while ((client = workers.Find(s => !s.IsBusy)) != null)
                    {
                        var nextPkg = Packages.Find(s => !s.IsDownloading && !s.IsDownloaded);
                        if (nextPkg == null)
                        {
                            breakFlag = true;
                            break;
                        }

                        nextPkg.IsDownloading = true;
                        Trace.TraceInformation($"包【{nextPkg.Name}】开始下载");
                        OnPackageDownload(new PackageEventArgs(nextPkg));
                        Context.ResetWebClient(client);
                        client.DownloadFileAsync(new Uri(Context.GetUpdatePackageFullUrl(nextPkg.Name)), nextPkg.LocalSavePath, nextPkg);
                    }
                }
                if (breakFlag) break;
                evt.WaitOne();
                Trace.TraceInformation("线程同步事件已收到");
            }
            //不管任何原因中止下载，到这里时都需要等待所有客户端中止
            while (true)
            {
                //出错了，那么对所有的客户端发出中止命令。这里不需要判断是否忙碌。
                if (hasError)
                {
                    Trace.TraceWarning("出现错误，正在取消所有包的下载队列");
                    workers.ForEach(s => s.CancelAsync());
                }
                lock (Packages)
                {
                    Trace.TraceInformation("等待下载队列完成操作");
                    if (workers.FindIndex(s => s.IsBusy) == -1) break;
                }
                evt.WaitOne();
            }
            Trace.TraceInformation("完成下载网络更新包");

            var errorPkgs = Packages.FindAll(m => m.LastError != null);
            if (errorPkgs.Count > 0) throw new PackageDownloadException(errorPkgs.ToArray());

            return !hasError;
        }

        #endregion

        #region 解压安装包

        /// <summary>
        /// 解压安装包
        /// </summary>
        void ExtractPackage(BackgroundWorker worker, DoWorkEventArgs e111)
        {
            Trace.TraceInformation("开始解压缩升级包");
            OnPackageExtractionBegin(new PackageEventArgs(null));

            var count = Packages.Count;
            var index = 0;
            var fze = new ICCEmbedded.SharpZipLib.Zip.FastZipEvents();
            fze.ProcessFile += (s, e) => worker.ReportProgress(Utility.GetPercentProgress(index, count), $"正在解压缩：{e.Name}");
            var fz = new ICCEmbedded.SharpZipLib.Zip.FastZip(fze);
            if (!string.IsNullOrEmpty(Context.UpdateMeta.PackagePassword))
            {
                fz.Password = Context.UpdateMeta.PackagePassword;
            }

            foreach (var pkg in Packages)
            {
                index++;

                Trace.TraceInformation("正在解压缩 " + pkg.Name);
                OnPackageExtractionBegin(new PackageEventArgs(pkg));

                fz.ExtractZip(pkg.LocalSavePath, Context.UpdateNewFilePath, null);

                OnPackageExtractionEnd(new PackageEventArgs(pkg));
                Trace.TraceInformation("完成解压缩 " + pkg.Name);
            }

            OnPackageExtractionEnd(new PackageEventArgs(null));
            Trace.TraceInformation("完成解压缩升级包");
        }
        #endregion

        #endregion

        #region 额外调用

        /// <summary>
        /// 加载额外调用
        /// </summary>
        /// <param name="namelist"></param>
        void LoadExtraAssemblies(string namelist)
        {
            var assFiles = namelist.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var assFile in assFiles)
            {
                Trace.TraceInformation($"开始尝试加载文件 '{assFile}'....");

                try
                {
                    var path = Path.Combine(Context.UpdateTempRoot, assFile);
                    var assembly = System.Reflection.Assembly.LoadFile(path);

                    Trace.TraceInformation("程序集已加载,检查通知接口...");
                    //检查接口调用
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        //if (type.GetInterface(typeof(IUpdateNotify).FullName) != null)
                        //{
                        //    Trace.TraceInformation("IUpdateNotify detected. Create object instance and invoke Init() method.");

                        //    var obj = Activator.CreateInstance(type) as IUpdateNotify;
                        //    obj.Init(this);
                        //}
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("file loading failed. error: " + ex.ToString());
                }
                Trace.TraceInformation("ending loading file '" + assFile + "'....");
            }
        }

        List<Assembly> _usingAssemblies;
        Type _mainFormType;

        /// <summary>
        /// 引用指定的程序集
        /// </summary>
        /// <param name="assemblies"></param>
        public void UsingAssembly(params Assembly[] assemblies)
        {
            (_usingAssemblies ??= new List<Assembly>()).AddRange(assemblies);
        }
        #endregion


        #region 启动外部更新进程

        /// <summary>
        /// 确保更新已经启动
        /// </summary>
        internal void EnsureUpdateStarted()
        {
            if ((Context.IsUpdaterSuccessfullyStarted == true && Context.AutoExitCurrentProcess) ||
                (Context.IsUpdaterSuccessfullyStarted == false && Context.MustUpdate))
            {
                //启动成功，而且指定了自动解除当前进程时，则自动退出
                //启动失败，但是要求强行更新时，则退出
                TerminateProcess(this);
            }
        }

        /// <summary>
        /// 强行中止当前进程
        /// </summary>
        internal static void TerminateProcess(object sender, int exitCode = 0)
        {
            var e = new CancelEventArgs();
            OnRequireTerminateProcess(sender, e);
            if (e.Cancel)
                return;

            Environment.Exit(exitCode);
        }


        /// <summary>
        /// 复制更新程序到临时目录并启动
        /// </summary>
        bool CopyAndStartUpdater(string[] ownerProcessList)
        {
            //写入更新文件
            var updateinfoFile = Context.UpdateMetaFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(updateinfoFile));
            File.WriteAllText(updateinfoFile, Context.UpdateMetaTextContent, Encoding.UTF8);
            //写入包列表
            SerializeHelper.XmlSerilizeToFile(Packages, Context.UpdatePackageListPath);
            SerializeHelper.XmlSerilizeToFile(FileInstaller.PreservedFiles.Keys.ToList(), Context.PreserveFileListPath);

            //启动外部程序
            var currentAssembly = Assembly.GetExecutingAssembly();
            if (CopyAssemblyToUpdateRoot(currentAssembly) == null)
            {
                throw new Exception("未能生成临时辅助更新文件");
            }

            var tempExePath = Path.Combine(Context.UpdateTempRoot, Path.GetFileName(currentAssembly.Location));

#if !STANDALONE
            tempExePath = Path.Combine(Context.UpdateTempRoot, "AutoUpdater.exe");
            //TODO:(Iwenli) System.IO.File.WriteAllBytes(tempExePath, ExtensionMethod.Decompress(Properties.Resources.FSLib_App_Utilities_exe));
#endif
            //写入配置文件。以便于多Framework支持。。
            //System.IO.File.WriteAllBytes(tempExePath + ".config", Properties.Resources.appconfig);

            //生成新的日志地址
            var logPath = "";
            if (!string.IsNullOrEmpty(Context.LogFile))
            {
                logPath = Path.Combine(
                         Path.GetDirectoryName(Context.LogFile),
                         Path.GetFileNameWithoutExtension(Context.LogFile) + "_1" +
                         Path.GetExtension(Context.LogFile)
                );
            }

            //启动
            var sb = new StringBuilder(0x400);
            sb.AppendFormat("/startupdate /cv \"{0}\" ", Context.CurrentVersion.ToString());
            sb.AppendFormat("/log \"{0}\" ", Utility.SafeQuotePathInCommandLine(logPath));
            sb.AppendFormat("/ad \"{0}\" ", Utility.SafeQuotePathInCommandLine(Context.ApplicationDirectory));
            sb.AppendFormat("/url \"{0}\" ", Context.UpdateMetaFileUrl);
            sb.AppendFormat("/proxy \"{0}\" ", Context.ProxyAddress ?? "");
            if (Context.NetworkCredential != null)
                sb.AppendFormat("/cred \"{0}\" ", string.Format("{0}:{1}", Context.NetworkCredential.UserName, Context.NetworkCredential.Password));
            if (Context.AutoKillProcesses) sb.Append("/autokill ");
            if (Context.ForceUpdate) sb.Append("/forceupdate ");
            //if (Context.HiddenUI) sb.Append("/noui ");
            //if (_mainFormType != null)
            //{
            //    CopyAssemblyToUpdateRoot(_mainFormType.Assembly);
            //    sb.Append("/ui \"" + _mainFormType.AssemblyQualifiedName + "\" ");

            //    if (!(_usingAssemblies ?? (_usingAssemblies = new List<Assembly>())).Contains(_mainFormType.Assembly))
            //        _usingAssemblies.Add(_mainFormType.Assembly);
            //}
            if (_usingAssemblies != null && _usingAssemblies.Count > 0)
            {
                var assemblyNames = new List<string>();
                foreach (var assembly in _usingAssemblies)
                {
                    if (CopyAssemblyToUpdateRoot(assembly) == true)
                        assemblyNames.Add(System.IO.Path.GetFileName(assembly.Location));
                }
                if (assemblyNames.Count > 0)
                {
                    sb.Append("/assembly \"" + string.Join(";", assemblyNames.ToArray()) + "\" ");
                }
            }

            FetchProcessList(ownerProcessList).ForEach(s => sb.AppendFormat("/p \"{0}\" ", s));

            var psi = new ProcessStartInfo(tempExePath, sb.ToString())
            {
                UseShellExecute = true
            };
            //检测是否要管理员权限
            if (Environment.OSVersion.Version.Major > 5 && (Context.UpdateMeta.RequreAdminstrorPrivilege || !EnsureAdminPrivilege()))
                psi.Verb = "runas";

            OnExternalUpdateStart();

            Trace.TraceInformation("启动外部更新进程, 路径=" + psi.FileName + ", 参数=" + psi.Arguments);
            try
            {
                Process.Start(psi);
                OnExternalUpdateStarted();
            }
            catch (Exception ex)
            {
                Context.Exception = ex;
                OnUpdateCancelled();

                return false;
            }

            return true;
        }

        readonly Dictionary<Assembly, string> _assemblies = new Dictionary<Assembly, string>();

        /// <summary>
        /// 复制指定程序集到目录
        /// </summary>
        /// <param name="assembly"></param>
        bool? CopyAssemblyToUpdateRoot(Assembly assembly)
        {
            if (_assemblies.ContainsKey(assembly))
                return true;

            var location = assembly.Location;
            if (!location.StartsWith(Context.ApplicationDirectory, StringComparison.OrdinalIgnoreCase)) return false;

            var dest = Path.Combine(Context.UpdateTempRoot, Path.GetFileName(location));
            Trace.TraceInformation(string.Format("复制引用程序集 {0} 到 {1}", location, dest));
            File.Copy(location, dest, true);
            _assemblies.Add(assembly, null);

            //如果有pdb文件, 那么也复制 
            location = Path.ChangeExtension(location, "pdb");
            if (File.Exists(location))
            {
                Trace.TraceInformation(string.Format("复制PDB文件 {0} 到 {1}", location, dest));
                dest = Path.ChangeExtension(dest, "pdb");
                File.Copy(location, dest, true);
            }

            foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
            {
                Trace.TraceInformation("检测程序集引用..." + referencedAssembly.FullName);
                try
                {
                    if (CopyAssemblyToUpdateRoot(Assembly.Load(referencedAssembly)) == null)
                        return null;
                }
                catch (Exception ex)
                {

                    Trace.TraceError("检测程序集引用异常 : " + ex.ToString());
                    return null;
                }
            }

            return true;
        }

        /// <summary>
        /// 确认当前用户对当前目录是否具有操作权限
        /// </summary>
        /// <returns></returns>
        bool EnsureAdminPrivilege()
        {
            var root = Context.ApplicationDirectory;
            var tempfile = Path.Combine(root, DateTime.Now.Ticks + ".tmp");
            Trace.TraceInformation("检查当前进程是否可以在没有管理员权限的情况下直接写入应用程序目录.");
            try
            {
                File.Create(tempfile).Close();
                File.Delete(tempfile);
                Trace.TraceInformation("权限检查完成，无需管理员权限.");

                return true;
            }
            catch (Exception)
            {
                Trace.TraceInformation("没有权限。执行操作所需的管理员权限，使用 /runas 执行更新.");
                return false;
            }
        }

        /// <summary>
        /// 获得自动更新所需要结束的进程列表
        /// </summary>
        /// <param name="ownerProcess"></param>
        /// <returns></returns>
        List<string> FetchProcessList(string[] ownerProcess)
        {
            var list = new List<string>();
            var mainProcessID = Process.GetCurrentProcess().Id;
            list.Add("*" + mainProcessID);
            list.AddRange(Context.ExternalProcessID.Select(m => "*" + m));
            if (Context.AutoEndProcessesWithinAppDir)
            {
                static string pathLookup(Process _)
                {
                    try
                    {
                        return _.MainModule.FileName;
                    }
                    catch (Exception)
                    {
                        return string.Empty;
                    }
                }
                //获得所有进程
                var processes = Process.GetProcesses();
                //查找当前目录下的进程, 并加入到列表
                foreach (var s in processes)
                {
                    if (!Context.ExternalProcessID.Contains(s.Id) && s.Id != mainProcessID && pathLookup(s).StartsWith(Context.ApplicationDirectory, StringComparison.OrdinalIgnoreCase))
                        list.Add("*" + s.Id);
                }
            }

            if (ownerProcess != null) list.AddRange(ownerProcess);

            return list;
        }

        /// <summary>
        /// 启动进程外更新程序
        /// </summary>
        /// <param name="ownerProcess">主进程列表</param>
        public bool StartExternalUpdater(string[] ownerProcess)
        {
            var result = CopyAndStartUpdater(ownerProcess);
            Context.IsUpdaterSuccessfullyStarted = result;

            return result;

        }

        /// <summary>
        /// 启动进程外更新程序
        /// </summary>
        public bool StartExternalUpdater()
        {
            return StartExternalUpdater(null);
        }
        #endregion

        #region 临时目录清理

        bool _hasCleanProcessStarted;

        /// <summary>
        /// 清理临时目录
        /// </summary>
        void CleanTemp()
        {
            if (_hasCleanProcessStarted) return;

            if (!System.IO.Directory.Exists(Context.UpdateTempRoot))
            {
                Trace.TraceInformation("未生成临时目录，不需要清理");
                return;
            }
            Trace.TraceInformation("启动外部清理进程。");

            var localpath = Environment.ExpandEnvironmentVariables(@"%TEMP%\FSLib.DeleteTmp_" + new Random().Next(100000) + ".exe");
            //System.IO.File.WriteAllBytes(localpath, ExtensionMethod.Decompress(Properties.Resources.FSLib_App_Utilities_exe));
            ////写入配置文件
            //System.IO.File.WriteAllBytes(localpath + ".config", Properties.Resources.appconfig);

            //var arg = "deletetmp \"" + Process.GetCurrentProcess().Id + "\" \"" + Utility.SafeQuotePathInCommandLine(Context.UpdateTempRoot) + "\"";
            //Process.Start(localpath, arg);
            _hasCleanProcessStarted = true;
        }

        #endregion
    }
}

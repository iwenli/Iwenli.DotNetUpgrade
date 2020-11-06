using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Text;

namespace Iwenli.DotNetUpgrade.Core
{
    /// <summary>
    /// 表示当前更新的上下文环境
    /// </summary>
    public class UpdateContext
    {

        TextWriterTraceListener _logger;
        private string _applicationDirectory;
        string _updateMetaFilePath;
        //private bool _autoEndProcessesWithinAppDir;

        public UpdateContext(params Server[] servers)
        {
            Services = servers?.ToList() ?? new List<Server>();

            InitializeCurrentVersion();

            //AutoEndProcessesWithinAppDir = true;
            ExternalProcessID = new List<int>();
            ExternalProcessName = new List<string>();
            MultipleDownloadCount = 3;
            MaxiumRetryDownloadCount = 3;
            //EnableEmbedDialog = true;
            //AutoClosePreviousPopup = true;
            ComponentStatus = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            //如果当前启动路径位于TEMP目录下，则处于临时路径模式
            var temppath = System.IO.Path.GetTempPath();
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (assemblyPath.IndexOf(temppath, StringComparison.OrdinalIgnoreCase) != -1)
            {
                UpdateTempRoot = System.IO.Path.GetDirectoryName(assemblyPath);
                IsInUpdateMode = true;
            }
            else
            {
                UpdateTempRoot = System.IO.Path.Combine(temppath, Guid.NewGuid().ToString());
                IsInUpdateMode = false;

                //尝试自动加载升级属性
                var assembly = Assembly.GetEntryAssembly() ?? TryGetCallingAssembly();
                if (assembly != null)
                {
                    var atts = assembly.GetCustomAttributes(false);

                    foreach (var item in atts)
                    {
                        if (item is UpdateableAttribute updateable && updateable.Server != null)
                        {
                            Services.Add(updateable.Server);
                        }
                    }
                }
            }
        }
        /// <summary> 获得或设置当前的版本 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public Version CurrentVersion { get; set; }

        /// <summary>
        /// 更新包服务器列表
        /// </summary>
        public List<Server> Services { get; private set; }


        /// <summary> 获得或设置当前应用程序的路径 </summary>
        /// <value></value>
        /// <remarks>如果设置的是相对路径，那么最终设置的结果将是当前的应用程序目录和设置值组合起来的路径</remarks>
        /// <exception cref="T:System.ArgumentException">当设置的值是null或空字符串时抛出此异常</exception>
        public string ApplicationDirectory
        {
            get { return _applicationDirectory; }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentException("ApplicationDirectory can not be null or empty.");

                _applicationDirectory = Path.IsPathRooted(value) ? value : Path.Combine(_applicationDirectory, value);
            }
        }
        /// <summary>
        /// 获得或设置是否正在进行更新中
        /// </summary>
        public bool IsInUpdating { get; internal set; }

        /// <summary>
        /// 更新发生异常时的异常信息
        /// </summary>
        public Exception Exception { get; internal set; }

        /// <summary>
        /// 获得或设置当前的更新信息
        /// </summary>
        public UpdateMeta UpdateMeta { get; internal set; }

        /// <summary>
        /// 获得当前更新信息文件保存的路径
        /// </summary>
        public string UpdateMetaFilePath
        {
            get { return _updateMetaFilePath ??= Path.Combine(UpdateTempRoot, "update.xml"); }
        }


        /// <summary>
        /// 获得或设置当前用于下载更新信息文件的地址
        /// </summary>
        public string UpdateMetaFileUrl { get; internal set; }
        /// <summary>
        /// 获得或设置更新信息文件的文本
        /// </summary>
        public string UpdateMetaTextContent { get; internal set; }

        /// <summary>
        /// 获得表示是否当前版本过低而无法升级的标记位
        /// </summary>
        public bool CurrentVersionTooLow { get; internal set; }

        /// <summary>
        /// 获得是否找到更新的标记位
        /// </summary>
        public bool HasUpdate { get; internal set; }
        /// <summary> 获得当前更新的临时目录 </summary>
		/// <value></value>
		/// <remarks></remarks>
		public string UpdateTempRoot { get; private set; }


        ///// <summary>
        ///// 获得或设置一个值，指示着当自动更新的时候是否将应用程序目录中的所有进程都作为主进程请求结束
        ///// </summary>
        //public bool AutoEndProcessesWithinAppDir
        //{
        //    get { return _autoEndProcessesWithinAppDir || (UpdateMeta != null && UpdateMeta.AutoEndProcessesWithinAppDir); }
        //    set { _autoEndProcessesWithinAppDir = value; }
        //}

        /// <summary>
		/// 外部要结束的进程ID列表
		/// </summary>
		public IList<int> ExternalProcessID { get; private set; }
        /// <summary>
        /// 外部要结束的进程名称
        /// </summary>
        public IList<string> ExternalProcessName { get; private set; }
        /// <summary> 获得或设置同时下载的文件数 </summary>
		/// <value></value>
		/// <remarks></remarks>
		public int MultipleDownloadCount { get; set; }

        /// <summary> 获得或设置重试的最大次数 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public int MaxiumRetryDownloadCount { get; set; }
        /// <summary>
		/// 获得组件状态
		/// </summary>
		public Dictionary<string, bool> ComponentStatus { get; private set; }

        /// <summary>
        /// 获得或设置是否正在更新模式中
        /// </summary>
        public bool IsInUpdateMode { get; private set; }

        bool _forceUpdate;
        /// <summary>
        /// 获得或设置是否不经提示便自动更新
        /// </summary>
        public bool ForceUpdate
        {
            get { return _forceUpdate || (UpdateMeta != null && UpdateMeta.ForceUpdate); }
            set { _forceUpdate = value; }
        }
        bool _mustUpdate;
        /// <summary>
        /// 获得是否强制更新，否则退出
        /// </summary>
        public bool MustUpdate
        {
            get { return _mustUpdate || (UpdateMeta != null && UpdateMeta.MustUpdate); }
            set { _mustUpdate = value; }
        }

        bool _autoKillProcesses;
        /// <summary>
        /// 获得或设置是否在更新时自动结束进程
        /// </summary>
        public bool AutoKillProcesses
        {
            get { return _autoKillProcesses || (UpdateMeta != null && UpdateMeta.AutoKillProcesses); }
            set { _autoKillProcesses = value; }
        }
        bool _autoEndProcessesWithinAppDir;
        /// <summary>
        /// 获得或设置一个值，指示着当自动更新的时候是否将应用程序目录中的所有进程都作为主进程请求结束
        /// </summary>
        public bool AutoEndProcessesWithinAppDir
        {
            get { return _autoEndProcessesWithinAppDir || (UpdateMeta != null && UpdateMeta.AutoEndProcessesWithinAppDir); }
            set { _autoEndProcessesWithinAppDir = value; }
        }
        string _updatePackageListPath;
        /// <summary> 获得当前要下载的包文件信息保存的路径 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public string UpdatePackageListPath
        {
            get { return _updatePackageListPath ??= Path.Combine(UpdateTempRoot, "packages.xml"); }
        }


        string _updatePackagePath;
        /// <summary> 获得当前下载的包文件目录 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public string UpdatePackagePath
        {
            get { return _updatePackagePath ??= Path.Combine(UpdateTempRoot, "packages"); }
        }

        string _preserveFileListPath;
        /// <summary> 获得当前要保留的文件信息保存的路径 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public string PreserveFileListPath
        {
            get { return _preserveFileListPath ??= Path.Combine(UpdateTempRoot, "reservefile.xml"); }
        }



        /// <summary> 获得指定下载包的完整路径 </summary>
        /// <param name="packageName" type="string">文件名</param>
        /// <returns>完整路径</returns>
        public string GetUpdatePackageFullUrl(string packageName)
        {
            if (!string.IsNullOrEmpty(UpdateMetaFileUrl)) return string.Format(UpdateMetaFileUrl.Replace("\\", "\\\\"), packageName);
            return (UpdateMetaFileUrl.Substring(0, UpdateMetaFileUrl.LastIndexOf("/") + 1) + packageName);
        }

        #region HttpClient

        /// <summary> 获得或设置服务器用户名密码标记 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public System.Net.NetworkCredential NetworkCredential { get; set; }

        /// <summary> 获得或设置用于下载的代理服务器地址 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public string ProxyAddress { get; set; }

        /// <summary> 获得或设置网络请求的 UserAgent </summary>
        /// <value></value>
        /// <remarks></remarks>
        public string UserAgent { get; set; }

        /// <summary> 创建新的WebClient </summary>
        /// <returns></returns>
        public WebClient CreateWebClient()
        {
            var client = new WebClientWrapper();
            ResetWebClient(client);

            if (!string.IsNullOrEmpty(ProxyAddress))
            {
                client.Proxy = new WebProxy(ProxyAddress);
                if (NetworkCredential != null)
                {
                    client.Proxy.Credentials = NetworkCredential;
                }
            }
            else if (NetworkCredential != null)
            {
                client.UseDefaultCredentials = false;
                client.Credentials = NetworkCredential;
            }

            return client;
        }

        public virtual void ResetWebClient(WebClient client)
        {
            client.Headers.Clear();

            var _ua = UserAgent;
            if (String.IsNullOrEmpty(_ua))
            {
                _ua = "DotNetUpgrade v" + Updater.UpdaterClientVersion;
            }
            client.Headers.Add(HttpRequestHeader.UserAgent, _ua);
            //client.Headers.Add(HttpRequestHeader.IfNoneMatch, "DisableCache");
            client.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            client.Headers.Add(HttpRequestHeader.Pragma, "no-cache");
        }
        #endregion

        #region InitSetting
        /// <summary>
        /// 初始化当前的版本信息
        /// </summary>
        void InitializeCurrentVersion()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                CurrentVersion = new Version(FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion);
                ApplicationDirectory = Path.GetDirectoryName(assembly.Location);
            }
            else
            {
                var processModule = Process.GetCurrentProcess().MainModule;

                CurrentVersion = new Version(processModule.FileVersionInfo.FileVersion);
                ApplicationDirectory = Path.GetDirectoryName(processModule.FileName);
            }
        }
        /// <summary>
		/// 尝试从程序集中获得升级属性
		/// </summary>
		/// <returns></returns>
		Assembly TryGetCallingAssembly()
        {
            Trace.TraceInformation("尝试从堆栈跟踪中获取入口程序集...");
            try
            {
                var st = new StackTrace();
                var frame = st.GetFrame(st.FrameCount - 1);

                var assembly = frame.GetMethod().DeclaringType.Assembly;
                Trace.TraceInformation("获取到的程序集为：" + assembly.FullName);

                return assembly;
            }
            catch (Exception ex)
            {
                Trace.TraceError("从堆栈中获取程序集异常，异常信息：" + ex.ToString());
                return null;
            }
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Iwenli.DotNetUpgrade.Core
{
    /// <summary>
    /// 升级元数据
    /// </summary>
	[Serializable]
    public class UpdateMeta : INotifyPropertyChanged
    {
        private string _requiredMinVersion;
        private bool _mustUpdate;
        private string _appName;
        private string _appVersion;
        private bool _autoKillProcesses;
        private bool _forceUpdate;
        private bool _autoEndProcessesWithinAppDir;
        private bool _autoExitCurrentProcess;
        private string _package;
        private List<Package> _packages;
        private string _md5;
        private long _packageSize;
        private string _updatePingUrl;
        private string _packagePassword;
        private bool _requreAdminstrorPrivilege;

        //bool _autoEndProcessesWithinAppDir;
        ///// <summary>
        ///// 获得或设置一个值，指示着当自动更新的时候是否将应用程序目录中的所有进程都作为主进程请求结束
        ///// </summary>
        //public bool AutoEndProcessesWithinAppDir
        //{
        //	get { return _autoEndProcessesWithinAppDir; }
        //	set
        //	{
        //		if (value.Equals(_autoEndProcessesWithinAppDir)) return;
        //		_autoEndProcessesWithinAppDir = value;
        //		OnPropertyChanged("AutoEndProcessesWithinAppDir");
        //}
        //	}
        /// <summary>
        /// 应用程序名
        /// </summary>
        public string AppName
        {
            get { return _appName; }
            set
            {
                if (value == _appName)
                    return;
                _appName = value;
                OnPropertyChanged(nameof(AppName));
            }
        }

        /// <summary>
        /// 应用程序版本
        /// </summary>
        public string AppVersion
        {
            get { return _appVersion; }
            set
            {
                if (value == _appVersion)
                    return;
                _appVersion = value;
                OnPropertyChanged(nameof(AppVersion));
            }
        }

        /// <summary>
        /// 升级需要的最低版本
        /// </summary>
        public string RequiredMinVersion
        {
            get { return _requiredMinVersion; }
            set
            {
                if (value == _requiredMinVersion)
                    return;
                _requiredMinVersion = value;
                OnPropertyChanged(nameof(RequiredMinVersion));
            }
        }
        /// <summary>
		/// 获得或设置是否必须进行升级,否则拒绝运行
		/// </summary>
		public bool MustUpdate
        {
            get { return _mustUpdate; }
            set
            {
                if (value.Equals(_mustUpdate))
                    return;
                _mustUpdate = value;
                OnPropertyChanged(nameof(MustUpdate));
            }
        }
        /// <summary>
		/// 获得或设置是否自动退出当前进程
		/// </summary>
		public bool AutoExitCurrentProcess
        {
            get { return _autoExitCurrentProcess; }
            set
            {
                if (value.Equals(_autoExitCurrentProcess)) return;
                _autoExitCurrentProcess = value;
                OnPropertyChanged(nameof(AutoExitCurrentProcess));
            }
        }

        /// <summary>
        /// 获得或设置一个值，指示着当自动更新的时候是否将应用程序目录中的所有进程都作为主进程请求结束
        /// </summary>
        public bool AutoEndProcessesWithinAppDir
        {
            get { return _autoEndProcessesWithinAppDir; }
            set
            {
                if (value.Equals(_autoEndProcessesWithinAppDir)) return;
                _autoEndProcessesWithinAppDir = value;
                OnPropertyChanged(nameof(AutoEndProcessesWithinAppDir));
            }
        }


        /// <summary>
        /// 获得或设置是否在更新时自动结束进程
        /// </summary>
        public bool AutoKillProcesses
        {
            get { return _autoKillProcesses; }
            set
            {
                if (value.Equals(_autoKillProcesses)) return;
                _autoKillProcesses = value;
                OnPropertyChanged(nameof(AutoKillProcesses));
            }
        }

        /// <summary> 是否不提示用户便强制升级 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public bool ForceUpdate
        {
            get { return _forceUpdate; }
            set
            {
                if (value.Equals(_forceUpdate))
                    return;
                _forceUpdate = value;
                OnPropertyChanged(nameof(ForceUpdate));
            }
        }

        /// <summary>
		/// 安装包文件名
		/// </summary>
		public string Package
        {
            get { return _package; }
            set
            {
                if (value == _package)
                    return;
                _package = value;
                OnPropertyChanged(nameof(Package));
            }
        }
        /// <summary>
		/// 校验的HASH
		/// </summary>
		public string MD5
        {
            get { return _md5; }
            set
            {
                if (value == _md5) return;
                _md5 = value;
                OnPropertyChanged(nameof(MD5));
            }
        }

        /// <summary>
        /// 包大小
        /// </summary>
        public long PackageSize
        {
            get { return _packageSize; }
            set
            {
                if (value == _packageSize)
                    return;
                _packageSize = value;
                OnPropertyChanged(nameof(PackageSize));
            }
        }
        /// <summary>
		/// 获得或设置在进行更新前发送响应的地址
		/// </summary>
		public string UpdatePingUrl
        {
            get { return _updatePingUrl; }
            set
            {
                if (value == _updatePingUrl)
                    return;
                _updatePingUrl = value;
                OnPropertyChanged(nameof(UpdatePingUrl));
            }
        }

        /// <summary>
        /// 升级包密码
        /// </summary>
        public string PackagePassword
        {
            get { return _packagePassword; }
            set
            {
                if (value == _packagePassword)
                    return;
                _packagePassword = value;
                OnPropertyChanged(nameof(PackagePassword));
            }
        }

        /// <summary>
        /// 强行请求Administrator权限
        /// </summary>
        public bool RequreAdminstrorPrivilege
        {
            get { return _requreAdminstrorPrivilege; }
            set
            {
                if (value.Equals(_requreAdminstrorPrivilege)) return;
                _requreAdminstrorPrivilege = value;
                OnPropertyChanged(nameof(RequreAdminstrorPrivilege));
            }
        }



        /// <summary> 获得或设置更新包集合 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public List<Package> Packages
        {
            get { return _packages ??= new List<Package>(); }
            set { _packages = value; }
        }

        /// <summary>
        /// 要删除或要保留的文件
        /// </summary>
        public string[] DeleteFileLimits { get; set; }

        /// <summary>
        /// 获得删除规则的正则表达式形式
        /// </summary>
        /// <returns></returns>
        internal List<Regex> GetDeleteFileLimitRuleSet()
        {
            return DeleteFileLimits?.Select(m => new Regex(m, RegexOptions.IgnoreCase))?.ToList() ?? new List<Regex>();
        }

        /// <summary>
        /// 删除方式
        /// </summary>
        public DeletePreviousProgramMethod DeleteMethod { get; set; }

        #region Events
        /// <summary>
        /// 当属性发生变更时引发
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// 引发 <see cref="PropertyChanged"/> 事件
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}

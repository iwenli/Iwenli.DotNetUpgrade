using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Iwenli.DotNetUpgrade.Core
{
    /// <summary>
    /// 升级的的单个文件包信息
    /// </summary>
    [Serializable]
    public class Package
    {
        #region 包的原信息 - 需要持久化的信息
        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// 包哈希值
        /// </summary>
        public string Hash { get; set; }
        /// <summary>
        /// 包大小
        /// </summary>
        public long Size { get; set; }
        /// <summary>
        /// 包名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }

        /// <summary> 获得或设置本地文件的哈希值 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public string FileHash { get; set; }

        /// <summary> 更新模式 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public UpdateMethod Method { get; set; }

        /// <summary> 获得或设置当前文件验证等级 </summary>
        /// <value></value>
        /// <remarks></remarks>
        public FileVerificationLevel VerificationLevel { get; set; }
        /// <summary>
        /// 获得或设置关联的文件
        /// </summary>
        public string[] Files { get; set; }

        /// <summary>
        /// 功能标记。
        /// </summary>
        public string ComponentId { get; set; }
        #endregion

        #region 扩展属性-为了运行时而引入，非固化在升级包中的属性

        /// <summary> 获得或设置处理用的上下文环境 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public UpdateContext Context { get; set; }


        /// <summary> 获得当前包是否正在下载 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public bool IsDownloading { get; internal set; }

        /// <summary> 获得当前包是否已经下载 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public bool IsDownloaded { get; internal set; }

        /// <summary> 获得处理过程中最后发生的错误 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public Exception LastError { get; internal set; }

        /// <summary> 获得重试次数计数 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public int? RetryCount { get; internal set; }

        /// <summary> 获得本地保存路径 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public string LocalSavePath
        {
            get
            {
                if (Context == null) throw new InvalidOperationException("尚未附加到上下文中");
                return System.IO.Path.Combine(Context.UpdatePackagePath, Name);
            }
        }

        string _sourceUri;

        /// <summary> 获得下载的源URL </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public string SourceUri
        {
            get
            {
                if (Context == null) throw new InvalidOperationException("尚未附加到上下文中");

                return _sourceUri ??= Context.GetUpdatePackageFullUrl(Name);
            }
        }

        bool? _hashResult;

        /// <summary> 获得本地的包文件是否有效 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public bool? IsLocalFileValid
        {
            get
            {
                var path = LocalSavePath;
                if (!System.IO.File.Exists(path)) return null;
                return _hashResult ??= path.GetFileHash() == Hash;
            }
        }

        /// <summary> 获得已下载的长度 </summary>
        /// <value></value>
        /// <remarks></remarks>
        [XmlIgnore]
        public long DownloadedSize { get; internal set; }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 确定是否有此标记位
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        internal bool HasVerifyFlag(FileVerificationLevel level)
        {
            return (level & VerificationLevel) > 0;
        }

        internal void IncreaseFailureCounter()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

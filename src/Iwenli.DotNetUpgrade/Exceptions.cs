using System;
using System.Collections.Generic;
using System.Text;

namespace Iwenli.DotNetUpgrade
{
    /// <summary>
    /// 版本过低无法更新异常
    /// </summary>
    public class VersionTooLowException : Exception
    {
        /// <summary>
        /// 需要的最低版本
        /// </summary>
        public Version MinimumVersion { get; private set; }

        /// <summary>
        /// 当前版本
        /// </summary>
        public Version CurrentVersion { get; private set; }

        /// <summary>
        /// 创建 <see cref="VersionTooLowException"/> 的新对象
        /// </summary>
        /// <param name="currentVersion"></param>
        /// <param name="minimumVersion"></param>
        public VersionTooLowException(Version currentVersion, Version minimumVersion) :
            base($"当前更新要求最低版本为{minimumVersion}，已安装版本为{currentVersion}，请手动跟新")
        {
            CurrentVersion = currentVersion;
            MinimumVersion = minimumVersion;
        }
    }
}

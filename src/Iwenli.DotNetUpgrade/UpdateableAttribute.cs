using Iwenli.DotNetUpgrade.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Iwenli.DotNetUpgrade
{
    /// <summary>
    /// 一个自定义属性，表示当前的应用程序支持自动更新
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class UpdateableAttribute : Attribute
    {
        /// <summary>
        /// 获得一个值，表示当前应用程序定义的升级服务器信息
        /// </summary>
        public Server Server { get; private set; }

        /// <summary>
        /// 创建 UpdateableAttribute class 的新实例
        /// </summary>
        public UpdateableAttribute(string address, string manifest)
        {
            Server = new Server(address, manifest);
        }
    }
}

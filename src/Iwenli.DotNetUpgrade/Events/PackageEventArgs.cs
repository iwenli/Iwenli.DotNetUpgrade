using Iwenli.DotNetUpgrade.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Iwenli.DotNetUpgrade.Events
{
	/// <summary> 表示包文件操作事件数据 </summary>
	/// <remarks></remarks>
	public class PackageEventArgs : EventArgs
	{
		/// <summary> 获得当前正在操作的包 </summary>
		/// <value></value>
		/// <remarks></remarks>
		public Package Package { get; private set; }

		/// <summary>
		/// 创建 <see cref="PackageEventArgs" />  的新实例(PackageDownloadEventArgs)
		/// </summary>
		public PackageEventArgs(Package package) { Package = package; }
	}
}

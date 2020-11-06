using Iwenli.DotNetUpgrade.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iwenli.DotNetUpgrade
{
    public partial class Updater
    {
        static Updater _instance;

        static Updater()
        {
            var ass = System.Reflection.Assembly.GetExecutingAssembly();
            UpdaterClientVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(ass.Location).ConvertVersionInfo().ToString();
        }

        /// <summary>
        /// 当前的更新实例。直接访问本属性将会获得默认创建的Updater实例。要使用更多信息创建，请使用 <see cref="CreateUpdaterInstance"/> 方法，请确保在使用本属性之前创建。
        /// </summary>
        public static Updater Instance => _instance ??= CreateUpdaterInstance();

        /// <summary>
        /// 获得当前的更新客户端版本
        /// </summary>
        public static string UpdaterClientVersion { get; private set; }



        public static bool CheckUpdate(Server server)
        {
            if (_instance == null)
                _instance = CreateUpdaterInstance(null, null, server);

            return Instance.BeginUpdateChecking();
        }

        /// <summary>
        /// 创建自动更新客户端
        /// </summary>
        /// <param name="appVersion">应用程序版本，留空将会使用自动判断</param>
        /// <param name="appDirectory">应用程序目录，留空将会使用自动判断</param>
        /// <param name="servers">升级服务器地址</param>
        /// <returns></returns>
        public static Updater CreateUpdaterInstance(Version appVersion = null, string appDirectory = null, params Server[] servers)
        {
            CheckInitialized();

            _instance = new Updater(appVersion, appDirectory, servers);

            return _instance;
        }

        /// <summary>
		/// 确认没有重复调用
		/// </summary>
		static void CheckInitialized()
        {
            if (_instance == null)
                return;

            throw new InvalidOperationException("Updater 已经被初始化。此方法调用之前，不可先调用任何可能会导致Updater被初始化的操作。");
        }
    }
}

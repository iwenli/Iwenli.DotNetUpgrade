using Iwenli.DotNetUpgrade.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Iwenli.DotNetUpgrade
{
    public partial class Updater
    {
        /// <summary>
		/// 检测组件标记
		/// </summary>
		/// <param name="compId">组件ID</param>
		/// <returns></returns>
        private bool CheckComponentFlag(string compId)
        {
            var dic = Context.ComponentStatus;
            if (dic.ContainsKey(compId))
                return dic[compId];

            var ea = new RequestCheckComponentFlagEventArgs(compId);
            OnRequestCheckComponentFlag(ea);
            dic.Add(compId, ea.Valid);

            return ea.Valid;
        }
        /// <summary>
        /// 请求检测组件状态位
        /// </summary>
        public event EventHandler<RequestCheckComponentFlagEventArgs> RequestCheckComponentFlag;

        /// <summary>
        /// 引发 <see cref="RequestCheckComponentFlag" /> 事件
        /// </summary>
        /// <param name="ea">包含此事件的参数</param>
        protected virtual void OnRequestCheckComponentFlag(RequestCheckComponentFlagEventArgs e)
        {
            RequestCheckComponentFlag?.Invoke(this, e);
        }

        /// <summary>
        /// 发现了更新
        /// </summary>
        public event EventHandler Found;
        /// <summary>
        /// 引发 <see cref="Found"/> 事件
        /// </summary>
        protected virtual void OnFound()
        {
            Found?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 没有发现更新
        /// </summary>
        public event EventHandler NoFound;
        /// <summary>
        /// 引发 <see cref="NoFound"/> 事件
        /// </summary>
        protected virtual void OnNoFound()
        {
            if (PeekNextServer())
            {
                Trace.TraceWarning("尝试更新时出现服务器错误。正尝试自动切换至其它的服务器节点。已切换至 " + Context.Services[_serverIndex].Address);
                BeginUpdateChecking();
            }
            else
            {
                Trace.TraceWarning("尝试更新时出现服务器错误，且服务器已遍历完成。");
                NoFound?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 更新发生错误
        /// </summary>
        public event EventHandler Error;
        /// <summary>
        /// 引发 <see cref="Error"/> 事件
        /// </summary>
        protected virtual void OnError()
        {
            if (PeekNextServer())
            {
                Trace.TraceWarning("尝试更新时出现服务器错误。正尝试自动切换至其它的服务器节点。已切换至 " + Context.Services[_serverIndex].Address);
                BeginUpdateChecking();
            }
            else
            {
                Trace.TraceWarning("尝试更新时出现服务器错误，且服务器已遍历完成。");
                CleanTemp();
                Error?.Invoke(this, EventArgs.Empty);

                if (!Context.IsInUpdateMode && Context.MustUpdate)
                    TerminateProcess(this);
            }
        }


        /// <summary>
        /// 检测更新完成
        /// </summary>
        public event EventHandler CheckCompleted;
        /// <summary>
        /// 引发 <see cref="CheckCompleted"/> 事件
        /// </summary>
        protected virtual void OnCheckCompleted()
        {
            CheckCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
		/// 不满足最低版本要求
		/// </summary>
		public event EventHandler MinmumVersionRequired;

        /// <summary>
        /// 引发 <see cref="MinmumVersionRequired" /> 事件
        /// </summary>
        protected virtual void OnMinmumVersionRequired()
        {
            MinmumVersionRequired?.Invoke(this, EventArgs.Empty);
        }

        /// <summary> 操作进度发生变更 </summary>
		/// <remarks></remarks>
		public event EventHandler<ProgressChangedEventArgs> OperationProgressChanged;

        protected virtual void OnOperationProgressChanged(ProgressChangedEventArgs e)
        {
            Trace.TraceInformation($"当前进度：{e.ProgressPercentage},参数:{e.UserState}");
            OperationProgressChanged?.Invoke(this, e);
        }

        #region 检测更新
        /// <summary>
		/// 开始下载更新信息文件
		/// </summary>
		public event EventHandler UpdateMetaDownloading;

        /// <summary>
        /// 引发 <see cref="UpdateMetaDownloading"/> 事件
        /// </summary>
        protected virtual void OnUpdateMetaDownloading()
        {
            UpdateMetaDownloading?.Invoke(this, EventArgs.Empty);
        }
        /// <summary>
		/// 结束下载更新信息文件
		/// </summary>
		public event EventHandler UpdateMetaDownloadFinished;

        /// <summary>
        /// 引发 <see cref="UpdateMetaDownloadFinished"/> 事件
        /// </summary>
        public virtual void OnUpdateMetaDownloadFinished()
        {
            UpdateMetaDownloadFinished?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region 更新流程
        /// <summary>
        /// 正在中止当前进程
        /// </summary>
        public static event EventHandler<CancelEventArgs> RequireTerminateProcess;

        /// <summary>
        /// 引发 <see cref="RequireTerminateProcess" /> 事件
        /// </summary>
        /// <param name="ea">包含此事件的参数</param>
        internal static void OnRequireTerminateProcess(object sender, CancelEventArgs e)
        {
            RequireTerminateProcess?.Invoke(sender, e);
        }
        #endregion

        #region 确定要下载的包

        /// <summary> 确定需要下载的包 </summary>
        /// <remarks></remarks>
        public event EventHandler GatheringPackages;

        /// <summary> 引发 <see cref="GatheringPackages"/> 事件 </summary>
        protected virtual void OnGatheringPackages()
        {
            GatheringPackages?.Invoke(this, EventArgs.Empty);
        }

        /// <summary> 已确定要下载的包 </summary>
        /// <remarks></remarks>
        public event EventHandler GatheredPackages;

        /// <summary> 引发 <see cref="GatheredPackages"/> 事件 </summary>
        protected virtual void OnGatheredPackages()
        {
            GatheredPackages?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region 包下载事件

        ///// <summary> 下载进度发生变化事件 </summary>
        ///// <remarks></remarks>
        //public event EventHandler<PackageDownloadProgressChangedEventArgs> DownloadProgressChanged;

        ///// <summary>
        ///// 引发 <see cref="DownloadProgressChanged"/> 事件
        ///// </summary>
        ///// <param name="e"></param>
        //public virtual void OnDownloadProgressChanged(PackageDownloadProgressChangedEventArgs e)
        //{
        //    var handler = DownloadProgressChanged;

        //    if (handler != null) handler(this, e);
        //}

        /// <summary> 开始下载指定的包 </summary>
        /// <remarks></remarks>
        public event EventHandler<PackageEventArgs> PackageDownload;

        /// <summary>
        /// 引发 <see cref="PackageDownload" /> 事件
        /// </summary>
        /// <param name="e">包含此事件的参数</param>
        public virtual void OnPackageDownload(PackageEventArgs e)
        {
            PackageDownload?.Invoke(this, e);
        }

        /// <summary> 指定的包下载完成 </summary>
        /// <remarks></remarks>
        public event EventHandler<PackageEventArgs> PackageDownloadFinished;

        /// <summary>
        /// 引发 <see cref="PackageDownloadFinished" /> 事件
        /// </summary>
        /// <param name="ea">包含此事件的参数</param>
        public virtual void OnPackageDownloadFinished(PackageEventArgs e)
        {
            PackageDownloadFinished?.Invoke(this, e);
        }

        /// <summary> 包下载失败 </summary>
        /// <remarks></remarks>
        public event EventHandler<PackageEventArgs> PackageDownloadFailed;

        /// <summary>
        /// 引发 <see cref="PackageDownloadFailed" /> 事件
        /// </summary>
        /// <param name="ea">包含此事件的参数</param>
        public virtual void OnPackageDownloadFailed(PackageEventArgs e)
        {
            PackageDownloadFailed?.Invoke(this, e);
        }

        /// <summary> 下载的包Hash不对 </summary>
        /// <remarks></remarks>
        public event EventHandler<PackageEventArgs> PackageHashMismatch;

        /// <summary>
        /// 引发 <see cref="PackageHashMismatch" /> 事件
        /// </summary>
        /// <param name="e">包含此事件的参数</param>
        public virtual void OnPackageHashMismatch(PackageEventArgs e)
        {
            PackageHashMismatch?.Invoke(this, e);
        }

        /// <summary> 包重试下载 </summary>
        /// <remarks></remarks>
        public event EventHandler<PackageEventArgs> PackageDownloadRetried;

        /// <summary>
        /// 引发 <see cref="PackageDownloadRetried" /> 事件
        /// </summary>
        /// <param name="e">包含此事件的参数</param>
        public virtual void OnPackageDownloadRetried(PackageEventArgs e)
        {
            PackageDownloadRetried?.Invoke(this, e);
        }
        #endregion
    }
}

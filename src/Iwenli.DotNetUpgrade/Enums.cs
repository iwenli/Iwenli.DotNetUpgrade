using System;
using System.Collections.Generic;
using System.Text;

namespace Iwenli.DotNetUpgrade
{
    /// <summary> 更新模式 </summary>
    /// <remarks></remarks>
    [Flags]
    public enum UpdateMethod
    {
        /// <summary>
        /// 同项目定义
        /// </summary>
        AsProject = 0,
        /// <summary>版本控制</summary>
        /// <remarks></remarks>
        VersionCompare = 1,
        /// <summary> 如果存在则跳过</summary>
        /// <remarks></remarks>
        SkipIfExists = 2,
        /// <summary>
        /// 忽略
        /// </summary>
        Ignore = 4,
        /// <summary>
        /// 如果不存在则跳过
        /// </summary>
        SkipIfNotExist = 8,
        /// <summary>总是更新</summary>
        /// <remarks></remarks>
        Always = 16,
    }


    /// <summary> 文件验证等级 </summary>
    /// <remarks></remarks>
    [Flags]
    public enum FileVerificationLevel
    {
        /// <summary>
        /// 没有
        /// </summary>
        None = 0,
        /// <summary> 验证大小 </summary>
        /// <remarks></remarks>
        Size = 1,
        /// <summary> 验证版本 </summary>
        /// <remarks></remarks>
        Version = 2,
        /// <summary> 验证Hash </summary>
        /// <remarks></remarks>
        Hash = 4
    }

    /// <summary>
	/// 删除旧的程序文件方式
	/// </summary>
	public enum DeletePreviousProgramMethod
    {
        /// <summary>
        /// 不主动删除仅覆盖
        /// </summary>
        None = 0,
        /// <summary>
        /// 删除除明确要求保留之外的文件和目录
        /// </summary>
        AllExceptSpecified = 1,
        /// <summary>
        /// 仅删除明确要求删除的文件和目录
        /// </summary>
        NoneButSpecified = 2
    }
}

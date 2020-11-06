using System;
using System.Collections.Generic;
using System.Text;

namespace Iwenli.DotNetUpgrade
{
    public static class Extensions
    {

        /// <summary> 比较文件的版本和指定的版本。如果文件版本低于指定版本则返回true </summary>
        /// <param name="filePath" type="string"></param>
        /// <param name="version" type="System.Version"></param>
        /// <returns> bool </returns>
        public static bool CompareVersion(this string filePath, string version)
        {
            var fv = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath);
            if (fv == null) throw new ApplicationException("无法获得文件 " + filePath + " 的版本信息");

            return version != fv.ConvertVersionInfo().ToString();
        }

        /// <summary> 将文件版本信息转换为本地版本信息 </summary>
        /// <param name="fvi" type="System.Diagnostics.FileVersionInfo">类型为 <see>System.Diagnostics.FileVersionInfo</see> 的参数</param>
        /// <returns></returns>
        public static Version ConvertVersionInfo(this System.Diagnostics.FileVersionInfo fvi)
        {
            return new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
        }

        /// <summary> 获得指定文件的Hash值 </summary>
		/// <param name="filePath" type="string">文件路径</param>
		/// <returns></returns>
		public static string GetFileHash(this string filePath)
        {
            var cpter = System.Security.Cryptography.MD5.Create();
            return BitConverter.ToString(cpter.ComputeHash(System.IO.File.ReadAllBytes(filePath))).Replace("-", "").ToUpper();
        }

        /// <summary>
        /// 解压缩一个字节流
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        internal static byte[] Decompress(this byte[] buffer)
        {
            using (var source = new System.IO.MemoryStream(buffer))
            {
                source.Seek(0, System.IO.SeekOrigin.Begin);

                using (var dest = new System.IO.MemoryStream())
                using (var gz = new System.IO.Compression.GZipStream(source, System.IO.Compression.CompressionMode.Decompress))
                {
                    var buf = new byte[0x400];
                    var count = 0;
                    while ((count = gz.Read(buf, 0, buf.Length)) > 0)
                    {
                        dest.Write(buf, 0, count);
                    }

                    dest.Close();
                    return dest.ToArray();
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Iwenli.DotNetUpgrade.Core
{
	/// <summary>
	/// XML序列化支持类
	/// </summary>
	public static class SerializeHelper
	{
		/// <summary>
		/// xml反序列化
		/// </summary>
		/// <returns>保存信息的 <see cref="T:System.String"/></returns>
		public static T XmlDeserialize<T>(string content) where T : class
		{
			if (String.IsNullOrEmpty(content))
				return null;

            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            try
            {
                var xso = new XmlSerializer(typeof(T));
                return (T)xso.Deserialize(ms);
            }
            catch (Exception ex)
            {
                Trace.TraceInformation("执行反序列化时发生错误 ----> \r\n" + ex.ToString());
                return default(T);
            }
        }

		/// <summary>
		/// 序列化xml对象到文件
		/// </summary>
		/// <param name="objectToSerialize">要序列化的对象</param>
		/// <param name="fileName">保存到的目标文件</param>
		public static void XmlSerilizeToFile(object objectToSerialize, string fileName)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(fileName));

            using var stream = new FileStream(fileName, FileMode.Create);
            var xso = new XmlSerializer(objectToSerialize.GetType());
            xso.Serialize(stream, objectToSerialize);
            stream.Close();
        }

	}
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Iwenli.DotNetUpgrade.Core
{
    /// <summary>
    /// 升级服务器
    /// </summary>
    public class Server
    {
        public Server(string address, string manifest)
        {
            Address = address;
            Manifest = manifest;
        }

        public string Address { get; }
        public string Manifest { get; }
    }
}

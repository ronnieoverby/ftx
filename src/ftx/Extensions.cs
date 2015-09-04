using System;
using System.Net;
using CoreTechs.Common;

namespace ftx
{
    public static class Extensions
    {
        public static IPEndPoint ToIpEndPoint(this DnsEndPoint dnsEndPoint)
        {
            if (dnsEndPoint == null) throw new ArgumentNullException("dnsEndPoint");
            var address = Dns.GetHostAddresses(dnsEndPoint.Host).RandomElement();
            return new IPEndPoint(address, dnsEndPoint.Port);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Threading;
using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
using Microsoft.Extensions.Configuration;
using tik4net;
using tik4net.Objects;
using tik4net.Objects.Ip;

namespace MikrotikAccounting
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            while (true)
            {
                using ITikConnection connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);

                var host = configuration.GetSection("Mikrotik").GetSection("Host").Value;
                var user = configuration.GetSection("Mikrotik").GetSection("User").Value;
                var password = configuration.GetSection("Mikrotik").GetSection("Password").Value;

                var influxDbEnabled = bool.Parse(configuration.GetSection("InfluxDb").GetSection("Enabled").Value);
                var influxDbUrl = configuration.GetSection("InfluxDb").GetSection("Url").Value;
                var influxDatabase = configuration.GetSection("InfluxDb").GetSection("Database").Value;
                var influxMeasurement = configuration.GetSection("InfluxDb").GetSection("Measurement").Value;
                var isConsoleOutput = bool.Parse(configuration.GetSection("App").GetSection("ConsoleOutput").Value);
                var sleepTimeout = int.Parse(configuration.GetSection("App").GetSection("SleepTimeout").Value);

                connection.Open(host, user, password);
                connection.ExecuteNonQuery("/ip/accounting/snapshot/take");
                var interfaces = connection.LoadList<IpAddress>()
                    .ToImmutableSortedDictionary(d => d.Address, a => a.Interface);
                var acctList = connection.LoadList<AccountingSnapshot>();

                var payload = new LineProtocolPayload();

                foreach (var item in acctList)
                {
                    var point = new LineProtocolPoint(
                        influxMeasurement,
                        new Dictionary<string, object>
                        {
                        { "bytes", item.Bytes },
                        { "packets", item.Packets },
                        },
                        TagDirection(item, interfaces),
                        DateTime.UtcNow);
                    payload.Add(point);
                }

                if(influxDbEnabled) { 
                    var client = new LineProtocolClient(new Uri(influxDbUrl), influxDatabase);
                    client.WriteAsync(payload);
                }
                if (isConsoleOutput)
                {
                    var wr = new StringWriter();
                    payload.Format(wr);
                    Console.WriteLine(wr.GetStringBuilder().ToString());
                }

                Thread.Sleep(sleepTimeout);
            }
        }

        private static IReadOnlyDictionary<string, string> TagDirection(AccountingSnapshot item, ImmutableSortedDictionary<string, string> interfaces)
        {
            var result = new Dictionary<string, string>
            {
                {"srcAddress", item.SrcAddress},
                {"dstAddress", item.DstAddress},
                //{"srcUser", item.SrcUser},
                //{"dstUser", item.DstUser},
            };
            foreach (var i in interfaces)
            {
                if (IPAddress.Parse(item.DstAddress).IsInSubnet(i.Key))
                {
                    result.Add("ip", item.DstAddress);
                    result.Add("direction", "Download");
                    result.Add("interface", interfaces[i.Key]);

                    break;
                }

                if (IPAddress.Parse(item.SrcAddress).IsInSubnet(i.Key))
                {
                    result.Add("ip", item.SrcAddress);
                    result.Add("direction", "Upload");
                    result.Add("interface", interfaces[i.Key]);

                    break;
                }

            }

            return result;
        }
    }
}

using Newtonsoft.Json.Linq;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using TencentCloud.Common;
using TencentCloud.Common.Profile;

[assembly: SupportedOSPlatform("windows")]

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin<
        TencentOptions, TencentOptionsFactory,
        DnsValidationCapability, TencentJson>
        ("6ea628c3-0f74-68bb-cf17-4fdd3d53f3af",
        "Tencent", "Create verification records in Tencent DNS")]
    public class Tencent : DnsValidation<Tencent>, IDisposable
    {
        private TencentOptions _options { get; }
        private SecretServiceManager _ssm { get; }
        private HttpClient _hc { get; }
        private Credential _cred { get; }

        public Tencent(
            TencentOptions options,
            SecretServiceManager ssm,
            IProxyService proxyService,
            LookupClientProvider dnsClient,
            ILogService log,
            ISettingsService settings) : base(dnsClient, log, settings)
        {
            _options = options;
            _ssm = ssm;
            _hc = proxyService.GetHttpClient();
            //
            _cred = new Credential
            {
                SecretId = _ssm.EvaluateSecret(_options.ApiID),
                SecretKey = _ssm.EvaluateSecret(_options.ApiKey),
            };
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            await Task.Delay(0);
            try
            {
                var identifier = record.Context.Identifier;
                var domain = record.Authority.Domain;
                var value = record.Value;
                //���ӽ�����¼
                return AddRecord(identifier, domain, value);
            }
            catch { }
            return false;
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            await Task.Delay(0);
            try
            {
                var identifier = record.Context.Identifier;
                var domain = record.Authority.Domain;
                //ɾ��������¼
                DelRecord(identifier, domain);
            }
            catch { }
        }

        #region ˽���߼�

        /// <summary>
        /// ���ӽ�����¼
        /// </summary>
        /// <param name="domain">����</param>
        /// <param name="subDomain">����¼</param>
        /// <param name="value">������¼</param>
        /// <returns></returns>
        private bool AddRecord(string domain, string subDomain, string value)
        {
            subDomain = subDomain.Replace($".{domain}", "");
            //ɾ��
            DelRecord(domain, subDomain);
            //����
            var mod = "dnspod";
            var ver = "2021-03-23";
            var act = "CreateRecord";
            var region = "";
            var endpoint = "dnspod.tencentcloudapi.com";
            var hpf = new HttpProfile
            {
                ReqMethod = "POST",
                Endpoint = endpoint,
            };
            var cpf = new ClientProfile(ClientProfile.SIGN_TC3SHA256, hpf);
            var client = new CommonClient(mod, ver, _cred, region, cpf);
            var param = new
            {
                Domain = domain,
                SubDomain = subDomain,
                RecordType = "TXT",
                RecordLine = "Ĭ��",
                Value = value,
            };
            var req = new CommonRequest(param);
            var resp = client.Call(req, act);
            //Console.WriteLine(resp);
            return true;
        }

        /// <summary>
        /// ɾ��������¼
        /// </summary>
        /// <param name="domain">����</param>
        /// <param name="subDomain">����¼</param>
        /// <returns></returns>
        private bool DelRecord(string domain, string subDomain)
        {
            subDomain = subDomain.Replace($".{domain}", "");
            //���
            var recordId = GetRecordID(domain, subDomain);
            if (recordId == default) return false;
            //ɾ��
            var mod = "dnspod";
            var ver = "2021-03-23";
            var act = "DeleteRecord";
            var region = "";
            var endpoint = "dnspod.tencentcloudapi.com";
            var hpf = new HttpProfile
            {
                ReqMethod = "POST",
                Endpoint = endpoint,
            };
            var cpf = new ClientProfile(ClientProfile.SIGN_TC3SHA256, hpf);
            var client = new CommonClient(mod, ver, _cred, region, cpf);
            var param = new { Domain = domain, RecordId = recordId };
            var req = new CommonRequest(param);
            var resp = client.Call(req, act);
            //Console.WriteLine(resp);
            return true;
        }

        /// <summary>
        /// ��ȡ����ID
        /// </summary>
        /// <param name="domain">����</param>
        /// <param name="subDomain">����¼</param>
        /// <returns></returns>
        private long GetRecordID(string domain, string subDomain)
        {
            var mod = "dnspod";
            var ver = "2021-03-23";
            var act = "DescribeRecordList";
            var region = "";
            var endpoint = "dnspod.tencentcloudapi.com";
            var hpf = new HttpProfile
            {
                ReqMethod = "POST",
                Endpoint = endpoint,
            };
            var cpf = new ClientProfile(ClientProfile.SIGN_TC3SHA256, hpf);
            var client = new CommonClient(mod, ver, _cred, region, cpf);
            var param = new { Domain = domain };
            var req = new CommonRequest(param);
            var resp = client.Call(req, act);
            //Console.WriteLine(resp);
            //����ȡֵ
            var json = JObject.Parse(resp);
            var jsonData = json["Response"]!["RecordList"];
            var jsonDataLinq = jsonData!.Where(w => w["Name"]!.ToString() == subDomain && w["Type"]!.ToString() == "TXT");
            if (jsonDataLinq.Any()) return (long)jsonDataLinq.First()["RecordId"]!;
            return default;
        }

        #endregion ˽���߼�

        public void Dispose() => _hc.Dispose();
    }
}

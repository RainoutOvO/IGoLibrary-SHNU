using RestSharp;
using IGoLibrary_Winform.CustomException;

namespace IGoLibrary_Winform.Controller
{
    public class GetCookieServiceImpl : IGetCookieService
    {
        public string GetCookie(string code)
        {
            var client = new RestClient(string.Format("http://wechat.v2.traceint.com/index.php/urlNew/auth.html?r=https%3A%2F%2Flibseat.shnu.edu.cn%2Fweb%2F&code={0}&state=1",code));
            var request = new RestRequest();
            request.Method = Method.Get;
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(5000);//设定超时时间为5000ms
            RestResponse response = client.Execute(request,cts.Token);
            var cookieCollection = response.Cookies;
            if(cookieCollection != null)
            {
                string authorization = string.Empty;
                string serverId = string.Empty;
                foreach (var cookie in cookieCollection)
                {
                    var cookieText = cookie.ToString();
                    if (cookieText.StartsWith("Authorization=", StringComparison.OrdinalIgnoreCase))
                    {
                        authorization = cookieText;
                    }
                    else if (cookieText.StartsWith("SERVERID=", StringComparison.OrdinalIgnoreCase))
                    {
                        serverId = cookieText;
                    }
                }

                if(!string.IsNullOrWhiteSpace(authorization) && !string.IsNullOrWhiteSpace(serverId))
                {
                    return authorization + "; " + serverId;
                }
                else
                    throw new GetCookieException("Cookie不包含关键身份信息，可能是code过期，重新填写含code的链接");
            }
            else
                throw new GetCookieException("响应报文返回的Cookie为空");
        }
    }
}

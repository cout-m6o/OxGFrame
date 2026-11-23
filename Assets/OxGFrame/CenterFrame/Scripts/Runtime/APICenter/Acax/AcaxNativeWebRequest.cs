using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OxGKit.LoggingSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace OxGFrame.CenterFrame.APICenter
{
    /// <summary>
    /// HTTP requester implemented with native HttpWebRequest
    /// </summary>
    public static class HttpNativeWebRequest
    {
        /// <summary>
        /// Default request timeout in seconds
        /// </summary>
        private const int _MAX_REQUEST_TIME_SECONDS = 60;

        /// <summary>
        /// Callback C# and Xml = Acax
        /// <para> This overload builds the JSON body from a key-value pair array </para>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        /// <param name="success"></param>
        /// <param name="error"></param>
        /// <param name="timeoutSeconds"></param>
        public static void Acax(string url, string method, string[,] headers, object[,] body, ResponseHandle success = null, ResponseErrorHandle error = null, int? timeoutSeconds = null)
        {
            method = method.ToUpperInvariant();
            RequestAPI(url, method, headers, body, success, error, timeoutSeconds).Forget();
        }

        /// <summary>
        /// Callback C# and Xml = Acax
        /// <para> This overload serializes the given body object directly to JSON </para>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        /// <param name="success"></param>
        /// <param name="error"></param>
        /// <param name="timeoutSeconds"></param>
        public static void Acax(string url, string method, string[,] headers, object body, ResponseHandle success = null, ResponseErrorHandle error = null, int? timeoutSeconds = null)
        {
            method = method.ToUpperInvariant();
            RequestAPI(url, method, headers, body, success, error, timeoutSeconds).Forget();
        }

        /// <summary>
        /// Asynchronous with Callback C# and Xml = Acax
        /// <para> This overload builds the JSON body from a key-value pair array </para>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        /// <param name="success"></param>
        /// <param name="error"></param>
        /// <param name="timeoutSeconds"></param>
        /// <returns></returns>
        public async static UniTask<string> AcaxAsync(string url, string method, string[,] headers, object[,] body, ResponseHandle success = null, ResponseErrorHandle error = null, int? timeoutSeconds = null)
        {
            method = method.ToUpperInvariant();
            return await RequestAPI(url, method, headers, body, success, error, timeoutSeconds);
        }

        /// <summary>
        /// Asynchronous with Callback C# and Xml = Acax
        /// <para> This overload serializes the given body object directly to JSON </para>
        /// </summary>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        /// <param name="success"></param>
        /// <param name="error"></param>
        /// <param name="timeoutSeconds"></param>
        /// <returns></returns>
        public async static UniTask<string> AcaxAsync(string url, string method, string[,] headers, object body, ResponseHandle success = null, ResponseErrorHandle error = null, int? timeoutSeconds = null)
        {
            method = method.ToUpperInvariant();
            return await RequestAPI(url, method, headers, body, success, error, timeoutSeconds);
        }

        #region Internal Methods
        internal static async UniTask<string> RequestAPI(string url, string method, string[,] headers, object body, ResponseHandle success, ResponseErrorHandle error, int? timeoutSeconds)
        {
            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = method;

                // Timeout (ms)
                request.Timeout = (timeoutSeconds ?? _MAX_REQUEST_TIME_SECONDS) * 1000;

                // Header args
                if (headers != null && headers.Length > 0)
                {
                    for (int row = 0; row < headers.GetLength(0); row++)
                    {
                        if (headers.GetLength(1) != 2)
                            continue;

                        string headerName = headers[row, 0];
                        string headerValue = headers[row, 1];
                        if (headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            request.ContentType = headerValue;
                        }
                        else
                        {
                            _SetRequestHeader(request, headerName, headerValue);
                        }
                    }
                }

                // Body args
                if (body != null)
                {
                    string json = null;
                    if (body is object[,] bodyArray)
                    {
                        if (bodyArray.Length > 0)
                        {
                            Dictionary<string, object> jsonArgs = new Dictionary<string, object>();
                            for (int row = 0; row < bodyArray.GetLength(0); row++)
                            {
                                if (bodyArray.GetLength(1) != 2)
                                    continue;
                                jsonArgs.Add((string)bodyArray[row, 0], bodyArray[row, 1]);
                            }
                            json = JsonConvert.SerializeObject(jsonArgs);
                        }
                    }
                    else
                    {
                        json = JsonConvert.SerializeObject(body);
                    }

                    if (!string.IsNullOrEmpty(json))
                    {
                        using (var streamWriter = new StreamWriter(await request.GetRequestStreamAsync()))
                        {
                            await streamWriter.WriteAsync(json);
                        }
                    }
                }

                // Start send request
                using (WebResponse response = await request.GetResponseAsync())
                {
                    using (Stream s = response.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(s))
                        {
                            string result = await sr.ReadToEndAsync();
                            success?.Invoke(result);
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorInfo = new ErrorInfo();
                errorInfo.url = url;
                errorInfo.message = ex.Message;
                errorInfo.exception = ex;
                error?.Invoke(errorInfo);
                Logging.PrintError<Logger>($"RequestAPI failed. URL: {errorInfo.url}, ErrorMsg: {errorInfo.message}, Exception: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 設置請求標頭
        /// <para> HttpWebRequest 的受限標頭 (Restricted Headers) 無法透過 Headers.Add 設置，會拋出 ArgumentException 導致整個請求失敗，因此常用的受限標頭需改由對應的屬性進行設置 </para>
        /// </summary>
        /// <param name="request"></param>
        /// <param name="headerName"></param>
        /// <param name="headerValue"></param>
        private static void _SetRequestHeader(HttpWebRequest request, string headerName, string headerValue)
        {
            if (!WebHeaderCollection.IsRestricted(headerName))
            {
                request.Headers.Add(headerName, headerValue);
                return;
            }

            if (headerName.Equals("Accept", StringComparison.OrdinalIgnoreCase))
            {
                request.Accept = headerValue;
            }
            else if (headerName.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                request.UserAgent = headerValue;
            }
            else if (headerName.Equals("Referer", StringComparison.OrdinalIgnoreCase))
            {
                request.Referer = headerValue;
            }
            else if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                request.Host = headerValue;
            }
            else if (headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase))
            {
                // Keep-Alive 與 Close 無法透過 Connection 屬性設置，需改由 KeepAlive 屬性控制
                string connection = (headerValue != null) ? headerValue.Trim() : string.Empty;
                if (connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase))
                    request.KeepAlive = true;
                else if (connection.Equals("close", StringComparison.OrdinalIgnoreCase))
                    request.KeepAlive = false;
                else
                    request.Connection = headerValue;
            }
            else
            {
                // 其餘的受限標頭維持原行為 (由 Headers.Add 拋出異常，交由外層的錯誤處理回調)
                request.Headers.Add(headerName, headerValue);
            }
        }
        #endregion
    }
}
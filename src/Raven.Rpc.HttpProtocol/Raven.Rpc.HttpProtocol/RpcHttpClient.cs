﻿#if RavenRpcHttpProtocol40
#else
using Raven.Rpc.HttpProtocol.Exceptions;
using Raven.Rpc.HttpProtocol.Formatters;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Raven.Rpc.HttpProtocol
{
    /// <summary>
    /// 
    /// </summary>
    public class RpcHttpClient : IRpcHttpClient, IRpcHttpClientAsync, IDisposable
    //where RT : class, new()
    {
        private const int defalut_timeout = 10000;

        private string _baseUrl;
        private int _timeout;
        private HttpClient _httpClient;
        private string _mediaType;
        private MediaTypeFormatter _mediaTypeFormatter;
        private MediaTypeWithQualityHeaderValue _mediaTypeWithQualityHeaderValue;
        private DecompressionMethods _decompressionMethods;

        private HttpClientHandler _handler;
        private static Encoding _defaultEncoding = Encoding.UTF8;
        private static MediaTypeFormatter[] _mediaTypeFormatterArray = new MediaTypeFormatter[]
        {
            new JsonMediaTypeFormatter(),
#if RavenRpcHttpProtocol40
#else
            new BsonMediaTypeFormatter(),
            new MsgPackTypeFormatter(),
#endif
            new FormUrlEncodedMediaTypeFormatter(),
            new XmlMediaTypeFormatter(),
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="mediaType"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="decompressionMethods"></param>
        public RpcHttpClient(string baseUrl, string mediaType = MediaType.json, int timeout = defalut_timeout, DecompressionMethods decompressionMethods = DecompressionMethods.Deflate) : this(baseUrl, null, null, mediaType, timeout, decompressionMethods)
        { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="mediaType"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="decompressionMethods"></param>
        /// <param name="encoding">默认UTF8</param>
        /// <param name="handler">内部调用Dispose</param>
        public RpcHttpClient(string baseUrl, Encoding encoding, HttpClientHandler handler, string mediaType = MediaType.json, int timeout = defalut_timeout, DecompressionMethods decompressionMethods = DecompressionMethods.Deflate)
        {
            var defaultConnectionLimit = Environment.ProcessorCount * 6;
            if (defaultConnectionLimit > System.Net.ServicePointManager.DefaultConnectionLimit)
            {
                System.Net.ServicePointManager.DefaultConnectionLimit = defaultConnectionLimit;
            }

            this._baseUrl = baseUrl;
            this._timeout = timeout > 0 ? timeout : defalut_timeout;
            _mediaType = mediaType;
            _mediaTypeFormatter = CreateMediaTypeFormatter(mediaType);
            _mediaTypeWithQualityHeaderValue = new MediaTypeWithQualityHeaderValue(mediaType);

            _decompressionMethods = decompressionMethods;
            _defaultEncoding = encoding ?? Encoding.UTF8;

            _handler = handler ?? new HttpClientHandler();
            //if (decompressionMethods != DecompressionMethods.None)
            //{
            //    _httpClient = new HttpClient(new HttpClientHandler() { AutomaticDecompression = decompressionMethods });
            //    _httpClient.DefaultRequestHeaders.Add("Accept-encoding", decompressionMethods.ToString().ToLower());
            //}
            //else
            //{
            //    _httpClient = new HttpClient();
            //}

            _httpClient = InitHttpClient(timeout);
        }

        /// <summary>
        /// 
        /// </summary>
        public HttpRequestHeaders DefaultRequestHeaders
        {
            get { return _httpClient.DefaultRequestHeaders; }
        }

        /// <summary>
        /// 创建MediaTypeFormatter
        /// </summary>
        /// <param name="mediaType"></param>
        /// <returns></returns>
        private MediaTypeFormatter CreateMediaTypeFormatter(string mediaType)
        {
            MediaTypeFormatter mediaTypeFormatter = null;
            switch (mediaType)
            {
#if RavenRpcHttpProtocol40
#else
                case MediaType.bson:
                    mediaTypeFormatter = new BsonMediaTypeFormatter();
                    break;
                case MediaType.msgpack:
                    mediaTypeFormatter = new MsgPackTypeFormatter();
                    break;
#endif
                case MediaType.form:
                    mediaTypeFormatter = new FormUrlEncodedMediaTypeFormatter();
                    break;
                case MediaType.xml:
                    mediaTypeFormatter = new XmlMediaTypeFormatter();
                    break;
                case MediaType.json:
                default:
                    mediaTypeFormatter = new JsonMediaTypeFormatter();
                    break;
            }

            return mediaTypeFormatter;
        }

        /// <summary>
        /// HttpClient初始化
        /// </summary>
        /// <param name="timeout"></param>
        private HttpClient InitHttpClient(int? timeout)
        {
            HttpClient client;
            if (_decompressionMethods != DecompressionMethods.None)
            {
                _handler.AutomaticDecompression = _decompressionMethods;
                //client = new HttpClient(new HttpClientHandler() { AutomaticDecompression = _decompressionMethods });
            }
            client = new HttpClient(_handler);
            if (_decompressionMethods != DecompressionMethods.None)
            {
                client.DefaultRequestHeaders.Add("Accept-encoding", _decompressionMethods.ToString().ToLower());
            }

            if (timeout.HasValue)
            {
                client.Timeout = TimeSpan.FromMilliseconds(timeout.Value);
            }
            else
            {
                client.Timeout = TimeSpan.FromMilliseconds(this._timeout);
            }

            if (!string.IsNullOrWhiteSpace(_baseUrl))
            {
                client.BaseAddress = new Uri(_baseUrl);
            }
            client.DefaultRequestHeaders.Accept.Add(_mediaTypeWithQualityHeaderValue);
            client.DefaultRequestHeaders.Connection.Add("keep-alive");

            return client;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="url"></param>
        /// <param name="httpMethod">默认Post</param>
        /// <param name="urlParameters"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual Task<TResult> InvokeAsync<TResult>(string url, IDictionary<string, string> urlParameters = null, HttpMethod httpMethod = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<object, TResult>(url, null, urlParameters, httpMethod, timeout);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <param name="httpMethod">默认Post</param>
        /// <param name="urlParameters"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual async Task<TResult> InvokeAsync<TData, TResult>(string url, TData data = default(TData), IDictionary<string, string> urlParameters = null, HttpMethod httpMethod = null, int? timeout = null)
            where TResult : class
        {
            var client = _httpClient;
            //using (var client = this.InitHttpClient(timeout))
            //{
            string requestUrl = _baseUrl + url;
            CreateUrlParams(urlParameters, ref requestUrl);
            HttpContent content = null;
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.Method = httpMethod ?? HttpMethod.Post;
                request.RequestUri = new Uri(requestUrl);

                object contentData = data as object;
                if (RequestContentDataHandler != null)
                {
                    RequestContentDataHandler(ref contentData);
                }

                if (contentData != null && (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put))
                {
                    content = CreateContent(contentData);
                    request.Content = content;
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(_mediaType);
                }

                RpcContext rpcContext = new RpcContext();
                rpcContext.RequestModel = contentData;
                // OnSend
                if (OnRequest != null)
                {
                    OnRequest(request, rpcContext);
                }
                HttpResponseMessage response = null;

                try
                {
                    rpcContext.SendStartTime = DateTime.Now;
                    if (!timeout.HasValue || timeout.Value <= 0)
                    {
                        timeout = this._timeout;
                    }
                    using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(timeout.Value))
                    {
                        response = await client.SendAsync(request, cancelTokenSource.Token).ConfigureAwait(false);
                    }

                    //if (timeout.HasValue && timeout.Value > 0)
                    //{
                    //    using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(timeout.Value))
                    //    {
                    //        response = await client.SendAsync(request, cancelTokenSource.Token).ConfigureAwait(false);
                    //    }
                    //}
                    //else
                    //{
                    //    response = await client.SendAsync(request).ConfigureAwait(false);
                    //}

                    rpcContext.ReceiveEndTime = DateTime.Now;

                    TResult result = await GetResultAsync<TResult>(response).ConfigureAwait(false);
                    rpcContext.ResponseModel = result;
                    rpcContext.ResponseSize = response.Content.Headers.ContentLength ?? 0;

                    if (OnResponse != null)
                    {
                        OnResponse(response, rpcContext);
                    }

                    return result;

                }
                catch (Exception ex)
                {
                    rpcContext.ExceptionTime = DateTime.Now;
                    if (OnError != null)
                    {
                        OnError(ex, request, rpcContext);
                        if (!rpcContext.ExceptionHandled)
                        {
                            throw ExceptionOptimize.Filter(ex);
                        }
                        if (ErrorResponseHandler != null)
                        {
                            return ErrorResponseHandler(ex, rpcContext) as TResult;
                        }
                        else
                            return default(TResult);
                    }
                    else
                        throw ExceptionOptimize.Filter(ex);
                }
                finally
                {
                    if (content != null)
                    {
                        content.Dispose();
                    }
                    if (response != null)
                    {
                        response.Dispose();
                    }
                }
            }

            //}
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="url"></param>
        /// <param name="httpMethod">默认Post</param>
        /// <param name="urlParameters"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual TResult Invoke<TResult>(string url, IDictionary<string, string> urlParameters = null, HttpMethod httpMethod = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<object, TResult>(url, null, urlParameters, httpMethod, timeout);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <param name="httpMethod">默认Post</param>
        /// <param name="urlParameters"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual TResult Invoke<TData, TResult>(string url, TData data = default(TData), IDictionary<string, string> urlParameters = null, HttpMethod httpMethod = null, int? timeout = null)
            where TResult : class
        {
            var client = _httpClient;
            //using (var client = this.InitHttpClient(timeout))
            //{
            string requestUrl = _baseUrl + url;
            CreateUrlParams(urlParameters, ref requestUrl);
            HttpContent content = null;
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.Method = httpMethod ?? HttpMethod.Post;
                request.RequestUri = new Uri(requestUrl);

                object contentData = data as object;
                if (RequestContentDataHandler != null)
                {
                    RequestContentDataHandler(ref contentData);
                }

                if (contentData != null && (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put))
                {
                    content = CreateContent(contentData);
                    request.Content = content;
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(_mediaType);
                }

                RpcContext rpcContext = new RpcContext();
                rpcContext.RequestModel = contentData;
                // OnSend
                if (OnRequest != null)
                {
                    OnRequest(request, rpcContext);
                }
                HttpResponseMessage response = null;

                try
                {
                    rpcContext.SendStartTime = DateTime.Now;

                    if (!timeout.HasValue || timeout.Value <= 0)
                    {
                        timeout = this._timeout;
                    }
                    using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(timeout.Value))
                    {
                        response = client.SendAsync(request, cancelTokenSource.Token).Result;
                    }

                    //if (timeout.HasValue && timeout.Value > 0)
                    //{
                    //    using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(timeout.Value))
                    //    {
                    //        response = client.SendAsync(request, cancelTokenSource.Token).Result;
                    //    }
                    //}
                    //else
                    //{
                    //    response = client.SendAsync(request).Result;
                    //}

                    rpcContext.ReceiveEndTime = DateTime.Now;

                    TResult result = GetResult<TResult>(response);
                    rpcContext.ResponseModel = result;
                    rpcContext.ResponseSize = response.Content.Headers.ContentLength ?? 0;

                    if (OnResponse != null)
                    {
                        OnResponse(response, rpcContext);
                    }

                    return result;

                }
                catch (Exception ex)
                {
                    rpcContext.ExceptionTime = DateTime.Now;
                    if (OnError != null)
                    {
                        OnError(ex, request, rpcContext);
                        if (!rpcContext.ExceptionHandled)
                        {
                            throw ExceptionOptimize.Filter(ex);
                        }
                        if (ErrorResponseHandler != null)
                        {
                            return ErrorResponseHandler(ex, rpcContext) as TResult;
                        }
                        else
                            return default(TResult);
                    }
                    else
                        throw ExceptionOptimize.Filter(ex);
                }
                finally
                {
                    if (content != null)
                    {
                        content.Dispose();
                    }
                    if (response != null)
                    {
                        response.Dispose();
                    }
                }
            }

            //}
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual TResult Get<TResult>(string url, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<TResult>(url, urlParameters, HttpMethod.Get, timeout);
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual Task<TResult> GetAsync<TResult>(string url, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<TResult>(url, urlParameters, HttpMethod.Get, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TData">提交数据类型</typeparam>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public virtual TResult Post<TData, TResult>(string url, TData data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<TData, TResult>(url, data, urlParameters, HttpMethod.Post, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TData">提交数据类型</typeparam>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public virtual Task<TResult> PostAsync<TData, TResult>(string url, TData data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<TData, TResult>(url, data, urlParameters, HttpMethod.Post, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public virtual TResult Post<TResult>(string url, byte[] data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Post<TResult>(url, data, 0, data.Length, urlParameters, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public virtual Task<TResult> PostAsync<TResult>(string url, byte[] data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return PostAsync<TResult>(url, data, 0, data.Length, urlParameters, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">数量</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public virtual TResult Post<TResult>(string url, byte[] data, int offset, int count, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<byte[], TResult>(url, data, urlParameters, HttpMethod.Post, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">数量</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public virtual Task<TResult> PostAsync<TResult>(string url, byte[] data, int offset, int count, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<byte[], TResult>(url, data, urlParameters, HttpMethod.Post, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual TResult Post<TResult>(string url, IDictionary<string, string> data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<IDictionary<string, string>, TResult>(url, data, urlParameters, HttpMethod.Post, timeout);
        }

        /// <summary>
        /// Post
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual Task<TResult> PostAsync<TResult>(string url, IDictionary<string, string> data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<IDictionary<string, string>, TResult>(url, data, urlParameters, HttpMethod.Post, timeout);
        }

        /// <summary>
        /// Put
        /// </summary>
        /// <typeparam name="TData">提交数据类型</typeparam>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual TResult Put<TData, TResult>(string url, TData data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<TData, TResult>(url, data, urlParameters, HttpMethod.Put, timeout);
        }

        /// <summary>
        /// Put
        /// </summary>
        /// <typeparam name="TData">提交数据类型</typeparam>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual Task<TResult> PutAsync<TData, TResult>(string url, TData data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<TData, TResult>(url, data, urlParameters, HttpMethod.Put, timeout);
        }

        /// <summary>
        /// Put
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">url parameter 数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual TResult Put<TResult>(string url, IDictionary<string, string> data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<IDictionary<string, string>, TResult>(url, data, urlParameters, HttpMethod.Put, timeout);
        }

        /// <summary>
        /// Put
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="data">url parameter 数据</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual Task<TResult> PutAsync<TResult>(string url, IDictionary<string, string> data, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<IDictionary<string, string>, TResult>(url, data, urlParameters, HttpMethod.Put, timeout);
        }

        /// <summary>
        /// Delete
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual TResult Delete<TResult>(string url, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return Invoke<TResult>(url, urlParameters, HttpMethod.Delete, timeout);
        }

        /// <summary>
        /// Delete
        /// </summary>
        /// <typeparam name="TResult">返回数据类型</typeparam>
        /// <param name="url">请求Url</param>
        /// <param name="urlParameters">url parameter 数据</param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public virtual Task<TResult> DeleteAsync<TResult>(string url, IDictionary<string, string> urlParameters = null, int? timeout = null)
            where TResult : class
        {
            return InvokeAsync<TResult>(url, urlParameters, HttpMethod.Delete, timeout);
        }

        /// <summary>
        /// 创建参数
        /// </summary>
        /// <param name="urlParameters"></param>
        /// <param name="baseUrl"></param>
        private void CreateUrlParams(IDictionary<string, string> urlParameters, ref string baseUrl)
        {
            StringBuilder buffer = null;
            AddDefaultUrlParameters(ref urlParameters);

            if (urlParameters != null)
            {
                buffer = new StringBuilder();
                int i = 0;
                foreach (string key in urlParameters.Keys)
                {
                    if (i == 0)
                    {
                        buffer.AppendFormat("{0}={1}", key, urlParameters[key]);
                        i++;
                    }
                    else
                    {
                        buffer.AppendFormat("&{0}={1}", key, urlParameters[key]);
                    }
                }
            }

            if (buffer != null && buffer.Length > 0)
            {
                int index = baseUrl.IndexOf("?");
                if (index >= 0)
                {
                    if (index < baseUrl.Length - 1)
                    {
                        baseUrl += "&" + buffer.ToString();
                    }
                    else
                    {
                        baseUrl += buffer.ToString();
                    }
                }
                else
                {
                    baseUrl += "?" + buffer.ToString();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        private HttpContent CreateContent<TData>(TData data)
        {
            HttpContent httpContent = null;
            //RequestContentDataHandler(ref contentData);
            Type type = data.GetType();

            var fullName = type.FullName;
            switch (fullName)
            {
                case "System.String":
                    httpContent = new StringContent(data.ToString(), _defaultEncoding, _mediaType);
                    break;
                case "System.Byte[]":
                    httpContent = new ByteArrayContent(data as byte[]);
                    break;
                default:
                    httpContent = new ObjectContent(type, data, _mediaTypeFormatter);
                    break;
            }
            return httpContent;

        }

        /// <summary>
        /// 获取Result对象
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="response"></param>
        /// <returns></returns>
        private TResult GetResult<TResult>(HttpResponseMessage response)
            where TResult : class
        {
            TResult result;
            if (response.IsSuccessStatusCode)
            {
                var fullName = typeof(TResult).FullName;
                switch (fullName)
                {
                    case "System.String":
                        result = response.Content.ReadAsStringAsync().Result as TResult;
                        break;
                    case "System.Byte[]":
                        result = response.Content.ReadAsByteArrayAsync().Result as TResult;
                        break;
                    default:
                        result = response.Content.ReadAsAsync<TResult>(_mediaTypeFormatterArray).Result;
                        break;
                }
                return result;
            }
            else
            {
                throw new Exception(CreateErrorResponseMessage(response));
            }
        }

        /// <summary>
        /// 获取Result对象
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="response"></param>
        /// <returns></returns>
        private Task<TResult> GetResultAsync<TResult>(HttpResponseMessage response)
            where TResult : class
        {
            if (response.IsSuccessStatusCode)
            {
                //var compressionType = Util.CompressionHelper.GetCompressionType(response.Content.Headers.ContentEncoding);
                //if (compressionType != Util.CompressionType.None)
                //{
                //    var resData = await response.Content.ReadAsByteArrayAsync();
                //    var uncompressData = Util.CompressionHelper.Uncompress(resData, compressionType);
                //}

                var fullName = typeof(TResult).FullName;
                switch (fullName)
                {
                    //case "System.String":
                    //    result = await response.Content.ReadAsStringAsync().ConfigureAwait(false) as TResult;
                    //    break;
                    //case "System.Byte[]":
                    //    result = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false) as TResult;
                    //    break;
                    //default:
                    //    result = await response.Content.ReadAsAsync<TResult>(_mediaTypeFormatterArray).ConfigureAwait(false);
                    //    break;
                    case "System.String":
                        return response.Content.ReadAsStringAsync() as Task<TResult>;
                    case "System.Byte[]":
                        return response.Content.ReadAsByteArrayAsync() as Task<TResult>;
                    default:
                        return response.Content.ReadAsAsync<TResult>();
                }
            }
            else
            {
                throw new Exception(CreateErrorResponseMessage(response));
            }
        }


        private string CreateErrorResponseMessage(HttpResponseMessage response)
        {
            return string.Format("ReasonPhrase:{0},StatusCode:{1}", response.ReasonPhrase, (int)response.StatusCode);
        }

        /// <summary>
        /// 添加默认参数
        /// </summary>
        /// <param name="urlParameters"></param>
        private void AddDefaultUrlParameters(ref IDictionary<string, string> urlParameters)
        {
            //系统参数
            if (urlParameters == null)
            {
                urlParameters = new Dictionary<string, string>();
            }

            //IDictionary<string, string> dp = null;
            //dp = FurnishDefaultParameters();
            if (DefaultUrlParametersHandler != null)
            {
                DefaultUrlParametersHandler(ref urlParameters);
            }

            //DefaultUrlParametersHandler(ref urlParameters);

            //if (dp != null && dp.Count > 0)
            //{
            //    foreach (var item in dp)
            //    {
            //        if (urlParameters.ContainsKey(item.Key)) continue;
            //        urlParameters.Add(item);
            //    }
            //}
        }

        ///// <summary>
        ///// 请求前，请求header定义
        ///// </summary>
        ///// <param name="headers"></param>
        //protected virtual void DefaultRequestHeadersHandler(HttpRequestHeaders headers)
        //{
        //}

        ///// <summary>
        ///// Send数据前
        ///// </summary>
        ///// <param name="request"></param>
        //protected virtual void OnSend(HttpRequestMessage request)
        //{
        //}

        /// <summary>
        /// 请求前
        /// </summary>
        public event OnRequestDelegate OnRequest;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request">HttpRequestMessage</param>
        /// <param name="rpcContext">RpcContext</param>
        public delegate void OnRequestDelegate(HttpRequestMessage request, RpcContext rpcContext);

        /// <summary>
        /// 响应后
        /// </summary>
        public event OnResponseDelegate OnResponse;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response">HttpResponseMessage</param>
        /// <param name="rpcContext">RpcContext</param>
        public delegate void OnResponseDelegate(HttpResponseMessage response, RpcContext rpcContext);

        /// <summary>
        /// OnError
        /// </summary>
        public event OnErrorDelegate OnError;
        /// <summary>
        /// 后续是否抛出异常
        /// </summary>
        /// <param name="origEx">original Exception</param>
        /// <param name="request">HttpResponseMessage</param>
        /// <param name="rpcContext">RpcContext</param>
        /// <returns></returns>
        public delegate void OnErrorDelegate(Exception origEx, HttpRequestMessage request, RpcContext rpcContext);

        /// <summary>
        /// 异常情况返回数据处理
        /// </summary>
        public event ErrorResponseDelegate ErrorResponseHandler;
        /// <summary>
        /// result
        /// </summary>
        /// <param name="origEx">original exception</param>
        /// <param name="rpcContext">RpcContext</param>
        /// <returns></returns>
        public delegate object ErrorResponseDelegate(Exception origEx, RpcContext rpcContext);

        ///// <summary>
        ///// 异常处理
        ///// </summary>
        ///// <typeparam name="TResult"></typeparam>
        ///// <param name="result"></param>
        ///// <param name="httpResponse"></param>
        //protected virtual TResult ErrorResponseHandler<TResult>(TResult result, HttpResponseMessage httpResponse)
        //    where TResult : class
        //{
        //    return result;
        //}

        ///// <summary>
        ///// 异常处理 事件
        ///// </summary>
        //public event ErrorResponseDelegate ErrorResponseHandler;
        ///// <summary>
        ///// 异常处理
        ///// </summary>
        ///// <param name="result"></param>
        ///// <param name="httpResponse"></param>
        ///// <returns></returns>
        //public delegate object ErrorResponseDelegate(object result, HttpResponseMessage httpResponse);

        ///// <summary>
        ///// Url默认参数处理
        ///// </summary>
        ///// <param name="urlParameters"></param>
        //protected virtual IDictionary<string, string> DefaultUrlParametersHandler(IDictionary<string, string> urlParameters)
        //{
        //    return urlParameters;
        //}

        /// <summary>
        /// Url默认参数处理 事件
        /// </summary>
        public event DefaultUrlParametersDelegate DefaultUrlParametersHandler;
        /// <summary>
        /// Url默认参数处理
        /// </summary>
        /// <param name="urlParameters"></param>
        /// <returns></returns>
        public delegate void DefaultUrlParametersDelegate(ref IDictionary<string, string> urlParameters);

        ///// <summary>
        ///// 请求数据处理
        ///// </summary>
        ///// <param name="data"></param>
        //protected virtual object RequestContentDataHandler(object data)
        //{
        //    return data;
        //}

        /// <summary>
        /// 请求数据处理 事件
        /// </summary>
        public event RequestContentDataDelegate RequestContentDataHandler;
        /// <summary>
        /// 请求数据处理
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public delegate void RequestContentDataDelegate(ref object data);

        #region IDispose

        private bool isDisposed = false;

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    if (_httpClient != null)
                    {
                        _httpClient.Dispose();
                    }
                    _httpClient = null;
                }

                _mediaTypeFormatter = null;
                //_mediaTypeFormatterArray = null;
            }
            isDisposed = true;
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        ~RpcHttpClient()
        {
            Dispose(false);
        }
    }
}

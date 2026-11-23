using OxGKit.LoggingSystem;
using System;
using Telepathy;

namespace OxGFrame.NetFrame
{
    public class TcpNetProvider : INetProvider
    {
        private Client _client = null;

        /// <summary>
        /// 當前連線是否已通知關閉
        /// <para> ※備註：Telepathy 於已建立的連線結束時，會派發兩次 Disconnected 事件 (ReceiveLoop 與 ReceiveThreadFunction 各一次)，因此每條連線僅通知一次 </para>
        /// </summary>
        private bool _isCloseNotified = false;

        /// <summary>
        /// Maximum number of packets to process per frame/tick
        /// </summary>
        public int processLimitPerTick;

        public event EventHandler<object> OnOpen;
        public event EventHandler<byte[]> OnBinary;
        public event EventHandler<string> OnMessage;
        public event EventHandler<object> OnError;
        public event EventHandler<object> OnClose;

        public void CreateConnect(NetOption netOption)
        {
            if (netOption == null)
            {
                Logging.PrintError<Logger>("ERROR: Connection failed because NetOption is null.");
                return;
            }

            string host = (netOption as TcpNetOption).host;
            int port = (netOption as TcpNetOption).port;
            int maxBufferSize = (netOption as TcpNetOption).maxBufferSize;
            this.processLimitPerTick = (netOption as TcpNetOption).processLimitPerTick;

            if (string.IsNullOrEmpty(host))
            {
                Logging.PrintError<Logger>("ERROR: TCP connection failed. NetOption.Host cannot be null or empty.");
                return;
            }

            this._client = new Client(maxBufferSize);
            this._isCloseNotified = false;

            this._client.OnConnected += this._OnOpenHandler;
            this._client.OnData += this._OnMessageHandler;
            this._client.OnDisconnected += this._OnCloseHandler;

            try
            {
                // Open connection
                this._client.Connect(host, port);
                Logging.PrintInfo<Logger>($"Endpoint: {host}:{port} => {nameof(TcpNetProvider)} is Connected.");
            }
            catch (Exception ex)
            {
                this._OnErrorHandler($"{ex}");
            }
        }

        #region Handlers
        private void _OnOpenHandler()
        {
            this.OnOpen?.Invoke(this, 0);
        }

        private void _OnMessageHandler(ArraySegment<byte> arrSeg)
        {
            var length = arrSeg.Count;
            var rcvData = new byte[length];
            Array.Copy(arrSeg.Array, arrSeg.Offset, rcvData, 0, length);
            this.OnBinary?.Invoke(this, rcvData);
        }

        private void _OnErrorHandler(string errorMsg)
        {
            this.OnError?.Invoke(this, errorMsg);
        }

        private void _OnCloseHandler()
        {
            // 每條連線僅通知一次 (Telepathy 於連線結束時會重複派發 Disconnected 事件)
            if (this._isCloseNotified)
                return;
            this._isCloseNotified = true;
            this.OnClose?.Invoke(this, -1);
        }

        private void _OnManualCloseHandler()
        {
            // 手動關閉僅需通知一次 (Telepathy 於連線結束時會重複派發 Disconnected 事件)
            if (this._client != null)
                this._client.OnDisconnected -= this._OnManualCloseHandler;
            // 該連線已通知過關閉時 (ex: 已被遠端中斷)，不需再重複通知
            if (this._isCloseNotified)
                return;
            this._isCloseNotified = true;
            this.OnClose?.Invoke(this, -1);
        }
        #endregion

        public bool SendBinary(byte[] buffer)
        {
            if (this.IsConnected())
            {
                try
                {
                    var segment = new ArraySegment<byte>(buffer);
                    bool sent = this._client.Send(segment);
                    Logging.Print<Logger>($"[Binary] TCP - Try Send Size: {buffer.Length} bytes.");
                    return sent;
                }
                catch (Exception ex)
                {
                    Logging.PrintError<Logger>($"Send Error: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        public bool SendMessage(string text)
        {
            throw new Exception("[Text] TCP not supports SendMessage!!! Please convert string to binary and send by binary.");
        }

        public bool IsConnected()
        {
            if (this._client == null)
                return false;
            return this._client.Connected;
        }

        public void OnUpdate()
        {
            if (this._client == null)
                return;
            this._client.Tick(this.processLimitPerTick);
        }

        public void Close()
        {
            if (this._client != null)
            {
                // 手動關閉後，不需再接收該連線殘留的連線與資料事件
                this._client.OnConnected -= this._OnOpenHandler;
                this._client.OnData -= this._OnMessageHandler;
                // 手動關閉的關閉事件改由專用的處理方法接收 (僅通知一次，重複調用 Close 時也不會重複訂閱)
                this._client.OnDisconnected -= this._OnCloseHandler;
                this._client.OnDisconnected -= this._OnManualCloseHandler;
                this._client.OnDisconnected += this._OnManualCloseHandler;
                // Telepathy 的 Disconnected 事件需透過後續的 Tick 才會派發，因此中斷連線後不可立即清空 Client
                // (否則將永遠收不到關閉事件，導致節點狀態停留在 CONNECTED，後續的 Connect 皆被忽略，心跳與逾時計時器也會持續觸發)
                this._client.Disconnect();
            }
        }
    }
}
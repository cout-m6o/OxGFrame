using OxGKit.LoggingSystem;
using OxGKit.TimeSystem;
using System.Collections.Generic;
using System.Linq;

namespace OxGFrame.NetFrame
{
    internal class NetManager
    {
        /// <summary>
        /// Cache for managing active network nodes.
        /// </summary>
        private Dictionary<int, NetNode> _netNodes = null;

        /// <summary>
        /// Reusable snapshot buffer for iterating network nodes (avoids allocating a new array on every update tick).
        /// </summary>
        private readonly List<NetNode> _updateNodesBuffer = new List<NetNode>();

        /// <summary>
        /// Indicates whether the snapshot buffer is currently being iterated.
        /// </summary>
        private bool _isUpdateNodesBufferInUse = false;

        /// <summary>
        /// The real-time updater driver that processes network logic.
        /// </summary>
        internal RTUpdater rtUpdater = null;

        private static readonly object _locker = new object();
        private static NetManager _instance = null;

        /// <summary>
        /// Retrieves the singleton instance of NetManager with thread-safety.
        /// </summary>
        /// <returns>The NetManager instance.</returns>
        public static NetManager GetInstance()
        {
            if (_instance == null)
            {
                lock (_locker)
                {
                    if (_instance == null)
                        _instance = new NetManager();
                }
            }
            return _instance;
        }

        public NetManager()
        {
            this._netNodes = new Dictionary<int, NetNode>();
            this.ResetUpdater(false);
        }

        /// <summary>
        /// Resets and restarts the Real-Time Updater. 
        /// Ensures the previous updater is stopped and nullified to allow Garbage Collection.
        /// </summary>
        /// <param name="useThreadedUpdater">If true, starts on separate thread. Default is false.</param>
        public void ResetUpdater(bool useThreadedUpdater)
        {
            // 1. Clean up existing updater
            if (this.rtUpdater != null)
            {
                this.rtUpdater.Stop();
                this.rtUpdater.onUpdate = null;
                this.rtUpdater.onFixedUpdate = null;
                this.rtUpdater.onLateUpdate = null;
                this.rtUpdater = null;
            }

            // 2. Re-initialize with specified threading mode
            this.rtUpdater = new RTUpdater();
            this.rtUpdater.onUpdate = this._OnUpdate;

            if (useThreadedUpdater)
            {
                this.rtUpdater.StartOnThread();
                Logging.Print<Logger>("NetManager: Updater started on separate thread.");
            }
            else
            {
                this.rtUpdater.Start();
                Logging.Print<Logger>("NetManager: Updater started on main thread.");
            }
        }

        /// <summary>
        /// Internal update loop triggered by the RTUpdater driver.
        /// </summary>
        /// <param name="dt">Delta time since the previous frame.</param>
        private void _OnUpdate(float dt)
        {
            if (this._netNodes.Count > 0)
            {
                // 緩衝區使用中時 (ex: 巢狀調用)，退回每次建立陣列快照的方式
                if (this._isUpdateNodesBufferInUse)
                {
                    // Create a snapshot to prevent "Collection Modified" exceptions during iteration
                    var nodes = this._netNodes.Values.ToArray();
                    foreach (var node in nodes)
                    {
                        // If the collection size changed significantly, stop current iteration to stay synced
                        if (this._netNodes.Count != nodes.Length) break;
                        else if (node == null) continue;

                        node.OnUpdate();
                    }
                    return;
                }

                // Create a snapshot to prevent "Collection Modified" exceptions during iteration
                // (使用可重複利用的緩衝區建立快照，避免每次刷新皆配置新的陣列)
                this._isUpdateNodesBufferInUse = true;
                try
                {
                    this._updateNodesBuffer.Clear();
                    foreach (var pair in this._netNodes)
                        this._updateNodesBuffer.Add(pair.Value);

                    int count = this._updateNodesBuffer.Count;
                    for (int i = 0; i < count; i++)
                    {
                        // If the collection size changed significantly, stop current iteration to stay synced
                        if (this._netNodes.Count != count) break;

                        var node = this._updateNodesBuffer[i];
                        if (node == null) continue;

                        node.OnUpdate();
                    }
                }
                finally
                {
                    this._updateNodesBuffer.Clear();
                    this._isUpdateNodesBufferInUse = false;
                }
            }
        }

        /// <summary>
        /// Returns the total count of registered network nodes.
        /// </summary>
        public int Count()
        {
            return this._netNodes.Count;
        }

        /// <summary>
        /// Adds or replaces a network node. If the ID exists, the previous node is disposed.
        /// </summary>
        /// <param name="netNode">The NetNode instance to manage.</param>
        /// <param name="nnId">Unique identifier for the node.</param>
        public void AddNetNode(NetNode netNode, int nnId)
        {
            if (!this._netNodes.ContainsKey(nnId))
            {
                this._netNodes.Add(nnId, netNode);
            }
            else
            {
                this._netNodes[nnId].Dispose();
                this._netNodes[nnId] = netNode;
            }
        }

        /// <summary>
        /// Disposes and removes a specific network node from management.
        /// </summary>
        /// <param name="nnId">The ID of the node to remove.</param>
        public void RemoveNetNode(int nnId)
        {
            if (this._netNodes.ContainsKey(nnId))
            {
                this._netNodes[nnId].Dispose();
                this._netNodes.Remove(nnId);
            }
        }

        /// <summary>
        /// Retrieves a managed network node by ID.
        /// </summary>
        /// <param name="nnId">Node identifier.</param>
        /// <returns>The NetNode instance or null if not found.</returns>
        public NetNode GetNetNode(int nnId)
        {
            if (this._netNodes.TryGetValue(nnId, out var node))
                return node;
            return null;
        }

        /// <summary>
        /// Initiates a connection for a specific network node.
        /// </summary>
        /// <param name="netOption">Configuration settings for the connection.</param>
        /// <param name="nnId">Node identifier.</param>
        public void Connect(NetOption netOption, int nnId)
        {
            if (this._netNodes.TryGetValue(nnId, out var node))
                node.Connect(netOption);
            else
                Logging.PrintError<Logger>($"NetManager: NodeId {nnId} not found. Connection failed.");
        }

        /// <summary>
        /// Checks if a specific node is currently connected to its provider.
        /// </summary>
        public bool IsConnected(int nnId)
        {
            if (this._netNodes.TryGetValue(nnId, out var node))
            {
                return node != null && node.IsConnected();
            }
            return false;
        }

        /// <summary>
        /// Sends binary data through a specific network node.
        /// </summary>
        /// <returns>True if data was successfully handed to the provider.</returns>
        public bool Send(byte[] buffer, int nnId)
        {
            if (this._netNodes.TryGetValue(nnId, out var node))
            {
                return node.Send(buffer);
            }
            Logging.PrintError<Logger>($"NetManager: NodeId {nnId} not found. Binary send failed.");
            return false;
        }

        /// <summary>
        /// Sends string/text data through a specific network node.
        /// </summary>
        /// <returns>True if the message was successfully handed to the provider.</returns>
        public bool Send(string text, int nnId)
        {
            if (this._netNodes.TryGetValue(nnId, out var node))
            {
                return node.Send(text);
            }
            Logging.PrintError<Logger>($"NetManager: NodeId {nnId} not found. Message send failed.");
            return false;
        }

        /// <summary>
        /// Closes a network node's connection and optionally removes it from the manager.
        /// </summary>
        /// <param name="nnId">Node identifier.</param>
        /// <param name="removeNetNode">If true, the node is removed from the cache after closing.</param>
        public void Close(int nnId, bool removeNetNode)
        {
            if (this._netNodes.TryGetValue(nnId, out var node))
            {
                node.Close();
                if (removeNetNode)
                    this.RemoveNetNode(nnId);
            }
        }

        /// <summary>
        /// Closes all active network connections managed by this class.
        /// </summary>
        /// <param name="removeNetNode">If true, clears the internal cache after closing.</param>
        public void CloseAll(bool removeNetNode)
        {
            var keys = this._netNodes.Keys.ToArray();
            foreach (var nnId in keys)
            {
                this.Close(nnId, removeNetNode);
            }
        }
    }
}
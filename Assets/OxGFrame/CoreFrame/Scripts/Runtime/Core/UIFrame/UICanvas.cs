using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OxGFrame.CoreFrame.UIFrame
{
    [DisallowMultipleComponent]
    public class UICanvas : MonoBehaviour
    {
        /* 階層 Canvas > UIRoot > UINodes, UIMaskManager, UIFreezeManager */

        [HideInInspector] public Canvas canvas;
        [HideInInspector] public CanvasScaler canvasScaler;
        [HideInInspector] public GraphicRaycaster graphicRaycaster;

        /// <summary>
        /// UI 根節點物件
        /// </summary>
        [HideInInspector] public GameObject uiRoot;

        /// <summary>
        /// UI 節點物件
        /// </summary>
        public Dictionary<string, GameObject> uiNodes = new Dictionary<string, GameObject>();

        /// <summary>
        /// UIMaskMgr，由 UIManager 進行單例管控
        /// </summary>
        public UIMaskManager uiMaskManager { get; protected set; }

        /// <summary>
        /// UIFreezeMgr，由 UIManager 進行單例管控
        /// </summary>
        public UIFreezeManager uiFreezeManager { get; protected set; }

        private void Awake()
        {
            this.canvas = this.GetComponent<Canvas>();
            this.canvasScaler = this.GetComponent<CanvasScaler>();
            this.graphicRaycaster = this.GetComponent<GraphicRaycaster>();
        }

        public void SetMaskManager(UIMaskManager maskManager)
        {
            this.uiMaskManager = maskManager;
        }

        public void SetFreezeManager(UIFreezeManager freezeManager)
        {
            this.uiFreezeManager = freezeManager;
        }

        public GameObject GetUINode(NodeType nodeType)
        {
            this.uiNodes.TryGetValue(nodeType.ToString(), out GameObject goNode);
            return goNode;
        }

        private void OnDestroy()
        {
            if (Time.frameCount == 0 || !Application.isPlaying)
                return;

            try
            {
                // 如果 UICavas Destroy 時，需要進行連動釋放，確保 UIManager 緩存操作正常
                UIBase[] uiBases = this.gameObject.GetComponentsInChildren<UIBase>(true);
                foreach (var uiBase in uiBases)
                {
                    // 逐一防護，避免單一 UI 的關閉流程拋出異常時，中斷其餘 UI 的連動釋放
                    try
                    {
                        UIManager.GetInstance().Close(uiBase.assetName, true, true);
                    }
                    catch
                    {
                        /* Nothing to do */
                    }
                }
            }
            catch
            {
                /* Nothing to do */
            }
            finally
            {
                // 釋放 UIManager 中的 UICanvas 緩存 (無論上述流程是否發生異常皆需移除，避免殘留已銷毀的 UICanvas 參考)
                try
                {
                    UIManager.GetInstance().RemoveUICanvasFromCache(this.name);
                }
                catch
                {
                    /* Nothing to do */
                }
            }
        }
    }
}
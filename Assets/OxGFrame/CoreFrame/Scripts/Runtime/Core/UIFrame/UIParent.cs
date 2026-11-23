using UnityEngine;

namespace OxGFrame.CoreFrame.UIFrame
{
    public class UIParent : MonoBehaviour
    {
        private void OnDestroy()
        {
            if (Time.frameCount == 0 || !Application.isPlaying)
                return;

            try
            {
                // 如果 Parent Destroy 時，需要進行連動釋放，確保 Manager 緩存操作正常
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
        }
    }
}

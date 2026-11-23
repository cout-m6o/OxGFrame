using UnityEngine;

namespace OxGFrame.CoreFrame.SRFrame
{
    public class SRParent : MonoBehaviour
    {
        private void OnDestroy()
        {
            if (Time.frameCount == 0 || !Application.isPlaying)
                return;

            try
            {
                // 如果 Parent Destroy 時，需要進行連動釋放，確保 Manager 緩存操作正常
                SRBase[] srBases = this.gameObject.GetComponentsInChildren<SRBase>(true);
                foreach (var srBase in srBases)
                {
                    // 逐一防護，避免單一 SR 的關閉流程拋出異常時，中斷其餘 SR 的連動釋放
                    try
                    {
                        SRManager.GetInstance().Close(srBase.assetName, true, true);
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

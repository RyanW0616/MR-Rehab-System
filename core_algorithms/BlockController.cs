using Fusion;
using Fusion.XR.Shared.Grabbing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit; // 假設用XR Interaction Toolkit處理抓取；若無，改用自訂事件

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))] // 確保Fusion同步
public class BlockController : NetworkBehaviour
{
    public int matrixSize;//不要從這裡設定 //方塊矩陣格數，計分用
    public float shrinkFactor;//不要從這裡設定 //方塊矩陣內縮的比例，計分用
    private Rigidbody rb;
    [HideInInspector]public bool isStacked = false;// 旗標：true表示已穩定疊起
    private Game4Manager game4Manager;
    private bool isCheckingStability = false;// 新鎖定旗標：防重複觸發（預設false）

    private Transform islandRoot;

    private FusionGameManager fusionGameManager;

    public GameObject debugSpherePrefab;

    [HideInInspector]public bool isMove = false;

    void Awake()
    {
        
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("BlockController 缺少 Rigidbody 組件！");
        }
    }
    public override void Spawned()
    {
        // 尋找管理器
        if (game4Manager == null)
        {
            game4Manager = FindObjectOfType<Game4Manager>();

            if (game4Manager == null)
            {
                Debug.LogError("未找到 Game4Manager！");
            }
        }

        if (fusionGameManager == null)
        {
            fusionGameManager = FindObjectOfType<FusionGameManager>();

            if (fusionGameManager == null)
            {
                Debug.LogError("未找到 FusionGameManager！");
            }
        }

        if (game4Manager != null && game4Manager.myIsland != null && Object.HasStateAuthority)
        {
            islandRoot = game4Manager.myIsland.transform;
            Debug.Log($"成功設定 islandRoot: {islandRoot.position}");
        }
    }

    public void OnGrabbed()
    {
        if (!Object.HasStateAuthority || isStacked) return; // 只允許本地玩家、非塔上方塊控制

        Debug.Log("方塊被抓取，物理已啟用！");

        game4Manager.OnGrabbed();//通知game4Manager方塊被抓取了
    }

    public void OnReleased()
    {
        if (!Object.HasStateAuthority || isStacked) return;

        isMove = true;
        rb.isKinematic = false;//關閉鋼體，物體可以受外力引響
        rb.useGravity = true;//開啟重力

        game4Manager.SpawnNewBlock();//放開西瓜後會生成新的方塊，但是不可互動，只有得分後可以互動

        game4Manager.OnReleased();//通知game4Manager方塊被放開了
    }


    void Update()
    {
        // 只當未疊起且高度低於-1f時刪除
        if (!isStacked && transform.position.y < 0f)
        {
            if (Object.HasStateAuthority) // 只在StateAuthority權限端刪除（Fusion安全）
            {
                Debug.Log("方塊高度低於-1f且未疊起，刪除方塊");
                Runner.Despawn(Object); // Fusion刪除網路物件


                //因為有方塊掉落並刪除代表新的方塊已經生成出來了，本身這顆掉落被刪除的方塊並沒有得分，所以方塊二並不能互動，所以要在這裡把他的互動打開
                if (game4Manager != null)
                {
                    Grabbable grabbable = game4Manager.TwoBlock.GetComponent<Grabbable>();
                    if (grabbable != null)
                    {
                        grabbable.enabled = true;
                    }
                    else
                    {
                        Debug.LogWarning("未找到Grabbable，檢查是否正確附著！");
                    }

                    // 取得並開啟NetworkGrabbable（開啟網路抓取同步）
                    NetworkGrabbable networkGrabbable = game4Manager.TwoBlock.GetComponent<NetworkGrabbable>();
                    if (networkGrabbable != null)
                    {
                        networkGrabbable.enabled = true;
                    }
                    else
                    {
                        Debug.LogWarning("未找到NetworkGrabbable，檢查Fusion設定！");
                    }
                }
            }
        }
    }


    void OnCollisionEnter(Collision collision)
    {
        if (isCheckingStability || !isStacked) return; // 鎖定中或已疊起，防重複/彈跳

        if (collision.gameObject.CompareTag("Game4Block")) // 確認是方塊碰撞
        {
            if (!Object.HasStateAuthority) return;

            // 取得上方方塊引用
            BlockController upperBlock = collision.gameObject.GetComponent<BlockController>();
            if (upperBlock != null)
            {
                isCheckingStability = true; // 鎖定，防後續觸發
                StartCoroutine(CheckStabilityAfterDelay(upperBlock));
            }
        }

    }

    private Vector3 GetExtents()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            Vector3 localExtents = meshFilter.mesh.bounds.extents;  // 本地空間 extents (原始模型一半大小)
            Vector3 scale = transform.lossyScale;  // 世界 scale (含父親影響)
            return new Vector3(localExtents.x * Mathf.Abs(scale.x), localExtents.y * Mathf.Abs(scale.y), localExtents.z * Mathf.Abs(scale.z));  // 乘 scale 獲絕對一半大小
        }
        else
        {
            Debug.LogWarning("未找到 MeshFilter，使用 scale fallback");
            return transform.lossyScale / 2f;  // 世界 scale fallback (假設單位模型)
        }
    }

    // 調整CheckStabilityAfterDelay（用GetExtents計算所有）
    private IEnumerator CheckStabilityAfterDelay(BlockController upperBlock)
    {
        yield return new WaitForSeconds(0.8f); // 等待0.5秒（可改為1f）

        // 檢查上方是否穩定
        if (upperBlock != null && upperBlock.gameObject != null && upperBlock.gameObject.transform.position.y > 0f)
        {
            //Debug
            Vector3 myExtents = GetExtents(); // 我的絕對extents
            float halfHeight = myExtents.y; // y一半高度
            //float bottomTopY = transform.position.y + halfHeight;
            Vector3 upperExtents = upperBlock.GetExtents(); // 上方絕對extents
            //float upperHalfHeight = upperExtents.y;
            //float upperBottomY = upperBlock.transform.position.y - upperHalfHeight;
            //float gap = upperBottomY - bottomTopY;
            //Debug.LogError("下方頂部y: " + bottomTopY + " | 上方底部y: " + upperBottomY + " | 間隙gap: " + gap);
            //Debug

            upperBlock.isStacked = true;
            // 計算分數
            Vector3 topCenter = transform.position + Vector3.up * halfHeight;
            //Debug.LogError("下方頂部中心點: " + topCenter);
            int score = CalculateOverlapScore(topCenter);

            Debug.Log("穩定確認，分數: " + score);

            if (game4Manager != null)
            {
                // 塔頂位置：基於上方方塊底部 + 偏移
                Vector3 towerTop = upperBlock.transform.position;
                game4Manager.AddScore(score, towerTop);//將計算出的分數傳送給manager
            }

            fusionGameManager = FindObjectOfType<FusionGameManager>();
            if (fusionGameManager != null)
            {
                fusionGameManager.DecrementRemainingBlocks();
            }
            else
            {
                Debug.LogWarning("FusionGameManager 為 null，無法遞減計數！");
            }

            if (game4Manager != null)
            {
                game4Manager.StartCoroutine(game4Manager.FixBlockAfterDelay(upperBlock)); // 直接呼叫
                game4Manager.IncrementPersonalStacked();
            }
            else
            {
                Debug.LogWarning("Game4Manager引用為null，無法固定方塊或無法遞增個人計數！");
            }

            if (islandRoot == null && game4Manager != null && game4Manager.myIsland != null && Object.HasStateAuthority)
            {
                if (game4Manager.myIsland.transform != null)
                {
                    islandRoot = game4Manager.myIsland.transform;
                    Debug.Log("成功動態設定islandRoot: " + islandRoot.position);
                }
                else
                {
                    Debug.LogWarning("game4Manager.myIsland.transform為null！檢查生成時序。");
                }
            }

            if (islandRoot != null)
            {
                upperBlock.transform.SetParent(islandRoot);
                Debug.Log("上方方塊已設為浮空島子物件");
            }
            else
            {
                Debug.LogWarning("islandRoot為null，無法設定子物件！檢查Game4Manager.myIsland。");
            }

            if (Object.HasStateAuthority && islandRoot != null)
            {
                float blockHeight = upperExtents.y * 2f;
                float sinkDuration = 0.5f;

                // 獨立移動島嶼 + 所有子方塊
                StartCoroutine(LerpIndividualSink(islandRoot, blockHeight, sinkDuration));
            }
        }
        isCheckingStability = false;
    }

    private IEnumerator LerpIndividualSink(Transform root, float height, float duration)
    {
        // 移動島嶼
        Vector3 islandStart = root.position;
        Vector3 islandEnd = islandStart + Vector3.down * height;

        // 收集所有子方塊起始位置
        List<Transform> children = new List<Transform>();
        List<Vector3> childStarts = new List<Vector3>();
        foreach (Transform child in root)
        {
            if (child.GetComponent<BlockController>() != null) // 只移方塊
            {
                children.Add(child);
                childStarts.Add(child.position);
            }
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // 移動島嶼
            root.position = Vector3.Lerp(islandStart, islandEnd, t);

            // 獨立移動每個子方塊（防偏移）
            for (int i = 0; i < children.Count; i++)
            {
                Vector3 childEnd = childStarts[i] + Vector3.down * height;
                children[i].position = Vector3.Lerp(childStarts[i], childEnd, t);
            }

            yield return null;
        }

        // 精確到位
        root.position = islandEnd;
        for (int i = 0; i < children.Count; i++)
        {
            children[i].position = childStarts[i] + Vector3.down * height;
        }
        Debug.Log("島嶼與子方塊獨立下沉完成");
    }
    
    private int CalculateOverlapScore(Vector3 topCenter)
    {
        int score = 0;
        Vector3 extents = GetExtents(); // 從之前方法獲取絕對extents
        float fullWidth = extents.x * 2f; // 絕對x寬度
        float fullDepth = extents.z * 2f; // 絕對z深度（如果方塊非正方，區分x/z）

        float shrunkWidth = fullWidth * shrinkFactor;
        float shrunkDepth = fullDepth * shrinkFactor;
        float spacingX = shrunkWidth / (matrixSize - 1); // x間距基於縮小寬度
        float spacingZ = shrunkDepth / (matrixSize - 1); // z間距基於縮小深度

        float rayLength = 0.1f;

        // 輕微下移起點，避免嵌入
        topCenter += Vector3.down * 0.05f;

        if(matrixSize%2 == 1)
        {
            for (int x = -(matrixSize / 2); x <= (matrixSize / 2); x++)
            {
                for (int z = -(matrixSize / 2); z <= (matrixSize / 2); z++)
                {
                    // 用local軸生成rayOrigin，內縮透過spacing實現
                    Vector3 rayOrigin = topCenter + transform.right * (x * spacingX) + transform.forward * (z * spacingZ);
                    Vector3 rayDirection = transform.up; // local up防旋轉
                    Vector3 expectedHit = rayOrigin + rayDirection * rayLength;
                    //Debug.LogError("WWWW - Origin: " + rayOrigin + " - Expected Hit: " + expectedHit);

                    //測試計分射線用
                    /*
                    if (Object.HasStateAuthority)
                    {
                        CreateDebugSphere(rayOrigin, Color.blue);  // 藍球：初始位
                        CreateDebugSphere(expectedHit, Color.red);  // 紅球：終點位
                    }
                    */
                    if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayLength))
                    {
                        //Debug.LogError("TTTT - Hit: " + hit.collider.name);

                        if (hit.collider.CompareTag("Game4Block"))
                        {
                            //Debug.LogError("++++");
                            score++;
                        }
                    }
                }
            }
        }
        else
        {
            for(int x = 0; x < matrixSize; x++)
            {
                for(int z = 0; z < matrixSize; z++)
                {
                    Vector3 FakeTopCenter = (topCenter + transform.right * (-((matrixSize - 1f) / 2f) * spacingX) + transform.forward * (-((matrixSize - 1f) / 2f) * spacingZ));
                    Vector3 rayOrigin = FakeTopCenter + transform.right * (x * spacingX) + transform.forward * (z * spacingZ);
                    Vector3 expectedHit = rayOrigin + transform.up * rayLength;

                    /*
                    if (Object.HasStateAuthority)
                    {
                        CreateDebugSphere(rayOrigin, Color.blue);  // 藍球：初始位
                        CreateDebugSphere(expectedHit, Color.red);  // 紅球：終點位
                    }
                    */

                    if (Physics.Raycast(rayOrigin, transform.up, out RaycastHit hit, rayLength))
                    {
                        //Debug.LogError("TTTT - Hit: " + hit.collider.name);

                        if (hit.collider.CompareTag("Game4Block"))
                        {
                            //Debug.LogError("++++");
                            score++;
                        }
                    }
                }
            }
        }
        return score;
    }

    //測試計分射線用
    
    private void CreateDebugSphere(Vector3 position, Color color)
    {
        GameObject sphere = Instantiate(debugSpherePrefab, position, Quaternion.identity);
        sphere.transform.rotation = transform.rotation;
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;  // 設定顏色
        }
    }
    



}
